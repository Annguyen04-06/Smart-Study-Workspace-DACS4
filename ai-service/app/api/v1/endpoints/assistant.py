"""
Endpoints Trợ giảng @bot:
- POST /api/v1/ai/chat-assistant        : trả lời trọn vẹn 1 lần (dự phòng khi luồng stream lỗi).
- POST /api/v1/ai/chat-assistant-stream : Streaming thật qua Server-Sent Events, đẩy từng cụm chữ ngay khi Gemini sinh ra.
"""

from fastapi import APIRouter, Depends, Request
from fastapi.responses import StreamingResponse
from app.core.security import verify_internal_api_key
from app.schemas.assistant_schema import AssistantChatRequest, AssistantChatResult
from app.schemas.base import BaseResponse
from app.services.assistant_service import AcademicAssistantService

router = APIRouter(tags=["Academic Assistant (@bot)"])

# Tắt bộ đệm của proxy/trình duyệt để từng sự kiện SSE tới Backend C# ngay lập tức
SSE_HEADERS = {
    "Cache-Control": "no-cache, no-transform",
    "X-Accel-Buffering": "no",
}


def get_assistant_service(request: Request) -> AcademicAssistantService:
    """Lấy thể hiện AcademicAssistantService từ Lifespan state của ứng dụng."""
    return request.app.state.assistant_service


@router.post(
    "/chat-assistant",
    response_model=BaseResponse[AssistantChatResult],
    dependencies=[Depends(verify_internal_api_key)],
)
async def chat_assistant_endpoint(
    payload: AssistantChatRequest,
    assistant_service: AcademicAssistantService = Depends(get_assistant_service),
) -> BaseResponse[AssistantChatResult]:
    """Giải đáp câu hỏi học thuật của sinh viên trong ngữ cảnh phòng học nhóm."""
    result, usage = await assistant_service.answer_room_question(payload)
    return BaseResponse[AssistantChatResult](
        success=True,
        message="Trợ giảng AI phản hồi thành công.",
        data=result,
        usage=usage,
    )


@router.post(
    "/chat-assistant-stream",
    dependencies=[Depends(verify_internal_api_key)],
    response_class=StreamingResponse,
)
async def chat_assistant_stream_endpoint(
    payload: AssistantChatRequest,
    assistant_service: AcademicAssistantService = Depends(get_assistant_service),
) -> StreamingResponse:
    """Phát luồng SSE: `{"type":"chunk","text":...}` liên tục, kết thúc bằng `{"type":"done", ...token, duration_ms}`."""
    return StreamingResponse(
        assistant_service.stream_room_answer_events(payload),
        media_type="text/event-stream",
        headers=SSE_HEADERS,
    )
