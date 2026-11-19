"""
Bộ xử lý NLP Học thuật Cục bộ dựa trên văn bản PDF thực tế (Extractive PDF-Grounded Fallback).
Được kích hoạt tự động khi người dùng chưa điền GEMINI_API_KEY hoặc mất kết nối Internet lúc bảo vệ đồ án,
giúp các tính năng Tóm tắt PDF, @bot, Sinh Quiz và Flashcards vẫn vận hành mượt mà từ chính nội dung PDF thật.
"""

import re
import time
from app.schemas.assistant_schema import AssistantChatRequest
from app.schemas.base import TokenUsageMetadata
from app.schemas.flashcard_schema import (
    GenerateFlashcardsRequest,
    GeneratedFlashcardDeckPayload,
    GeneratedFlashcardItemSchema,
)
from app.schemas.mindmap_schema import GenerateMindmapRequest, GeneratedMindmapPayload
from app.schemas.quiz_schema import (
    AnalyzeWeaknessPayload,
    AnalyzeWeaknessRequest,
    GenerateQuizRequest,
    GeneratedQuizPayload,
    GeneratedQuizQuestionSchema,
    WeaknessGapSchema,
)


def _split_clean_sentences(text: str) -> list[str]:
    """Tách văn bản học liệu thực tế thành danh sách các câu có độ dài và ngữ nghĩa rõ ràng."""
    if not text:
        return []
    cleaned = re.sub(r"\s+", " ", text).strip()
    raw_parts = re.split(r"(?<=[\.\!\?\n])\s+", cleaned)
    sentences: list[str] = []
    seen: set[str] = set()
    for part in raw_parts:
        s = part.strip(" -•\t\r\n")
        if len(s) >= 28 and s.lower() not in seen:
            seen.add(s.lower())
            sentences.append(s)
    return sentences


def _score_sentence_relevance(sentence: str, query: str) -> float:
    """Tính điểm độ liên quan từ vựng giữa câu trong tài liệu PDF và câu hỏi của sinh viên."""
    query_tokens = {
        w.lower()
        for w in re.findall(r"\w+", query)
        if len(w) >= 2 and w.lower() not in {"bot", "cho", "hỏi", "của", "trong", "như", "thế", "nào", "hãy", "các", "và", "là"}
    }
    if not query_tokens:
        return float(len(sentence) <= 240)
    sent_lower = sentence.lower()
    hits = sum(1 for token in query_tokens if token in sent_lower)
    return hits * 3.0 + (1.0 if ":" in sentence or "(" in sentence else 0.2)


def score_text_relevance(text: str, query: str) -> float:
    """Điểm liên quan từ vựng giữa một khối văn bản và câu hỏi (dùng chọn học liệu đưa vào ngữ cảnh @bot)."""
    return _score_sentence_relevance(text, query)


def build_extractive_pdf_summary(title: str, cleaned_text: str, started_at: float) -> tuple[str, TokenUsageMetadata]:
    """Trích xuất các luận điểm trọng tâm trực tiếp từ văn bản PDF thực tế."""
    sentences = _split_clean_sentences(cleaned_text)
    key_points = sentences[:6] if sentences else [cleaned_text[:300]]
    bullets = "\n".join(f"- {pt}" for pt in key_points)

    summary = (
        f"### Tóm tắt Học liệu: {title}\n"
        f"{bullets}\n\n"
        f"> *Ghi chú: Bản tóm tắt trích xuất trực tiếp từ văn bản gốc PDF. "
        f"Bạn có thể gắn `GEMINI_API_KEY` trên thanh công cụ để sử dụng suy luận sâu từ `gemini-2.5-flash`.*"
    )
    duration_ms = max(1, int((time.perf_counter() - started_at) * 1000))
    usage = TokenUsageMetadata(
        model_name="local-pdf-grounded",
        prompt_tokens=0,
        completion_tokens=0,
        total_tokens=0,
        duration_ms=duration_ms,
    )
    return summary, usage


