/**
 * Cột 1 Workspace (Midnight Dark Sidebar) — View hiển thị danh sách Thành viên phòng học
 * kèm chấm xanh Online phát sáng thời gian thực và huy hiệu Trưởng phòng.
 */

import { resolveBackendFileUrl } from '../../../core/config/app.config.js';
import { escapeHtml, getInitials } from '../../../shared/utils/dom.utils.js';

const AVATAR_GRADIENTS = [
    'from-indigo-500 to-violet-600',
    'from-emerald-500 to-teal-600',
    'from-sky-500 to-blue-600',
    'from-amber-500 to-orange-600',
    'from-pink-500 to-rose-600'
];

export function renderRoomMembersList(members = [], onlineUserIds = new Set(), currentUserId = 0) {
    if (!members.length) {
        return `<p class="px-3 py-4 text-xs text-slate-400">Chưa có thành viên nào trong phòng.</p>`;
    }

    const sorted = [...members].sort((a, b) => {
        const aOnline = onlineUserIds.has(a.userId) ? 1 : 0;
        const bOnline = onlineUserIds.has(b.userId) ? 1 : 0;
        if (aOnline !== bOnline) return bOnline - aOnline;
        if (a.roleInRoom !== b.roleInRoom) return a.roleInRoom === 'Owner' ? -1 : 1;
        return a.fullName.localeCompare(b.fullName, 'vi');
    });

    return sorted
        .map((member) => {
            const isOnline = onlineUserIds.has(member.userId);
            const isMe = member.userId === currentUserId;
            const isOwner = member.roleInRoom === 'Owner';
            const grad = AVATAR_GRADIENTS[Math.abs(Number(member.userId) || 0) % AVATAR_GRADIENTS.length];

            const avatarNode = member.avatarUrl
                ? `<img src="${escapeHtml(resolveBackendFileUrl(member.avatarUrl))}" alt="" class="h-8 w-8 rounded-xl object-cover ring-1 ring-white/15" />`
                : `<span class="flex h-8 w-8 items-center justify-center rounded-xl bg-gradient-to-br ${grad} text-xs font-bold text-white shadow-sm">
                       ${escapeHtml(getInitials(member.fullName))}
                   </span>`;

            return `
                <div class="flex items-center justify-between gap-2.5 rounded-xl px-2.5 py-2 hover:bg-white/[0.06] transition">
                    <div class="flex items-center gap-2.5 min-w-0">
                        <div class="relative shrink-0">
                            ${avatarNode}
                            <span class="absolute -bottom-0.5 -right-0.5 h-2.5 w-2.5 rounded-full ring-2 ring-[#0b1120] ${isOnline ? 'bg-emerald-400 shadow-[0_0_8px_#34d399]' : 'bg-slate-600'}"
                                  title="${isOnline ? 'Đang trực tuyến' : 'Ngoại tuyến'}"></span>
                        </div>
                        <div class="min-w-0">
                            <p class="truncate text-xs font-semibold text-slate-100">
                                ${escapeHtml(member.fullName)}
                                ${isMe ? '<span class="text-[10px] font-normal text-indigo-300">(Bạn)</span>' : ''}
                            </p>
                            <p class="truncate text-[11px] text-slate-400">
                                ${isOnline ? '<span class="text-emerald-400 font-medium">Online</span>' : 'Offline'}
                                ${member.studentCode ? ` &bull; <span class="font-mono text-[10px]">${escapeHtml(member.studentCode)}</span>` : ''}
                            </p>
                        </div>
                    </div>
                    ${isOwner ? '<span class="shrink-0 rounded-md bg-amber-400/15 border border-amber-400/30 px-1.5 py-0.5 text-[10px] font-semibold text-amber-300">Owner</span>' : ''}
                </div>
            `;
        })
        .join('');
}
