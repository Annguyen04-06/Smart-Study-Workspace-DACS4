/**
 * Cột 2 Workspace — View hiển thị:
 * 1. Skeleton Shimmer khi đang tải lịch sử hội thoại
 * 2. Thanh "Tin nhắn đã ghim" (Pinned Knowledge Banner) thu/mở linh hoạt
 * 3. Bong bóng chat Sinh viên (escapeHtml chống XSS + nút Ghim + Huy hiệu Cảm xúc trực tiếp)
 * 4. Khung phản hồi đẳng cấp của Trợ giảng @bot (.ai-prose + Cảm xúc + huy hiệu tốc độ ⚡ +
 *    thanh công cụ chân tin nhắn: [Copy câu trả lời], [Ghim kiến thức], [Đọc thành tiếng]).
 * 5. Bong bóng "đang gửi" (Optimistic UI) và khung @bot đang Streaming thật (chữ thô + con trỏ nhấp nháy).
 */

import { resolveBackendFileUrl } from '../../../core/config/app.config.js';
import { renderAcademicMarkdownHtml } from '../../../shared/components/markdownViewer.component.js';
import { escapeHtml, getInitials } from '../../../shared/utils/dom.utils.js';
import { formatShortTime } from '../../../shared/utils/format.utils.js';

const QUICK_REACTION_EMOJIS = ['🔥', '💡', '👏', '❤️', '🎯'];

export function renderChatSkeletons() {
    return `
        <div class="space-y-4 py-2">
            <div class="flex items-start gap-3">
                <div class="skeleton-box h-8 w-8 rounded-xl shrink-0"></div>
                <div class="space-y-2 w-2/3">
                    <div class="skeleton-box h-3 w-28"></div>
                    <div class="skeleton-box h-12 w-full rounded-2xl"></div>
                </div>
            </div>
            <div class="flex justify-end">
                <div class="space-y-2 w-1/2">
                    <div class="skeleton-box h-10 w-full rounded-2xl"></div>
                </div>
            </div>
            <div class="flex items-start gap-3">
                <div class="skeleton-box h-9 w-9 rounded-xl shrink-0"></div>
                <div class="space-y-2 w-4/5">
                    <div class="skeleton-box h-3 w-44"></div>
                    <div class="skeleton-box h-28 w-full rounded-2xl"></div>
                </div>
            </div>
        </div>
    `;
}

