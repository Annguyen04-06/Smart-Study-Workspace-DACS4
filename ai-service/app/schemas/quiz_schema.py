"""
Pydantic Structured Output Schema đảm bảo Gemini sinh đề trắc nghiệm chuẩn JSON 100%.
"""

from typing import Literal
from pydantic import BaseModel, Field


class GenerateQuizRequest(BaseModel):
    """Dữ liệu đầu vào từ C# yêu cầu sinh bộ câu hỏi trắc nghiệm từ học liệu PDF."""

    material_title: str = Field(min_length=1, max_length=250)
    extracted_text: str = Field(min_length=20)
    ai_summary: str | None = Field(default=None)
    question_count: int = Field(default=10, ge=3, le=30)
    difficulty: Literal["Easy", "Medium", "Hard"] = Field(default="Medium")


class GeneratedQuizQuestionSchema(BaseModel):
    """Cấu trúc 1 câu hỏi trắc nghiệm ép kiểu trực tiếp qua Gemini response_schema."""

    question_text: str = Field(description="Nội dung câu hỏi trắc nghiệm rõ ràng bằng tiếng Việt")
    option_a: str = Field(description="Nội dung lựa chọn A (không kèm nhãn 'A.')")
    option_b: str = Field(description="Nội dung lựa chọn B (không kèm nhãn 'B.')")
    option_c: str = Field(description="Nội dung lựa chọn C (không kèm nhãn 'C.')")
    option_d: str = Field(description="Nội dung lựa chọn D (không kèm nhãn 'D.')")
    correct_option: Literal["A", "B", "C", "D"] = Field(
        description="Ký tự đáp án đúng duy nhất: A, B, C hoặc D"
    )
    explanation: str = Field(
        description="Giải thích học thuật chi tiết vì sao đáp án đó đúng dựa trên tài liệu"
    )


class GeneratedQuizPayload(BaseModel):
    """Gói dữ liệu đề thi hoàn chỉnh do Gemini sinh theo chuẩn Structured Output."""

    suggested_title: str = Field(description="Tiêu đề đề thi gợi ý ngắn gọn bằng tiếng Việt")
    questions: list[GeneratedQuizQuestionSchema] = Field(
        description="Danh sách các câu hỏi trắc nghiệm đã biên soạn"
    )


class SubjectPerformanceInput(BaseModel):
    """Thống kê độ chính xác theo từng môn học/phòng học của sinh viên."""

    subject_category: str = Field(min_length=1, max_length=150)
    room_name: str = Field(min_length=1, max_length=200)
    total_answered: int = Field(ge=0)
    correct_count: int = Field(ge=0)
    accuracy_percent: float = Field(ge=0.0, le=100.0)


class WrongAnswerItemInput(BaseModel):
    """Chi tiết một câu hỏi sinh viên từng trả lời sai trong lịch sử QuizAttemptAnswers."""

    subject_category: str = Field(min_length=1, max_length=150)
    room_name: str = Field(min_length=1, max_length=200)
    quiz_title: str = Field(min_length=1, max_length=250)
    question_text: str = Field(min_length=3)
    selected_option: str | None = Field(default=None)
    correct_option: str = Field(min_length=1, max_length=5)
    explanation: str = Field(default="")


class AnalyzeWeaknessRequest(BaseModel):
    """Payload từ C# Backend gửi sang AI Microservice để chẩn đoán lỗ hổng kiến thức."""

    student_name: str = Field(min_length=1, max_length=120)
    subject_stats: list[SubjectPerformanceInput] = Field(default_factory=list)
    wrong_answers: list[WrongAnswerItemInput] = Field(default_factory=list)


class WeaknessGapSchema(BaseModel):
    """Một mảng kiến thức sinh viên đang bị hổng theo chuẩn Structured Output."""

    topic_name: str = Field(description="Tên chủ đề hoặc mảng kiến thức bị hổng (ngắn gọn, rõ ý)")
    subject_category: str = Field(description="Tên môn học chứa lỗ hổng này")
    mistake_count: int = Field(ge=1, description="Số câu làm sai thuộc mảng kiến thức này")
    severity: Literal["High", "Medium", "Low"] = Field(description="Mức độ nghiêm trọng: High, Medium hoặc Low")
    root_cause: str = Field(description="Phân tích nguyên nhân sinh viên hay nhầm lẫn ở mảng này")
    study_action: str = Field(description="Gợi ý hành động ôn tập thực chiến cụ thể để vá lỗ hổng")
    recommended_prompt: str = Field(description="Câu hỏi gợi ý bắt đầu bằng '@bot ' để hỏi trực tiếp AI")


class AnalyzeWeaknessPayload(BaseModel):
    """Kết quả chẩn đoán Smart Weakness Radar từ Gemini 2.5 Flash Structured Output."""

    overall_diagnosis: str = Field(description="Nhận xét tổng quan 2-3 câu về năng lực và xu hướng sai sót của sinh viên")
    strongest_subject: str = Field(description="Môn học sinh viên đang làm tốt nhất")
    priority_subject: str = Field(description="Môn học cần ưu tiên củng cố sớm nhất")
    weakness_gaps: list[WeaknessGapSchema] = Field(
        description="Danh sách tối đa 3 lỗ hổng kiến thức nổi bật nhất cần khắc phục"
    )
    study_roadmap: list[str] = Field(
        description="Lộ trình ôn tập 3-4 bước cụ thể bằng tiếng Việt theo thứ tự ưu tiên"
    )
