"""
Dịch vụ Trợ giảng ảo @bot tối ưu tốc độ phản hồi:
- Ngữ cảnh gọn: tối đa 8 tin nhắn gần nhất (mỗi tin cắt 400 ký tự) + tối đa 2.500 ký tự học liệu
  liên quan nhất tới câu hỏi (xếp hạng theo độ trùng từ khóa) => giảm mạnh Input Tokens.
- Gọi Gemini ở chế độ hội thoại nhanh (thinking_budget=0, max_output_tokens) cho cả bản thường và bản Streaming SSE.
- Chưa gắn khóa Gemini: trả lời Grounded PDF cục bộ (vẫn phát qua luồng SSE để Backend dùng chung 1 đường xử lý).
"""

import json
import logging
import time
from typing import AsyncIterator

from pydantic import BaseModel

from app.core.config import Settings
from app.core.exceptions import AiServiceError
from app.prompts.academic_prompts import (
    ASSISTANT_CHAT_SYSTEM_INSTRUCTION,
    ASSISTANT_TRUNCATED_ANSWER_NOTE,
    build_assistant_chat_prompt,
)
from app.schemas.assistant_schema import (
    AssistantChatRequest,
    AssistantChatResult,
    AssistantStreamChunkEvent,
    AssistantStreamDoneEvent,
    AssistantStreamErrorEvent,
    MaterialContextItem,
)
from app.schemas.base import TokenUsageMetadata
from app.services.gemini_client import GeminiClientWrapper, TextStreamSummary
from app.services.local_grounded_nlp import build_extractive_assistant_reply, score_text_relevance

logger = logging.getLogger("smart_study.assistant")


def _to_sse(event: BaseModel) -> str:
    """Đóng gói 1 sự kiện thành khung Server-Sent Events chuẩn `data: {...}\\n\\n` (giữ nguyên tiếng Việt UTF-8)."""
    return f"data: {json.dumps(event.model_dump(), ensure_ascii=False)}\n\n"


