"""
Dịch vụ trích xuất văn bản từ tệp PDF tiếng Việt (pdfplumber kết hợp pypdf dự phòng),
chuẩn hóa bảng mã Unicode dựng sẵn (NFC), chia đoạn ngữ nghĩa và gọi Gemini tóm tắt.
"""

import io
import re
import time
import unicodedata
import pdfplumber
from pypdf import PdfReader

from app.core.config import Settings
from app.core.exceptions import PdfParsingError
from app.prompts.academic_prompts import (
    PDF_SUMMARY_SYSTEM_INSTRUCTION,
    build_pdf_summary_prompt,
)
from app.schemas.base import TokenUsageMetadata
from app.schemas.pdf_schema import PdfExtractionResult
from app.services.gemini_client import GeminiClientWrapper
from app.services.local_grounded_nlp import build_extractive_pdf_summary


class PdfProcessingService:
    """Thực thi quy trình đọc tệp PDF nhị phân -> làm sạch tiếng Việt -> chia đoạn -> tóm tắt AI."""

    def __init__(self, settings: Settings, gemini_client: GeminiClientWrapper) -> None:
        self._settings = settings
        self._gemini = gemini_client

    @staticmethod
    def clean_vietnamese_text(raw_text: str) -> str:
        """Chuẩn hóa Unicode NFC cho dấu tiếng Việt, khử ký tự điều khiển và dòng trống thừa."""
        normalized = unicodedata.normalize("NFC", raw_text or "")
        normalized = normalized.replace("\x00", "").replace("\r\n", "\n").replace("\r", "\n")
        normalized = re.sub(r"(\w)-\n(\w)", r"\1\2", normalized)
        normalized = re.sub(r"[ \t]+", " ", normalized)
        normalized = re.sub(r"\n{3,}", "\n\n", normalized)
        return normalized.strip()

    @staticmethod
    def split_into_chunks(text: str, chunk_size: int = 1800, overlap: int = 200) -> list[str]:
        """Chia văn bản dài thành các đoạn chồng lấp nhẹ để bảo toàn ngữ cảnh học thuật."""
        if not text:
            return []
        paragraphs = [p.strip() for p in text.split("\n\n") if p.strip()]
        chunks: list[str] = []
        current_buffer = ""

        for para in paragraphs:
            candidate = f"{current_buffer}\n\n{para}".strip() if current_buffer else para
            if len(candidate) <= chunk_size:
                current_buffer = candidate
            else:
                if current_buffer:
                    chunks.append(current_buffer)
                    tail = current_buffer[-overlap:] if len(current_buffer) > overlap else current_buffer
                    current_buffer = f"{tail}\n\n{para}".strip()
                else:
                    for idx in range(0, len(para), chunk_size - overlap):
                        chunks.append(para[idx : idx + chunk_size])
                    current_buffer = ""

        if current_buffer:
            chunks.append(current_buffer)
        return chunks

    def _extract_raw_pdf_bytes(self, pdf_bytes: bytes) -> tuple[int, str]:
        """Đọc số trang và bóc tách văn bản bằng pdfplumber, tự động dự phòng sang pypdf."""
        max_bytes = self._settings.max_pdf_size_mb * 1024 * 1024
        if not pdf_bytes:
            raise PdfParsingError("Tệp PDF tải lên bị rỗng.")
        if len(pdf_bytes) > max_bytes:
            raise PdfParsingError(
                f"Dung lượng tệp PDF vượt quá giới hạn {self._settings.max_pdf_size_mb} MB."
            )

        extracted_pages: list[str] = []
        page_count = 0

        try:
            with pdfplumber.open(io.BytesIO(pdf_bytes)) as pdf_doc:
                page_count = len(pdf_doc.pages)
                for page in pdf_doc.pages:
                    page_text = page.extract_text(x_tolerance=2, y_tolerance=3) or ""
                    if page_text.strip():
                        extracted_pages.append(page_text)
        except Exception:
            extracted_pages = []

        if not extracted_pages:
            try:
                reader = PdfReader(io.BytesIO(pdf_bytes))
                page_count = len(reader.pages)
                for page in reader.pages:
                    page_text = page.extract_text() or ""
                    if page_text.strip():
                        extracted_pages.append(page_text)
            except Exception as exc:
                raise PdfParsingError(f"Không thể đọc cấu trúc tệp PDF: {exc}") from exc

        if page_count <= 0:
            raise PdfParsingError("Tệp PDF không chứa trang hợp lệ nào.")

        cleaned = self.clean_vietnamese_text("\n\n".join(extracted_pages))
        if len(cleaned) < 15:
            raise PdfParsingError(
                "Không trích xuất được văn bản từ tệp PDF (tệp có thể là ảnh scan chưa OCR)."
            )

        return page_count, cleaned

    async def extract_and_summarize(
        self, pdf_bytes: bytes, title: str
    ) -> tuple[PdfExtractionResult, TokenUsageMetadata]:
        """Bóc tách toàn văn tệp PDF và sinh bản Tóm tắt Học thuật qua Gemini 2.5 Flash (hoặc Grounded NLP cục bộ)."""
        started_at = time.perf_counter()
        page_count, cleaned_text = self._extract_raw_pdf_bytes(pdf_bytes)
        chunks = self.split_into_chunks(cleaned_text)

        if self._gemini.is_configured:
            context_for_ai = cleaned_text[: self._settings.max_context_chars]
            prompt = build_pdf_summary_prompt(title=title, cleaned_text=context_for_ai)
            ai_summary, usage = await self._gemini.generate_text(
                prompt=prompt,
                system_instruction=PDF_SUMMARY_SYSTEM_INSTRUCTION,
                temperature=0.25,
            )
        else:
            ai_summary, usage = build_extractive_pdf_summary(title, cleaned_text, started_at)

        result = PdfExtractionResult(
            page_count=page_count,
            extracted_text=cleaned_text,
            chunks=chunks,
            ai_summary=ai_summary,
        )
        return result, usage
