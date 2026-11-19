/**
 * Thành phần giao diện trạng thái trống (Empty State) chuẩn SaaS kèm nút hành động tùy chọn.
 */

import { escapeHtml } from '../utils/dom.utils.js';

export function renderEmptyState({
    title = 'Chưa có dữ liệu',
    description = 'Hãy bắt đầu bằng cách tạo nội dung đầu tiên.',
    actionLabel = '',
    actionId = ''
}) {
    const buttonHtml = actionLabel && actionId
        ? `<button type="button" data-action="${escapeHtml(actionId)}"
               class="mt-4 inline-flex items-center gap-2 rounded-xl bg-indigo-600 px-4 py-2.5 text-sm font-semibold text-white shadow-sm hover:bg-indigo-700 transition">
               <svg class="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2.2">
                   <path stroke-linecap="round" stroke-linejoin="round" d="M12 4v16m8-8H4"/>
               </svg>
               <span>${escapeHtml(actionLabel)}</span>
           </button>`
        : '';

    return `
        <div class="flex flex-col items-center justify-center rounded-2xl border border-dashed border-slate-200 bg-slate-50/70 px-6 py-12 text-center">
            <div class="flex h-12 w-12 items-center justify-center rounded-2xl bg-indigo-50 text-indigo-600 mb-3.5">
                <svg class="w-6 h-6" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="1.8">
                    <path stroke-linecap="round" stroke-linejoin="round" d="M19 11H5m14 0a2 2 0 012 2v6a2 2 0 01-2 2H5a2 2 0 01-2-2v-6a2 2 0 012-2m14 0V9a2 2 0 00-2-2M5 11V9a2 2 0 012-2m0 0V5a2 2 0 012-2h6a2 2 0 012 2v2M7 7h10"/>
                </svg>
            </div>
            <h4 class="text-base font-semibold text-slate-900">${escapeHtml(title)}</h4>
            <p class="mt-1 max-w-md text-sm text-slate-500">${escapeHtml(description)}</p>
            ${buttonHtml}
        </div>
    `;
}
