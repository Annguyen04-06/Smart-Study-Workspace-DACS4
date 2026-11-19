/**
 * Command Palette Spotlight (Ctrl + K / Cmd + K) dùng chung trên toàn hệ thống.
 * Cho phép tìm và nhảy nhanh vào bất kỳ Phòng học VKU nào, đổi giao diện Sáng/Tối,
 * mở nhanh Tab Học liệu / Quiz / Flashcard / Bảng xếp hạng hoặc đặt câu hỏi cho @bot.
 */

import { ROUTES, buildWorkspaceUrl } from '../../core/config/app.config.js';
import { appStore } from '../../core/state/appStore.js';
import { roomsApi } from '../../features/dashboard/rooms.api.js';
import { escapeHtml, qs } from '../utils/dom.utils.js';
import { getCurrentTheme, toggleTheme } from '../utils/theme.utils.js';

const PALETTE_ID = 'ssw-command-palette';
let isInitialized = false;
let extraCommandsProvider = null;
let cachedRooms = [];
let activeIndex = 0;
let filteredItems = [];

export function registerWorkspacePaletteActions(providerFn) {
    extraCommandsProvider = providerFn;
}

export function initCommandPalette() {
    if (isInitialized) return;
    isInitialized = true;

    const backdrop = document.createElement('div');
    backdrop.id = PALETTE_ID;
    backdrop.className = 'ss-modal-backdrop hidden';
    backdrop.innerHTML = `
        <div class="saas-card w-full max-w-xl overflow-hidden shadow-2xl animate-slide-fade border border-slate-200 dark:border-slate-700">
            <div class="flex items-center gap-3 border-b border-slate-200 dark:border-slate-800 px-4 py-3.5">
                <svg class="w-4 h-4 text-indigo-500 shrink-0" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2.2">
                    <path stroke-linecap="round" stroke-linejoin="round" d="M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0z"/>
                </svg>
                <input id="cmd-palette-input" type="text" autocomplete="off"
                    placeholder="Gõ tên phòng học (NET101, DSA202...), lệnh Sáng/Tối, Quiz, @bot..."
                    class="flex-1 bg-transparent text-sm text-slate-900 dark:text-slate-100 placeholder-slate-400 focus:outline-none" />
                <kbd class="rounded-md border border-slate-200 dark:border-slate-700 bg-slate-100 dark:bg-slate-800 px-2 py-0.5 font-mono text-[10px] font-semibold text-slate-500">ESC</kbd>
            </div>
            <div id="cmd-palette-list" class="max-h-80 overflow-y-auto thin-scrollbar p-2 space-y-1"></div>
            <div class="flex items-center justify-between border-t border-slate-100 dark:border-slate-800 bg-slate-50/80 dark:bg-slate-900/60 px-4 py-2 text-[11px] text-slate-400">
                <span>Dùng phím <strong class="font-mono">↑ ↓</strong> để chọn, <strong class="font-mono">Enter</strong> để thực thi</span>
                <span class="font-mono text-indigo-500">Smart Study Spotlight</span>
            </div>
        </div>
    `;
    document.body.appendChild(backdrop);

    const inputEl = qs('#cmd-palette-input', backdrop);
    const listEl = qs('#cmd-palette-list', backdrop);

    const close = () => {
        backdrop.classList.add('hidden');
        document.body.classList.remove('overflow-hidden');
    };

    backdrop.addEventListener('click', (e) => {
        if (e.target === backdrop) close();
        const itemBtn = e.target.closest('[data-cmd-idx]');
        if (itemBtn) {
            const idx = Number(itemBtn.dataset.cmdIdx);
            const chosen = filteredItems[idx];
            if (chosen) {
                close();
                chosen.run();
            }
        }
    });

    inputEl?.addEventListener('input', () => {
        activeIndex = 0;
        renderItems(inputEl.value.trim(), listEl);
    });

    inputEl?.addEventListener('keydown', (e) => {
        if (e.key === 'ArrowDown') {
            e.preventDefault();
            if (filteredItems.length > 0) {
                activeIndex = (activeIndex + 1) % filteredItems.length;
                renderItems(inputEl.value.trim(), listEl);
            }
        } else if (e.key === 'ArrowUp') {
            e.preventDefault();
            if (filteredItems.length > 0) {
                activeIndex = (activeIndex - 1 + filteredItems.length) % filteredItems.length;
                renderItems(inputEl.value.trim(), listEl);
            }
        } else if (e.key === 'Enter') {
            e.preventDefault();
            const chosen = filteredItems[activeIndex];
            if (chosen) {
                close();
                chosen.run();
            }
        } else if (e.key === 'Escape') {
            close();
        }
    });

    // Bắt phím tắt toàn cục Ctrl + K / Cmd + K
    document.addEventListener('keydown', (event) => {
        if ((event.ctrlKey || event.metaKey) && event.key.toLowerCase() === 'k') {
            event.preventDefault();
            if (backdrop.classList.contains('hidden')) {
                openCommandPalette();
            } else {
                close();
            }
        }
    });
}

