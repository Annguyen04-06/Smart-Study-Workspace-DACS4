"""
Wrapper quản lý kết nối tới Google Gemini / Vertex AI thông qua SDK chính thức google-genai.
Hỗ trợ đa định dạng khóa API:
- Khóa "AQ.*": Khởi tạo Vertex AI Express Mode genai.Client(vertexai=True, api_key=...),
  đồng thời tự động chuyển mạch dự phòng sang Generative Language Express endpoint nếu GCP Project chưa bật aiplatform.googleapis.com.
- Khóa "AIza*": Khởi tạo chuẩn Google AI Studio genai.Client(api_key=...).
- Tự động chuyển mạch chuỗi model Gemini Flash tương thích mới nhất khi gặp 404 (model ngừng cấp mới), 503 (quá tải cục bộ)
  hoặc 429 (hết hạn mức của riêng model đó); model chậm/lỗi bị tạm xếp cuối để câu hỏi sau dùng model nhanh hơn.
- Client google-genai được tạo 1 lần (Singleton theo khóa) và tái sử dụng kết nối cho mọi request.
- Chế độ hội thoại nhanh cho @bot: suy luận tối thiểu + giới hạn max_output_tokens, hỗ trợ Streaming từng cụm chữ
  (generate_content_stream). Gemini 3.x dùng thinking_level="minimal", Gemini 2.5 dùng thinking_budget=0;
  model nào từ chối cấu hình nào sẽ được ghi nhớ và tự hạ xuống cấu hình kế tiếp.
"""

import asyncio
import logging
import re
import time
from dataclasses import dataclass
from pathlib import Path
from typing import AsyncIterator, Awaitable, Callable, TypeVar
from google import genai
from google.genai import types
from pydantic import BaseModel

from app.core.config import Settings
from app.core.exceptions import AiConfigurationError, AiGenerationError
from app.schemas.base import TokenUsageMetadata

logger = logging.getLogger("smart_study.gemini")
TSchema = TypeVar("TSchema", bound=BaseModel)
TextRequestFn = Callable[[genai.Client, str, types.GenerateContentConfig], Awaitable[object]]


@dataclass(frozen=True)
class TextStreamSummary:
    """Phần tử cuối cùng của luồng stream_text: Token tiêu thụ, thời gian ra chữ đầu tiên và cờ bị cắt do giới hạn token."""

    usage: TokenUsageMetadata
    first_chunk_ms: int
    truncated: bool


