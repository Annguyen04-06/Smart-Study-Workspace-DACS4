"""
Schema cơ sở dùng chung cho toàn bộ API phản hồi từ FastAPI sang .NET 8 Backend Core.
Luôn đính kèm TokenUsageMetadata để C# ghi nhận chính xác vào bảng AiUsageLogs.
"""

from typing import Generic, TypeVar
from pydantic import BaseModel, Field

T = TypeVar("T")


class TokenUsageMetadata(BaseModel):
    """Thông số tiêu thụ Token và thời gian phản hồi thực tế từ Google Gemini API."""

    model_name: str = Field(description="Tên model Gemini đã thực thi yêu cầu")
    prompt_tokens: int = Field(default=0, ge=0, description="Số token đầu vào (prompt)")
    completion_tokens: int = Field(default=0, ge=0, description="Số token sinh ra (candidates)")
    total_tokens: int = Field(default=0, ge=0, description="Tổng số token tiêu thụ")
    duration_ms: int = Field(default=0, ge=0, description="Thời gian xử lý tính bằng mili-giây")


class BaseResponse(BaseModel, Generic[T]):
    """Khung phản hồi JSON chuẩn hóa của tầng AI Microservice."""

    success: bool = Field(default=True)
    message: str = Field(default="Xử lý yêu cầu AI thành công.")
    data: T | None = Field(default=None)
    usage: TokenUsageMetadata | None = Field(default=None)
