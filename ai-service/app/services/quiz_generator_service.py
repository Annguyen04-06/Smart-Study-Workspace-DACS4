"""
Dịch vụ sinh đề thi trắc nghiệm tự động từ văn bản PDF bằng Gemini Structured Output.
Đảm bảo kết quả trả về luôn tuân thủ 100% GeneratedQuizPayload Schema.
"""

import re
import time
from app.core.config import Settings
from app.prompts.academic_prompts import (
    QUIZ_GENERATION_SYSTEM_INSTRUCTION,
    WEAKNESS_ANALYSIS_SYSTEM_INSTRUCTION,
    build_quiz_generation_prompt,
    build_weakness_analysis_prompt,
)
from app.schemas.base import TokenUsageMetadata
from app.schemas.quiz_schema import (
    AnalyzeWeaknessPayload,
    AnalyzeWeaknessRequest,
    GenerateQuizRequest,
    GeneratedQuizPayload,
)
from app.services.gemini_client import GeminiClientWrapper
from app.services.local_grounded_nlp import (
    build_extractive_quiz,
    build_local_weakness_analysis,
)


class QuizGeneratorService:
    """Biên soạn bộ câu hỏi trắc nghiệm khách quan 4 đáp án bám sát nội dung bài giảng."""

    def __init__(self, settings: Settings, gemini_client: GeminiClientWrapper) -> None:
        self._settings = settings
        self._gemini = gemini_client

    @staticmethod
    def _strip_option_prefix(option_text: str) -> str:
        """Loại bỏ tiền tố thừa như 'A. ' hoặc 'B) ' nếu AI vô tình sinh kèm trong nội dung đáp án."""
        return re.sub(r"^[A-Da-d][\.\)\:\-]\s+", "", option_text.strip())

    async def generate_quiz(
        self, request: GenerateQuizRequest
    ) -> tuple[GeneratedQuizPayload, TokenUsageMetadata]:
        """Gọi Gemini với Pydantic response_schema (hoặc trích xuất học liệu cục bộ khi chưa gắn Key)."""
        started_at = time.perf_counter()

        if not self._gemini.is_configured:
            return build_extractive_quiz(request, started_at)

        combined_source = request.extracted_text.strip()
        if request.ai_summary:
            combined_source = (
                f"Tóm tắt trọng tâm:\n{request.ai_summary.strip()}\n\n"
                f"Nội dung chi tiết:\n{combined_source}"
            )
        trimmed_source = combined_source[: self._settings.max_context_chars]

        prompt = build_quiz_generation_prompt(
            material_title=request.material_title,
            question_count=request.question_count,
            difficulty=request.difficulty,
            source_content=trimmed_source,
        )

        payload, usage = await self._gemini.generate_structured(
            prompt=prompt,
            system_instruction=QUIZ_GENERATION_SYSTEM_INSTRUCTION,
            schema_cls=GeneratedQuizPayload,
            temperature=0.3,
        )

        cleaned_questions = payload.questions[: request.question_count]
        for q in cleaned_questions:
            q.option_a = self._strip_option_prefix(q.option_a)
            q.option_b = self._strip_option_prefix(q.option_b)
            q.option_c = self._strip_option_prefix(q.option_c)
            q.option_d = self._strip_option_prefix(q.option_d)

        payload.questions = cleaned_questions
        return payload, usage

    async def analyze_weakness(
        self, request: AnalyzeWeaknessRequest
    ) -> tuple[AnalyzeWeaknessPayload, TokenUsageMetadata]:
        """Chẩn đoán lỗ hổng kiến thức từ danh sách câu trả lời sai bằng Gemini 2.5 Flash Structured Output."""
        started_at = time.perf_counter()

        if not self._gemini.is_configured or len(request.wrong_answers) == 0:
            return build_local_weakness_analysis(request, started_at)

        stats_lines = [
            f"- Môn: {s.subject_category} (Phòng: {s.room_name}) | Đúng {s.correct_count}/{s.total_answered} câu ({s.accuracy_percent:.1f}%)"
            for s in request.subject_stats
        ]
        formatted_stats = "\n".join(stats_lines) if stats_lines else "(Chưa có thống kê môn học)"

        mistake_lines = []
        for idx, item in enumerate(request.wrong_answers[:25], start=1):
            mistake_lines.append(
                f"{idx}. [{item.subject_category} - {item.quiz_title}] Câu hỏi: {item.question_text[:280]}\n"
                f"   SV chọn: {item.selected_option or 'Bỏ trống'} | Đáp án đúng: {item.correct_option}\n"
                f"   Giải thích chuẩn: {(item.explanation or '')[:260]}"
            )
        formatted_mistakes = "\n".join(mistake_lines)

        prompt = build_weakness_analysis_prompt(
            student_name=request.student_name,
            formatted_stats=formatted_stats,
            formatted_mistakes=formatted_mistakes,
        )

        try:
            payload, usage = await self._gemini.generate_structured(
                prompt=prompt,
                system_instruction=WEAKNESS_ANALYSIS_SYSTEM_INSTRUCTION,
                schema_cls=AnalyzeWeaknessPayload,
                temperature=0.25,
            )
            payload.weakness_gaps = payload.weakness_gaps[:3]
            return payload, usage
        except Exception:
            return build_local_weakness_analysis(request, started_at)
