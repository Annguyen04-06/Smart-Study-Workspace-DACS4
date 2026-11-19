/**
 * View thuần (chỉ trả về chuỗi HTML tĩnh) cho nút "Tải App" và các hộp thoại hướng dẫn cài đặt PWA:
 * - iPhone / iPad: minh họa 2 bước Chia sẻ (Share) -> Thêm vào MH chính (Add to Home Screen)
 * - Android / Máy tính khi trình duyệt chưa cấp beforeinstallprompt: hướng dẫn cài thủ công qua menu
 */

const SHARE_ICON_SVG = `
    <svg class="w-5 h-5" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
        <path stroke-linecap="round" stroke-linejoin="round" d="M12 15V3m0 0L8 7m4-4l4 4M6 11H5a2 2 0 00-2 2v7a2 2 0 002 2h14a2 2 0 002-2v-7a2 2 0 00-2-2h-1"/>
    </svg>`;

const ADD_SQUARE_ICON_SVG = `
    <svg class="w-5 h-5" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
        <rect x="3.5" y="3.5" width="17" height="17" rx="4"/>
        <path stroke-linecap="round" d="M12 8v8M8 12h8"/>
    </svg>`;

const DOWNLOAD_ICON_SVG = `
    <svg class="w-4 h-4 text-indigo-100 transition-transform group-hover:translate-y-0.5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
        <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2.2" d="M4 16v1a3 3 0 003 3h10a3 3 0 003-3v-1m-4-4l-4 4m0 0l-4-4m4 4V4" />
    </svg>`;

export function renderInstallButtonContent() {
    return `${DOWNLOAD_ICON_SVG}<span class="pwa-install-label">Tải App</span>`;
}

function renderStepBadge(stepNumber) {
    return `<span class="flex h-7 w-7 shrink-0 items-center justify-center rounded-full bg-indigo-600 text-xs font-extrabold text-white shadow-md shadow-indigo-500/30">${stepNumber}</span>`;
}

function renderInsecureContextNotice(isSecureContext) {
    if (isSecureContext) return '';
    return `
        <div class="mb-4 rounded-2xl border border-amber-300/70 bg-amber-50 dark:bg-amber-500/10 dark:border-amber-400/30 p-3 text-xs leading-relaxed text-amber-900 dark:text-amber-200">
            <strong>Bạn đang mở qua HTTP (mạng LAN).</strong> Trình duyệt chỉ cấp đầy đủ tính năng App (toàn màn hình, chạy offline)
            khi truy cập bằng đường dẫn <strong>HTTPS</strong>. Hãy quét mã QR trên trang <em>QR Portal</em> để mở link
            <code class="font-mono">https://…trycloudflare.com</code> rồi cài lại.
        </div>
    `;
}

/**
 * Hướng dẫn trực quan 2 bước cho iPhone / iPad (Safari không hỗ trợ beforeinstallprompt).
 */
