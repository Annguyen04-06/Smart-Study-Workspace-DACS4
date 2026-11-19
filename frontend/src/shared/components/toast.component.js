/**
 * Thành phần hiển thị thông báo nổi góc phải (Toast Notification: success, error, info, warning).
 */

import { escapeHtml } from '../utils/dom.utils.js';

const TOAST_VARIANTS = {
    success: {
        border: 'border-emerald-200 bg-white',
        badge: 'bg-emerald-100 text-emerald-700',
        icon: `<svg class="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2.2"><path stroke-linecap="round" stroke-linejoin="round" d="M5 13l4 4L19 7"/></svg>`
    },
    error: {
        border: 'border-rose-200 bg-white',
        badge: 'bg-rose-100 text-rose-700',
        icon: `<svg class="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2.2"><path stroke-linecap="round" stroke-linejoin="round" d="M6 18L18 6M6 6l12 12"/></svg>`
    },
    warning: {
        border: 'border-amber-200 bg-white',
        badge: 'bg-amber-100 text-amber-700',
        icon: `<svg class="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2.2"><path stroke-linecap="round" stroke-linejoin="round" d="M12 9v3m0 4h.01M10.29 3.86L1.82 18a2 2 0 001.71 3h16.94a2 2 0 001.71-3L13.71 3.86a2 2 0 00-3.42 0z"/></svg>`
    },
    info: {
        border: 'border-indigo-200 bg-white',
        badge: 'bg-indigo-100 text-indigo-700',
        icon: `<svg class="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2.2"><path stroke-linecap="round" stroke-linejoin="round" d="M13 16h-1v-4h-1m1-4h.01M12 2a10 10 0 100 20 10 10 0 000-20z"/></svg>`
    }
};

function ensureContainer() {
    let container = document.getElementById('ss-toast-container');
    if (!container) {
        container = document.createElement('div');
        container.id = 'ss-toast-container';
        document.body.appendChild(container);
    }
    return container;
}

function showToast(type, message, durationMs = 3800) {
    const container = ensureContainer();
    const variant = TOAST_VARIANTS[type] || TOAST_VARIANTS.info;

    const item = document.createElement('div');
    item.className = `ss-toast-item animate-slide-fade flex items-start gap-3 p-3.5 rounded-xl border ${variant.border}`;
    item.innerHTML = `
        <span class="mt-0.5 flex h-7 w-7 shrink-0 items-center justify-center rounded-lg ${variant.badge}">
            ${variant.icon}
        </span>
        <div class="flex-1 text-sm font-medium text-slate-800 leading-snug">
            ${escapeHtml(message)}
        </div>
        <button type="button" data-toast-close class="text-slate-400 hover:text-slate-600 transition p-0.5" aria-label="Đóng">
            <svg class="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2"><path stroke-linecap="round" stroke-linejoin="round" d="M6 18L18 6M6 6l12 12"/></svg>
        </button>
    `;

    const dismiss = () => {
        item.style.opacity = '0';
        item.style.transform = 'translateX(12px)';
        item.style.transition = 'all 0.18s ease';
        setTimeout(() => item.remove(), 180);
    };

    item.querySelector('[data-toast-close]')?.addEventListener('click', dismiss);
    container.appendChild(item);

    if (durationMs > 0) {
        setTimeout(dismiss, durationMs);
    }
}

export const toast = {
    success: (msg, ms) => showToast('success', msg, ms),
    error: (msg, ms) => showToast('error', msg, ms ?? 4800),
    warning: (msg, ms) => showToast('warning', msg, ms),
    info: (msg, ms) => showToast('info', msg, ms)
};