def build_extractive_assistant_reply(request: AssistantChatRequest, started_at: float) -> tuple[str, TokenUsageMetadata]:
    """Đối chiếu câu hỏi @bot với toàn bộ tóm tắt và trích đoạn PDF của phòng học."""
    corpus_blocks: list[tuple[str, str]] = []
    for mat in request.material_summaries:
        combined = f"{mat.summary}\n{mat.extracted_excerpt or ''}".strip()
        for sent in _split_clean_sentences(combined):
            corpus_blocks.append((mat.title, sent))

    if not corpus_blocks:
        answer = (
            f"Chào bạn! Phòng **{request.room_name}** hiện chưa có học liệu PDF nào để đối chiếu.\n\n"
            "1. Hãy tải lên giáo trình `.pdf` ở cột **Không gian Học liệu & AI** bên phải.\n"
            "2. Hoặc nhấn nút **Cấu hình Gemini AI** trên thanh tiêu đề để kết nối trực tiếp model `gemini-2.5-flash`."
        )
    else:
        ranked = sorted(
            corpus_blocks,
            key=lambda item: _score_sentence_relevance(item[1], request.question),
            reverse=True,
        )
        selected = ranked[:4]
        source_titles = sorted({item[0] for item in selected})
        bullet_lines = "\n".join(f"{idx}. {sent}" for idx, (_, sent) in enumerate(selected, start=1))

        answer = (
            f"Dựa trên học liệu **{', '.join(source_titles)}** trong phòng **{request.room_name}**, "
            f"các luận điểm cốt lõi liên quan tới câu hỏi của bạn gồm:\n\n"
            f"{bullet_lines}\n\n"
            f"---\n"
            f"*💡 Đang phản hồi bằng chế độ **Grounded PDF Cục bộ**. Bạn có thể nhấn nút **Cấu hình Gemini AI** ở góc trên để gắn `GEMINI_API_KEY` mở khóa phân tích chuyên sâu từ `gemini-2.5-flash`.*"
        )

    duration_ms = max(1, int((time.perf_counter() - started_at) * 1000))
    usage = TokenUsageMetadata(
        model_name="local-pdf-grounded",
        prompt_tokens=0,
        completion_tokens=0,
        total_tokens=0,
        duration_ms=duration_ms,
    )
    return answer, usage


def build_extractive_quiz(request: GenerateQuizRequest, started_at: float) -> tuple[GeneratedQuizPayload, TokenUsageMetadata]:
    """Sinh bộ câu hỏi trắc nghiệm 4 lựa chọn bám sát các mệnh đề thật trong tài liệu PDF."""
    source_text = f"{request.extracted_text}\n{request.ai_summary or ''}"
    sentences = _split_clean_sentences(source_text)
    if not sentences:
        sentences = [request.extracted_text.strip()[:220]]

    distractor_pool = [
        "Phương án này đảo ngược chiều phụ thuộc và vi phạm nguyên tắc thiết kế cốt lõi trong tài liệu.",
        "Chỉ áp dụng cho hệ thống đơn khối tĩnh, không phản ánh đúng cơ chế nêu trong học liệu.",
        "Bỏ qua điều kiện ràng buộc bắt buộc khiến kết quả thực thi không đảm bảo tính đúng đắn.",
        "Làm tăng độ phức tạp không cần thiết và trái với kết luận trọng tâm của bài giảng.",
    ]
    option_letters: list[str] = ["A", "B", "C", "D"]
    questions: list[GeneratedQuizQuestionSchema] = []

    for idx in range(request.question_count):
        fact = sentences[idx % len(sentences)]
        short_fact = fact[:240].rstrip(".") + "."
        correct_pos = idx % 4

        # Lấy các mệnh đề khác trong bài hoặc mệnh đề đối lập học thuật làm phương án nhiễu
        wrong_candidates: list[str] = []
        for offset in range(1, len(sentences)):
            other = sentences[(idx + offset) % len(sentences)]
            altered = f"Không áp dụng nguyên tắc: {other[:160].rstrip('.')}."
            if altered != short_fact and altered not in wrong_candidates:
                wrong_candidates.append(altered)
            if len(wrong_candidates) >= 3:
                break

        while len(wrong_candidates) < 3:
            wrong_candidates.append(distractor_pool[(idx + len(wrong_candidates)) % len(distractor_pool)])

        opts = wrong_candidates[:3]
        opts.insert(correct_pos, short_fact)

        questions.append(
            GeneratedQuizQuestionSchema(
                question_text=(
                    f"Theo nội dung học liệu \"{request.material_title}\", nhận định nào sau đây là CHÍNH XÁC?"
                    if idx % 2 == 0
                    else f"Phát biểu nào dưới đây phản ánh đúng kiến thức trọng tâm trong \"{request.material_title}\" (Mức {request.difficulty})?"
                ),
                option_a=opts[0],
                option_b=opts[1],
                option_c=opts[2],
                option_d=opts[3],
                correct_option=option_letters[correct_pos],  # type: ignore[arg-type]
                explanation=f"Đối chiếu trực tiếp từ học liệu gốc: \"{short_fact}\"",
            )
        )

    payload = GeneratedQuizPayload(
        suggested_title=f"Đề ôn tập ({request.difficulty}): {request.material_title[:120]}",
        questions=questions,
    )
    duration_ms = max(1, int((time.perf_counter() - started_at) * 1000))
    usage = TokenUsageMetadata(
        model_name="local-pdf-grounded",
        prompt_tokens=0,
        completion_tokens=0,
        total_tokens=0,
        duration_ms=duration_ms,
    )
    return payload, usage