class GeminiClientWrapper:
    """Quản lý kết nối google-genai Client đa chế độ (Vertex Express / AI Studio) & tự phục hồi model."""

    FALLBACK_FLASH_MODELS: tuple[str, ...] = (
        "gemini-2.5-flash",
        "gemini-3.5-flash-lite",
        "gemini-3-flash-preview",
        "gemini-3.8-flash",
        "gemini-flash-latest",
    )

    # Chọn model theo độ trễ thực tế: model ra chữ đầu tiên quá chậm bị xếp cuối 5 phút, model 404 bị xếp cuối 1 giờ
    SLOW_FIRST_CHUNK_MS = 2500
    SLOW_MODEL_COOLDOWN_SECONDS = 300
    MISSING_MODEL_COOLDOWN_SECONDS = 3600
    QUOTA_COOLDOWN_SECONDS = 60

    def __init__(self, settings: Settings) -> None:
        self._settings = settings
        self._configured_model = (settings.gemini_model_name or "gemini-2.5-flash").strip()
        self._last_working_model: str = self._configured_model
        self._api_key = settings.gemini_api_key.strip()
        self._vertex_disabled_on_project = False
        self._vertex_client: genai.Client | None = None
        self._studio_client: genai.Client | None = None
        self._rejected_fast_thinking: dict[str, int] = {}
        self._demoted_until: dict[str, float] = {}

    @property
    def is_configured(self) -> bool:
        return bool(self._api_key) and self._api_key != "YOUR_GEMINI_API_KEY_HERE"

    @property
    def is_vertex_express_key(self) -> bool:
        return self.is_configured and self._api_key.startswith("AQ.")

    @property
    def provider_mode_label(self) -> str:
        if not self.is_configured:
            return "Local Grounded Fallback Mode"
        if self.is_vertex_express_key:
            return (
                "Vertex AI Express Mode (GL Fallback Active)"
                if self._vertex_disabled_on_project
                else "Vertex AI Express Mode"
            )
        return "Google AI Studio Mode"

    @property
    def model_name(self) -> str:
        return self._last_working_model or self._configured_model

    @property
    def masked_key(self) -> str | None:
        if not self.is_configured:
            return None
        if len(self._api_key) <= 10:
            return "••••••••"
        return f"{self._api_key[:5]}••••••••{self._api_key[-4:]}"

    def _get_candidate_clients(self, key: str) -> list[tuple[str, genai.Client]]:
        """Trả về danh sách Client ưu tiên: nếu key AQ.* thì ưu tiên vertexai=True trước, dự phòng sang api_key trực tiếp."""
        cleaned = key.strip()
        candidates: list[tuple[str, genai.Client]] = []

        # Giới hạn thời gian chờ mỗi lần đọc mạng để 1 kết nối treo không chặn @bot vô thời hạn (SDK mặc định không có timeout)
        http_options = types.HttpOptions(timeout=self._settings.gemini_request_timeout_seconds * 1000)

        if cleaned.startswith("AQ.") and not self._vertex_disabled_on_project:
            if self._vertex_client is None or cleaned != self._api_key:
                self._vertex_client = genai.Client(vertexai=True, api_key=cleaned, http_options=http_options)
            candidates.append(("vertex", self._vertex_client))

        if self._studio_client is None or cleaned != self._api_key:
            self._studio_client = genai.Client(api_key=cleaned, http_options=http_options)
        candidates.append(("studio", self._studio_client))

        return candidates

    def _get_candidate_models(self) -> list[str]:
        """Xây dựng chuỗi model ưu tiên bắt đầu từ model vừa chạy thành công gần nhất."""
        ordered: list[str] = []
        for m in (self._last_working_model, self._configured_model, *self.FALLBACK_FLASH_MODELS):
            if m and m not in ordered:
                ordered.append(m)
        # Model đang bị hạ hạng (quá chậm / không tồn tại) được dời xuống cuối, vẫn giữ thứ tự tương đối
        now = time.monotonic()
        demoted = {m for m, until in self._demoted_until.items() if until > now}
        return [m for m in ordered if m not in demoted] + [m for m in ordered if m in demoted]

    def _demote_model(self, model: str, seconds: int, reason: str) -> None:
        self._demoted_until[model] = time.monotonic() + seconds
        if self._last_working_model == model:
            self._last_working_model = self._configured_model
        logger.info("Tạm xếp cuối model [%s] trong %d giây: %s.", model, seconds, reason)

    @staticmethod
    def _is_service_disabled_error(err_upper: str) -> bool:
        return "SERVICE_DISABLED" in err_upper or "AIPLATFORM.GOOGLEAPIS.COM" in err_upper

    @staticmethod
    def _is_retryable_model_error(err_upper: str) -> bool:
        return (
            "404" in err_upper
            or "NOT_FOUND" in err_upper
            or "NO LONGER AVAILABLE" in err_upper
            or "503" in err_upper
            or "UNAVAILABLE" in err_upper
            or "HIGH DEMAND" in err_upper
            or GeminiClientWrapper._is_quota_error(err_upper)
        )

    @staticmethod
    def _is_quota_error(err_upper: str) -> bool:
        # Hạn mức Gemini tính riêng theo từng model => hết quota model này vẫn thử được model khác
        return "429" in err_upper or "RESOURCE_EXHAUSTED" in err_upper

    @staticmethod
    def _translate_provider_exception(exc: Exception) -> str:
        raw = str(exc)
        upper = raw.upper()
        if "429" in upper or "RESOURCE_EXHAUSTED" in upper or "QUOTA" in upper:
            return (
                "Hạn mức (Quota) gọi Google Gemini/Vertex AI tạm thời đạt giới hạn trong phút này. "
                "Vui lòng đợi vài giây rồi thử lại."
            )
        if "401" in upper or "403" in upper or "API_KEY_INVALID" in upper or "UNAUTHENTICATED" in upper or "PERMISSION_DENIED" in upper:
            return (
                "Khóa API Gemini/Vertex không hợp lệ hoặc dự án Google Cloud chưa bật quyền truy cập API. "
                f"Chi tiết: {raw[:220]}"
            )
        if "TIMEOUT" in upper or "CONNECT" in upper or "NETWORK" in upper or "DNS" in upper:
            return "Kết nối mạng tới máy chủ Google Gemini/Vertex AI bị gián đoạn hoặc quá thời gian chờ."
        return f"Lỗi phản hồi từ dịch vụ Gemini/Vertex AI: {raw[:260]}"

    async def _execute_with_resilience(
        self,
        key: str,
        call_fn: Callable[[genai.Client, str], Awaitable[object]],
    ) -> tuple[object, str]:
        """Thực thi cuộc gọi Gemini có tự động chuyển đổi Vertex -> GL Express và tự động chọn model khả dụng."""
        last_exc: Exception | None = None
        clients = self._get_candidate_clients(key)
        models = self._get_candidate_models()

        for client_kind, client in clients:
            for candidate_model in models:
                try:
                    response = await call_fn(client, candidate_model)
                    if key == self._api_key and self._last_working_model != candidate_model:
                        logger.info(
                            "Chuyển sang sử dụng model khả dụng [%s] trên kênh [%s].",
                            candidate_model,
                            client_kind,
                        )
                        self._last_working_model = candidate_model
                    return response, candidate_model
                except Exception as exc:
                    last_exc = exc
                    err_upper = str(exc).upper()

                    if client_kind == "vertex" and self._is_service_disabled_error(err_upper):
                        logger.warning(
                            "Vertex AI Platform API chưa bật trên GCP Project của khóa AQ.*; "
                            "tự động chuyển sang kênh Generative Language Express."
                        )
                        if key == self._api_key:
                            self._vertex_disabled_on_project = True
                        break

                    if self._is_retryable_model_error(err_upper):
                        is_missing_model = "404" in err_upper or "NOT_FOUND" in err_upper
                        if is_missing_model and key == self._api_key and client_kind == "studio":
                            self._demote_model(candidate_model, self.MISSING_MODEL_COOLDOWN_SECONDS, "model không tồn tại với khóa này")
                        elif self._is_quota_error(err_upper) and key == self._api_key:
                            self._demote_model(candidate_model, self.QUOTA_COOLDOWN_SECONDS, "hết hạn mức (429) trong phút này")
                        continue

                    break

        assert last_exc is not None
        raise AiGenerationError(self._translate_provider_exception(last_exc)) from last_exc

    async def warm_up(self) -> None:
        """
        Gọi thử 1 yêu cầu siêu nhỏ lúc khởi động để dò sẵn kênh (Vertex/Generative Language), model khả dụng
        và cấu hình suy luận nhanh => câu hỏi @bot đầu tiên của sinh viên không phải chờ chuỗi thử 403/404.
        """
        if not self.is_configured:
            return
        try:
            # Dùng đường stream để hưởng cơ chế hạn chờ chữ đầu tiên: model treo kết nối sẽ bị bỏ qua sau 8 giây
            async for _ in self.stream_text(
                prompt="Trả lời đúng 1 từ: OK",
                system_instruction="Bạn là trợ lý kiểm tra kết nối.",
                temperature=0.0,
                max_output_tokens=16,
                disable_thinking=True,
                first_chunk_timeout_seconds=8.0,
            ):
                pass
            logger.info("Đã khởi động nóng Gemini: model [%s] sẵn sàng phục vụ @bot.", self.model_name)
        except AiGenerationError as exc:
            # Model đã được chọn xong dù câu trả lời thử rỗng; chỉ ghi nhận để theo dõi
            logger.info("Khởi động nóng Gemini hoàn tất với cảnh báo: %s", exc.message)
        except Exception:
            logger.warning("Không khởi động nóng được Gemini (sẽ tự dò model ở yêu cầu đầu tiên).", exc_info=True)

    async def verify_and_update_api_key(self, new_api_key: str) -> None:
        """Kiểm định trực tiếp khóa mới (cả AQ.* lẫn AIza*), cập nhật bộ nhớ và ghi vào .env."""
        cleaned_key = new_api_key.strip()
        if len(cleaned_key) < 12 or cleaned_key == "YOUR_GEMINI_API_KEY_HERE":
            raise AiConfigurationError("Khóa GEMINI_API_KEY không đúng định dạng hợp lệ.")

        self._vertex_client = None
        self._studio_client = None
        self._vertex_disabled_on_project = False

        _, resolved_model = await self._execute_with_resilience(
            cleaned_key,
            lambda c, m: c.aio.models.generate_content(
                model=m,
                contents="Trả lời đúng 1 từ: OK",
                config=types.GenerateContentConfig(max_output_tokens=16, temperature=0.0),
            ),
        )

        self._api_key = cleaned_key
        self._settings.gemini_api_key = cleaned_key
        self._last_working_model = resolved_model
        self._persist_key_to_dotenv(cleaned_key)
        logger.info("Đã kích hoạt khóa AI [%s] với model [%s].", self.provider_mode_label, resolved_model)

    @staticmethod
    def _persist_key_to_dotenv(cleaned_key: str) -> None:
        env_path = Path(".env")
        escaped_line = f"GEMINI_API_KEY={cleaned_key}"

        if not env_path.exists():
            env_path.write_text(f"{escaped_line}\nGEMINI_MODEL=gemini-2.5-flash\n", encoding="utf-8")
            return

        lines = env_path.read_text(encoding="utf-8").splitlines()
        replaced = False
        updated_lines: list[str] = []
        for line in lines:
            if line.strip().startswith("GEMINI_API_KEY="):
                updated_lines.append(escaped_line)
                replaced = True
            else:
                updated_lines.append(line)

        if not replaced:
            updated_lines.append(escaped_line)

        env_path.write_text("\n".join(updated_lines) + "\n", encoding="utf-8")

    def _extract_usage(self, response: object, used_model: str, started_at: float) -> TokenUsageMetadata:
        duration_ms = max(1, int((time.perf_counter() - started_at) * 1000))
        usage_meta = getattr(response, "usage_metadata", None)

        prompt_tokens = int(getattr(usage_meta, "prompt_token_count", 0) or 0)
        completion_tokens = int(getattr(usage_meta, "candidates_token_count", 0) or 0)
        total_tokens = int(
            getattr(usage_meta, "total_token_count", 0) or (prompt_tokens + completion_tokens)
        )

        return TokenUsageMetadata(
            model_name=used_model,
            prompt_tokens=prompt_tokens,
            completion_tokens=completion_tokens,
            total_tokens=total_tokens,
            duration_ms=duration_ms,
        )

    @staticmethod
    def _zero_budget_thinking() -> types.ThinkingConfig | None:
        """Gemini 2.5: thinking_budget=0 tắt hẳn suy luận nội bộ."""
        try:
            return types.ThinkingConfig(thinking_budget=0)
        except (AttributeError, TypeError, ValueError):
            return None

    @staticmethod
    def _minimal_level_thinking() -> types.ThinkingConfig | None:
        """Gemini 3.x: thinking_level="minimal" là mức suy luận thấp nhất (đo thực tế TTFT ~1 giây)."""
        try:
            return types.ThinkingConfig(thinking_level="minimal")
        except (AttributeError, TypeError, ValueError):
            return None

    @staticmethod
    def _is_invalid_argument_error(err_upper: str) -> bool:
        return "INVALID_ARGUMENT" in err_upper or re.search(r"\b400\b", err_upper) is not None

    def _fast_thinking_options(self, model: str, disable_thinking: bool) -> list[types.ThinkingConfig | None]:
        """
        Thứ tự cấu hình suy luận nhanh cần thử cho model, kết thúc bằng None (cấu hình mặc định của model).
        Bỏ qua các cấu hình model này từng từ chối (Gemini trả 400 INVALID_ARGUMENT chung chung, không nêu lý do).
        """
        if not disable_thinking:
            return [None]
        builders = [self._zero_budget_thinking, self._minimal_level_thinking]
        if not model.startswith("gemini-2"):
            builders.reverse()
        options: list[types.ThinkingConfig | None] = [cfg for cfg in (build() for build in builders) if cfg is not None]
        options.append(None)
        return options[self._rejected_fast_thinking.get(model, 0):]

    @staticmethod
    def _build_text_config(
        system_instruction: str,
        temperature: float,
        max_output_tokens: int | None,
        thinking_config: types.ThinkingConfig | None,
    ) -> types.GenerateContentConfig:
        config_kwargs: dict[str, object] = {
            "system_instruction": system_instruction,
            "temperature": temperature,
        }
        if max_output_tokens:
            config_kwargs["max_output_tokens"] = max_output_tokens
        if thinking_config is not None:
            config_kwargs["thinking_config"] = thinking_config
        return types.GenerateContentConfig(**config_kwargs)

    async def _call_text_model(
        self,
        client: genai.Client,
        model: str,
        request_fn: TextRequestFn,
        system_instruction: str,
        temperature: float,
        max_output_tokens: int | None,
        disable_thinking: bool,
    ) -> object:
        """Gọi model với cấu hình suy luận nhanh nhất mà model chấp nhận; ghi nhớ để các lần sau gọi trúng ngay."""
        options = self._fast_thinking_options(model, disable_thinking)
        for index, thinking_config in enumerate(options):
            config = self._build_text_config(system_instruction, temperature, max_output_tokens, thinking_config)
            try:
                return await request_fn(client, model, config)
            except Exception as exc:
                is_last_option = index == len(options) - 1
                if is_last_option or not self._is_invalid_argument_error(str(exc).upper()):
                    raise
                self._rejected_fast_thinking[model] = self._rejected_fast_thinking.get(model, 0) + 1
                logger.info("Model [%s] từ chối cấu hình suy luận nhanh %s -> thử cấu hình kế tiếp.", model, thinking_config)
        raise AiGenerationError("Không có cấu hình Gemini khả dụng cho model này.")

    @staticmethod
    def _read_chunk_text(chunk: object) -> str:
        try:
            return getattr(chunk, "text", None) or ""
        except (ValueError, AttributeError):
            return ""

    @staticmethod
    def _is_truncated_by_token_limit(chunk: object | None) -> bool:
        candidates = getattr(chunk, "candidates", None) or []
        finish_reason = getattr(candidates[0], "finish_reason", None) if candidates else None
        return str(finish_reason).upper().endswith("MAX_TOKENS")

    async def generate_text(
        self,
        prompt: str,
        system_instruction: str,
        temperature: float = 0.35,
        max_output_tokens: int | None = None,
        disable_thinking: bool = False,
    ) -> tuple[str, TokenUsageMetadata]:
        """Sinh văn bản học thuật tự do (Markdown + KaTeX) cho Tóm tắt PDF và Trợ giảng @bot (bản không stream)."""
        if not self.is_configured:
            raise AiConfigurationError("Chưa cấu hình khóa GEMINI_API_KEY hợp lệ.")

        async def request_content(client: genai.Client, model: str, config: types.GenerateContentConfig) -> object:
            return await client.aio.models.generate_content(model=model, contents=prompt, config=config)

        started_at = time.perf_counter()
        response, used_model = await self._execute_with_resilience(
            self._api_key,
            lambda c, m: self._call_text_model(
                c, m, request_content, system_instruction, temperature, max_output_tokens, disable_thinking
            ),
        )

        text_output = (getattr(response, "text", None) or "").strip()
        if not text_output:
            raise AiGenerationError("Google Gemini trả về nội dung rỗng.")

        usage = self._extract_usage(response, used_model, started_at)
        return text_output, usage

    async def stream_text(
        self,
        prompt: str,
        system_instruction: str,
        temperature: float = 0.4,
        max_output_tokens: int | None = None,
        disable_thinking: bool = True,
        first_chunk_timeout_seconds: float = 12.0,
    ) -> AsyncIterator[str | TextStreamSummary]:
        """
        Streaming thật từ Gemini: phát từng cụm chữ (str) ngay khi model sinh ra, phần tử cuối là TextStreamSummary.
        Chunk đầu tiên được đọc trong vòng chọn model dự phòng nên lỗi 404/503 vẫn tự chuyển model như bản thường;
        model không trả chunk đầu tiên trong hạn chờ được coi như 503 (quá tải) và chuyển sang model kế tiếp.
        """
        if not self.is_configured:
            raise AiConfigurationError("Chưa cấu hình khóa GEMINI_API_KEY hợp lệ.")

        async def read_first_chunk(client: genai.Client, model: str, config: types.GenerateContentConfig) -> object:
            stream = await client.aio.models.generate_content_stream(model=model, contents=prompt, config=config)
            first_chunk = await anext(stream, None)
            return stream, first_chunk

        async def open_stream(client: genai.Client, model: str, config: types.GenerateContentConfig) -> object:
            try:
                return await asyncio.wait_for(read_first_chunk(client, model, config), first_chunk_timeout_seconds)
            except TimeoutError as exc:
                self._demote_model(model, self.SLOW_MODEL_COOLDOWN_SECONDS, "quá hạn chờ chữ đầu tiên")
                raise AiGenerationError(
                    f"503 UNAVAILABLE: model {model} không trả chữ đầu tiên sau {first_chunk_timeout_seconds:g} giây."
                ) from exc

        started_at = time.perf_counter()
        opened, used_model = await self._execute_with_resilience(
            self._api_key,
            lambda c, m: self._call_text_model(
                c, m, open_stream, system_instruction, temperature, max_output_tokens, disable_thinking
            ),
        )
        stream, chunk = opened

        first_chunk_ms = 0
        usage_source: object | None = None
        last_chunk: object | None = None
        while chunk is not None:
            text = self._read_chunk_text(chunk)
            if text:
                if not first_chunk_ms:
                    first_chunk_ms = max(1, int((time.perf_counter() - started_at) * 1000))
                    if first_chunk_ms > self.SLOW_FIRST_CHUNK_MS:
                        self._demote_model(used_model, self.SLOW_MODEL_COOLDOWN_SECONDS, f"chữ đầu tiên chậm {first_chunk_ms} ms")
                yield text
            if getattr(chunk, "usage_metadata", None) is not None:
                usage_source = chunk
            last_chunk = chunk
            try:
                chunk = await anext(stream, None)
            except Exception as exc:
                # Model quá tải cắt ngang luồng (503 high demand) => hạ hạng để câu hỏi kế tiếp dùng model khác
                if self._is_retryable_model_error(str(exc).upper()):
                    self._demote_model(used_model, self.SLOW_MODEL_COOLDOWN_SECONDS, "luồng bị ngắt giữa chừng do quá tải")
                raise AiGenerationError(self._translate_provider_exception(exc)) from exc

        if not first_chunk_ms:
            raise AiGenerationError("Google Gemini trả về nội dung rỗng.")

        yield TextStreamSummary(
            usage=self._extract_usage(usage_source, used_model, started_at),
            first_chunk_ms=first_chunk_ms,
            truncated=self._is_truncated_by_token_limit(last_chunk),
        )

    async def generate_structured(
        self,
        prompt: str,
        system_instruction: str,
        schema_cls: type[TSchema],
        temperature: float = 0.25,
    ) -> tuple[TSchema, TokenUsageMetadata]:
        """Sinh dữ liệu có cấu trúc JSON khớp 100% với Pydantic v2 Schema (Quiz & Flashcards)."""
        if not self.is_configured:
            raise AiConfigurationError("Chưa cấu hình khóa GEMINI_API_KEY hợp lệ.")

        started_at = time.perf_counter()
        response, used_model = await self._execute_with_resilience(
            self._api_key,
            lambda c, m: c.aio.models.generate_content(
                model=m,
                contents=prompt,
                config=types.GenerateContentConfig(
                    system_instruction=system_instruction,
                    temperature=temperature,
                    response_mime_type="application/json",
                    response_schema=schema_cls,
                ),
            ),
        )

        parsed_obj = getattr(response, "parsed", None)
        if isinstance(parsed_obj, schema_cls):
            validated = parsed_obj
        else:
            raw_json = (getattr(response, "text", None) or "").strip()
            if not raw_json:
                raise AiGenerationError("Gemini không trả về chuỗi JSON hợp lệ.")
            try:
                validated = schema_cls.model_validate_json(raw_json)
            except Exception as exc:
                raise AiGenerationError(
                    f"Dữ liệu JSON từ Gemini không khớp Schema {schema_cls.__name__}: {exc}"
                ) from exc

        usage = self._extract_usage(response, used_model, started_at)
        return validated, usage
