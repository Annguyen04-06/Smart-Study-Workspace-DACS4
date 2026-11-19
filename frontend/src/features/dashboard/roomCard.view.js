/**
 * Tầng View hiển thị thẻ Phòng học (Room Card) chuẩn SaaS hiện đại với dải màu nhấn trên đỉnh.
 */

import { escapeHtml, getInitials } from '../../shared/utils/dom.utils.js';
import { formatRelativeTime } from '../../shared/utils/format.utils.js';

export function renderRoomSkeletons(count = 6) {
    return Array.from({ length: count })
        .map(
            () => `
            <div class="saas-card p-5 space-y-4">
                <div class="flex justify-between">
                    <div class="skeleton-box h-5 w-28"></div>
                    <div class="skeleton-box h-5 w-16"></div>
                </div>
                <div class="skeleton-box h-6 w-3/4"></div>
                <div class="skeleton-box h-10 w-full"></div>
                <div class="flex justify-between pt-3 border-t border-slate-100">
                    <div class="skeleton-box h-5 w-24"></div>
                    <div class="skeleton-box h-8 w-28"></div>
                </div>
            </div>
        `
        )
        .join('');
}

export function renderRoomCard(room) {
    const roleBadge = room.isJoined
        ? room.myRoleInRoom === 'Owner'
            ? `<span class="rounded-full bg-indigo-50 dark:bg-indigo-950/60 px-2.5 py-0.5 text-[11px] font-bold text-indigo-700 dark:text-indigo-300 border border-indigo-200/80 dark:border-indigo-800/70">Trưởng phòng</span>`
            : `<span class="rounded-full bg-emerald-50 dark:bg-emerald-950/60 px-2.5 py-0.5 text-[11px] font-bold text-emerald-700 dark:text-emerald-300 border border-emerald-200/80 dark:border-emerald-800/70">Đã tham gia</span>`
        : `<span class="rounded-full bg-slate-100 dark:bg-slate-800 px-2.5 py-0.5 text-[11px] font-medium text-slate-600 dark:text-slate-300">Phòng mở</span>`;

    const actionBtn = room.isJoined
        ? `<button type="button" data-ripple data-action="enter-room" data-room-id="${room.id}"
               class="btn-primary-glow inline-flex items-center gap-1.5 rounded-xl px-4 py-2 text-xs font-semibold">
               <span>Vào Workspace</span>
               <svg class="w-3.5 h-3.5" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2.3"><path stroke-linecap="round" stroke-linejoin="round" d="M13 7l5 5m0 0l-5 5m5-5H6"/></svg>
           </button>`
        : `<button type="button" data-ripple data-action="join-room" data-room-id="${room.id}"
               class="inline-flex items-center gap-1.5 rounded-xl border border-indigo-200 dark:border-indigo-800 bg-indigo-50 dark:bg-indigo-950/50 px-4 py-2 text-xs font-bold text-indigo-700 dark:text-indigo-300 hover:bg-indigo-600 hover:text-white transition">
               <span>Tham gia ngay</span>
           </button>`;

    return `
        <article class="saas-card saas-card-interactive spotlight-surface relative overflow-hidden flex flex-col justify-between p-5 pt-6">
            <div class="absolute inset-x-0 top-0 h-1 bg-gradient-to-r from-indigo-500 via-violet-500 to-sky-400"></div>

            <div>
                <div class="flex items-center justify-between gap-2">
                    <span class="inline-flex items-center rounded-lg bg-slate-100/90 dark:bg-slate-800/90 border border-slate-200/70 dark:border-slate-700 px-2.5 py-1 text-[11px] font-semibold text-slate-700 dark:text-slate-200">
                        ${escapeHtml(room.subjectCategory)}
                    </span>
                    ${roleBadge}
                </div>

                <h3 class="mt-3.5 text-base font-extrabold text-slate-900 dark:text-slate-100 line-clamp-2 leading-snug">
                    ${escapeHtml(room.name)}
                </h3>

                <p class="mt-2 text-xs text-slate-500 dark:text-slate-400 line-clamp-2 min-h-[2.25rem] leading-relaxed">
                    ${escapeHtml(room.description || 'Không gian thảo luận nhóm, chia sẻ giáo trình PDF và luyện đề AI.')}
                </p>

                <div class="mt-4 flex flex-wrap items-center gap-2 text-xs">
                    <span class="inline-flex items-center gap-1 rounded-lg bg-slate-50 dark:bg-slate-800/70 border border-slate-200/70 dark:border-slate-700 px-2.5 py-1 text-slate-600 dark:text-slate-300">
                        <strong class="font-bold text-slate-800 dark:text-slate-100">${room.memberCount}</strong> thành viên
                    </span>
                    <span class="inline-flex items-center gap-1 rounded-lg bg-slate-50 dark:bg-slate-800/70 border border-slate-200/70 dark:border-slate-700 px-2.5 py-1 text-slate-600 dark:text-slate-300">
                        <strong class="font-bold text-slate-800 dark:text-slate-100">${room.materialCount}</strong> PDF
                    </span>
                    <span class="inline-flex items-center gap-1 rounded-lg bg-slate-50 dark:bg-slate-800/70 border border-slate-200/70 dark:border-slate-700 px-2.5 py-1 text-slate-600 dark:text-slate-300">
                        <strong class="font-bold text-slate-800 dark:text-slate-100">${room.quizCount}</strong> Quiz
                    </span>
                </div>
            </div>

            <div class="mt-5 pt-3.5 border-t border-slate-100 dark:border-slate-800 flex items-center justify-between gap-2">
                <div class="flex items-center gap-2 min-w-0">
                    <span class="flex h-7 w-7 shrink-0 items-center justify-center rounded-lg bg-indigo-50 dark:bg-indigo-950/60 text-[10px] font-bold text-indigo-700 dark:text-indigo-300">
                        ${escapeHtml(getInitials(room.createdByName))}
                    </span>
                    <div class="min-w-0">
                        <button type="button" data-action="copy-invite" data-invite-code="${escapeHtml(room.inviteCode)}"
                            class="inline-flex items-center gap-1 font-mono text-xs font-bold text-indigo-600 dark:text-indigo-400 hover:text-indigo-800 dark:hover:text-indigo-300 transition"
                            title="Nhấn để sao chép mã mời">
                            <span>#${escapeHtml(room.inviteCode)}</span>
                            <svg class="w-3 h-3 opacity-70" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2"><path stroke-linecap="round" stroke-linejoin="round" d="M8 16H6a2 2 0 01-2-2V6a2 2 0 012-2h8a2 2 0 012 2v2m-6 12h8a2 2 0 002-2v-8a2 2 0 00-2-2h-8a2 2 0 00-2 2v8a2 2 0 002 2z"/></svg>
                        </button>
                        <p class="truncate text-[10px] text-slate-400">${escapeHtml(formatRelativeTime(room.createdAt))}</p>
                    </div>
                </div>
                ${actionBtn}
            </div>
        </article>
    `;
}
