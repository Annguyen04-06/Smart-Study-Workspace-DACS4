"""
Pydantic Structured Output Schema đảm bảo Gemini sinh mã Sơ đồ Tư duy (Mermaid Mindmap / Flowchart) chuẩn JSON 100%.
"""

from pydantic import BaseModel, Field


class GenerateMindmapRequest(BaseModel):
    """Dữ liệu đầu vào từ C# Backend yêu cầu sinh Sơ đồ Tư duy từ học liệu PDF."""

    material_title: str = Field(min_length=1, max_length=250)
    extracted_text: str = Field(min_length=10)
    ai_summary: str | None = Field(default=None)


class GeneratedMindmapPayload(BaseModel):
    """Gói dữ liệu Sơ đồ Tư duy Mermaid chuẩn Structured Output trả về cho C# Backend."""

    title: str = Field(description="Tiêu đề sơ đồ tư duy kiến thức bằng tiếng Việt")
    mermaid_code: str = Field(
        description=(
            "Mã nguồn sơ đồ Mermaid bắt đầu bằng 'flowchart TD', mọi nhãn nút đặt trong cặp dấu ngoặc kép "
            "theo dạng id[\"Nhãn tiếng Việt\"], phân cấp từ root sang các nhánh chính và nút con"
        )
    )
    node_count: int = Field(
        default=12,
        ge=4,
        le=60,
        description="Tổng số nút kiến thức xuất hiện trong sơ đồ",
    )
