/**
 * Cột 3 Tab 2 — View hiển thị Danh sách đề thi, Màn hình làm bài đếm ngược,
 * Màn hình chấm điểm lời giải .ai-prose và nút "Xuất / In Phiếu Ôn Tập".
 */

import { renderEmptyState } from '../../../shared/components/emptyState.component.js';
import { renderAcademicMarkdownHtml } from '../../../shared/components/markdownViewer.component.js';
import { escapeHtml } from '../../../shared/utils/dom.utils.js';
import { formatTimerMmSs } from '../../../shared/utils/format.utils.js';

export function renderQuizMaterialOptions(materials = [], preselectedId = null) {
    if (!materials.length) {
        return `<option value="">-- Tải lên tài liệu PDF trước --</option>`;
    }
    return materials
        .map(
            (m) => `<option value="${m.id}" ${Number(m.id) === Number(preselectedId) ? 'selected' : ''}>
                        ${escapeHtml(m.title)} (${m.pageCount} trang)
                    </option>`
        )
        .join('');
}

export function renderQuizzesList(quizzes = []) {
    if (!quizzes.length) {
        return renderEmptyState({
            title: 'Chưa có đề thi trắc nghiệm nào',
            description: 'Chọn một tài liệu PDF ở biểu mẫu phía trên để Gemini AI biên soạn đề thi theo mức độ khó mong muốn.'
        });
    }

    const diffColors = {
        Easy: 'bg-emerald-50 text-emerald-700 border-emerald-200 dark:bg-emerald-500/15 dark:text-emerald-300 dark:border-emerald-500/30',
        Medium: 'bg-amber-50 text-amber-700 border-amber-200 dark:bg-amber-500/15 dark:text-amber-300 dark:border-amber-500/30',
        Hard: 'bg-rose-50 text-rose-700 border-rose-200 dark:bg-rose-500/15 dark:text-rose-300 dark:border-rose-500/30'
    };

    return quizzes
        .map((quiz) => {
            const diffCls = diffColors[quiz.difficulty] || diffColors.Medium;
            const bestText = quiz.myBestScore !== null && quiz.myBestScore !== undefined
                ? `Kỷ lục của bạn: ${Number(quiz.myBestScore).toFixed(1)}/10`
                : 'Chưa thử sức';

            return `
                <div class="spotlight-surface rounded-xl border border-slate-200 dark:border-slate-800 bg-white dark:bg-slate-900 p-3.5 hover:border-indigo-300 transition">
                    <div class="flex items-start justify-between gap-2">
                        <h4 class="text-xs font-bold text-slate-900 dark:text-white leading-snug">${escapeHtml(quiz.title)}</h4>
                        <span class="shrink-0 rounded-md border px-2 py-0.5 text-[10px] font-semibold ${diffCls}">
                            ${escapeHtml(quiz.difficulty)}
                        </span>
                    </div>
                    <p class="mt-1 text-[11px] text-slate-500 dark:text-slate-400 truncate">Nguồn: ${escapeHtml(quiz.materialTitle)}</p>
                    <div class="mt-3 flex items-center justify-between gap-2 pt-2.5 border-t border-slate-100 dark:border-slate-800">
                        <div class="text-[11px] text-slate-500 dark:text-slate-400">
                            <span class="font-semibold text-slate-700 dark:text-slate-200">${quiz.questionCount} câu</span> &bull; ${quiz.durationMinutes} phút &bull; ${quiz.attemptCount} lượt thi
                            <p class="text-[10px] text-indigo-600 dark:text-indigo-400 font-semibold">${escapeHtml(bestText)}</p>
                        </div>
                        <button type="button" data-quiz-action="start" data-quiz-id="${quiz.id}"
                            class="btn-primary-glow rounded-xl px-3.5 py-1.5 text-xs font-semibold">
                            Bắt đầu Thi
                        </button>
                    </div>
                </div>
            `;
        })
        .join('');
}

