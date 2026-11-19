/**
 * Cột 3 Tab 4 — View Bảng xếp hạng Thi đua Học tập trong Phòng (Room Leaderboard)
 * với huy hiệu Vương miện Vàng / Bạc / Đồng cho Top 3 sinh viên xuất sắc nhất.
 */

import { resolveBackendFileUrl } from '../../../core/config/app.config.js';
import { renderEmptyState } from '../../../shared/components/emptyState.component.js';
import { escapeHtml, getInitials } from '../../../shared/utils/dom.utils.js';

export function renderLeaderboardSkeletons() {
    return `
        <div class="space-y-2.5">
            <div class="skeleton-box h-20 w-full rounded-2xl"></div>
            <div class="skeleton-box h-16 w-full rounded-xl"></div>
            <div class="skeleton-box h-16 w-full rounded-xl"></div>
        </div>
    `;
}

export function renderRoomLeaderboard(items = [], currentUserId = 0) {
    if (!items.length) {
        return renderEmptyState({
            title: 'Chưa có bảng xếp hạng',
            description: 'Hãy hoàn thành đề thi trắc nghiệm và thảo luận trong phòng để tích lũy điểm thi đua.'
        });
    }

    const rankDecor = {
        1: {
            crown: '👑 🥇',
            cardCls: 'border-amber-300 bg-gradient-to-r from-amber-50/90 via-yellow-50/50 to-white dark:from-amber-500/15 dark:via-slate-900 dark:to-slate-900 dark:border-amber-400/40 shadow-sm',
            ptsCls: 'bg-amber-500 text-white'
        },
        2: {
            crown: '🥈',
            cardCls: 'border-slate-300 bg-gradient-to-r from-slate-100/90 to-white dark:from-slate-800/80 dark:to-slate-900 dark:border-slate-600/50',
            ptsCls: 'bg-slate-700 text-white'
        },
        3: {
            crown: '🥉',
            cardCls: 'border-orange-300 bg-gradient-to-r from-orange-50/80 to-white dark:from-orange-500/15 dark:to-slate-900 dark:border-orange-400/40',
            ptsCls: 'bg-orange-600 text-white'
        }
    };

    return `
        <div class="rounded-2xl border border-indigo-200/80 dark:border-indigo-500/25 bg-indigo-50/50 dark:bg-indigo-500/10 p-3 text-[11px] text-slate-600 dark:text-slate-300">
            <strong class="text-indigo-700 dark:text-indigo-300">Công thức điểm Thi đua:</strong>
            <code class="font-mono text-[10px]">(Điểm TB Quiz × 10) + (Số đề × 15) + (Tin nhắn × 2)</code>
        </div>

        <div data-lb-list class="space-y-2.5 mt-3">
            ${items
                .map((m) => {
                    const decor = rankDecor[m.rank] || {
                        crown: `#${m.rank}`,
                        cardCls: 'border-slate-200 dark:border-slate-800 bg-white dark:bg-slate-900',
                        ptsCls: 'bg-indigo-50 dark:bg-indigo-500/20 text-indigo-700 dark:text-indigo-300'
                    };
                    const isTop3 = m.rank >= 1 && m.rank <= 3;
                    const isMe = Number(m.userId) === Number(currentUserId);
                    const avatarNode = m.avatarUrl
                        ? `<img src="${escapeHtml(resolveBackendFileUrl(m.avatarUrl))}" alt="" class="h-10 w-10 rounded-xl object-cover bg-white ring-2 ring-white dark:ring-slate-800 shadow-xs" />`
                        : `<span class="flex h-10 w-10 items-center justify-center rounded-xl bg-indigo-600 text-xs font-bold text-white">${escapeHtml(getInitials(m.fullName))}</span>`;

                    return `
                        <div ${isTop3 ? 'data-tilt-card' : ''} class="${isTop3 ? 'tilt-3d-card ' : ''}spotlight-surface flex items-center justify-between gap-3 rounded-2xl border p-3 transition ${decor.cardCls} ${isMe ? 'ring-2 ring-indigo-500/50' : ''}">
                            <div class="flex items-center gap-2.5 min-w-0">
                                <span class="w-8 text-center font-mono text-sm font-extrabold text-slate-700 dark:text-slate-200 shrink-0">
                                    ${decor.crown}
                                </span>
                                ${avatarNode}
                                <div class="min-w-0">
                                    <p class="truncate text-xs font-extrabold text-slate-900 dark:text-white">
                                        ${escapeHtml(m.fullName)}
                                        ${isMe ? '<span class="text-[10px] font-semibold text-indigo-600 dark:text-indigo-400">(Bạn)</span>' : ''}
                                    </p>
                                    <p class="text-[11px] text-slate-500 dark:text-slate-400">
                                        TB: <strong>${Number(m.averageQuizScore).toFixed(1)}</strong> &bull;
                                        ${m.completedQuizzesCount} đề &bull; ${m.messageCount} chat
                                    </p>
                                </div>
                            </div>
                            <span class="shrink-0 rounded-xl px-2.5 py-1 font-mono text-xs font-extrabold ${decor.ptsCls}">
                                ${Number(m.totalPoints).toFixed(0)} đ
                            </span>
                        </div>
                    `;
                })
                .join('')}
        </div>
    `;
}
