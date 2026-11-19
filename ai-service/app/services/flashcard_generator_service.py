"""
Dịch vụ trích xuất khái niệm, định lý và công thức từ học liệu PDF để tạo bộ thẻ Flashcard.
Sử dụng Gemini Structured Output đảm bảo định dạng JSON chuẩn 100%.
"""

import time
from app.core.config import Settings
from app.prompts.academic_prompts import (
    FLASHCARD_GENERATION_SYSTEM_INSTRUCTION,
    build_flashcard_generation_prompt,
)
from app.schemas.base import TokenUsageMetadata
from app.schemas.flashcard_schema import (
    GenerateFlashcardsRequest,
    GeneratedFlashcardDeckPayload,
)
from app.services.gemini_client import GeminiClientWrapper
from app.services.local_grounded_nlp import build_extractive_flashcards


class FlashcardGeneratorService:
    """Sinh bộ thẻ ghi nhớ 2 mặt phục vụ ôn tập Spaced Repetition."""

    def __init__(self, settings: Settings, gemini_client: GeminiClientWrapper) -> None:
        self._settings = settings
        self._gemini = gemini_client

    async def generate_flashcards(
        self, request: GenerateFlashcardsRequest
    ) -> tuple[GeneratedFlashcardDeckPayload, TokenUsageMetadata]:
        """Gọi Gemini với GeneratedFlashcardDeckPayload schema (hoặc trích xuất cục bộ khi chưa gắn Key)."""
        started_at = time.perf_counter()

        if not self._gemini.is_configured:
            return build_extractive_flashcards(request, started_at)

        combined_source = request.extracted_text.strip()
        if request.ai_summary:
            combined_source = (
                f"Tóm tắt trọng tâm:\n{request.ai_summary.strip()}\n\n"
                f"Nội dung chi tiết:\n{combined_source}"
            )
        trimmed_source = combined_source[: self._settings.max_context_chars]

        prompt = build_flashcard_generation_prompt(
            material_title=request.material_title,
            card_count=request.card_count,
            source_content=trimmed_source,
        )

        payload, usage = await self._gemini.generate_structured(
            prompt=prompt,
            system_instruction=FLASHCARD_GENERATION_SYSTEM_INSTRUCTION,
            schema_cls=GeneratedFlashcardDeckPayload,
            temperature=0.3,
        )

        payload.cards = payload.cards[: request.card_count]
        return payload, usage
