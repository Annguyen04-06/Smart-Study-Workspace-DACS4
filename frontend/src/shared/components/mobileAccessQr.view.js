/**
 * View nút "Mở trên Điện thoại (QR HTTPS)" và nội dung Modal mã QR cỡ lớn.
 * Mọi chuỗi động (URL, thời gian) đều đi qua escapeHtml chống XSS; ảnh QR (SVG) do controller gắn vào [data-mobile-qr-code].
 */

import { escapeHtml } from '../utils/dom.utils.js';

export const MOBILE_QR_MODAL_ID = 'ssw-mobile-qr-modal';

const QR_ICON = '<svg class="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2"><path stroke-linecap="round" stroke-linejoin="round" d="M4 4h6v6H4V4zm10 0h6v6h-6V4zM4 14h6v6H4v-6zm10 0h2m4 0v2m-6 2v2m4 0h2m-2-4h2v2m-6-2v2h2"/></svg>';

export function renderMobileQrButton() {
    return `
        <button type="button" data-action="open-mobile-qr"
            class="flex h-9 w-9 items-center justify-center rounded-xl border border-slate-200 dark:border-slate-800 bg-white dark:bg-slate-900 text-slate-600 dark:text-slate-300 hover:border-indigo-300 hover:text-indigo-600 transition"
            title="Mở trên Điện thoại (QR HTTPS)" aria-label="Mở trên Điện thoại bằng mã QR">
            ${QR_ICON}
        </button>
    `;
}

export function renderMobileQrModalShell() {
    return `
        <div id="${MOBILE_QR_MODAL_ID}" class="ss-modal-backdrop hidden" role="dialog" aria-modal="true" aria-labelledby="ssw-mobile-qr-title">
            <div class="saas-card w-full max-w-md p-6 shadow-2xl animate-slide-fade bg-white dark:bg-slate-900">
                <div class="flex items-center justify-between border-b border-slate-100 dark:border-slate-800 pb-3.5">
                    <div class="flex items-center gap-2.5">
                        <span class="flex h-8 w-8 items-center justify-center rounded-xl btn-primary-glow">${QR_ICON}</span>
                        <h2 id="ssw-mobile-qr-title" class="text-base font-extrabold text-slate-900 dark:text-white">Mở trên Điện thoại</h2>
                    </div>
                    <button type="button" data-modal-close class="rounded-lg p-1 text-slate-400 hover:bg-slate-100 dark:hover:bg-slate-800 hover:text-slate-700" aria-label="Đóng">
                        <svg class="w-5 h-5" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2"><path stroke-linecap="round" stroke-linejoin="round" d="M6 18L18 6M6 6l12 12"/></svg>
                    </button>
                </div>
                <div data-mobile-qr-body class="pt-4"></div>
            </div>
        </div>
    `;
}

export function renderMobileQrLoading() {
    return `
        <div class="flex flex-col items-center gap-3 py-10 text-sm text-slate-500 dark:text-slate-400">
            <span class="h-8 w-8 rounded-full border-2 border-indigo-200 border-t-indigo-600 animate-spin"></span>
            <span>Đang lấy đường dẫn HTTPS mới nhất...</span>
        </div>
    `;
}

export function renderMobileQrError(message) {
    return `
        <div class="space-y-3 py-6 text-center">
            <p class="text-sm font-bold text-rose-600 dark:text-rose-400">Không lấy được đường dẫn mở trên điện thoại</p>
            <p class="text-xs text-slate-500 dark:text-slate-400">${escapeHtml(message)}</p>
            <button type="button" data-action="refresh-mobile-qr"
                class="rounded-xl bg-indigo-600 px-4 py-2 text-xs font-bold text-white hover:bg-indigo-500 transition">Thử lại</button>
        </div>
    `;
}

function renderStatusBadge(isHttps) {
    if (isHttps) {
        return `
            <span class="inline-flex items-center gap-1.5 rounded-full bg-emerald-50 dark:bg-emerald-500/15 border border-emerald-200 dark:border-emerald-400/30 px-3 py-1 text-[11px] font-bold text-emerald-700 dark:text-emerald-300">
                <span class="h-1.5 w-1.5 rounded-full bg-emerald-500 animate-pulse"></span>
                HTTPS Ready (Hỗ trợ cài App 1 chạm &amp; Micro)
            </span>
        `;
    }
    return `
        <span class="inline-flex items-center gap-1.5 rounded-full bg-amber-50 dark:bg-amber-500/15 border border-amber-200 dark:border-amber-400/30 px-3 py-1 text-[11px] font-bold text-amber-700 dark:text-amber-300">
            <span class="h-1.5 w-1.5 rounded-full bg-amber-500"></span>
            Chỉ có link LAN (HTTP) — chưa cài App 1 chạm được
        </span>
    `;
}

function renderLinkRow(label, url, accentClass) {
    return `
        <div class="rounded-2xl border border-slate-200 dark:border-slate-800 bg-slate-50/80 dark:bg-slate-800/60 p-3">
            <div class="text-[10px] font-bold uppercase tracking-wide text-slate-400 mb-1">${escapeHtml(label)}</div>
            <div class="flex items-center justify-between gap-2">
                <code class="min-w-0 break-all text-xs font-bold ${accentClass}">${escapeHtml(url)}</code>
                <button type="button" data-action="copy-mobile-link" data-copy-value="${escapeHtml(url)}"
                    class="shrink-0 rounded-lg bg-indigo-50 dark:bg-indigo-500/20 px-2.5 py-1 text-[11px] font-bold text-indigo-700 dark:text-indigo-300 hover:bg-indigo-100 transition">
                    Copy
                </button>
            </div>
        </div>
    `;
}

/**
 * @param {{ targetUrl: string, isHttps: boolean, lanUrl: string|null, updatedAtLabel: string }} model
 */
export function renderMobileQrContent({ targetUrl, isHttps, lanUrl, updatedAtLabel }) {
    const secondaryLanRow = isHttps && lanUrl
        ? renderLinkRow('Dự phòng: cùng mạng Wi-Fi (HTTP)', lanUrl, 'text-emerald-600 dark:text-emerald-400')
        : '';
    const guide = isHttps
        ? 'Quét bằng Camera iPhone/Android → mở link → bấm <b>Tải App</b> (Android) hoặc <b>Chia sẻ → Thêm vào MH chính</b> (iPhone).'
        : 'Điện thoại phải dùng chung Wi-Fi với máy tính. Muốn cài App 1 chạm, hãy chạy <b>run-local-vps.bat</b> để bật đường hầm HTTPS.';

    return `
        <div class="space-y-4">
            <div class="flex justify-center">${renderStatusBadge(isHttps)}</div>
            <div class="mx-auto w-64 max-w-full rounded-2xl bg-white p-3 shadow-lg ring-1 ring-slate-200">
                <div data-mobile-qr-code class="aspect-square w-full [&>svg]:h-full [&>svg]:w-full"></div>
            </div>
            ${renderLinkRow(isHttps ? 'Đường dẫn HTTPS (Cloudflare Tunnel)' : 'Đường dẫn mạng LAN', targetUrl, 'text-indigo-600 dark:text-indigo-300')}
            ${secondaryLanRow}
            <p class="text-[11px] leading-relaxed text-slate-500 dark:text-slate-400">${guide}</p>
            <div class="flex items-center justify-between text-[10px] text-slate-400">
                <span>${escapeHtml(updatedAtLabel)}</span>
                <button type="button" data-action="refresh-mobile-qr" class="font-bold text-indigo-600 dark:text-indigo-300 hover:underline">Làm mới</button>
            </div>
        </div>
    `;
}
