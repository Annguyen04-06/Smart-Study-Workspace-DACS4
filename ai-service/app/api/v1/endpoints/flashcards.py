"""
Endpoint POST /api/v1/ai/generate-flashcards:
Sinh bộ thẻ ghi nhớ 2 mặt phục vụ ôn tập Spaced Repetition từ học liệu PDF.
"""

from fastapi import APIRouter, Depends, Request
from app.core.security import verify_internal_api_key
from app.schemas.base import BaseResponse
from app.schemas.flashcard_schema import (
    GenerateFlashcardsRequest,
    GeneratedFlashcardDeckPayload,
)
from app.services.flashcard_generator_service import FlashcardGeneratorService

router = APIRouter(tags=["Flashcards Generation"])


def get_flashcard_service(request: Request) -> FlashcardGeneratorService:
    """Lấy thể hiện FlashcardGeneratorService từ Lifespan state của ứng dụng."""
    return request.app.state.flashcard_service


@router.post(
    "/generate-flashcards",
    response_model=BaseResponse[GeneratedFlashcardDeckPayload],
    dependencies=[Depends(verify_internal_api_key)],
)
async def generate_flashcards_endpoint(
    payload: GenerateFlashcardsRequest,
    flashcard_service: FlashcardGeneratorService = Depends(get_flashcard_service),
) -> BaseResponse[GeneratedFlashcardDeckPayload]:
    """Trích xuất khái niệm/công thức sinh bộ thẻ Flashcard chuẩn JSON Schema."""
    result, usage = await flashcard_service.generate_flashcards(payload)
    return BaseResponse[GeneratedFlashcardDeckPayload](
        success=True,
        message="Sinh bộ thẻ ghi nhớ Flashcard thành công.",
        data=result,
        usage=usage,
    )