function buildAllCommands() {
    const { user } = appStore.getState();
    const isAdmin = String(user?.role || '').toLowerCase() === 'admin';
    const isDark = getCurrentTheme() === 'dark';

    const commands = [
        {
            group: 'Giao diện & Điều hướng',
            title: isDark ? 'Chuyển sang Giao diện Sáng (Light Mode)' : 'Chuyển sang Giao diện Tối (Dark Mode)',
            subtitle: 'Đổi bảng màu hiển thị toàn trang',
            badge: 'Theme',
            run: () => toggleTheme()
        },
        {
            group: 'Giao diện & Điều hướng',
            title: 'Mở Sảnh Tất cả Phòng học',
            subtitle: 'Tìm kiếm, lọc môn học hoặc tham gia bằng mã mời',
            badge: 'Phòng học',
            run: () => { window.location.href = ROUTES.STUDENT_DASHBOARD; }
        },
        {
            group: 'Giao diện & Điều hướng',
            title: 'Mở Hồ sơ Cá nhân & Lịch sử Thi',
            subtitle: 'Xem điểm trung bình và đối chiếu lời giải chi tiết',
            badge: 'Hồ sơ',
            run: () => { window.location.href = ROUTES.STUDENT_PROFILE; }
        }
    ];

    if (isAdmin) {
        commands.push({
            group: 'Giao diện & Điều hướng',
            title: 'Mở Cổng Quản trị Hệ thống',
            subtitle: 'Quản lý sinh viên, phê duyệt phòng học & kho học liệu',
            badge: 'Quản trị',
            run: () => { window.location.href = ROUTES.ADMIN_DASHBOARD; }
        });
    }

    if (typeof extraCommandsProvider === 'function') {
        const wsCommands = extraCommandsProvider() || [];
        commands.unshift(...wsCommands);
    }

    for (const room of cachedRooms) {
        commands.push({
            group: 'Nhảy nhanh vào Phòng học',
            title: `${room.name}`,
            subtitle: `${room.subjectCategory} • Mã #${room.inviteCode} • ${room.memberCount} thành viên`,
            badge: `#${room.inviteCode}`,
            run: () => { window.location.href = buildWorkspaceUrl(room.id); }
        });
    }

    return commands;
}

function renderItems(query, listEl) {
    if (!listEl) return;
    const all = buildAllCommands();
    const q = query.toLowerCase();

    filteredItems = q
        ? all.filter(
              (c) =>
                  c.title.toLowerCase().includes(q) ||
                  c.subtitle.toLowerCase().includes(q) ||
                  c.badge.toLowerCase().includes(q)
          )
        : all;

    if (filteredItems.length === 0) {
        listEl.innerHTML = `<p class="py-8 text-center text-xs text-slate-400">Không tìm thấy lệnh hoặc phòng học khớp với "${escapeHtml(query)}".</p>`;
        return;
    }

    if (activeIndex >= filteredItems.length) {
        activeIndex = 0;
    }

    listEl.innerHTML = filteredItems
        .map((item, idx) => {
            const active = idx === activeIndex;
            return `
                <button type="button" data-cmd-idx="${idx}"
                    class="flex w-full items-center justify-between gap-3 rounded-xl px-3.5 py-2.5 text-left transition ${
                        active
                            ? 'bg-indigo-600 text-white shadow-sm'
                            : 'text-slate-800 dark:text-slate-200 hover:bg-slate-100 dark:hover:bg-slate-800/70'
                    }">
                    <div class="min-w-0">
                        <p class="truncate text-xs font-bold">${escapeHtml(item.title)}</p>
                        <p class="truncate text-[11px] ${active ? 'text-indigo-100' : 'text-slate-400'}">${escapeHtml(item.subtitle)}</p>
                    </div>
                    <span class="shrink-0 rounded-md px-2 py-0.5 font-mono text-[10px] font-semibold ${
                        active ? 'bg-white/20 text-white' : 'bg-slate-100 dark:bg-slate-800 text-indigo-600 dark:text-indigo-300'
                    }">${escapeHtml(item.badge)}</span>
                </button>
            `;
        })
        .join('');
}

export async function openCommandPalette() {
    initCommandPalette();
    const backdrop = document.getElementById(PALETTE_ID);
    const inputEl = qs('#cmd-palette-input', backdrop);
    const listEl = qs('#cmd-palette-list', backdrop);
    if (!backdrop) return;

    backdrop.classList.remove('hidden');
    document.body.classList.add('overflow-hidden');
    if (inputEl) {
        inputEl.value = '';
        inputEl.focus();
    }
    activeIndex = 0;
    renderItems('', listEl);

    try {
        const paged = await roomsApi.getRooms({ pageSize: 30 });
        cachedRooms = paged?.items || [];
        renderItems(inputEl?.value.trim() || '', listEl);
    } catch {
        // Giữ danh sách lệnh cơ bản nếu chưa đăng nhập
    }
}
