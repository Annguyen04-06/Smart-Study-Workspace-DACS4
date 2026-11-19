/**
 * Bottom Sheet dùng chung chuẩn Native App:
 * - Điện thoại (< 768px): trượt lên từ đáy màn hình, vuốt xuống để đóng, chừa vùng an toàn Home Indicator.
 * - Máy tính: hiển thị dạng hộp thoại giữa màn hình.
 * Đóng bằng: nút Đóng, chạm lớp phủ, phím Escape hoặc vuốt xuống.
 * Lưu ý bảo mật: contentHtml phải là HTML tĩnh do lập trình viên soạn; dữ liệu người dùng phải qua escapeHtml() trước.
 */

import { escapeHtml } from '../utils/dom.utils.js';

const MOBILE_MEDIA_QUERY = '(max-width: 767px)';
const SWIPE_CLOSE_THRESHOLD_PX = 90;
const CLOSE_ANIMATION_MS = 280;

let activeSheet = null;

function buildSheetMarkup({ title, subtitle, contentHtml }) {
    return `
        <div class="ss-sheet-panel" data-sheet-panel>
            <span class="ss-sheet-handle" aria-hidden="true"></span>
            <div class="mb-4 flex items-start justify-between gap-3">
                <div class="min-w-0">
                    <h2 class="text-base font-extrabold leading-snug">${escapeHtml(title)}</h2>
                    ${subtitle ? `<p class="mt-0.5 text-xs text-slate-500 dark:text-slate-400">${escapeHtml(subtitle)}</p>` : ''}
                </div>
                <button type="button" data-sheet-close aria-label="Đóng"
                    class="flex h-9 w-9 shrink-0 items-center justify-center rounded-full bg-slate-100 dark:bg-slate-800 text-slate-500 dark:text-slate-300 hover:text-rose-600 transition">
                    <svg class="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2.2"><path stroke-linecap="round" stroke-linejoin="round" d="M6 18L18 6M6 6l12 12"/></svg>
                </button>
            </div>
            <div data-sheet-body>${contentHtml}</div>
        </div>
    `;
}

/**
 * Cho phép vuốt xuống trên Bottom Sheet (khi nội dung đang ở đầu trang) để đóng — chỉ áp dụng trên điện thoại.
 */
function bindSwipeDownToClose(panelEl) {
    let startY = null;
    let offsetY = 0;

    panelEl.addEventListener('touchstart', (event) => {
        if (!window.matchMedia(MOBILE_MEDIA_QUERY).matches || panelEl.scrollTop > 0 || event.touches.length !== 1) {
            startY = null;
            return;
        }
        startY = event.touches[0].clientY;
        offsetY = 0;
        panelEl.style.transition = 'none';
    }, { passive: true });

    panelEl.addEventListener('touchmove', (event) => {
        if (startY === null) return;
        offsetY = Math.max(0, event.touches[0].clientY - startY);
        panelEl.style.transform = `translateY(${offsetY}px)`;
    }, { passive: true });

    panelEl.addEventListener('touchend', () => {
        if (startY === null) return;
        startY = null;
        panelEl.style.transition = '';
        panelEl.style.transform = '';
        if (offsetY > SWIPE_CLOSE_THRESHOLD_PX) {
            closeBottomSheet();
        }
    });
}

function handleEscapeKey(event) {
    if (event.key === 'Escape') {
        closeBottomSheet();
    }
}

/**
 * Mở Bottom Sheet mới (tự đóng sheet đang mở trước đó nếu có).
 * @returns {HTMLElement} Phần tử gốc của sheet để controller gắn thêm sự kiện nếu cần
 */
export function openBottomSheet({ title, subtitle = '', contentHtml = '', ariaLabel = '' }) {
    closeBottomSheet({ immediate: true });

    const backdropEl = document.createElement('div');
    backdropEl.className = 'ss-sheet-backdrop';
    backdropEl.setAttribute('role', 'dialog');
    backdropEl.setAttribute('aria-modal', 'true');
    backdropEl.setAttribute('aria-label', ariaLabel || title);
    backdropEl.innerHTML = buildSheetMarkup({ title, subtitle, contentHtml });

    backdropEl.addEventListener('click', (event) => {
        if (event.target === backdropEl || event.target.closest('[data-sheet-close]')) {
            closeBottomSheet();
        }
    });

    document.body.appendChild(backdropEl);
    document.addEventListener('keydown', handleEscapeKey);
    bindSwipeDownToClose(backdropEl.querySelector('[data-sheet-panel]'));

    activeSheet = {
        element: backdropEl,
        previousFocus: document.activeElement,
        previousOverflow: document.documentElement.style.overflow
    };
    document.documentElement.style.overflow = 'hidden';

    // Đợi 1 khung hình để trình duyệt áp trạng thái ban đầu rồi mới kích hoạt hiệu ứng trượt lên
    requestAnimationFrame(() => {
        backdropEl.classList.add('is-open');
        backdropEl.querySelector('[data-sheet-close]')?.focus({ preventScroll: true });
    });

    return backdropEl;
}

/**
 * Đóng Bottom Sheet đang mở (nếu có) và trả lại trạng thái cuộn/focus ban đầu cho trang.
 */
export function closeBottomSheet({ immediate = false } = {}) {
    if (!activeSheet) return;

    const { element, previousFocus, previousOverflow } = activeSheet;
    activeSheet = null;
    document.removeEventListener('keydown', handleEscapeKey);
    document.documentElement.style.overflow = previousOverflow;

    if (immediate) {
        element.remove();
    } else {
        element.classList.remove('is-open');
        setTimeout(() => element.remove(), CLOSE_ANIMATION_MS);
    }

    if (previousFocus && typeof previousFocus.focus === 'function') {
        previousFocus.focus({ preventScroll: true });
    }
}
