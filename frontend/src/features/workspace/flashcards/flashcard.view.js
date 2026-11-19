/**
 * Cột 3 Tab 3 — View Bộ thẻ Ghi nhớ 3D hỗ trợ phím tắt đầy đủ:
 * Space (lật thẻ), Mũi tên Trái/Phải (chuyển thẻ) và phím 1/2/3 (đánh giá Spaced Repetition).
 * Trên điện thoại hiển thị gợi ý cảm ứng: Chạm để lật, Vuốt trái/phải để chuyển thẻ.
 */

import { renderEmptyState } from '../../../shared/components/emptyState.component.js';
import { escapeHtml } from '../../../shared/utils/dom.utils.js';

export function renderFlashcardDecksList(decks = []) {
    if (!decks.length) {
        return renderEmptyState({
            title: 'Chưa có bộ thẻ ghi nhớ 3D nào',
            description: 'Chọn một tài liệu PDF ở trên để Gemini AI trích xuất khái niệm và công thức trọng tâm thành bộ thẻ học nhanh.'
        });
    }

    return decks
        .map((deck) => {
            const total = Math.max(1, deck.cardCount);
            const masteredPct = Math.round(((deck.masteredCount || 0) / total) * 100);

            return `
                <div class="spotlight-surface rounded-xl border border-slate-200 dark:border-slate-800 bg-white dark:bg-slate-900 p-3.5 hover:border-purple-300 transition">
                    <div class="flex items-start justify-between gap-2">
                        <h4 class="text-xs font-bold text-slate-900 dark:text-white leading-snug">${escapeHtml(deck.title)}</h4>
                        <span class="shrink-0 rounded-md bg-purple-50 dark:bg-purple-500/15 border border-purple-200 dark:border-purple-500/30 px-2 py-0.5 text-[10px] font-semibold text-purple-700 dark:text-purple-300">
                            ${deck.cardCount} thẻ
                        </span>
                    </div>
                    <p class="mt-1 text-[11px] text-slate-500 dark:text-slate-400 truncate">Nguồn: ${escapeHtml(deck.materialTitle)}</p>

                    <div class="mt-2.5">
                        <div class="flex justify-between text-[10px] text-slate-500 mb-1">
                            <span>Đã thuộc ${deck.masteredCount}/${deck.cardCount} thẻ</span>
                            <span class="font-semibold text-emerald-600">${masteredPct}%</span>
                        </div>
                        <div class="h-1.5 w-full overflow-hidden rounded-full bg-slate-100 dark:bg-slate-800">
                            <div class="h-full bg-emerald-500 transition-all" style="width: ${masteredPct}%"></div>
                        </div>
                    </div>

                    <div class="mt-3 flex justify-end">
                        <button type="button" data-fc-action="open-deck" data-deck-id="${deck.id}"
                            class="btn-primary-glow rounded-xl px-3.5 py-1.5 text-xs font-semibold">
                            Luyện Thẻ 3D
                        </button>
                    </div>
                </div>
            `;
        })
        .join('');
}

