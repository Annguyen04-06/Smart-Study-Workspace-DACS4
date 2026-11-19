"""
Dịch vụ sinh Sơ đồ Tư duy Kiến thức (Mermaid Flowchart / Mindmap) từ học liệu PDF bằng Gemini Structured Output.
Bao gồm bộ chuẩn hóa cú pháp Mermaid tự động nhằm triệt tiêu lỗi render trên trình duyệt.
"""

import re
import time
from app.core.config import Settings
from app.prompts.academic_prompts import (
    MINDMAP_GENERATION_SYSTEM_INSTRUCTION,
    build_mindmap_generation_prompt,
)
from app.schemas.base import TokenUsageMetadata
from app.schemas.mindmap_schema import GenerateMindmapRequest, GeneratedMindmapPayload
from app.services.gemini_client import GeminiClientWrapper
from app.services.local_grounded_nlp import build_extractive_mindmap


class MindmapGeneratorService:
    """Tổng hợp nội dung bài giảng PDF thành sơ đồ tư duy trực quan chuẩn cú pháp Mermaid."""

    def __init__(self, settings: Settings, gemini_client: GeminiClientWrapper) -> None:
        self._settings = settings
        self._gemini = gemini_client

    @staticmethod
    def _sanitize_mermaid_code(raw_code: str) -> str:
        """Làm sạch hàng rào Markdown và đảm bảo khai báo đầu biểu đồ hợp lệ."""
        cleaned = (raw_code or "").strip()
        cleaned = re.sub(r"^```(?:mermaid)?\s*", "", cleaned, flags=re.IGNORECASE)
        cleaned = re.sub(r"\s*```$", "", cleaned)
        cleaned = cleaned.replace("<br/>", " - ").replace("<br>", " - ")

        lines = [ln.rstrip() for ln in cleaned.splitlines() if ln.strip()]
        if not lines:
            return 'flowchart TD\n    root["Sơ đồ Tư duy Học thuật"]'

        first = lines[0].strip()
        if not re.match(r"^(flowchart|graph|mindmap)\b", first, flags=re.IGNORECASE):
            lines.insert(0, "flowchart TD")

        return "\n".join(lines)

    async def generate_mindmap(
        self, request: GenerateMindmapRequest
    ) -> tuple[GeneratedMindmapPayload, TokenUsageMetadata]:
        """Gọi Gemini 2.5 Flash Structured Output sinh sơ đồ Mermaid kèm thống kê Token."""
        started_at = time.perf_counter()

        if not self._gemini.is_configured:
            return build_extractive_mindmap(request, started_at)

        combined_source = request.extracted_text.strip()
        if request.ai_summary:
            combined_source = (
                f"Tóm tắt trọng tâm:\n{request.ai_summary.strip()}\n\n"
                f"Nội dung chi tiết:\n{combined_source}"
            )
        trimmed_source = combined_source[: self._settings.max_context_chars]

        prompt = build_mindmap_generation_prompt(
            material_title=request.material_title,
            source_content=trimmed_source,
        )

        payload, usage = await self._gemini.generate_structured(
            prompt=prompt,
            system_instruction=MINDMAP_GENERATION_SYSTEM_INSTRUCTION,
            schema_cls=GeneratedMindmapPayload,
            temperature=0.25,
        )

        payload.mermaid_code = self._sanitize_mermaid_code(payload.mermaid_code)
        if payload.node_count < 4:
            payload.node_count = max(4, payload.mermaid_code.count("["))
        return payload, usage
