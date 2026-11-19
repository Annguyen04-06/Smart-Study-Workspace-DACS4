/**
 * Cột 3 Tab 1 — View hiển thị danh sách Học liệu PDF, Skeleton Shimmer,
 * bản Tóm tắt Học thuật .ai-prose kèm nút "Xuất / In Phiếu Ôn Tập" và nhúng đọc PDF trực tiếp.
 */

import { resolveBackendFileUrl } from '../../../core/config/app.config.js';
import { renderEmptyState } from '../../../shared/components/emptyState.component.js';
import { renderAcademicMarkdownHtml } from '../../../shared/components/markdownViewer.component.js';
import { escapeHtml } from '../../../shared/utils/dom.utils.js';
import { formatFileSize, formatRelativeTime } from '../../../shared/utils/format.utils.js';

export function renderMaterialsSkeletons() {
    return `
        <div class="space-y-2.5">
            <div class="skeleton-box h-24 w-full rounded-xl"></div>
            <div class="skeleton-box h-24 w-full rounded-xl"></div>
        </div>
    `;
}

export function renderMaterialsList(materials, { selectedId = null, canManageAll = false, currentUserId = 0 } = {}) {
    if (!materials || materials.length === 0) {
        return renderEmptyState({
            title: 'Phòng học chưa có tài liệu PDF',
            description: 'Tải lên bài giảng hoặc giáo trình PDF để Gemini AI tự động tóm tắt và mở khóa tính năng tạo Quiz / Flashcards.'
        });
    }

    return materials
        .map((mat) => {
            const isSelected = Number(mat.id) === Number(selectedId);
            const canDelete = canManageAll || Number(mat.uploadedById) === Number(currentUserId);

            return `
                <div class="spotlight-surface rounded-xl border p-3.5 transition ${
                    isSelected
                        ? 'border-indigo-400 bg-indigo-50/40 dark:bg-indigo-500/15 shadow-xs'
                        : 'border-slate-200 dark:border-slate-800 bg-white dark:bg-slate-900 hover:border-indigo-200'
                }">
                    <div class="flex items-start justify-between gap-2">
                        <div class="min-w-0 flex-1">
                            <h4 class="text-xs font-bold text-slate-900 dark:text-white leading-snug line-clamp-2">${escapeHtml(mat.title)}</h4>
                            <p class="mt-1 text-[11px] text-slate-500 dark:text-slate-400">
                                ${mat.pageCount} trang &bull; ${escapeHtml(formatFileSize(mat.fileSizeBytes))} &bull; ${escapeHtml(mat.uploadedByName)}
                            </p>
                            <p class="text-[10px] text-slate-400">${escapeHtml(formatRelativeTime(mat.createdAt))}</p>
                        </div>
                        ${canDelete ? `
                            <button type="button" data-mat-action="delete" data-mat-id="${mat.id}"
                                class="rounded-lg p-1 text-slate-400 hover:bg-rose-50 hover:text-rose-600 transition"
                                title="Xóa tài liệu">
                                <svg class="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="1.9"><path stroke-linecap="round" stroke-linejoin="round" d="M19 7l-.867 12.142A2 2 0 0116.138 21H7.862a2 2 0 01-1.995-1.858L5 7m5 4v6m4-6v6m1-10V4a1 1 0 00-1-1h-4a1 1 0 00-1 1v3M4 7h16"/></svg>
                            </button>
                        ` : ''}
                    </div>

                    <div class="mt-3 flex flex-wrap items-center gap-1.5">
                        <button type="button" data-mat-action="summary" data-mat-id="${mat.id}"
                            class="rounded-lg bg-indigo-50 dark:bg-indigo-500/20 px-2.5 py-1 text-[11px] font-semibold text-indigo-700 dark:text-indigo-300 hover:bg-indigo-600 hover:text-white transition">
                            Tóm tắt AI
                        </button>
                        <button type="button" data-mat-action="mindmap" data-mat-id="${mat.id}"
                            class="rounded-lg border border-violet-200 dark:border-violet-500/30 bg-violet-50 dark:bg-violet-500/15 px-2.5 py-1 text-[11px] font-bold text-violet-700 dark:text-violet-300 hover:bg-violet-600 hover:text-white transition">
                            🧠 Xem Sơ đồ Tư duy AI
                        </button>
                        <button type="button" data-mat-action="preview" data-mat-id="${mat.id}"
                            class="rounded-lg bg-slate-100 dark:bg-slate-800 px-2.5 py-1 text-[11px] font-semibold text-slate-700 dark:text-slate-200 hover:bg-slate-200 transition">
                            Xem trực tiếp PDF
                        </button>
                        <button type="button" data-mat-action="print-summary" data-mat-id="${mat.id}"
                            class="rounded-lg border border-emerald-200 dark:border-emerald-500/30 bg-emerald-50 dark:bg-emerald-500/15 px-2.5 py-1 text-[11px] font-semibold text-emerald-700 dark:text-emerald-300 hover:bg-emerald-100 transition">
                            🖨️ In Phiếu Ôn
                        </button>
                        <button type="button" data-mat-action="quick-quiz" data-mat-id="${mat.id}"
                            class="rounded-lg border border-amber-200 dark:border-amber-500/30 bg-amber-50 dark:bg-amber-500/15 px-2.5 py-1 text-[11px] font-semibold text-amber-800 dark:text-amber-300 hover:bg-amber-100 transition">
                            + Quiz
                        </button>
                        <button type="button" data-mat-action="quick-flashcard" data-mat-id="${mat.id}"
                            class="rounded-lg border border-purple-200 dark:border-purple-500/30 bg-purple-50 dark:bg-purple-500/15 px-2.5 py-1 text-[11px] font-semibold text-purple-700 dark:text-purple-300 hover:bg-purple-100 transition">
                            + Thẻ 3D
                        </button>
                    </div>
                </div>
            `;
        })
        .join('');
}