def build_extractive_flashcards(
    request: GenerateFlashcardsRequest, started_at: float
) -> tuple[GeneratedFlashcardDeckPayload, TokenUsageMetadata]:
    """Trích xuất các mệnh đề/định nghĩa từ văn bản PDF để tạo bộ thẻ học 2 mặt."""
    source_text = f"{request.extracted_text}\n{request.ai_summary or ''}"
    sentences = _split_clean_sentences(source_text)
    if not sentences:
        sentences = [request.extracted_text.strip()[:220]]

    cards: list[GeneratedFlashcardItemSchema] = []
    for idx in range(request.card_count):
        sent = sentences[idx % len(sentences)]
        if ":" in sent:
            head, tail = sent.split(":", 1)
            front = f"Giải thích khái niệm / nguyên tắc: {head.strip(' *-#')}?"
            back = tail.strip() or sent
        else:
            words = sent.split()
            topic_hint = " ".join(words[: min(7, len(words))])
            front = f"Trình bày nội dung trọng tâm #{idx + 1} liên quan đến \"{topic_hint}...\"?"
            back = sent

        cards.append(
            GeneratedFlashcardItemSchema(
                front_content=front[:950],
                back_content=back[:1900],
            )
        )

    payload = GeneratedFlashcardDeckPayload(
        suggested_title=f"Thẻ ghi nhớ: {request.material_title[:130]}",
        cards=cards,
    )
    duration_ms = max(1, int((time.perf_counter() - started_at) * 1000))
    usage = TokenUsageMetadata(
        model_name="local-pdf-grounded",
        prompt_tokens=0,
        completion_tokens=0,
        total_tokens=0,
        duration_ms=duration_ms,
    )
    return payload, usage


def _clean_mermaid_label(raw: str, max_len: int = 52) -> str:
    """Làm sạch chuỗi nhãn để hiển thị an toàn 100% bên trong cú pháp id[\"...\"] của Mermaid."""
    text = re.sub(r"[\"`\r\n\t#*>|]", " ", raw or "")
    text = re.sub(r"\$+", "", text)
    text = re.sub(r"\[|\]", "(", text)
    text = re.sub(r"\s+", " ", text).strip(" -:;,.")
    if len(text) > max_len:
        text = text[: max_len - 1].rstrip() + "…"
    return text or "Khái niệm trọng tâm"


def build_extractive_mindmap(
    request: GenerateMindmapRequest, started_at: float
) -> tuple[GeneratedMindmapPayload, TokenUsageMetadata]:
    """Trích xuất cấu trúc phân cấp từ văn bản PDF để dựng sơ đồ Mermaid flowchart TD an toàn."""
    source_text = f"{request.ai_summary or ''}\n{request.extracted_text}"
    sentences = _split_clean_sentences(source_text)
    if not sentences:
        sentences = [request.material_title]

    root_label = _clean_mermaid_label(request.material_title, max_len=56)
    lines: list[str] = [
        "flowchart TD",
        f'    root["📚 {root_label}"]',
    ]

    branch_titles = [
        "1. Nền tảng & Khái niệm Cốt lõi",
        "2. Cơ chế & Giải thuật Trọng tâm",
        "3. Phân tích Hiệu năng & Thực tiễn",
        "4. Trọng tâm Ôn tập & Vận dụng",
    ]

    node_total = 1
    sent_idx = 0
    for b_idx, b_title in enumerate(branch_titles, start=1):
        b_id = f"b{b_idx}"
        lines.append(f'    {b_id}["{b_title}"]')
        lines.append(f"    root --> {b_id}")
        node_total += 1

        for c_idx in range(1, 3):
            if sent_idx >= len(sentences) and b_idx > 2:
                break
            sent = sentences[sent_idx % len(sentences)]
            sent_idx += 1
            leaf_label = _clean_mermaid_label(sent, max_len=48)
            c_id = f"c{b_idx}_{c_idx}"
            lines.append(f'    {c_id}["{leaf_label}"]')
            lines.append(f"    {b_id} --> {c_id}")
            node_total += 1

    payload = GeneratedMindmapPayload(
        title=f"Sơ đồ Tư duy: {request.material_title[:120]}",
        mermaid_code="\n".join(lines),
        node_count=node_total,
    )
    duration_ms = max(1, int((time.perf_counter() - started_at) * 1000))
    usage = TokenUsageMetadata(
        model_name="local-pdf-grounded",
        prompt_tokens=0,
        completion_tokens=0,
        total_tokens=0,
        duration_ms=duration_ms,
    )
    return payload, usage


