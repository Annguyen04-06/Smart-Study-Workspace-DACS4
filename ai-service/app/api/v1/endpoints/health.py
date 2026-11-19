"""
Endpoint kiểm tra trạng thái sẵn sàng (Health Check) và chế độ khóa AI (Vertex Express / AI Studio).
"""

from fastapi import APIRouter, Depends, Request
from app.core.config import Settings, get_settings

router = APIRouter(tags=["Health"])


@router.get("/health")
async def check_health(
    request: Request,
    settings: Settings = Depends(get_settings),
) -> dict[str, object]:
    """Trả về tình trạng hoạt động và chế độ vận hành AI (Vertex AI Express Mode / Google AI Studio Mode)."""
    gemini_client = getattr(request.app.state, "gemini_client", None)
    configured = bool(gemini_client.is_configured) if gemini_client else settings.is_gemini_configured
    provider_mode = (
        gemini_client.provider_mode_label
        if gemini_client
        else ("Vertex AI Express Mode" if settings.gemini_api_key.strip().startswith("AQ.") else "Google AI Studio Mode")
    )
    return {
        "success": True,
        "message": f"AI Microservice đang hoạt động ({provider_mode}).",
        "service": settings.app_name,
        "model": settings.gemini_model_name,
        "gemini_configured": configured,
        "provider_mode": provider_mode,
    }
