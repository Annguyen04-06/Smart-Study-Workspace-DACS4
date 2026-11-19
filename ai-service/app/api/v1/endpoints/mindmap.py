"""
Endpoint POST /api/v1/ai/generate-mindmap:
Sinh mã Sơ đồ Tư duy Kiến thức chuẩn cú pháp Mermaid từ văn bản trích xuất của học liệu PDF.
"""

from fastapi import APIRouter, Depends, Request
from app.core.security import verify_internal_api_key
from app.schemas.base import BaseResponse
from app.schemas.mindmap_schema import GenerateMindmapRequest, GeneratedMindmapPayload
from app.services.mindmap_generator_service import MindmapGeneratorService

router = APIRouter(tags=["Mindmap Generation"])


def get_mindmap_service(request: Request) -> MindmapGeneratorService:
    """Lấy thể hiện MindmapGeneratorService từ Lifespan state của ứng dụng."""
    return request.app.state.mindmap_service


@router.post(
    "/generate-mindmap",
    response_model=BaseResponse[GeneratedMindmapPayload],
    dependencies=[Depends(verify_internal_api_key)],
)
async def generate_mindmap_endpoint(
    payload: GenerateMindmapRequest,
    mindmap_service: MindmapGeneratorService = Depends(get_mindmap_service),
) -> BaseResponse[GeneratedMindmapPayload]:
    """Tổng hợp nội dung bài giảng PDF thành sơ đồ Mermaid kèm thông số Token."""
    result, usage = await mindmap_service.generate_mindmap(payload)
    return BaseResponse[GeneratedMindmapPayload](
        success=True,
        message="Sinh Sơ đồ Tư duy Kiến thức AI thành công.",
        data=result,
        usage=usage,
    )
