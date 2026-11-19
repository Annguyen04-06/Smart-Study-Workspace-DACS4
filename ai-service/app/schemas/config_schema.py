"""
Schema kiểm tra trạng thái cấu hình khóa Google Gemini API và cập nhật nóng (Hot-Reload) từ giao diện.
"""

from pydantic import BaseModel, Field


class AiKeyStatusResult(BaseModel):
    """Trạng thái hoạt động hiện tại của khóa GEMINI_API_KEY."""

    gemini_configured: bool = Field(description="Đã gắn khóa GEMINI_API_KEY hợp lệ hay chưa")
    model_name: str = Field(description="Tên model Gemini đang cấu hình")
    masked_key: str | None = Field(default=None, description="Chuỗi khóa đã che bảo mật để hiển thị")
    active_mode: str = Field(description="'gemini-cloud' hoặc 'local-grounded'")


class UpdateGeminiKeyRequest(BaseModel):
    """Yêu cầu cập nhật nóng khóa GEMINI_API_KEY vào tệp .env và bộ nhớ."""

    api_key: str = Field(min_length=10, max_length=256)
