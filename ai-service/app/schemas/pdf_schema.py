"""
Schema kết quả bóc tách văn bản PDF tiếng Việt và sinh bản tóm tắt học thuật AI.
"""

from pydantic import BaseModel, Field


class PdfExtractionResult(BaseModel):
    """Dữ liệu trả về sau khi phân tích tệp PDF và gọi Gemini tóm tắt."""

    page_count: int = Field(ge=1, description="Tổng số trang của tệp PDF")
    extracted_text: str = Field(description="Toàn bộ văn bản tiếng Việt đã làm sạch")
    chunks: list[str] = Field(default_factory=list, description="Danh sách các đoạn văn đã chia nhỏ")
    ai_summary: str = Field(description="Bản tóm tắt học thuật định dạng Markdown từ Gemini")