export function renderIosInstallGuide({ isSafari, isSecureContext }) {
    const browserNote = isSafari
        ? 'Nút <strong>Chia sẻ</strong> nằm ở thanh công cụ phía dưới (iPhone) hoặc phía trên (iPad) của Safari.'
        : 'Trên Chrome / trình duyệt khác của iOS, nút <strong>Chia sẻ</strong> nằm ở góc phải thanh địa chỉ. Khuyên dùng <strong>Safari</strong> để cài đặt ổn định nhất.';

    return `
        ${renderInsecureContextNotice(isSecureContext)}
        <ol class="space-y-4">
            <li class="flex gap-3">
                ${renderStepBadge(1)}
                <div class="min-w-0 flex-1 space-y-2.5">
                    <p class="text-sm font-semibold leading-snug">Bấm vào biểu tượng <strong>Chia sẻ (Share)</strong> trên thanh công cụ của trình duyệt.</p>
                    <div class="flex items-center justify-around rounded-2xl border border-slate-200 dark:border-slate-700 bg-slate-100/90 dark:bg-slate-800/80 px-3 py-2.5 text-slate-400" aria-hidden="true">
                        <span class="text-lg leading-none">&lsaquo;</span>
                        <span class="text-lg leading-none">&rsaquo;</span>
                        <span class="relative flex h-10 w-10 items-center justify-center rounded-xl bg-white dark:bg-slate-900 text-indigo-600 dark:text-indigo-300 ring-2 ring-indigo-500 shadow-lg shadow-indigo-500/30">
                            <span class="absolute inset-0 rounded-xl ring-4 ring-indigo-400/40 animate-ping"></span>
                            ${SHARE_ICON_SVG}
                        </span>
                        <svg class="w-5 h-5" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2"><path stroke-linecap="round" stroke-linejoin="round" d="M12 6.253v13m0-13C10.832 5.477 9.246 5 7.5 5S4.168 5.477 3 6.253v13C4.168 18.477 5.754 18 7.5 18s3.332.477 4.5 1.253m0-13C13.168 5.477 14.754 5 16.5 5c1.747 0 3.332.477 4.5 1.253v13C19.832 18.477 18.247 18 16.5 18c-1.746 0-3.332.477-4.5 1.253"/></svg>
                        <svg class="w-5 h-5" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2"><rect x="4" y="4" width="12" height="12" rx="2"/><path d="M8 20h10a2 2 0 002-2V8"/></svg>
                    </div>
                    <p class="text-[11px] leading-relaxed text-slate-500 dark:text-slate-400">${browserNote}</p>
                </div>
            </li>
            <li class="flex gap-3">
                ${renderStepBadge(2)}
                <div class="min-w-0 flex-1 space-y-2.5">
                    <p class="text-sm font-semibold leading-snug">Cuộn xuống và chọn <strong>"Thêm vào MH chính"</strong> (Add to Home Screen), sau đó bấm <strong>Thêm</strong>.</p>
                    <div class="overflow-hidden rounded-2xl border border-slate-200 dark:border-slate-700 bg-white dark:bg-slate-900 text-sm" aria-hidden="true">
                        <div class="flex items-center justify-between px-3.5 py-2.5 text-slate-400 border-b border-slate-100 dark:border-slate-800">
                            <span>Sao chép</span>
                            <svg class="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2"><rect x="8" y="8" width="12" height="12" rx="2"/><path d="M16 8V6a2 2 0 00-2-2H6a2 2 0 00-2 2v8a2 2 0 002 2h2"/></svg>
                        </div>
                        <div class="flex items-center justify-between px-3.5 py-2.5 bg-indigo-50 dark:bg-indigo-500/15 font-bold text-indigo-700 dark:text-indigo-300 ring-2 ring-inset ring-indigo-500/60">
                            <span>Thêm vào MH chính</span>
                            ${ADD_SQUARE_ICON_SVG}
                        </div>
                        <div class="flex items-center justify-between px-3.5 py-2.5 text-slate-400">
                            <span>Thêm dấu trang</span>
                            <svg class="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2"><path stroke-linecap="round" stroke-linejoin="round" d="M5 5a2 2 0 012-2h10a2 2 0 012 2v16l-7-3.5L5 21V5z"/></svg>
                        </div>
                    </div>
                    <p class="text-[11px] leading-relaxed text-slate-500 dark:text-slate-400">
                        Biểu tượng <strong>SmartStudy</strong> sẽ xuất hiện trên màn hình chính — mở từ đó để dùng như ứng dụng gốc (toàn màn hình, không thanh địa chỉ).
                    </p>
                </div>
            </li>
        </ol>
        <button type="button" data-sheet-close
            class="mt-5 w-full rounded-2xl btn-primary-glow py-3 text-sm font-bold">
            Đã hiểu
        </button>
    `;
}

/**
 * Hướng dẫn cài thủ công khi trình duyệt chưa phát sự kiện beforeinstallprompt
 * (Firefox, Samsung Internet, đã từ chối lời mời trước đó hoặc đang mở qua HTTP LAN).
 */
export function renderManualInstallGuide({ isAndroid, isSecureContext }) {
    const primarySteps = isAndroid
        ? `
            <li class="flex gap-3">${renderStepBadge(1)}<p class="text-sm leading-snug">Mở menu <strong>&#8942;</strong> ở góc trên bên phải của Chrome (hoặc menu <strong>&#9776;</strong> của Samsung Internet).</p></li>
            <li class="flex gap-3">${renderStepBadge(2)}<p class="text-sm leading-snug">Chọn <strong>"Cài đặt ứng dụng"</strong> hoặc <strong>"Thêm vào màn hình chính"</strong> rồi xác nhận <strong>Cài đặt</strong>.</p></li>
          `
        : `
            <li class="flex gap-3">${renderStepBadge(1)}<p class="text-sm leading-snug">Trên <strong>Chrome / Edge</strong>: bấm biểu tượng <strong>Cài đặt (&#8853;)</strong> ở cuối thanh địa chỉ, hoặc mở menu <strong>&#8942; / &hellip;</strong> &rarr; <strong>Ứng dụng</strong> &rarr; <strong>Cài đặt Smart Study</strong>.</p></li>
            <li class="flex gap-3">${renderStepBadge(2)}<p class="text-sm leading-snug">Trên <strong>Safari macOS</strong>: chọn menu <strong>Tệp</strong> &rarr; <strong>Thêm vào Dock</strong>. Firefox máy tính hiện chưa hỗ trợ cài đặt PWA.</p></li>
          `;

    return `
        ${renderInsecureContextNotice(isSecureContext)}
        <ol class="space-y-3.5">${primarySteps}</ol>
        <button type="button" data-sheet-close
            class="mt-5 w-full rounded-2xl btn-primary-glow py-3 text-sm font-bold">
            Đã hiểu
        </button>
    `;
}