export function renderActiveQuizExam(quiz, selectedAnswersMap = new Map(), remainingSeconds = 600) {
    const answeredCount = selectedAnswersMap.size;
    const total = quiz.questions.length;
    const progressPercent = total > 0 ? Math.round((answeredCount / total) * 100) : 0;
    const isUrgent = remainingSeconds <= 60;

    const questionsHtml = quiz.questions
        .map((q) => {
            const chosen = selectedAnswersMap.get(q.id) || null;
            const opts = [
                { key: 'A', label: q.optionA },
                { key: 'B', label: q.optionB },
                { key: 'C', label: q.optionC },
                { key: 'D', label: q.optionD }
            ];

            return `
                <div class="rounded-xl border border-slate-200 dark:border-slate-800 bg-white dark:bg-slate-900 p-4 space-y-2.5">
                    <p class="text-xs font-bold text-slate-900 dark:text-white leading-relaxed">
                        <span class="text-indigo-600 dark:text-indigo-400">Câu ${q.orderIndex}.</span> ${escapeHtml(q.questionText)}
                    </p>
                    <div class="grid grid-cols-1 gap-2">
                        ${opts
                            .map((opt) => {
                                const active = chosen === opt.key;
                                return `
                                    <button type="button"
                                        data-quiz-action="select-option"
                                        data-question-id="${q.id}"
                                        data-option-key="${opt.key}"
                                        class="flex items-start gap-2.5 rounded-xl border p-2.5 text-left text-xs transition ${
                                            active
                                                ? 'border-indigo-600 bg-indigo-50/80 dark:bg-indigo-500/20 text-indigo-950 dark:text-white ring-1 ring-indigo-600'
                                                : 'border-slate-200 dark:border-slate-800 bg-slate-50/50 dark:bg-slate-800/40 text-slate-700 dark:text-slate-300 hover:border-indigo-300'
                                        }">
                                        <span class="flex h-5 w-5 shrink-0 items-center justify-center rounded-md font-mono text-[11px] font-bold ${
                                            active ? 'bg-indigo-600 text-white' : 'bg-white dark:bg-slate-900 border border-slate-300 dark:border-slate-700 text-slate-600 dark:text-slate-300'
                                        }">${opt.key}</span>
                                        <span class="leading-relaxed">${escapeHtml(opt.label)}</span>
                                    </button>
                                `;
                            })
                            .join('')}
                    </div>
                </div>
            `;
        })
        .join('');

    // data-ota-guard: đang làm bài thi -> OTA Auto-Sync hoãn tải lại trang để không mất bài làm
    return `
        <div class="space-y-3.5" data-ota-guard="quiz-in-progress">
            <div class="sticky top-0 z-10 rounded-xl border border-slate-200 dark:border-slate-700 bg-white/95 dark:bg-slate-900/95 backdrop-blur p-3 shadow-sm">
                <div class="flex items-center justify-between gap-2">
                    <div class="min-w-0">
                        <h4 class="truncate text-xs font-bold text-slate-900 dark:text-white">${escapeHtml(quiz.title)}</h4>
                        <p class="text-[11px] text-slate-500">Đã chọn <strong id="quiz-answered-counter">${answeredCount}/${total}</strong> câu</p>
                    </div>
                    <div id="quiz-timer-badge" class="rounded-lg px-2.5 py-1 font-mono text-sm font-bold ${
                        isUrgent ? 'bg-rose-100 text-rose-700 animate-pulse' : 'bg-slate-900 dark:bg-indigo-600 text-white'
                    }">
                        ${escapeHtml(formatTimerMmSs(remainingSeconds))}
                    </div>
                </div>
                <div class="mt-2 h-1.5 w-full overflow-hidden rounded-full bg-slate-100 dark:bg-slate-800">
                    <div id="quiz-progress-bar" class="h-full bg-indigo-600 transition-all" style="width: ${progressPercent}%"></div>
                </div>
            </div>

            <div class="space-y-3">${questionsHtml}</div>

            <div class="flex items-center justify-between gap-2 pt-2">
                <button type="button" data-quiz-action="cancel-exam"
                    class="rounded-xl border border-slate-200 dark:border-slate-700 bg-white dark:bg-slate-900 px-3.5 py-2 text-xs font-semibold text-slate-600 dark:text-slate-300">
                    Thoát đề
                </button>
                <button type="button" data-quiz-action="submit-exam"
                    class="rounded-xl bg-emerald-600 px-4 py-2.5 text-xs font-semibold text-white shadow-sm hover:bg-emerald-700 transition">
                    Nộp bài &amp; Chấm điểm
                </button>
            </div>
        </div>
    `;
}

