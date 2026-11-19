"""
Endpoint POST /api/v1/ai/extract-pdf:
Nhận tệp PDF từ .NET Backend, trích xuất văn bản tiếng Việt và sinh bản Tóm tắt Học thuật.
"""

from fastapi import APIRouter, Depends, File, Form, Request, UploadFile
from app.core.exceptions import PdfParsingError
from app.core.security import verify_internal_api_key
from app.schemas.base import BaseResponse
from app.schemas.pdf_schema import PdfExtractionResult
from app.services.pdf_service import PdfProcessingService

router = APIRouter(tags=["PDF Extraction"])


def get_pdf_service(request: Request) -> PdfProcessingService:
    """Lấy thể hiện PdfProcessingService từ Lifespan state của ứng dụng."""
    return request.app.state.pdf_service


@router.post(
    "/extract-pdf",
    response_model=BaseResponse[PdfExtractionResult],
    dependencies=[Depends(verify_internal_api_key)],
)
async def extract_pdf_endpoint(
    file: UploadFile = File(...),
    title: str = Form(default=""),
    pdf_service: PdfProcessingService = Depends(get_pdf_service),
) -> BaseResponse[PdfExtractionResult]:
    """Đọc tệp PDF tải lên, làm sạch văn bản tiếng Việt và trả về bản tóm tắt AI."""
    filename = (file.filename or "").lower()
    if not filename.endswith(".pdf"):
        raise PdfParsingError("Chỉ chấp nhận tệp định dạng .pdf hợp lệ.")

    pdf_bytes = await file.read()
    effective_title = title.strip() or (file.filename or "Tài liệu PDF")
    result, usage = await pdf_service.extract_and_summarize(pdf_bytes=pdf_bytes, title=effective_title)

    return BaseResponse[PdfExtractionResult](
        success=True,
        message="Trích xuất văn bản PDF và tóm tắt học thuật thành công.",
        data=result,
        usage=usage,
    )
