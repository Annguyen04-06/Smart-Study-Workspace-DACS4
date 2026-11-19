/**
 * Tầng View hiển thị bảng Lịch sử làm bài kiểm tra của Sinh viên và Modal đối chiếu lời giải AI.
 */

import { buildWorkspaceUrl } from '../../core/config/app.config.js';
import { renderEmptyState } from '../../shared/components/emptyState.component.js';
import { renderAcademicMarkdownHtml } from '../../shared/components/markdownViewer.component.js';
import { escapeHtml } from '../../shared/utils/dom.utils.js';
import { formatDateTime, formatTimerMmSs } from '../../shared/utils/format.utils.js';

export function renderAttemptHistoryTable(attempts) {
    if (!attempts || attempts.length === 0) {
        return renderEmptyState({
            title: 'Bạn chưa thực hiện bài kiểm tra trắc nghiệm nào',
            description: 'Hãy tham gia một phòng học, chọn Tab Thi Trắc Nghiệm AI để luyện đề và theo dõi tiến độ tại đây.'
        });
    }

    const rowsHtml = attempts
        .map((item) => {
            const scoreNum = Number(item.score) || 0;
            const scoreBadge =
                scoreNum >= 8
                    ? 'bg-emerald-50 text-emerald-700 border-emerald-200'
                    : scoreNum >= 5
                    ? 'bg-amber-50 text-amber-700 border-amber-200'
                    : 'bg-rose-50 text-rose-700 border-rose-200';

            return `
                <tr class="hover:bg-slate-50/80 transition">
                    <td class="px-4 py-3.5">
                        <p class="text-sm font-semibold text-slate-900">${escapeHtml(item.quizTitle)}</p>
                        <a href="${buildWorkspaceUrl(item.roomId)}" class="text-xs text-indigo-600 hover:underline">
                            ${escapeHtml(item.roomName)}
                        </a>
                    </td>
                    <td class="px-4 py-3.5 text-xs text-slate-600">${escapeHtml(item.difficulty)}</td>
                    <td class="px-4 py-3.5">
                        <span class="inline-flex items-center rounded-lg border px-2.5 py-1 font-mono text-xs font-bold ${scoreBadge}">
                            ${scoreNum.toFixed(2)} / 10
                        </span>
                    </td>
                    <td class="px-4 py-3.5 text-xs text-slate-600">
                        ${item.correctAnswersCount}/${item.totalQuestions} câu (${escapeHtml(formatTimerMmSs(item.timeSpentSeconds))})
                    </td>
                    <td class="px-4 py-3.5 text-xs text-slate-500">${escapeHtml(formatDateTime(item.completedAt))}</td>
                    <td class="px-4 py-3.5 text-right">
                        <button type="button" data-action="view-attempt-detail" data-attempt-id="${item.attemptId}"
                            class="rounded-lg border border-indigo-200 bg-indigo-50 px-3 py-1.5 text-xs font-semibold text-indigo-700 hover:bg-indigo-600 hover:text-white transition">
                            Xem lời giải AI
                        </button>
                    </td>
                </tr>
            `;
        })
        .join('');

    return `
        <div class="overflow-x-auto rounded-xl border border-slate-200">
            <table class="min-w-full divide-y divide-slate-200 text-left">
                <thead class="bg-slate-50 text-[11px] font-semibold uppercase tracking-wider text-slate-500">
                    <tr>
                        <th class="px-4 py-3">Đề kiểm tra &amp; Phòng học</th>
                        <th class="px-4 py-3">Độ khó</th>
                        <th class="px-4 py-3">Điểm số</th>
                        <th class="px-4 py-3">Kết quả</th>
                        <th class="px-4 py-3">Hoàn thành</th>
                        <th class="px-4 py-3 text-right">Chi tiết</th>
                    </tr>
                </thead>
                <tbody class="divide-y divide-slate-100 bg-white">
                    ${rowsHtml}
                </tbody>
            </table>
        </div>
    `;
}

export function renderAttemptReviewModalContent(result) {
    const questionsHtml = (result.details || [])
        .map((q) => {
            const options = [
                { key: 'A', text: q.optionA },
                { key: 'B', text: q.optionB },
                { key: 'C', text: q.optionC },
                { key: 'D', text: q.optionD }
            ];

            const optionRows = options
                .map((opt) => {
                    const isCorrectOpt = opt.key === q.correctOption;
                    const isSelectedOpt = opt.key === q.selectedOption;
                    let cls = 'border-slate-200 bg-white text-slate-700';
                    if (isCorrectOpt) {
                        cls = 'border-emerald-300 bg-emerald-50 text-emerald-900 font-medium';
                    } else if (isSelectedOpt && !q.isCorrect) {
                        cls = 'border-rose-300 bg-rose-50 text-rose-900';
                    }
                    return `
                        <div class="flex items-start gap-2.5 rounded-xl border px-3 py-2 text-xs ${cls}">
                            <span class="font-mono font-bold">${opt.key}.</span>
                            <span class="flex-1">${escapeHtml(opt.text)}</span>
                            ${isCorrectOpt ? '<span class="text-[11px] font-semibold text-emerald-700">Đáp án đúng</span>' : ''}
                            ${isSelectedOpt && !isCorrectOpt ? '<span class="text-[11px] font-semibold text-rose-700">Bạn chọn</span>' : ''}
                        </div>
                    `;
                })
                .join('');

            return `
                <div class="rounded-xl border border-slate-200 bg-slate-50/50 p-4 space-y-2.5">
                    <div class="flex items-start justify-between gap-2">
                        <p class="text-sm font-semibold text-slate-900">Câu ${q.orderIndex}: ${escapeHtml(q.questionText)}</p>
                        <span class="shrink-0 rounded-full px-2.5 py-0.5 text-[11px] font-semibold ${q.isCorrect ? 'bg-emerald-100 text-emerald-700' : 'bg-rose-100 text-rose-700'}">
                            ${q.isCorrect ? 'Chính xác' : q.selectedOption ? 'Chưa đúng' : 'Bỏ trống'}
                        </span>
                    </div>
                    <div class="grid grid-cols-1 gap-1.5">${optionRows}</div>
                    <div class="rounded-lg bg-indigo-50/80 dark:bg-slate-900 border border-indigo-100 dark:border-slate-800 p-3 text-xs">
                        <strong class="block text-[10px] uppercase tracking-wider text-indigo-700 dark:text-indigo-400 mb-1">Giải thích Học thuật AI</strong>
                        ${renderAcademicMarkdownHtml(q.explanation)}
                    </div>
                </div>
            `;
        })
        .join('');

    return `
        <div class="flex flex-wrap items-center justify-between gap-3 rounded-xl bg-slate-100 p-3.5 mb-4">
            <div>
                <p class="text-sm font-bold text-slate-900">${escapeHtml(result.quizTitle)}</p>
                <p class="text-xs text-slate-500">Phòng: ${escapeHtml(result.roomName)} &bull; Thời gian làm: ${escapeHtml(formatTimerMmSs(result.timeSpentSeconds))}</p>
            </div>
            <div class="text-right">
                <span class="font-mono text-xl font-extrabold text-indigo-600">${Number(result.score).toFixed(2)}/10</span>
                <p class="text-xs text-slate-500">Đúng ${result.correctAnswersCount}/${result.totalQuestions} câu</p>
            </div>
        </div>
        <div class="space-y-3 max-h-[62vh] overflow-y-auto thin-scrollbar pr-1">
            ${questionsHtml}
        </div>
    `;
}
