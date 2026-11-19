"""
Pydantic Structured Output Schema đảm bảo Gemini sinh bộ thẻ ghi nhớ Flashcard chuẩn JSON 100%.
"""

from pydantic import BaseModel, Field


class GenerateFlashcardsRequest(BaseModel):
    """Dữ liệu đầu vào từ C# yêu cầu sinh bộ thẻ Flashcard từ học liệu PDF."""

    material_title: str = Field(min_length=1, max_length=250)
    extracted_text: str = Field(min_length=20)
    ai_summary: str | None = Field(default=None)
    card_count: int = Field(default=10, ge=3, le=40)


class GeneratedFlashcardItemSchema(BaseModel):
    """Cấu trúc 1 thẻ ghi nhớ 2 mặt ép kiểu qua Gemini response_schema."""

    front_content: str = Field(
        description="Mặt trước thẻ: Câu hỏi gợi nhớ, thuật ngữ hoặc tên định lý/thuật toán"
    )
    back_content: str = Field(
        description="Mặt sau thẻ: Định nghĩa súc tích, công thức hoặc lời giải chuẩn xác"
    )


class GeneratedFlashcardDeckPayload(BaseModel):
    """Gói dữ liệu bộ thẻ Flashcard hoàn chỉnh do Gemini sinh theo chuẩn Structured Output."""

    suggested_title: str = Field(description="Tiêu đề bộ thẻ ghi nhớ bằng tiếng Việt")
    cards: list[GeneratedFlashcardItemSchema] = Field(
        description="Danh sách các thẻ ghi nhớ 2 mặt"
    )
