"""
Endpoint kiểm tra và cập nhật nóng (Hot-Reload) khóa GEMINI_API_KEY (hỗ trợ AQ.* và AIza*).
"""

from fastapi import APIRouter, Depends, Request
from app.core.security import verify_internal_api_key
from app.schemas.base import BaseResponse
from app.schemas.config_schema import AiKeyStatusResult, UpdateGeminiKeyRequest
from app.services.gemini_client import GeminiClientWrapper

router = APIRouter(tags=["AI Runtime Configuration"])


def get_gemini_wrapper(request: Request) -> GeminiClientWrapper:
    return request.app.state.gemini_client


@router.get(
    "/config-status",
    response_model=BaseResponse[AiKeyStatusResult],
    dependencies=[Depends(verify_internal_api_key)],
)
async def get_ai_config_status(
    gemini: GeminiClientWrapper = Depends(get_gemini_wrapper),
) -> BaseResponse[AiKeyStatusResult]:
    """Trả về trạng thái cấu hình GEMINI_API_KEY và chế độ vận hành (Vertex Express / AI Studio)."""
    status = AiKeyStatusResult(
        gemini_configured=gemini.is_configured,
        model_name=gemini.model_name,
        masked_key=gemini.masked_key,
        active_mode=gemini.provider_mode_label,
    )
    return BaseResponse[AiKeyStatusResult](
        success=True,
        message=f"Đang vận hành ở chế độ: {gemini.provider_mode_label}.",
        data=status,
    )


@router.post(
    "/config-key",
    response_model=BaseResponse[AiKeyStatusResult],
    dependencies=[Depends(verify_internal_api_key)],
)
async def update_gemini_api_key(
    payload: UpdateGeminiKeyRequest,
    gemini: GeminiClientWrapper = Depends(get_gemini_wrapper),
) -> BaseResponse[AiKeyStatusResult]:
    """Kiểm định khóa Gemini/Vertex mới, ghi vào tệp .env và kích hoạt ngay lập tức trong bộ nhớ."""
    await gemini.verify_and_update_api_key(payload.api_key)
    status = AiKeyStatusResult(
        gemini_configured=gemini.is_configured,
        model_name=gemini.model_name,
        masked_key=gemini.masked_key,
        active_mode=gemini.provider_mode_label,
    )
    return BaseResponse[AiKeyStatusResult](
        success=True,
        message=f"Đã xác thực và kích hoạt [{gemini.provider_mode_label}] thành công!",
        data=status,
    )