export function renderActiveFlashcardPlayer(deck, cards, currentIndex, isFlipped) {
    if (!cards.length) {
        return `<p class="text-xs text-slate-500">Bộ thẻ hiện đang trống.</p>`;
    }

    const card = cards[currentIndex];
    const total = cards.length;
    const masteredCount = cards.filter((c) => c.masteryLevel === 2).length;
    const learningCount = cards.filter((c) => c.masteryLevel === 1).length;
    const unlearnedCount = cards.filter((c) => c.masteryLevel === 0).length;
    const masteredPct = Math.round((masteredCount / total) * 100);

    const masteryLabelMap = {
        0: { text: 'Chưa thuộc', cls: 'bg-rose-100 text-rose-700' },
        1: { text: 'Tạm nhớ', cls: 'bg-amber-100 text-amber-800' },
        2: { text: 'Đã thuộc', cls: 'bg-emerald-100 text-emerald-700' }
    };
    const badge = masteryLabelMap[card.masteryLevel] || masteryLabelMap[0];

    return `
        <div class="space-y-3.5">
            <div class="flex items-center justify-between gap-2">
                <button type="button" data-fc-action="close-deck"
                    class="inline-flex items-center gap-1 rounded-lg border border-slate-200 dark:border-slate-700 bg-white dark:bg-slate-900 px-2.5 py-1 text-xs font-semibold text-slate-600 dark:text-slate-300 hover:bg-slate-50">
                    &larr; Danh sách bộ thẻ
                </button>
                <button type="button" data-fc-action="shuffle-cards"
                    class="inline-flex items-center gap-1 rounded-lg bg-purple-50 dark:bg-purple-500/15 border border-purple-200 dark:border-purple-500/30 px-2.5 py-1 text-xs font-semibold text-purple-700 dark:text-purple-300 hover:bg-purple-100 transition">
                    🔀 Trộn thẻ
                </button>
            </div>

            <!-- Thanh tiến độ Spaced Repetition -->
            <div class="rounded-xl border border-slate-200 dark:border-slate-800 bg-white dark:bg-slate-900 p-3">
                <div class="flex items-center justify-between text-[11px] mb-1.5">
                    <span class="font-bold text-slate-800 dark:text-white truncate">${escapeHtml(deck.title)}</span>
                    <span class="font-mono font-semibold text-indigo-600 dark:text-indigo-400">Thẻ ${currentIndex + 1}/${total}</span>
                </div>
                <div class="h-2 w-full overflow-hidden rounded-full bg-slate-100 dark:bg-slate-800">
                    <div class="h-full bg-emerald-500 transition-all" style="width: ${masteredPct}%"></div>
                </div>
                <div class="mt-2 flex items-center justify-between text-[10px] text-slate-500 dark:text-slate-400">
                    <span>Chưa thuộc: <strong class="text-rose-600">${unlearnedCount}</strong></span>
                    <span>Tạm nhớ: <strong class="text-amber-600">${learningCount}</strong></span>
                    <span>Đã thuộc: <strong class="text-emerald-600">${masteredCount}</strong></span>
                </div>
            </div>

            <!-- Khung Thẻ 3D 2 Mặt kết hợp Độ nghiêng 3D Tilt mượt theo chuột -->
            <div class="flashcard-scene">
                <div class="tilt-3d-card" data-tilt-card>
                    <div id="flashcard-3d-box" data-fc-action="flip-card"
                         class="flashcard-3d-inner ${isFlipped ? 'is-flipped' : ''}">
                        <div class="flashcard-face border border-indigo-200 dark:border-indigo-500/30 bg-gradient-to-br from-white via-white to-indigo-50/70 dark:from-slate-900 dark:via-slate-900 dark:to-indigo-950/60">
                            <div class="flex items-center justify-between">
                                <span class="text-[10px] font-bold uppercase tracking-wider text-indigo-600 dark:text-indigo-400">Mặt trước &bull; Câu hỏi</span>
                                <span class="rounded-full px-2 py-0.5 text-[10px] font-semibold ${badge.cls}">${badge.text}</span>
                            </div>
                            <div class="my-auto py-4 text-center">
                                <p class="text-sm sm:text-base font-bold text-slate-900 dark:text-white leading-relaxed">${escapeHtml(card.frontContent)}</p>
                            </div>
                            <p class="hidden md:block text-center text-[10px] text-slate-400">
                                Click hoặc nhấn <kbd class="rounded border border-slate-300 dark:border-slate-700 px-1.5 py-0.2 font-mono">Space</kbd> để lật &bull; <kbd class="rounded border border-slate-300 dark:border-slate-700 px-1 py-0.2 font-mono">← →</kbd> chuyển thẻ
                            </p>
                            <p class="md:hidden text-center text-[11px] font-medium text-slate-400">
                                👆 Chạm để lật thẻ &bull; 👈 Vuốt ngang để chuyển thẻ 👉
                            </p>
                        </div>

                        <div class="flashcard-face flashcard-face-back border border-emerald-400/40 bg-gradient-to-br from-slate-900 via-indigo-950 to-slate-950 text-white">
                            <div class="flex items-center justify-between">
                                <span class="text-[10px] font-bold uppercase tracking-wider text-emerald-300">Mặt sau &bull; Đáp án chuẩn</span>
                                <span class="text-[10px] text-indigo-200">Thẻ #${currentIndex + 1}</span>
                            </div>
                            <div class="my-auto py-4 text-center overflow-y-auto thin-scrollbar max-h-40">
                                <p class="text-sm leading-relaxed text-slate-100">${escapeHtml(card.backContent)}</p>
                            </div>
                            <p class="hidden md:block text-center text-[10px] text-indigo-300">Nhấn phím <strong>1 / 2 / 3</strong> để đánh giá mức độ nhớ nhanh</p>
                            <p class="md:hidden text-center text-[11px] text-indigo-300">Chọn mức độ nhớ ở các nút bên dưới thẻ</p>
                        </div>
                    </div>
                </div>
            </div>

            <!-- Điều hướng trước / sau -->
            <div class="flex items-center justify-between gap-2">
                <button type="button" data-fc-action="prev-card" ${currentIndex === 0 ? 'disabled' : ''}
                    class="rounded-xl border border-slate-200 dark:border-slate-700 bg-white dark:bg-slate-900 px-3.5 py-2 text-xs font-semibold text-slate-700 dark:text-slate-200 disabled:opacity-40">
                    &larr; Trước
                </button>
                <button type="button" data-fc-action="flip-card"
                    class="rounded-xl border border-indigo-200 dark:border-indigo-500/30 bg-indigo-50 dark:bg-indigo-500/15 px-4 py-2 text-xs font-semibold text-indigo-700 dark:text-indigo-300">
                    Lật thẻ<span class="hidden md:inline"> (Space)</span>
                </button>
                <button type="button" data-fc-action="next-card" ${currentIndex >= total - 1 ? 'disabled' : ''}
                    class="rounded-xl border border-slate-200 dark:border-slate-700 bg-white dark:bg-slate-900 px-3.5 py-2 text-xs font-semibold text-slate-700 dark:text-slate-200 disabled:opacity-40">
                    Tiếp &rarr;
                </button>
            </div>

            <!-- 3 nút Đánh giá Spaced Repetition kèm phím tắt 1 / 2 / 3 -->
            <div class="grid grid-cols-3 gap-2 pt-1">
                <button type="button" data-fc-action="rate-mastery" data-mastery="0"
                    class="rounded-xl border border-rose-200 bg-rose-50 dark:bg-rose-500/15 py-2 text-xs font-semibold text-rose-700 dark:text-rose-300 hover:bg-rose-600 hover:text-white transition">
                    [1] Chưa thuộc
                </button>
                <button type="button" data-fc-action="rate-mastery" data-mastery="1"
                    class="rounded-xl border border-amber-200 bg-amber-50 dark:bg-amber-500/15 py-2 text-xs font-semibold text-amber-800 dark:text-amber-300 hover:bg-amber-500 hover:text-white transition">
                    [2] Tạm nhớ
                </button>
                <button type="button" data-fc-action="rate-mastery" data-mastery="2"
                    class="rounded-xl border border-emerald-200 bg-emerald-50 dark:bg-emerald-500/15 py-2 text-xs font-semibold text-emerald-700 dark:text-emerald-300 hover:bg-emerald-600 hover:text-white transition">
                    [3] Đã thuộc
                </button>
            </div>
        </div>
    `;
}
