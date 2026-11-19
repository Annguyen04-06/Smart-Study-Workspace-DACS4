"""
Bộ định tuyến trung tâm gom toàn bộ Endpoints phiên bản v1 của AI Microservice.
"""

from fastapi import APIRouter
from app.api.v1.endpoints import assistant, config_key, flashcards, health, mindmap, pdf, quiz

api_v1_router = APIRouter(prefix="/api/v1")

# Endpoint kiểm tra sức khỏe công khai: GET /api/v1/health
api_v1_router.include_router(health.router)

# Nhóm Endpoints nghiệp vụ AI nội bộ: /api/v1/ai/*
api_v1_router.include_router(config_key.router, prefix="/ai")
api_v1_router.include_router(pdf.router, prefix="/ai")
api_v1_router.include_router(assistant.router, prefix="/ai")
api_v1_router.include_router(quiz.router, prefix="/ai")
api_v1_router.include_router(flashcards.router, prefix="/ai")
api_v1_router.include_router(mindmap.router, prefix="/ai")
