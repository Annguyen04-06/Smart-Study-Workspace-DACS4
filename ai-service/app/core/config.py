"""
Cấu hình trung tâm cho AI Microservice sử dụng Pydantic BaseSettings.
Hỗ trợ đồng thời biến GEMINI_MODEL / GEMINI_MODEL_NAME và tự nhận diện khóa AQ.* (Vertex Express) hoặc AIza* (AI Studio).
"""

from functools import lru_cache
from pydantic import AliasChoices, Field
from pydantic_settings import BaseSettings, SettingsConfigDict


class Settings(BaseSettings):
    """Lớp cấu hình cho toàn bộ dịch vụ FastAPI AI."""

    model_config = SettingsConfigDict(
        env_file=".env",
        env_file_encoding="utf-8",
        case_sensitive=False,
        extra="ignore",
    )

    app_name: str = Field(default="Smart Study AI Microservice")
    app_env: str = Field(default="development")
    host: str = Field(default="0.0.0.0")
    port: int = Field(default=8000)

    gemini_api_key: str = Field(default="")
    gemini_model_name: str = Field(
        default="gemini-2.5-flash",
        validation_alias=AliasChoices("GEMINI_MODEL", "GEMINI_MODEL_NAME"),
    )

    internal_api_key: str = Field(default="smartstudy-internal-secret-key-2026")

    max_pdf_size_mb: int = Field(default=25)
    max_context_chars: int = Field(default=48000)
    gemini_request_timeout_seconds: int = Field(default=120)

    # Tối ưu tốc độ Trợ giảng @bot: tắt thinking, giới hạn đầu ra và rút gọn ngữ cảnh gửi Gemini
    assistant_temperature: float = Field(default=0.4)
    assistant_max_output_tokens: int = Field(default=1024)
    assistant_history_limit: int = Field(default=8)
    assistant_history_item_chars: int = Field(default=400)
    assistant_material_context_chars: int = Field(default=2500)
    # Model quá tải (chữ đầu tiên chậm hơn 5 giây) bị bỏ qua và model nhanh hơn được ghi nhớ cho các câu hỏi sau
    assistant_first_chunk_timeout_seconds: float = Field(default=5.0)
    cors_origins: str = Field(
        default="http://localhost:5000,http://localhost:5500,http://127.0.0.1:5500"
    )

    @property
    def cors_origin_list(self) -> list[str]:
        """Chuyển chuỗi CORS_ORIGINS phân tách dấu phẩy sang danh sách URL sạch."""
        return [origin.strip() for origin in self.cors_origins.split(",") if origin.strip()]

    @property
    def is_gemini_configured(self) -> bool:
        """Kiểm tra khóa GEMINI_API_KEY đã được thiết lập hợp lệ hay chưa."""
        cleaned = self.gemini_api_key.strip()
        return bool(cleaned) and cleaned != "YOUR_GEMINI_API_KEY_HERE"


@lru_cache(maxsize=1)
def get_settings() -> Settings:
    """Trả về thể hiện Singleton của Settings."""
    return Settings()