export function renderMaterialInspector(material, mode = 'summary') {
    if (!material) {
        return '';
    }

    const pdfFullUrl = resolveBackendFileUrl(material.fileUrl);

    if (mode === 'preview') {
        return `
            <div class="rounded-2xl border border-slate-200 dark:border-slate-700 bg-white dark:bg-slate-900 overflow-hidden shadow-sm">
                <div class="flex items-center justify-between gap-2 bg-slate-900 px-3.5 py-2.5 text-white">
                    <span class="truncate text-xs font-semibold">${escapeHtml(material.title)}</span>
                    <div class="flex items-center gap-2 shrink-0">
                        <a href="${escapeHtml(pdfFullUrl)}" target="_blank" rel="noopener"
                            class="rounded-md bg-white/15 px-2 py-1 text-[11px] font-medium hover:bg-white/25 transition">
                            Mở tab mới
                        </a>
                        <button type="button" data-mat-action="close-inspector"
                            class="rounded-md bg-white/10 px-2 py-1 text-[11px] hover:bg-white/20">
                            Đóng
                        </button>
                    </div>
                </div>
                <iframe src="${escapeHtml(pdfFullUrl)}#toolbar=1&view=FitH"
                    title="${escapeHtml(material.title)}"
                    class="w-full h-[470px] border-0 bg-slate-100"></iframe>
            </div>
        `;
    }

    return `
        <div class="saas-card border-indigo-200 dark:border-indigo-500/30 p-4">
            <div class="flex items-center justify-between gap-2 border-b border-slate-100 dark:border-slate-800 pb-2.5 mb-3">
                <div class="min-w-0">
                    <span class="text-[10px] font-bold uppercase tracking-wider text-indigo-600 dark:text-indigo-400">Bản Tóm tắt Học thuật AI</span>
                    <h4 class="truncate text-xs font-bold text-slate-900 dark:text-white">${escapeHtml(material.title)}</h4>
                </div>
                <div class="flex items-center gap-1.5 shrink-0">
                    <button type="button" data-mat-action="mindmap" data-mat-id="${material.id}"
                        class="rounded-lg border border-violet-200 dark:border-violet-500/30 bg-violet-50 dark:bg-violet-500/15 px-2 py-1 text-[11px] font-bold text-violet-700 dark:text-violet-300 hover:bg-violet-100">
                        🧠 Sơ đồ AI
                    </button>
                    <button type="button" data-mat-action="print-summary" data-mat-id="${material.id}"
                        class="rounded-lg border border-emerald-200 dark:border-emerald-500/30 bg-emerald-50 dark:bg-emerald-500/15 px-2 py-1 text-[11px] font-semibold text-emerald-700 dark:text-emerald-300">
                        🖨️ Xuất / In
                    </button>
                    <button type="button" data-mat-action="preview" data-mat-id="${material.id}"
                        class="rounded-lg bg-slate-100 dark:bg-slate-800 px-2.5 py-1 text-[11px] font-semibold text-slate-700 dark:text-slate-200 hover:bg-slate-200">
                        PDF gốc
                    </button>
                    <button type="button" data-mat-action="close-inspector"
                        class="rounded-lg p-1 text-slate-400 hover:bg-slate-100 hover:text-slate-700">
                        <svg class="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2"><path stroke-linecap="round" stroke-linejoin="round" d="M6 18L18 6M6 6l12 12"/></svg>
                    </button>
                </div>
            </div>
            <div class="max-h-[380px] overflow-y-auto thin-scrollbar pr-1">
                ${renderAcademicMarkdownHtml(material.aiSummary || '*Tài liệu này chưa có bản tóm tắt AI.*')}
            </div>
        </div>
    `;
}
