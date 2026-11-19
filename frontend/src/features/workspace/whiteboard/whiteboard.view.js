/**
 * Cột 3 Tab 5 — Giao diện Bảng Trắng Học Nhóm Trực Tiếp (Collaborative Group Whiteboard):
 * Thanh công cụ chọn Bút vẽ đa màu, Tẩy, Hình khối cơ bản (Đường thẳng, Chữ nhật, Hình tròn, Mũi tên),
 * Xóa bảng đồng bộ, Tải ảnh .png và lớp phủ Con trỏ chuột Figma thời gian thực.
 */

const PALETTE_COLORS = [
    { hex: '#4f46e5', label: 'Tím Indigo' },
    { hex: '#ef4444', label: 'Đỏ nổi bật' },
    { hex: '#10b981', label: 'Xanh lục' },
    { hex: '#f59e0b', label: 'Vàng cam' },
    { hex: '#ec4899', label: 'Hồng phấn' },
    { hex: '#0ea5e9', label: 'Xanh dương' },
    { hex: '#1e293b', label: 'Mực đậm' }
];

export function renderWhiteboardStudioShell() {
    const swatchesHtml = PALETTE_COLORS.map(
        (c, idx) => `
            <button type="button"
                data-wb-color="${c.hex}"
                title="${c.label}"
                class="h-6 w-6 rounded-full border-2 transition transform hover:scale-110 ${
                    idx === 0 ? 'ring-2 ring-offset-1 ring-indigo-500 border-white' : 'border-white/80 dark:border-slate-800'
                }"
                style="background-color: ${c.hex}">
            </button>
        `
    ).join('');

    return `
        <div class="flex flex-col h-full min-h-[510px] space-y-2.5">
            <!-- Thanh công cụ Vẽ & Hình khối -->
            <div class="saas-card spotlight-surface p-2.5 space-y-2">
                <div class="flex flex-wrap items-center justify-between gap-2">
                    <div class="inline-flex flex-wrap items-center gap-1 rounded-xl bg-slate-100 dark:bg-slate-800/90 p-1">
                        <button type="button" data-wb-tool="pen"
                            class="rounded-lg bg-indigo-600 px-2.5 py-1 text-[11px] font-bold text-white shadow-xs transition"
                            title="Bút vẽ tự do">
                            ✏️ Bút
                        </button>
                        <button type="button" data-wb-tool="line"
                            class="rounded-lg px-2 py-1 text-[11px] font-semibold text-slate-600 dark:text-slate-300 hover:bg-white dark:hover:bg-slate-700 transition"
                            title="Vẽ đoạn thẳng">
                            ╱ Đoạn
                        </button>
                        <button type="button" data-wb-tool="arrow"
                            class="rounded-lg px-2 py-1 text-[11px] font-semibold text-slate-600 dark:text-slate-300 hover:bg-white dark:hover:bg-slate-700 transition"
                            title="Vẽ mũi tên sơ đồ">
                            ➜ Mũi tên
                        </button>
                        <button type="button" data-wb-tool="rect"
                            class="rounded-lg px-2 py-1 text-[11px] font-semibold text-slate-600 dark:text-slate-300 hover:bg-white dark:hover:bg-slate-700 transition"
                            title="Vẽ khung chữ nhật">
                            ▭ Khung
                        </button>
                        <button type="button" data-wb-tool="circle"
                            class="rounded-lg px-2 py-1 text-[11px] font-semibold text-slate-600 dark:text-slate-300 hover:bg-white dark:hover:bg-slate-700 transition"
                            title="Vẽ nút tròn / elip">
                            ◯ Tròn
                        </button>
                        <button type="button" data-wb-tool="eraser"
                            class="rounded-lg px-2 py-1 text-[11px] font-semibold text-slate-600 dark:text-slate-300 hover:bg-white dark:hover:bg-slate-700 transition"
                            title="Tẩy nét vẽ">
                            🧽 Tẩy
                        </button>
                    </div>

                    <div class="flex items-center gap-1.5">
                        <button type="button" data-wb-action="download"
                            class="rounded-lg border border-emerald-200 dark:border-emerald-500/30 bg-emerald-50 dark:bg-emerald-500/15 px-2.5 py-1 text-[11px] font-bold text-emerald-700 dark:text-emerald-300 hover:bg-emerald-100 transition"
                            title="Tải ảnh chụp bảng trắng (.png)">
                            ⬇️ Lưu .PNG
                        </button>
                        <button type="button" data-wb-action="clear"
                            class="rounded-lg border border-rose-200 dark:border-rose-500/30 bg-rose-50 dark:bg-rose-500/15 px-2.5 py-1 text-[11px] font-bold text-rose-700 dark:text-rose-300 hover:bg-rose-600 hover:text-white transition"
                            title="Xóa toàn bộ bảng trắng của nhóm">
                            🗑️ Xóa bảng
                        </button>
                    </div>
                </div>

                <div class="flex flex-wrap items-center justify-between gap-2 pt-1 border-t border-slate-100 dark:border-slate-800">
                    <div class="flex items-center gap-1.5">
                        <span class="text-[10px] font-bold uppercase tracking-wider text-slate-400 mr-1">Màu:</span>
                        ${swatchesHtml}
                    </div>

                    <div class="flex items-center gap-1.5">
                        <span class="text-[10px] font-bold uppercase tracking-wider text-slate-400">Nét:</span>
                        <button type="button" data-wb-width="2.5" class="rounded-md bg-indigo-50 dark:bg-indigo-500/20 px-2 py-0.5 text-[10px] font-bold text-indigo-700 dark:text-indigo-300 ring-1 ring-indigo-400">Mảnh</button>
                        <button type="button" data-wb-width="4.5" class="rounded-md px-2 py-0.5 text-[10px] font-semibold text-slate-500 hover:bg-slate-100 dark:hover:bg-slate-800">Vừa</button>
                        <button type="button" data-wb-width="8" class="rounded-md px-2 py-0.5 text-[10px] font-semibold text-slate-500 hover:bg-slate-100 dark:hover:bg-slate-800">Đậm</button>
                    </div>
                </div>
            </div>

            <!-- Khung Canvas Vẽ Nhóm + Lớp Con trỏ Figma Thời gian thực -->
            <div id="wb-canvas-stage"
                class="relative flex-1 min-h-[420px] rounded-2xl border border-slate-200 dark:border-slate-800 bg-white dark:bg-slate-900 overflow-hidden shadow-inner select-none touch-none"
                style="background-image: radial-gradient(rgba(148, 163, 184, 0.35) 1px, transparent 1px); background-size: 20px 20px;">
                <canvas id="wb-main-canvas" class="block w-full h-full cursor-crosshair"></canvas>
                <div id="wb-remote-cursors" class="pointer-events-none absolute inset-0 overflow-hidden"></div>
                <div class="pointer-events-none absolute bottom-2.5 left-3 inline-flex items-center gap-1.5 rounded-full bg-slate-900/75 px-2.5 py-1 text-[10px] font-semibold text-slate-200 backdrop-blur">
                    <span class="h-1.5 w-1.5 rounded-full bg-emerald-400 animate-pulse"></span>
                    <span id="wb-live-status">Đồng bộ trực tiếp 35ms &bull; Con trỏ nhóm Figma</span>
                </div>
            </div>
        </div>
    `;
}