def build_local_weakness_analysis(
    request: AnalyzeWeaknessRequest, started_at: float
) -> tuple[AnalyzeWeaknessPayload, TokenUsageMetadata]:
    """Phân tích lỗ hổng kiến thức cục bộ dựa trên dữ liệu QuizAttemptAnswers thật khi chưa cấu hình Gemini Key."""
    stats_sorted_best = sorted(request.subject_stats, key=lambda s: s.accuracy_percent, reverse=True)
    stats_sorted_weak = sorted(request.subject_stats, key=lambda s: s.accuracy_percent)

    strongest = stats_sorted_best[0].subject_category if stats_sorted_best else "Đang cập nhật"
    priority = stats_sorted_weak[0].subject_category if stats_sorted_weak else "Tổng hợp CNTT"

    # Nhóm câu sai theo môn học và tên đề thi để nhận diện chính xác 3 mảng kiến thức yếu nhất
    grouped: dict[tuple[str, str], list] = {}
    for item in request.wrong_answers:
        key = (item.subject_category, item.quiz_title)
        grouped.setdefault(key, []).append(item)

    sorted_groups = sorted(grouped.items(), key=lambda kv: len(kv[1]), reverse=True)[:3]

    gaps: list[WeaknessGapSchema] = []
    for idx, ((subj, quiz_title), items) in enumerate(sorted_groups):
        sample = items[0]
        severity = "High" if idx == 0 or len(items) >= 3 else ("Medium" if len(items) >= 2 else "Low")
        short_q = sample.question_text.strip()[:120]
        short_exp = (sample.explanation or "").strip()[:180]
        gaps.append(
            WeaknessGapSchema(
                topic_name=f"{quiz_title[:75]}",
                subject_category=subj,
                mistake_count=len(items),
                severity=severity,
                root_cause=(
                    f"Bạn đã trả lời nhầm {len(items)} câu liên quan đến mảng này "
                    f"(ví dụ: \"{short_q}\"). {short_exp or 'Cần đối chiếu lại bản chất khái niệm và điều kiện áp dụng trong giáo trình.'}"
                ),
                study_action=(
                    f"Đọc lại tóm tắt AI của học liệu môn {subj}, lật lại bộ thẻ 3D Flashcards và làm ngay đề vá lỗ hổng 5 câu."
                ),
                recommended_prompt=f"@bot Giải thích rõ bản chất và các lỗi hay nhầm trong chủ đề {quiz_title[:80]} kèm ví dụ dễ hiểu?",
            )
        )

    if not gaps and stats_sorted_weak:
        weak = stats_sorted_weak[0]
        gaps.append(
            WeaknessGapSchema(
                topic_name=f"Kiến thức nền tảng {weak.subject_category}",
                subject_category=weak.subject_category,
                mistake_count=max(1, weak.total_answered - weak.correct_count),
                severity="Medium",
                root_cause=f"Tỷ lệ chính xác hiện đạt {weak.accuracy_percent:.1f}% trên {weak.total_answered} câu hỏi đã làm.",
                study_action=f"Ôn tập lại giáo trình phòng {weak.room_name} và luyện thêm 1 đề mức Medium/Hard.",
                recommended_prompt=f"@bot Tổng hợp các kiến thức trọng tâm dễ ra thi của môn {weak.subject_category}?",
            )
        )

    total_wrong = len(request.wrong_answers)
    overall = (
        f"Dựa trên lịch sử làm bài của {request.student_name}, hệ thống ghi nhận {total_wrong} câu trả lời cần củng cố. "
        f"Bạn đang nắm vững nhất mảng {strongest}, trong khi môn {priority} là trọng tâm nên ưu tiên ôn tập ngay để nâng điểm trung bình."
        if total_wrong > 0
        else f"{request.student_name} hiện có nền tảng làm bài rất tốt ở môn {strongest}! Hãy thử sức thêm các đề mức độ Khó (Hard) để mở rộng tư duy."
    )

    roadmap = [
        f"Bước 1 (10 phút): Xem lại lời giải chi tiết của {min(total_wrong, 5)} câu làm sai gần nhất thuộc môn {priority}.",
        f"Bước 2 (10 phút): Hỏi @bot trong phòng học để làm rõ các khái niệm gây nhầm lẫn và lưu vào Sổ tay ghi chú cá nhân.",
        "Bước 3 (10 phút): Bấm 'Tạo ngay đề Quiz 5 câu vá lỗ hổng' để kiểm chứng lại mức độ nắm vững kiến thức.",
    ]

    payload = AnalyzeWeaknessPayload(
        overall_diagnosis=overall,
        strongest_subject=strongest,
        priority_subject=priority,
        weakness_gaps=gaps,
        study_roadmap=roadmap,
    )
    duration_ms = max(1, int((time.perf_counter() - started_at) * 1000))
    usage = TokenUsageMetadata(
        model_name="local-weakness-analyzer",
        prompt_tokens=0,
        completion_tokens=0,
        total_tokens=0,
        duration_ms=duration_ms,
    )
    return payload, usage