class AcademicAssistantService:
    """Điều phối ngữ cảnh phòng học và gọi Gemini trả lời câu hỏi @bot của sinh viên."""

    def __init__(self, settings: Settings, gemini_client: GeminiClientWrapper) -> None:
        self._settings = settings
        self._gemini = gemini_client

    @staticmethod
    def _clean_question(request: AssistantChatRequest) -> str:
        question = request.question.strip()
        if question.lower().startswith("@bot"):
            return question[4:].strip() or question
        return question

    def _format_materials(self, request: AssistantChatRequest, question: str) -> str:
        """Chọn học liệu liên quan nhất tới câu hỏi và gói gọn trong ngân sách ký tự cấu hình (mặc định 2.500)."""
        if not request.material_summaries:
            return "(Phòng học hiện chưa có tài liệu PDF nào được tải lên.)"

        def relevance(material: MaterialContextItem) -> float:
            haystack = f"{material.title}\n{material.summary}\n{material.extracted_excerpt or ''}"
            return score_text_relevance(haystack, question)

        # sorted() ổn định: các tài liệu cùng điểm giữ thứ tự mới nhất trước như Backend gửi sang
        ranked = sorted(request.material_summaries, key=relevance, reverse=True)
        remaining = self._settings.assistant_material_context_chars
        blocks: list[str] = []

        for material in ranked:
            if remaining <= 160:
                break
            header = f"[Tài liệu] {material.title.strip()}\n"
            body = f"Tóm tắt: {material.summary.strip() or 'Chưa có tóm tắt.'}"
            excerpt = (material.extracted_excerpt or "").strip()
            if excerpt:
                body += f"\nTrích đoạn gốc: {excerpt}"
            block = (header + body)[:remaining].rstrip()
            blocks.append(block)
            remaining -= len(block) + 2

        return "\n\n".join(blocks)

    def _format_recent_messages(self, request: AssistantChatRequest) -> str:
        """Lấy tối đa N tin gần nhất (bỏ chính câu hỏi hiện tại ở cuối), mỗi tin cắt gọn theo cấu hình."""
        history = list(request.recent_messages)
        if history and history[-1].content.strip() == request.question.strip():
            history.pop()

        window = history[-self._settings.assistant_history_limit:]
        if not window:
            return "(Chưa có lịch sử trò chuyện trước đó.)"

        limit = self._settings.assistant_history_item_chars
        lines: list[str] = []
        for msg in window:
            role_label = "Trợ giảng @bot" if msg.is_ai_bot else msg.sender_name
            content = " ".join(msg.content.split())
            if len(content) > limit:
                content = content[:limit].rstrip() + "…"
            lines.append(f"- {role_label}: {content}")
        return "\n".join(lines)

    def _build_prompt(self, request: AssistantChatRequest) -> str:
        question = self._clean_question(request)
        return build_assistant_chat_prompt(
            room_name=request.room_name,
            subject_category=request.subject_category,
            question=question,
            formatted_materials=self._format_materials(request, question),
            formatted_history=self._format_recent_messages(request),
        )

    async def answer_room_question(
        self, request: AssistantChatRequest
    ) -> tuple[AssistantChatResult, TokenUsageMetadata]:
        """Bản không stream (dự phòng cho Backend): cùng ngữ cảnh gọn và cấu hình hội thoại nhanh."""
        started_at = time.perf_counter()

        if not self._gemini.is_configured:
            answer_markdown, usage = build_extractive_assistant_reply(request, started_at)
            return AssistantChatResult(answer=answer_markdown), usage

        answer_markdown, usage = await self._gemini.generate_text(
            prompt=self._build_prompt(request),
            system_instruction=ASSISTANT_CHAT_SYSTEM_INSTRUCTION,
            temperature=self._settings.assistant_temperature,
            max_output_tokens=self._settings.assistant_max_output_tokens,
            disable_thinking=True,
        )
        return AssistantChatResult(answer=answer_markdown), usage

    async def stream_room_answer_events(self, request: AssistantChatRequest) -> AsyncIterator[str]:
        """
        Phát luồng SSE: nhiều sự kiện `chunk` ngay khi Gemini sinh chữ, kết thúc bằng 1 sự kiện `done` (Token + thời gian).
        Lỗi giữa chừng được chuyển thành sự kiện `error` để Backend chủ động gọi lại endpoint thường.
        """
        started_at = time.perf_counter()

        if not self._gemini.is_configured:
            answer_markdown, usage = build_extractive_assistant_reply(request, started_at)
            yield _to_sse(AssistantStreamChunkEvent(text=answer_markdown))
            yield _to_sse(AssistantStreamDoneEvent(**usage.model_dump(), first_chunk_ms=usage.duration_ms))
            return

        try:
            async for item in self._gemini.stream_text(
                prompt=self._build_prompt(request),
                system_instruction=ASSISTANT_CHAT_SYSTEM_INSTRUCTION,
                temperature=self._settings.assistant_temperature,
                max_output_tokens=self._settings.assistant_max_output_tokens,
                disable_thinking=True,
                first_chunk_timeout_seconds=self._settings.assistant_first_chunk_timeout_seconds,
            ):
                if isinstance(item, TextStreamSummary):
                    if item.truncated:
                        yield _to_sse(AssistantStreamChunkEvent(text=ASSISTANT_TRUNCATED_ANSWER_NOTE))
                    yield _to_sse(
                        AssistantStreamDoneEvent(**item.usage.model_dump(), first_chunk_ms=item.first_chunk_ms)
                    )
                else:
                    yield _to_sse(AssistantStreamChunkEvent(text=item))
        except AiServiceError as exc:
            logger.warning("Luồng @bot streaming gặp lỗi: %s", exc.message)
            yield _to_sse(AssistantStreamErrorEvent(message=exc.message))
        except Exception as exc:
            logger.exception("Lỗi không mong muốn khi streaming câu trả lời @bot.")
            yield _to_sse(AssistantStreamErrorEvent(message=f"Lỗi streaming tại AI Microservice: {exc}"))
