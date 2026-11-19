"""
Định nghĩa hệ thống ngoại lệ chuyên biệt cho tầng AI Microservice.
Giúp Global Exception Handler ở main.py chuyển đổi thành mã HTTP chuẩn xác cho C# Backend.
"""


class AiServiceError(Exception):
    """Lớp cơ sở cho mọi ngoại lệ nghiệp vụ trong AI Microservice."""

    def __init__(self, message: str, status_code: int = 500) -> None:
        super().__init__(message)
        self.message = message
        self.status_code = status_code


class UnauthorizedInternalRequestError(AiServiceError):
    """Ngoại lệ khi Header X-Internal-Api-Key không hợp lệ (HTTP 401)."""

    def __init__(self, message: str) -> None:
        super().__init__(message=message, status_code=401)


class PdfParsingError(AiServiceError):
    """Ngoại lệ khi tệp PDF bị hỏng, rỗng hoặc vượt dung lượng cho phép (HTTP 422)."""

    def __init__(self, message: str) -> None:
        super().__init__(message=message, status_code=422)


class AiConfigurationError(AiServiceError):
    """Ngoại lệ khi chưa cấu hình GEMINI_API_KEY trong môi trường (HTTP 503)."""

    def __init__(self, message: str) -> None:
        super().__init__(message=message, status_code=503)


class AiGenerationError(AiServiceError):
    """Ngoại lệ khi gọi Google Gemini API thất bại hoặc phản hồi sai định dạng (HTTP 502)."""

    def __init__(self, message: str) -> None:
        super().__init__(message=message, status_code=502)
