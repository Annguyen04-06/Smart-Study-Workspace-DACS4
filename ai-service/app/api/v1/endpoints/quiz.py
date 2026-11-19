"""
Endpoint POST /api/v1/ai/generate-quiz:
Sinh bộ câu hỏi trắc nghiệm 4 lựa chọn chuẩn Structured Output JSON từ nội dung PDF.
"""

from fastapi import APIRouter, Depends, Request
from app.core.security import verify_internal_api_key
from app.schemas.base import BaseResponse
from app.schemas.quiz_schema import (
    AnalyzeWeaknessPayload,
    AnalyzeWeaknessRequest,
    GenerateQuizRequest,
    GeneratedQuizPayload,
)
from app.services.quiz_generator_service import QuizGeneratorService

router = APIRouter(tags=["Quiz Generation"])


def get_quiz_service(request: Request) -> QuizGeneratorService:
    """Lấy thể hiện QuizGeneratorService từ Lifespan state của ứng dụng."""
    return request.app.state.quiz_service


@router.post(
    "/generate-quiz",
    response_model=BaseResponse[GeneratedQuizPayload],
    dependencies=[Depends(verify_internal_api_key)],
)
async def generate_quiz_endpoint(
    payload: GenerateQuizRequest,
    quiz_service: QuizGeneratorService = Depends(get_quiz_service),
) -> BaseResponse[GeneratedQuizPayload]:
    """Biên soạn đề thi trắc nghiệm tự động theo độ khó và số câu chỉ định."""
    result, usage = await quiz_service.generate_quiz(payload)
    return BaseResponse[GeneratedQuizPayload](
        success=True,
        message="Sinh đề thi trắc nghiệm từ tài liệu PDF thành công.",
        data=result,
        usage=usage,
    )


@router.post(
    "/analyze-weakness",
    response_model=BaseResponse[AnalyzeWeaknessPayload],
    dependencies=[Depends(verify_internal_api_key)],
)
async def analyze_weakness_endpoint(
    payload: AnalyzeWeaknessRequest,
    quiz_service: QuizGeneratorService = Depends(get_quiz_service),
) -> BaseResponse[AnalyzeWeaknessPayload]:
    """Phân tích lỗ hổng kiến thức cá nhân hóa từ lịch sử câu trả lời sai của sinh viên."""
    result, usage = await quiz_service.analyze_weakness(payload)
    return BaseResponse[AnalyzeWeaknessPayload](
        success=True,
        message="Phân tích lỗ hổng kiến thức bằng Gemini AI thành công.",
        data=result,
        usage=usage,
    )
