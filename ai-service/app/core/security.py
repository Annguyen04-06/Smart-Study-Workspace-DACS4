"""
Dependency bảo mật kiểm tra Header X-Internal-Api-Key.
Đảm bảo chỉ .NET 8 Backend Core mới được phép gọi các endpoint tiêu tốn Token Gemini.
"""

import secrets
from fastapi import Depends, Header
from app.core.config import Settings, get_settings
from app.core.exceptions import UnauthorizedInternalRequestError


async def verify_internal_api_key(
    x_internal_api_key: str | None = Header(default=None, alias="X-Internal-Api-Key"),
    settings: Settings = Depends(get_settings),
) -> str:
    """So sánh hằng thời gian (constant-time) khóa nội bộ gửi từ Backend Core."""
    if not x_internal_api_key or not secrets.compare_digest(
        x_internal_api_key.strip(), settings.internal_api_key.strip()
    ):
        raise UnauthorizedInternalRequestError(
            "Từ chối truy cập: Header X-Internal-Api-Key bị thiếu hoặc không hợp lệ."
        )
    return x_internal_api_key