export function renderQuizResultReview(result) {
    const score = Number(result.score) || 0;
    const scoreColor = score >= 8 ? 'text-emerald-600' : score >= 5 ? 'text-amber-600' : 'text-rose-600';

    const reviewCards = (result.details || [])
        .map((q) => {
            return `
                <div class="rounded-xl border ${
                    q.isCorrect
                        ? 'border-emerald-200 bg-emerald-50/30 dark:border-emerald-500/30 dark:bg-emerald-500/10'
                        : 'border-rose-200 bg-rose-50/30 dark:border-rose-500/30 dark:bg-rose-500/10'
                } p-3.5 space-y-2">
                    <div class="flex items-start justify-between gap-2">
                        <p class="text-xs font-bold text-slate-900 dark:text-white">Câu ${q.orderIndex}. ${escapeHtml(q.questionText)}</p>
                        <span class="shrink-0 rounded-md px-2 py-0.5 text-[10px] font-bold ${q.isCorrect ? 'bg-emerald-100 text-emerald-700' : 'bg-rose-100 text-rose-700'}">
                            ${q.isCorrect ? 'ĐÚNG' : 'SAI'}
                        </span>
                    </div>
                    <p class="text-xs text-slate-700 dark:text-slate-300">
                        Bạn chọn: <strong class="font-mono">${escapeHtml(q.selectedOption || 'Chưa chọn')}</strong> &bull;
                        Đáp án đúng: <strong class="font-mono text-emerald-600 dark:text-emerald-400">${escapeHtml(q.correctOption)}</strong>
                    </p>
                    <div class="rounded-lg bg-white/95 dark:bg-slate-900/90 border border-slate-200/80 dark:border-slate-800 p-3">
                        <span class="block text-[10px] font-bold uppercase tracking-wider text-indigo-600 dark:text-indigo-400 mb-1">Giải thích Học thuật AI</span>
                        ${renderAcademicMarkdownHtml(q.explanation)}
                    </div>
                </div>
            `;
        })
        .join('');

    return `
        <div class="space-y-4">
            <div class="saas-card border-indigo-200 dark:border-indigo-500/30 p-4 text-center">
                <p class="text-xs font-semibold uppercase tracking-wider text-slate-500">Kết quả Chấm điểm Thang 10</p>
                <p class="mt-1 font-mono text-3xl font-extrabold ${scoreColor}">${score.toFixed(2)} <span class="text-base text-slate-400">/ 10</span></p>
                <p class="mt-1 text-xs text-slate-600 dark:text-slate-300">
                    Trả lời đúng <strong>${result.correctAnswersCount}/${result.totalQuestions}</strong> câu &bull; Thời gian: ${escapeHtml(formatTimerMmSs(result.timeSpentSeconds))}
                </p>
                <div class="mt-3 flex flex-wrap items-center justify-center gap-2">
                    <button type="button" data-quiz-action="print-sheet"
                        class="rounded-xl border border-emerald-200 bg-emerald-50 dark:bg-emerald-500/15 px-3.5 py-2 text-xs font-semibold text-emerald-700 dark:text-emerald-300 hover:bg-emerald-100 transition">
                        🖨️ Xuất / In Phiếu Ôn Tập
                    </button>
                    <button type="button" data-quiz-action="back-to-list"
                        class="rounded-xl bg-slate-900 dark:bg-indigo-600 px-4 py-2 text-xs font-semibold text-white hover:opacity-90 transition">
                        Quay lại Danh sách Đề
                    </button>
                </div>
            </div>

            <div class="space-y-2.5">${reviewCards}</div>
        </div>
    `;
}
