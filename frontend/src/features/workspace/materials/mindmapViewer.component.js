/**
 * Cột 3 Tab 1 — Bộ hiển thị Sơ đồ Tư duy Kiến thức AI (Mermaid Mindmap Interactive Viewer):
 * - Gọi mermaid.min.js render SVG sắc nét tương thích cả Light Mode & Dark Mode.
 * - Hỗ trợ Phóng to (Zoom In +), Thu nhỏ (Zoom Out -), Đặt lại (Reset 100%),
 *   cuộn con lăn chuột (Mouse Wheel Zoom) và kéo giữ chuột (Drag-to-Pan) mượt 60FPS.
 */

import { escapeHtml, qs } from '../../../shared/utils/dom.utils.js';

let renderCounter = 0;

function buildFallbackMermaidFromTitle(title) {
    const clean = String(title || 'Học liệu PDF')
        .replace(/["`[\]{}<>|#*$]/g, ' ')
        .replace(/\s+/g, ' ')
        .trim()
        .slice(0, 50);

    return [
        'flowchart TD',
        `    root["📚 ${clean}"]`,
        '    b1["1. Khái niệm & Định nghĩa Cốt lõi"]',
        '    b2["2. Giải thuật & Cơ chế Hoạt động"]',
        '    b3["3. Độ phức tạp & Tối ưu Hiệu năng"]',
        '    b4["4. Tổng kết & Câu hỏi Ôn tập"]',
        '    root --> b1',
        '    root --> b2',
        '    root --> b3',
        '    root --> b4'
    ].join('\n');
}

export function renderMindmapInspectorShell(material, { isLoading = false } = {}) {
    return `
        <div class="saas-card spotlight-surface border-indigo-200 dark:border-indigo-500/35 p-3.5 space-y-2.5">
            <div class="flex flex-wrap items-center justify-between gap-2 border-b border-slate-100 dark:border-slate-800 pb-2.5">
                <div class="min-w-0">
                    <span class="inline-flex items-center gap-1 text-[10px] font-extrabold uppercase tracking-wider text-violet-600 dark:text-violet-400">
                        <span>🧠 Sơ đồ Tư duy Kiến thức AI</span>
                    </span>
                    <h4 class="truncate text-xs font-bold text-slate-900 dark:text-white">${escapeHtml(material.title)}</h4>
                </div>

                <div class="flex items-center gap-1 shrink-0">
                    <button type="button" data-mm-zoom="out"
                        class="rounded-lg border border-slate-200 dark:border-slate-700 bg-slate-50 dark:bg-slate-800 px-2 py-1 text-xs font-bold text-slate-700 dark:text-slate-200 hover:border-indigo-400"
                        title="Thu nhỏ (-)">−</button>
                    <button type="button" data-mm-zoom="reset" id="mm-zoom-label"
                        class="rounded-lg border border-slate-200 dark:border-slate-700 bg-white dark:bg-slate-900 px-2 py-1 font-mono text-[10px] font-bold text-indigo-600 dark:text-indigo-300 hover:border-indigo-400"
                        title="Đặt lại tỷ lệ 100%">100%</button>
                    <button type="button" data-mm-zoom="in"
                        class="rounded-lg border border-slate-200 dark:border-slate-700 bg-slate-50 dark:bg-slate-800 px-2 py-1 text-xs font-bold text-slate-700 dark:text-slate-200 hover:border-indigo-400"
                        title="Phóng to (+)">+</button>

                    <button type="button" data-mat-action="regenerate-mindmap" data-mat-id="${material.id}"
                        class="ml-1 rounded-lg border border-violet-200 dark:border-violet-500/30 bg-violet-50 dark:bg-violet-500/15 px-2.5 py-1 text-[11px] font-semibold text-violet-700 dark:text-violet-300 hover:bg-violet-600 hover:text-white transition"
                        title="Yêu cầu AI phân tích lại sơ đồ mới">
                        ↻ Vẽ lại AI
                    </button>
                    <button type="button" data-mat-action="close-inspector"
                        class="rounded-lg p-1 text-slate-400 hover:bg-slate-100 dark:hover:bg-slate-800 hover:text-slate-700"
                        title="Đóng sơ đồ">
                        ✕
                    </button>
                </div>
            </div>

            <div id="mm-pan-viewport"
                class="relative h-[430px] w-full overflow-hidden rounded-xl border border-slate-200/80 dark:border-slate-800 bg-slate-50/70 dark:bg-slate-950/80 cursor-grab select-none"
                style="background-image: radial-gradient(rgba(148, 163, 184, 0.3) 1px, transparent 1px); background-size: 18px 18px;">
                ${
                    isLoading
                        ? `
                        <div class="flex h-full flex-col items-center justify-center gap-3 p-6 text-center">
                            <div class="ai-aura-card px-4 py-2.5">
                                <div class="ai-aura-inner px-4 py-2 text-xs font-bold text-indigo-700 dark:text-indigo-300">
                                    ✨ Gemini AI đang tổng hợp Sơ đồ Tư duy phân cấp từ PDF...
                                </div>
                            </div>
                        </div>
                    `
                        : `
                        <div id="mm-transform-stage" class="flex min-h-full min-w-full items-center justify-center p-6 origin-center will-change-transform">
                            <div id="mm-svg-host" class="transition-opacity duration-200"></div>
                        </div>
                        <div class="pointer-events-none absolute bottom-2 right-2.5 rounded-full bg-slate-900/70 px-2.5 py-0.5 text-[10px] font-medium text-slate-200 backdrop-blur">
                            Kéo chuột để di chuyển &bull; Lăn chuột để thu phóng
                        </div>
                    `
                }
            </div>
        </div>
    `;
}

export async function mountInteractiveMindmap(containerEl, mermaidCode, fallbackTitle = 'Học liệu PDF') {
    const viewportEl = qs('#mm-pan-viewport', containerEl);
    const stageEl = qs('#mm-transform-stage', containerEl);
    const svgHostEl = qs('#mm-svg-host', containerEl);
    const zoomLabelEl = qs('#mm-zoom-label', containerEl);

    if (!viewportEl || !stageEl || !svgHostEl) return;

    const mermaidGlobal = window.mermaid;
    if (!mermaidGlobal) {
        svgHostEl.innerHTML = `<p class="text-xs text-rose-500">Thư viện Mermaid chưa tải xong. Vui lòng thử lại.</p>`;
        return;
    }

    const isDark = document.documentElement.classList.contains('dark');
    mermaidGlobal.initialize({
        startOnLoad: false,
        securityLevel: 'strict',
        theme: isDark ? 'dark' : 'default',
        flowchart: {
            htmlLabels: false,
            curve: 'basis',
            padding: 14
        }
    });

    renderCounter += 1;
    const renderId = `ss-mindmap-svg-${renderCounter}`;

    try {
        const { svg } = await mermaidGlobal.render(renderId, mermaidCode);
        svgHostEl.innerHTML = svg;
    } catch {
        const safeCode = buildFallbackMermaidFromTitle(fallbackTitle);
        const { svg } = await mermaidGlobal.render(`${renderId}-safe`, safeCode);
        svgHostEl.innerHTML = svg;
    }

    const svgEl = svgHostEl.querySelector('svg');
    if (svgEl) {
        svgEl.style.maxWidth = 'none';
        svgEl.style.height = 'auto';
    }

    let scale = 1;
    let panX = 0;
    let panY = 0;
    let isPanning = false;
    let startX = 0;
    let startY = 0;

    const applyTransform = () => {
        stageEl.style.transform = `translate3d(${panX.toFixed(1)}px, ${panY.toFixed(1)}px, 0) scale(${scale.toFixed(3)})`;
        if (zoomLabelEl) {
            zoomLabelEl.textContent = `${Math.round(scale * 100)}%`;
        }
    };

    const setScale = (nextScale) => {
        scale = Math.max(0.4, Math.min(2.8, nextScale));
        applyTransform();
    };

    containerEl.querySelectorAll('[data-mm-zoom]').forEach((btn) => {
        btn.addEventListener('click', (e) => {
            e.stopPropagation();
            const dir = btn.dataset.mmZoom;
            if (dir === 'in') setScale(scale + 0.18);
            else if (dir === 'out') setScale(scale - 0.18);
            else {
                scale = 1;
                panX = 0;
                panY = 0;
                applyTransform();
            }
        });
    });

    viewportEl.addEventListener(
        'wheel',
        (e) => {
            e.preventDefault();
            const delta = e.deltaY < 0 ? 0.12 : -0.12;
            setScale(scale + delta);
        },
        { passive: false }
    );

    viewportEl.addEventListener('pointerdown', (e) => {
        if (e.button !== 0) return;
        isPanning = true;
        startX = e.clientX - panX;
        startY = e.clientY - panY;
        viewportEl.classList.remove('cursor-grab');
        viewportEl.classList.add('cursor-grabbing');
        viewportEl.setPointerCapture?.(e.pointerId);
    });

    viewportEl.addEventListener('pointermove', (e) => {
        if (!isPanning) return;
        panX = e.clientX - startX;
        panY = e.clientY - startY;
        applyTransform();
    });

    const stopPan = () => {
        if (!isPanning) return;
        isPanning = false;
        viewportEl.classList.remove('cursor-grabbing');
        viewportEl.classList.add('cursor-grab');
    };

    viewportEl.addEventListener('pointerup', stopPan);
    viewportEl.addEventListener('pointercancel', stopPan);

    applyTransform();
}
