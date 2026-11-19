"""
Điểm khởi chạy chính (Application Factory) của tầng AI Microservice (Port 8000).
Quản lý Lifespan khởi tạo các dịch vụ Gemini/Vertex (kèm khởi động nóng model), ghi log chế độ khóa rõ ràng, cấu hình CORS và bắt ngoại lệ tập trung.
"""

import asyncio
import logging
from contextlib import asynccontextmanager
from typing import AsyncIterator
from fastapi import FastAPI, Request
from fastapi.exceptions import RequestValidationError
from fastapi.middleware.cors import CORSMiddleware
from fastapi.responses import JSONResponse

from app.api.v1.router import api_v1_router
from app.core.config import get_settings
from app.core.exceptions import AiServiceError
from app.services.assistant_service import AcademicAssistantService
from app.services.flashcard_generator_service import FlashcardGeneratorService
from app.services.gemini_client import GeminiClientWrapper
from app.services.mindmap_generator_service import MindmapGeneratorService
from app.services.pdf_service import PdfProcessingService
from app.services.quiz_generator_service import QuizGeneratorService

logging.basicConfig(
    level=logging.INFO,
    format="%(asctime)s | %(levelname)s | %(name)s | %(message)s",
)
logger = logging.getLogger("smart_study.startup")


@asynccontextmanager
async def lifespan(app: FastAPI) -> AsyncIterator[None]:
    """Khởi tạo các Singleton Service vào app.state và ghi log rõ chế độ AI đang chạy."""
    settings = get_settings()
    gemini_client = GeminiClientWrapper(settings=settings)

    logger.info(
        "=== SMART STUDY AI MICROSERVICE READY | Mode: %s | Model: %s | Key: %s ===",
        gemini_client.provider_mode_label,
        gemini_client.model_name,
        gemini_client.masked_key or "Not Configured",
    )

    app.state.gemini_client = gemini_client
    app.state.pdf_service = PdfProcessingService(settings=settings, gemini_client=gemini_client)
    app.state.assistant_service = AcademicAssistantService(
        settings=settings, gemini_client=gemini_client
    )
    app.state.quiz_service = QuizGeneratorService(settings=settings, gemini_client=gemini_client)
    app.state.flashcard_service = FlashcardGeneratorService(
        settings=settings, gemini_client=gemini_client
    )
    app.state.mindmap_service = MindmapGeneratorService(
        settings=settings, gemini_client=gemini_client
    )

    # Khởi động nóng Gemini ở nền (không chặn cổng 8000 mở) để câu hỏi @bot đầu tiên có TTFT thấp
    warm_up_task = asyncio.create_task(gemini_client.warm_up())
    yield
    warm_up_task.cancel()


def create_app() -> FastAPI:
    """Khởi tạo và cấu hình hoàn chỉnh ứng dụng FastAPI."""
    settings = get_settings()
    application = FastAPI(
        title=settings.app_name,
        version="2.0.0",
        description="Microservice bóc tách PDF tiếng Việt và tích hợp Google Gemini 2.5 Flash (Vertex Express & AI Studio).",
        lifespan=lifespan,
    )

    application.add_middleware(
        CORSMiddleware,
        allow_origins=settings.cors_origin_list,
        allow_credentials=True,
        allow_methods=["*"],
        allow_headers=["*"],
    )

    @application.exception_handler(AiServiceError)
    async def handle_ai_service_error(_: Request, exc: AiServiceError) -> JSONResponse:
        return JSONResponse(
            status_code=exc.status_code,
            content={
                "success": False,
                "message": exc.message,
                "data": None,
                "usage": None,
            },
        )

    @application.exception_handler(RequestValidationError)
    async def handle_validation_error(_: Request, exc: RequestValidationError) -> JSONResponse:
        return JSONResponse(
            status_code=422,
            content={
                "success": False,
                "message": "Dữ liệu yêu cầu gửi sang AI Microservice không hợp lệ.",
                "data": {"details": exc.errors()},
                "usage": None,
            },
        )

    @application.exception_handler(Exception)
    async def handle_unexpected_error(_: Request, exc: Exception) -> JSONResponse:
        return JSONResponse(
            status_code=500,
            content={
                "success": False,
                "message": f"Lỗi nội bộ tại AI Microservice: {exc}",
                "data": None,
                "usage": None,
            },
        )

    application.include_router(api_v1_router)
    return application


app = create_app()
