/**
 * Cột 3 — View Sổ tay Ghi chú Học tập Thông minh (Personal Markdown Scratchpad):
 * Hỗ trợ chế độ Chia đôi Soạn thảo + Xem trước Markdown/KaTeX trực tiếp,
 * chèn nhanh công thức/code, lưu tự động vào IndexedDB và xuất file .md hoặc In PDF 1 click.
 */

import { renderAcademicMarkdownHtml } from '../../../shared/components/markdownViewer.component.js';
import { escapeHtml } from '../../../shared/utils/dom.utils.js';

export function renderScratchpadShell({ roomName = '', content = '', mode = 'split', saveStatus = 'Đã lưu vào IndexedDB' } = {}) {
    const wordCount = String(content || '').trim()
        ? String(content || '').trim().split(/\s+/).length
        : 0;

    const tabBtnCls = (active) =>
        active
            ? 'rounded-lg bg-indigo-600 px-2.5 py-1 text-[11px] font-bold text-white shadow-xs transition'
            : 'rounded-lg px-2.5 py-1 text-[11px] font-semibold text-slate-600 dark:text-slate-300 hover:bg-slate-200/70 dark:hover:bg-slate-800 transition';

    return `
        <div class="flex h-full flex-col gap-3">
            <!-- Header & Thanh công cụ -->
            <div class="saas-card p-3.5 space-y-2.5">
                <div class="flex flex-wrap items-center justify-between gap-2">
                    <div class="min-w-0">
                        <h3 class="text-xs font-extrabold text-slate-900 dark:text-white flex items-center gap-1.5">
                            <span>📝 Sổ tay Ôn thi Markdown</span>
                            <span class="rounded-md bg-emerald-50 dark:bg-emerald-500/15 border border-emerald-200 dark:border-emerald-500/30 px-1.5 py-0.5 text-[10px] font-bold text-emerald-700 dark:text-emerald-300">Offline Ready</span>
                        </h3>
                        <p class="truncate text-[11px] text-slate-500 dark:text-slate-400">
                            Phòng: ${escapeHtml(roomName)} &bull; <span id="scratchpad-save-status">${escapeHtml(saveStatus)}</span>
                        </p>
                    </div>
                    <div class="flex items-center gap-1 rounded-xl bg-slate-100 dark:bg-slate-950 p-0.5">
                        <button type="button" data-sp-mode="edit" class="${tabBtnCls(mode === 'edit')}">Soạn</button>
                        <button type="button" data-sp-mode="split" class="${tabBtnCls(mode === 'split')}">Chia đôi</button>
                        <button type="button" data-sp-mode="preview" class="${tabBtnCls(mode === 'preview')}">Xem trước</button>
                    </div>
                </div>

                <!-- Thanh chèn nhanh Markdown + Xuất file -->
                <div class="flex flex-wrap items-center justify-between gap-1.5 pt-2 border-t border-slate-100 dark:border-slate-800">
                    <div class="flex flex-wrap items-center gap-1">
                        <button type="button" data-sp-insert="heading" class="rounded-lg border border-slate-200 dark:border-slate-700 bg-slate-50 dark:bg-slate-800 px-2 py-1 font-mono text-[10px] font-bold text-slate-700 dark:text-slate-200 hover:border-indigo-400" title="Thêm tiêu đề">H2</button>
                        <button type="button" data-sp-insert="bold" class="rounded-lg border border-slate-200 dark:border-slate-700 bg-slate-50 dark:bg-slate-800 px-2 py-1 text-[10px] font-extrabold text-slate-700 dark:text-slate-200 hover:border-indigo-400" title="In đậm">B</button>
                        <button type="button" data-sp-insert="math" class="rounded-lg border border-slate-200 dark:border-slate-700 bg-slate-50 dark:bg-slate-800 px-2 py-1 font-mono text-[10px] font-bold text-indigo-700 dark:text-indigo-300 hover:border-indigo-400" title="Chèn công thức KaTeX">$\sum$</button>
                        <button type="button" data-sp-insert="code" class="rounded-lg border border-slate-200 dark:border-slate-700 bg-slate-50 dark:bg-slate-800 px-2 py-1 font-mono text-[10px] font-bold text-slate-700 dark:text-slate-200 hover:border-indigo-400" title="Chèn khối mã nguồn">&lt;/&gt;</button>
                        <button type="button" data-sp-insert="checklist" class="rounded-lg border border-slate-200 dark:border-slate-700 bg-slate-50 dark:bg-slate-800 px-2 py-1 text-[10px] font-bold text-slate-700 dark:text-slate-200 hover:border-indigo-400" title="Danh sách ôn tập">☑</button>
                    </div>
                    <div class="flex items-center gap-1.5">
                        <button type="button" data-sp-action="export-md"
                            class="inline-flex items-center gap-1 rounded-lg border border-indigo-200 dark:border-indigo-500/35 bg-indigo-50 dark:bg-indigo-500/15 px-2.5 py-1 text-[11px] font-bold text-indigo-700 dark:text-indigo-300 hover:bg-indigo-100 transition"
                            title="Tải xuống toàn bộ sổ tay dạng tệp .md">
                            ⬇ .MD
                        </button>
                        <button type="button" data-sp-action="export-pdf"
                            class="inline-flex items-center gap-1 rounded-lg border border-emerald-200 dark:border-emerald-500/35 bg-emerald-50 dark:bg-emerald-500/15 px-2.5 py-1 text-[11px] font-bold text-emerald-700 dark:text-emerald-300 hover:bg-emerald-100 transition"
                            title="In hoặc Lưu thành PDF học thuật sạch đẹp">
                            🖨️ PDF
                        </button>
                    </div>
                </div>
            </div>

            <!-- Vùng Soạn thảo / Xem trước chia đôi -->
            <div class="flex-1 min-h-[340px] grid ${mode === 'split' ? 'grid-cols-1 xl:grid-cols-2' : 'grid-cols-1'} gap-3">
                <div class="${mode === 'preview' ? 'hidden' : 'flex'} flex-col rounded-2xl border border-slate-200 dark:border-slate-800 bg-white dark:bg-slate-900 overflow-hidden shadow-xs">
                    <div class="flex items-center justify-between px-3 py-1.5 bg-slate-50 dark:bg-slate-950/70 border-b border-slate-100 dark:border-slate-800 text-[10px] font-bold uppercase tracking-wider text-slate-400">
                        <span>Trình soạn Markdown</span>
                        <span id="scratchpad-word-count" class="font-mono">${wordCount} từ</span>
                    </div>
                    <textarea id="scratchpad-textarea"
                        placeholder="Ghi chú bài giảng, công thức $O(V \log V)$ hoặc nhấn '📝 Lưu vào Sổ tay' trên bất kỳ câu trả lời nào của @bot..."
                        class="flex-1 w-full min-h-[260px] resize-none border-0 bg-transparent p-3.5 font-mono text-xs leading-relaxed text-slate-800 dark:text-slate-100 placeholder-slate-400 focus:outline-none thin-scrollbar">${escapeHtml(content)}</textarea>
                </div>

                <div class="${mode === 'edit' ? 'hidden' : 'flex'} flex-col rounded-2xl border border-slate-200 dark:border-slate-800 bg-white dark:bg-slate-900 overflow-hidden shadow-xs">
                    <div class="flex items-center justify-between px-3 py-1.5 bg-slate-50 dark:bg-slate-950/70 border-b border-slate-100 dark:border-slate-800 text-[10px] font-bold uppercase tracking-wider text-indigo-500 dark:text-indigo-300">
                        <span>Xem trước Học thuật (KaTeX &amp; Code)</span>
                        <button type="button" data-sp-action="clear" class="text-rose-500 hover:underline normal-case font-semibold">Xóa nháp</button>
                    </div>
                    <div id="scratchpad-preview-pane" class="flex-1 overflow-y-auto thin-scrollbar p-3.5 text-sm">
                        ${renderScratchpadPreviewContent(content)}
                    </div>
                </div>
            </div>
        </div>
    `;
}

export function renderScratchpadPreviewContent(content = '') {
    if (!String(content || '').trim()) {
        return `
            <div class="py-10 text-center text-xs text-slate-400 space-y-1.5">
                <p class="font-semibold text-slate-500 dark:text-slate-300">Sổ tay ôn thi phòng này đang trống</p>
                <p>Hãy gõ ghi chú bên khung soạn thảo hoặc bấm <strong>"📝 Lưu vào Sổ tay"</strong> trên câu trả lời của <code>@bot</code>!</p>
            </div>
        `;
    }
    return renderAcademicMarkdownHtml(content);
}