export function renderPinnedMessagesBanner(pinnedMessages = [], isExpanded = false) {
    if (!pinnedMessages.length) {
        return '';
    }

    const latest = pinnedMessages[0];
    const previewLine = String(latest.content || '')
        .replace(/[#*`>|$-]/g, ' ')
        .replace(/\s+/g, ' ')
        .trim()
        .slice(0, 115);

    const expandedItemsHtml = isExpanded
        ? `
            <div class="mt-2.5 max-h-60 overflow-y-auto thin-scrollbar space-y-2 border-t border-amber-200/70 dark:border-amber-500/20 pt-2.5">
                ${pinnedMessages
                    .map(
                        (pm) => `
                        <div class="rounded-xl border border-slate-200/90 dark:border-slate-800 bg-white dark:bg-slate-900 p-3 shadow-xs">
                            <div class="flex items-center justify-between gap-2 mb-1.5">
                                <span class="text-[11px] font-bold ${pm.isAiBot ? 'text-indigo-600 dark:text-indigo-400' : 'text-slate-700 dark:text-slate-200'}">
                                    📌 ${escapeHtml(pm.senderName)} &bull; ${escapeHtml(formatShortTime(pm.createdAt))}
                                </span>
                                <div class="flex items-center gap-1.5">
                                    <button type="button" data-pin-banner-action="jump" data-message-id="${pm.id}"
                                        class="rounded-md bg-indigo-50 dark:bg-indigo-500/15 px-2 py-0.5 text-[10px] font-semibold text-indigo-700 dark:text-indigo-300 hover:bg-indigo-100">
                                        Đi tới tin nhắn
                                    </button>
                                    <button type="button" data-pin-banner-action="unpin" data-message-id="${pm.id}"
                                        class="rounded-md bg-rose-50 dark:bg-rose-500/15 px-2 py-0.5 text-[10px] font-semibold text-rose-600 dark:text-rose-300 hover:bg-rose-100">
                                        Bỏ ghim
                                    </button>
                                </div>
                            </div>
                            <div class="max-h-40 overflow-y-auto thin-scrollbar">
                                ${pm.isAiBot ? renderAcademicMarkdownHtml(pm.content) : `<p class="text-xs text-slate-700 dark:text-slate-300">${escapeHtml(pm.content)}</p>`}
                            </div>
                        </div>
                    `
                    )
                    .join('')}
            </div>
        `
        : '';

    return `
        <div class="mx-auto max-w-4xl rounded-2xl border border-amber-200/90 dark:border-amber-500/30 bg-amber-50/90 dark:bg-slate-900/95 px-3.5 py-2 shadow-sm backdrop-blur">
            <div class="flex items-center justify-between gap-3">
                <button type="button" data-pin-banner-action="toggle-expand" class="flex items-center gap-2 min-w-0 text-left flex-1">
                    <span class="flex h-6 w-6 shrink-0 items-center justify-center rounded-lg bg-amber-500/15 text-amber-700 dark:text-amber-300 text-xs">📌</span>
                    <div class="min-w-0">
                        <p class="text-[11px] font-extrabold text-amber-900 dark:text-amber-300">
                            Kiến thức Đã Ghim (${pinnedMessages.length})
                        </p>
                        <p class="truncate text-xs text-amber-800/90 dark:text-slate-300">${escapeHtml(previewLine)}...</p>
                    </div>
                </button>
                <button type="button" data-pin-banner-action="toggle-expand"
                    class="shrink-0 rounded-lg border border-amber-300/80 dark:border-amber-500/30 bg-white/80 dark:bg-slate-800 px-2.5 py-1 text-[11px] font-bold text-amber-900 dark:text-amber-200 hover:bg-white transition">
                    ${isExpanded ? 'Thu gọn ▲' : 'Xem tất cả ▼'}
                </button>
            </div>
            ${expandedItemsHtml}
        </div>
    `;
}

function renderMessageReactionBar(msg, currentUserId, alignRight = false) {
    const reactionsMap = msg.reactions && typeof msg.reactions === 'object' ? msg.reactions : {};
    const activeEntries = Object.entries(reactionsMap).filter(([, uids]) => Array.isArray(uids) && uids.length > 0);

    const activePillsHtml = activeEntries
        .map(([emoji, uids]) => {
            const reactedByMe = uids.some((id) => Number(id) === Number(currentUserId));
            return `
                <button type="button"
                    data-msg-action="react"
                    data-message-id="${msg.id}"
                    data-emoji="${escapeHtml(emoji)}"
                    class="inline-flex items-center gap-1 rounded-full border px-2 py-0.5 text-[11px] font-semibold transition transform hover:scale-105 ${
                        reactedByMe
                            ? 'border-indigo-400 bg-indigo-50 text-indigo-700 dark:border-indigo-400/50 dark:bg-indigo-500/20 dark:text-indigo-200 shadow-xs'
                            : 'border-slate-200 dark:border-slate-700 bg-white/90 dark:bg-slate-800/90 text-slate-600 dark:text-slate-300 hover:border-indigo-300'
                    }">
                    <span>${escapeHtml(emoji)}</span>
                    <span class="font-mono text-[10px]">${uids.length}</span>
                </button>
            `;
        })
        .join('');

    const hoverQuickStripHtml = `
        <div class="opacity-0 group-hover:opacity-100 inline-flex items-center gap-0.5 rounded-full border border-slate-200/80 dark:border-slate-700 bg-white/95 dark:bg-slate-900/95 px-1.5 py-0.5 shadow-xs transition">
            ${QUICK_REACTION_EMOJIS.map(
                (em) => `
                    <button type="button"
                        data-msg-action="react"
                        data-message-id="${msg.id}"
                        data-emoji="${em}"
                        class="rounded-full px-1 text-xs hover:scale-125 transition-transform"
                        title="Thả cảm xúc ${em}">
                        ${em}
                    </button>
                `
            ).join('')}
        </div>
    `;

    return `
        <div class="mt-1.5 flex flex-wrap items-center gap-1 ${alignRight ? 'justify-end' : 'justify-start'}">
            ${activePillsHtml}
            ${hoverQuickStripHtml}
        </div>
    `;
}

function renderStudentContentHtml(content) {
    const safeContent = escapeHtml(content).replace(/\n/g, '<br/>');
    if (!String(content || '').trim().toLowerCase().startsWith('@bot')) {
        return safeContent;
    }
    return safeContent.replace(
        /^(@bot)/i,
        '<span class="mr-1 inline-flex items-center rounded-md bg-white/20 px-1.5 py-0.5 font-mono text-xs font-bold tracking-tight ring-1 ring-white/30">$1</span>'
    );
}

function renderOwnBubble(safeContent) {
    return `
        <div class="rounded-2xl rounded-tr-sm bg-gradient-to-br from-indigo-600 via-indigo-600 to-violet-600 px-4 py-2.5 text-sm text-white shadow-md shadow-indigo-600/15 leading-relaxed">
            ${safeContent}
        </div>
    `;
}

/**
 * Bong bóng tin nhắn của chính mình hiển thị tức thì (0ms) trong lúc chờ máy chủ xác nhận.
 * clientId chỉ gồm [A-Za-z0-9_-] (do chat.controller sinh) nên an toàn khi gắn vào thuộc tính HTML.
 */
export function renderPendingOwnMessage({ clientId, content }) {
    return `
        <div class="group flex justify-end spa-fade-in" data-client-id="${escapeHtml(clientId)}" data-delivery-state="sending">
            <div class="max-w-[82%] sm:max-w-[74%] opacity-75">
                <div class="flex items-center justify-end gap-2 mb-1 pr-1">
                    <span class="inline-flex items-center gap-1 text-[11px] text-slate-400">
                        <span class="h-1.5 w-1.5 rounded-full bg-indigo-400 animate-pulse"></span>Đang gửi…
                    </span>
                    <span class="text-xs font-semibold text-slate-600 dark:text-slate-300">Bạn</span>
                </div>
                ${renderOwnBubble(renderStudentContentHtml(content))}
            </div>
        </div>
    `;
}

/**
 * Huy hiệu tốc độ phản hồi @bot: thời gian ra chữ đầu tiên (TTFT), tooltip kèm thời gian hoàn tất.
 */
function renderResponseSpeedBadge(responseStats) {
    if (!responseStats || !Number.isFinite(Number(responseStats.firstChunkMs))) {
        return '';
    }
    const firstSeconds = (Number(responseStats.firstChunkMs) / 1000).toFixed(1);
    const totalSeconds = (Number(responseStats.durationMs || responseStats.firstChunkMs) / 1000).toFixed(1);
    return `
        <span class="inline-flex items-center gap-0.5 rounded-full bg-emerald-50 dark:bg-emerald-500/15 border border-emerald-200/80 dark:border-emerald-400/30 px-2 py-0.5 text-[10px] font-bold text-emerald-700 dark:text-emerald-300"
            title="Chữ đầu tiên sau ${firstSeconds}s · Hoàn tất sau ${totalSeconds}s">
            ⚡ ${firstSeconds}s
        </span>
    `;
}

export function renderMessageItem(msg, currentUserId, { isRealtimeNew = false, responseStats = null } = {}) {
    if (msg.isAiBot) {
        return renderBotMessageCard(msg, currentUserId, isRealtimeNew, responseStats);
    }

    const isMine = Number(msg.senderId) === Number(currentUserId);
    const safeContent = renderStudentContentHtml(msg.content);

    const pinButton = `
        <button type="button" data-msg-action="pin" data-message-id="${msg.id}"
            class="opacity-0 group-hover:opacity-100 rounded-md px-1.5 py-0.5 text-[10px] font-semibold ${
                msg.isPinned ? 'opacity-100 text-amber-600 bg-amber-50 dark:bg-amber-500/20' : 'text-slate-400 hover:text-indigo-600'
            } transition"
            title="${msg.isPinned ? 'Bỏ ghim' : 'Ghim tin nhắn'}">
            ${msg.isPinned ? '📌 Đã ghim' : '📌 Ghim'}
        </button>
    `;

    if (isMine) {
        return `
            <div class="group flex justify-end ${isRealtimeNew ? 'spa-fade-in' : ''}" data-message-id="${msg.id}">
                <div class="max-w-[82%] sm:max-w-[74%]">
                    <div class="flex items-center justify-end gap-2 mb-1 pr-1">
                        ${pinButton}
                        <span class="text-[11px] text-slate-400">${escapeHtml(formatShortTime(msg.createdAt))}</span>
                        <span class="text-xs font-semibold text-slate-600 dark:text-slate-300">Bạn</span>
                    </div>
                    ${renderOwnBubble(safeContent)}
                    ${renderMessageReactionBar(msg, currentUserId, true)}
                </div>
            </div>
        `;
    }

    const avatarHtml = msg.senderAvatarUrl
        ? `<img src="${escapeHtml(resolveBackendFileUrl(msg.senderAvatarUrl))}" alt="" class="h-8 w-8 rounded-xl object-cover shrink-0 mt-1 shadow-xs bg-white" />`
        : `<span class="mt-1 flex h-8 w-8 shrink-0 items-center justify-center rounded-xl bg-gradient-to-br from-slate-700 to-slate-900 text-xs font-bold text-white shadow-xs">
               ${escapeHtml(getInitials(msg.senderName))}
           </span>`;

    return `
        <div class="group flex items-start gap-3 ${isRealtimeNew ? 'spa-fade-in' : ''}" data-message-id="${msg.id}">
            ${avatarHtml}
            <div class="max-w-[82%] sm:max-w-[74%]">
                <div class="flex items-center gap-2 mb-1 pl-0.5">
                    <span class="text-xs font-bold text-slate-800 dark:text-slate-200">${escapeHtml(msg.senderName)}</span>
                    <span class="text-[11px] text-slate-400">${escapeHtml(formatShortTime(msg.createdAt))}</span>
                    ${pinButton}
                </div>
                <div class="rounded-2xl rounded-tl-sm border border-slate-200/90 dark:border-slate-800 bg-white dark:bg-slate-900 px-4 py-2.5 text-sm text-slate-800 dark:text-slate-100 shadow-sm leading-relaxed">
                    ${safeContent}
                </div>
                ${renderMessageReactionBar(msg, currentUserId, false)}
            </div>
        </div>
    `;
}

/**
 * Khung chung của thẻ @bot (ảnh đại diện + tiêu đề + khung nội dung) dùng cho cả bản đang stream và bản hoàn chỉnh.
 */
function renderBotCardShell({ rootAttributes, rootClass = '', createdAt, headerExtraHtml = '', bodyHtml, footerHtml, afterCardHtml = '' }) {
    return `
        <div class="group flex items-start gap-3 ${rootClass}" ${rootAttributes}>
            <div class="mt-1 flex h-9 w-9 shrink-0 items-center justify-center rounded-xl bg-gradient-to-br from-indigo-600 via-violet-600 to-purple-600 text-white shadow-[0_0_18px_rgba(99,102,241,0.45)] ring-2 ring-indigo-100 dark:ring-indigo-500/30">
                <svg class="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2.2">
                    <path stroke-linecap="round" stroke-linejoin="round" d="M13 10V3L4 14h7v7l9-11h-7z"/>
                </svg>
            </div>
            <div class="flex-1 min-w-0 max-w-[93%]">
                <div class="flex flex-wrap items-center gap-2 mb-1.5">
                    <span class="text-xs font-extrabold text-indigo-700 dark:text-indigo-300">Trợ giảng AI (@bot)</span>
                    <span class="inline-flex items-center gap-1 rounded-full bg-indigo-50 dark:bg-indigo-500/15 border border-indigo-200/80 dark:border-indigo-400/30 px-2.5 py-0.5 text-[10px] font-bold text-indigo-700 dark:text-indigo-300">
                        <span>Trợ lý Học thuật Phòng học</span>
                    </span>
                    <span class="text-[11px] text-slate-400">${escapeHtml(formatShortTime(createdAt))}</span>
                    ${headerExtraHtml}
                </div>

                <div class="spotlight-surface relative overflow-hidden rounded-2xl rounded-tl-sm border border-indigo-200/85 dark:border-indigo-500/25 bg-white dark:bg-slate-900/95 p-4 sm:p-5 shadow-md shadow-indigo-950/[0.04]">
                    <div class="absolute inset-x-0 top-0 h-1 bg-gradient-to-r from-indigo-500 via-violet-500 to-emerald-400"></div>
                    <div data-bot-prose-body>
                        ${bodyHtml}
                    </div>
                    ${footerHtml}
                </div>
                ${afterCardHtml}
            </div>
        </div>
    `;
}

/**
 * Thẻ @bot đang Streaming: chữ thô (textContent, chống XSS) + con trỏ nhấp nháy; Markdown/KaTeX chỉ dựng 1 lần khi hoàn tất.
 * tempId do máy chủ sinh dạng "bot-<hex>" nhưng vẫn được escape trước khi gắn vào thuộc tính.
 */
export function renderStreamingBotCard({ tempId, createdAt, text = '' }) {
    return renderBotCardShell({
        rootAttributes: `data-bot-stream-id="${escapeHtml(tempId)}"`,
        rootClass: 'animate-bot-reveal',
        createdAt,
        bodyHtml: `<div class="ai-prose whitespace-pre-wrap text-sm leading-relaxed"><span data-stream-text>${escapeHtml(text)}</span><span class="bot-streaming-cursor"></span></div>`,
        footerHtml: `
            <div class="mt-4 pt-3 border-t border-slate-100 dark:border-slate-800 flex items-center gap-2 text-[11px] font-semibold text-indigo-600 dark:text-indigo-300">
                <span class="inline-flex items-center gap-0.5"><span class="typing-dot"></span><span class="typing-dot"></span><span class="typing-dot"></span></span>
                <span>Trợ giảng đang trả lời trực tiếp…</span>
            </div>
        `
    });
}

function renderBotMessageCard(msg, currentUserId, isRealtimeNew, responseStats) {
    const pinActiveClass = msg.isPinned
        ? 'border-amber-300 bg-amber-50 text-amber-800 dark:border-amber-500/40 dark:bg-amber-500/15 dark:text-amber-300'
        : 'border-slate-200 dark:border-slate-700 bg-slate-50/80 dark:bg-slate-800/80 text-slate-600 dark:text-slate-300 hover:border-indigo-300 hover:text-indigo-700';

    return renderBotCardShell({
        rootAttributes: `data-message-id="${msg.id}" data-raw-content="${escapeHtml(msg.content)}"`,
        rootClass: isRealtimeNew ? 'animate-bot-reveal' : '',
        createdAt: msg.createdAt,
        headerExtraHtml: renderResponseSpeedBadge(responseStats),
        bodyHtml: renderAcademicMarkdownHtml(msg.content),
        afterCardHtml: renderMessageReactionBar(msg, currentUserId, false),
        footerHtml: `
                    <!-- Thanh công cụ chân tin nhắn @bot: Copy câu trả lời | Ghim kiến thức | Đọc thành tiếng -->
                    <div class="mt-4 pt-3 border-t border-slate-100 dark:border-slate-800 flex flex-wrap items-center gap-2">
                        <button type="button" data-msg-action="copy-bot" data-message-id="${msg.id}"
                            class="inline-flex items-center gap-1.5 rounded-lg border border-slate-200 dark:border-slate-700 bg-slate-50/80 dark:bg-slate-800/80 px-2.5 py-1 text-[11px] font-semibold text-slate-600 dark:text-slate-300 hover:border-indigo-300 hover:text-indigo-700 transition">
                            <span>📋 Copy câu trả lời</span>
                        </button>

                        <button type="button" data-msg-action="pin" data-message-id="${msg.id}"
                            class="inline-flex items-center gap-1.5 rounded-lg border px-2.5 py-1 text-[11px] font-semibold transition ${pinActiveClass}">
                            <span>${msg.isPinned ? '📌 Đã ghim kiến thức' : '📌 Ghim kiến thức'}</span>
                        </button>

                        <button type="button" data-msg-action="speak-bot" data-message-id="${msg.id}"
                            class="inline-flex items-center gap-1.5 rounded-lg border border-slate-200 dark:border-slate-700 bg-slate-50/80 dark:bg-slate-800/80 px-2.5 py-1 text-[11px] font-semibold text-slate-600 dark:text-slate-300 hover:border-emerald-300 hover:text-emerald-700 transition">
                            <span>🔊 Đọc thành tiếng</span>
                        </button>

                        <button type="button" data-msg-action="save-to-scratchpad" data-message-id="${msg.id}"
                            class="inline-flex items-center gap-1.5 rounded-lg border border-indigo-200 dark:border-indigo-500/35 bg-indigo-50/80 dark:bg-indigo-500/15 px-2.5 py-1 text-[11px] font-bold text-indigo-700 dark:text-indigo-300 hover:bg-indigo-100 transition"
                            title="Chép gọn câu trả lời và công thức toán này sang Sổ tay ôn thi Markdown">
                            <span>📝 Lưu vào Sổ tay</span>
                        </button>
                    </div>
        `
    });
}
