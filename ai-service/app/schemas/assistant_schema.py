"""
Schema Request/Response cho Trợ giảng @bot kết hợp Sliding Window chat và tóm tắt PDF phòng học.
"""

from pydantic import BaseModel, Field


class ChatHistoryItem(BaseModel):
    """Một tin nhắn nằm trong cửa sổ trượt (Sliding Window các tin gần nhất) của phòng học."""

    sender_name: str = Field(default="Sinh viên")
    is_ai_bot: bool = Field(default=False)
    content: str = Field(min_length=1)
    created_at: str | None = Field(default=None)


class MaterialContextItem(BaseModel):
    """Ngữ cảnh học liệu PDF trong phòng dùng để Grounding chống ảo giác cho @bot."""

    title: str
    summary: str = Field(default="")
    extracted_excerpt: str | None = Field(default=None)


class AssistantChatRequest(BaseModel):
    """Yêu cầu giải đáp câu hỏi @bot gửi từ StudyRoomHub (.NET 8 Backend)."""

    room_name: str = Field(min_length=1, max_length=200)
    subject_category: str = Field(default="Công nghệ Thông tin", max_length=150)
    question: str = Field(min_length=1, max_length=4000)
    recent_messages: list[ChatHistoryItem] = Field(default_factory=list)
    material_summaries: list[MaterialContextItem] = Field(default_factory=list)


class AssistantChatResult(BaseModel):
    """Nội dung phản hồi định dạng Markdown + KaTeX của Trợ giảng @bot."""

    answer: str = Field(description="Câu trả lời học thuật chi tiết của @bot")


class AssistantStreamChunkEvent(BaseModel):
    """Sự kiện SSE mang 1 cụm chữ vừa được Gemini sinh ra (đẩy ngay, không chờ hết câu trả lời)."""

    type: str = Field(default="chunk")
    text: str


class AssistantStreamDoneEvent(BaseModel):
    """Sự kiện SSE tổng kết cuối luồng: Token tiêu thụ cho AiUsageLogs và độ trễ chữ đầu tiên (TTFT)."""

    type: str = Field(default="done")
    model_name: str
    prompt_tokens: int = Field(default=0, ge=0)
    completion_tokens: int = Field(default=0, ge=0)
    total_tokens: int = Field(default=0, ge=0)
    duration_ms: int = Field(default=0, ge=0)
    first_chunk_ms: int = Field(default=0, ge=0)


class AssistantStreamErrorEvent(BaseModel):
    """Sự kiện SSE báo lỗi giữa chừng để Backend C# tự chuyển sang endpoint không stream."""

    type: str = Field(default="error")
    message: str
