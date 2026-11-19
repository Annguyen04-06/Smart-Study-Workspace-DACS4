/**
 * Thanh điều hướng Header Kính mờ đồng bộ giữa Dashboard, Profile và Admin,
 * tích hợp: Command Palette (Ctrl + K), Dark/Light Theme Toggle, nút "Tải App" PWA,
 * nút "Mở trên Điện thoại (QR HTTPS)" trên Laptop và thanh Bottom Navigation dạng Native App trên điện thoại (< 768px).
 */

import { ROUTES, resolveBackendFileUrl } from '../../core/config/app.config.js';
import { appStore } from '../../core/state/appStore.js';
import { initOtaAutoSync } from '../../core/sync/otaUpdater.service.js';
import { escapeHtml, getInitials } from '../utils/dom.utils.js';
import {
    getCurrentTheme,
    initTheme,
    onThemeChanged,
    toggleTheme
} from '../utils/theme.utils.js';
import { initCommandPalette, openCommandPalette } from './commandPalette.component.js';
import {
    onNotificationStateChange,
    renderNotificationBellMarkup,
    toggleDesktopNotifications
} from './notification.service.js';
import { mountMobileQrButton } from './mobileAccessQr.component.js';
import { initPwaInstaller, mountPwaInstallButton } from './pwaInstaller.component.js';
import {
    initServerHealthMonitor,
    mountServerHealthBadge
} from './serverHealth.component.js';

const MOBILE_NAV_ICONS = {
    rooms: '<svg fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2"><path stroke-linecap="round" stroke-linejoin="round" d="M3 12l2-2m0 0l7-7 7 7M5 10v10a1 1 0 001 1h3m10-11l2 2m-2-2v10a1 1 0 01-1 1h-3m-6 0a1 1 0 001-1v-4a1 1 0 011-1h2a1 1 0 011 1v4a1 1 0 001 1m-6 0h6"/></svg>',
    admin: '<svg fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2"><path stroke-linecap="round" stroke-linejoin="round" d="M9 12l2 2 4-4m5.618-4.016A11.955 11.955 0 0112 2.944a11.955 11.955 0 01-8.618 3.04A12.02 12.02 0 003 9c0 5.591 3.824 10.29 9 11.622 5.176-1.332 9-6.03 9-11.622 0-1.042-.133-2.052-.382-3.016z"/></svg>',
    search: '<svg fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2"><path stroke-linecap="round" stroke-linejoin="round" d="M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0z"/></svg>',
    profile: '<svg fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2"><path stroke-linecap="round" stroke-linejoin="round" d="M16 7a4 4 0 11-8 0 4 4 0 018 0zM12 14a7 7 0 00-7 7h14a7 7 0 00-7-7z"/></svg>',
    logout: '<svg fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2"><path stroke-linecap="round" stroke-linejoin="round" d="M17 16l4-4m0 0l-4-4m4 4H7m6 4v1a3 3 0 01-3 3H6a3 3 0 01-3-3V7a3 3 0 013-3h4a3 3 0 013 3v1"/></svg>'
};

/**
 * Thanh Bottom Navigation trên điện thoại cho các trang Dashboard / Hồ sơ / Quản trị.
 */
function renderMobileAppNav(activePage, isAdmin) {
    const items = [
        { key: 'dashboard', label: 'Phòng học', icon: MOBILE_NAV_ICONS.rooms, href: ROUTES.STUDENT_DASHBOARD },
        ...(isAdmin ? [{ key: 'admin', label: 'Quản trị', icon: MOBILE_NAV_ICONS.admin, href: ROUTES.ADMIN_DASHBOARD }] : []),
        { key: 'search', label: 'Tìm nhanh', icon: MOBILE_NAV_ICONS.search, action: 'navbar-open-palette' },
        { key: 'profile', label: 'Hồ sơ', icon: MOBILE_NAV_ICONS.profile, href: ROUTES.STUDENT_PROFILE },
        { key: 'logout', label: 'Đăng xuất', icon: MOBILE_NAV_ICONS.logout, action: 'navbar-logout' }
    ];

    const itemsHtml = items.map((item) => {
        const activeClass = item.key === activePage ? ' active' : '';
        const inner = `
            <span class="mobile-nav-icon-wrap">${item.icon}</span>
            <span class="mobile-nav-label">${item.label}</span>
        `;
        return item.href
            ? `<a href="${item.href}" class="mobile-nav-item${activeClass}"${activeClass ? ' aria-current="page"' : ''}>${inner}</a>`
            : `<button type="button" data-action="${item.action}" class="mobile-nav-item">${inner}</button>`;
    }).join('');

    return `
        <nav class="mobile-bottom-nav hidden" style="--mobile-nav-cols: ${items.length}" aria-label="Điều hướng ứng dụng">
            ${itemsHtml}
        </nav>
    `;
}

export function mountNavbar(containerSelector = '#app-navbar', activePage = 'dashboard') {
    initTheme();
    initCommandPalette();
    initPwaInstaller();
    initOtaAutoSync();
    initServerHealthMonitor();

    const container = document.querySelector(containerSelector);
    if (!container) {
        return;
    }
    document.body.classList.add('has-mobile-bottom-nav');

    const render = () => {
        const { user } = appStore.getState();
        if (!user) {
            container.innerHTML = '';
            return;
        }

        const isAdmin = String(user.role).toLowerCase() === 'admin';
        const isDark = getCurrentTheme() === 'dark';

        const avatarHtml = user.avatarUrl
            ? `<img src="${escapeHtml(resolveBackendFileUrl(user.avatarUrl))}" alt="Avatar" class="h-8 w-8 rounded-full object-cover ring-2 ring-indigo-100 dark:ring-indigo-500/30" />`
            : `<span class="flex h-8 w-8 items-center justify-center rounded-full bg-gradient-to-br from-indigo-600 to-violet-600 text-xs font-bold text-white shadow-xs">${escapeHtml(getInitials(user.fullName))}</span>`;

        const navItemClass = (key) =>
            activePage === key
                ? 'rounded-xl bg-indigo-50 dark:bg-indigo-500/20 px-3.5 py-2 text-xs font-bold text-indigo-700 dark:text-indigo-300 ring-1 ring-indigo-200/70 dark:ring-indigo-400/30'
                : 'rounded-xl px-3.5 py-2 text-xs font-semibold text-slate-600 dark:text-slate-300 hover:bg-slate-100/90 dark:hover:bg-white/5 hover:text-slate-900 dark:hover:text-white transition';

        container.innerHTML = `
            <header class="pt-safe sticky top-0 z-30 border-b border-slate-200/80 dark:border-slate-800/80 bg-white/85 dark:bg-slate-950/85 backdrop-blur-xl">
                <div class="mx-auto flex h-16 max-w-7xl items-center justify-between px-4 sm:px-6 lg:px-8 gap-3">
                    <div class="flex items-center gap-5">
                        <a href="${isAdmin ? ROUTES.ADMIN_DASHBOARD : ROUTES.STUDENT_DASHBOARD}" class="flex items-center gap-2.5 group">
                            <span class="flex h-9 w-9 items-center justify-center rounded-xl btn-primary-glow">
                                <svg class="w-5 h-5" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2.1">
                                    <path stroke-linecap="round" stroke-linejoin="round" d="M12 6.253v13m0-13C10.832 5.477 9.246 5 7.5 5S4.168 5.477 3 6.253v13C4.168 18.477 5.754 18 7.5 18s3.332.477 4.5 1.253m0-13C13.168 5.477 14.754 5 16.5 5c1.747 0 3.332.477 4.5 1.253v13C19.832 18.477 18.247 18 16.5 18c-1.746 0-3.332.477-4.5 1.253"/>
                                </svg>
                            </span>
                            <div>
                                <span class="text-base font-extrabold tracking-tight text-slate-900 dark:text-white group-hover:text-indigo-600 transition">Smart Study</span>
                                <span class="ml-1.5 hidden sm:inline-block rounded-md bg-indigo-50 dark:bg-indigo-500/20 border border-indigo-200/60 dark:border-indigo-400/30 px-1.5 py-0.5 text-[10px] font-bold uppercase tracking-wide text-indigo-600 dark:text-indigo-300">VKU</span>
                            </div>
                        </a>

                        <nav class="hidden md:flex items-center gap-1.5">
                            <a href="${ROUTES.STUDENT_DASHBOARD}" class="${navItemClass('dashboard')}">Không gian Phòng học</a>
                            <a href="${ROUTES.STUDENT_PROFILE}" class="${navItemClass('profile')}">Hồ sơ &amp; Lịch sử Thi</a>
                            ${isAdmin ? `<a href="${ROUTES.ADMIN_DASHBOARD}" class="${navItemClass('admin')}">Quản trị Hệ thống</a>` : ''}
                        </nav>
                    </div>

                    <div class="flex items-center gap-2">
                        <!-- Chấm trạng thái kết nối máy chủ (Online • VPS Connected / Offline Mode) -->
                        <div id="server-health-slot" data-server-health-slot class="hidden sm:inline-flex items-center"></div>

                        <!-- Khu vực gắn nút Cài đặt PWA nếu đủ điều kiện -->
                        <div id="pwa-install-container" class="inline-flex items-center"></div>

                        <!-- Nút "Mở trên Điện thoại (QR HTTPS)" chỉ hiện trên Laptop >= 768px -->
                        <div data-mobile-qr-slot class="hidden md:inline-flex items-center"></div>

                        <!-- Nút bật/tắt Thông báo Đẩy Hệ điều hành -->
                        ${renderNotificationBellMarkup()}

                        <!-- Nút mở nhanh Spotlight Command Palette (Ctrl + K) -->
                        <button type="button" data-action="navbar-open-palette"
                            class="hidden sm:inline-flex items-center gap-2 rounded-xl border border-slate-200 dark:border-slate-800 bg-slate-50/90 dark:bg-slate-900 px-3 py-1.5 text-xs text-slate-500 dark:text-slate-400 hover:border-indigo-300 transition"
                            title="Tìm kiếm nhanh phòng học & điều hướng (Ctrl + K)">
                            <svg class="w-3.5 h-3.5" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2"><path stroke-linecap="round" stroke-linejoin="round" d="M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0z"/></svg>
                            <span class="hidden lg:inline">Tìm nhanh...</span>
                            <kbd class="rounded border border-slate-300 dark:border-slate-700 bg-white dark:bg-slate-800 px-1.5 py-0.2 font-mono text-[10px] font-semibold">Ctrl K</kbd>
                        </button>

                        <!-- Nút chuyển đổi Sáng / Tối -->
                        <button type="button" data-action="navbar-toggle-theme"
                            class="flex h-9 w-9 items-center justify-center rounded-xl border border-slate-200 dark:border-slate-800 bg-white dark:bg-slate-900 text-slate-600 dark:text-amber-300 hover:border-indigo-300 transition"
                            title="${isDark ? 'Chuyển sang Giao diện Sáng' : 'Chuyển sang Giao diện Tối'}">
                            ${isDark
                                ? `<svg class="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2"><path stroke-linecap="round" stroke-linejoin="round" d="M12 3v1m0 16v1m9-9h-1M4 12H3m15.364 6.364l-.707-.707M6.343 6.343l-.707-.707m12.728 0l-.707.707M6.343 17.657l-.707.707M16 12a4 4 0 11-8 0 4 4 0 018 0z"/></svg>`
                                : `<svg class="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2"><path stroke-linecap="round" stroke-linejoin="round" d="M20.354 15.354A9 9 0 018.646 3.646 9.003 9.003 0 0012 21a9.003 9.003 0 008.354-5.646z"/></svg>`}
                        </button>

                        <a href="${ROUTES.STUDENT_PROFILE}" class="flex items-center gap-2.5 rounded-xl border border-slate-200/90 dark:border-slate-800 bg-white dark:bg-slate-900 px-2.5 py-1.5 shadow-xs hover:border-indigo-300 transition">
                            ${avatarHtml}
                            <div class="hidden xl:block text-left leading-tight">
                                <p class="text-xs font-bold text-slate-900 dark:text-white max-w-[130px] truncate">${escapeHtml(user.fullName)}</p>
                                <p class="text-[11px] text-slate-500 dark:text-slate-400">${isAdmin ? 'Quản trị viên' : escapeHtml(user.studentCode || 'Sinh viên')}</p>
                            </div>
                        </a>

                        <button type="button" data-action="navbar-logout"
                            class="hidden md:inline-flex items-center gap-1.5 rounded-xl border border-slate-200 dark:border-slate-800 bg-white dark:bg-slate-900 px-3 py-2 text-xs font-semibold text-slate-600 dark:text-slate-300 hover:border-rose-200 hover:bg-rose-50 hover:text-rose-600 transition"
                            title="Đăng xuất">
                            <svg class="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
                                <path stroke-linecap="round" stroke-linejoin="round" d="M17 16l4-4m0 0l-4-4m4 4H7m6 4v1a3 3 0 01-3 3H6a3 3 0 01-3-3V7a3 3 0 013-3h4a3 3 0 013 3v1"/>
                            </svg>
                        </button>
                    </div>
                </div>
            </header>
            ${renderMobileAppNav(activePage, isAdmin)}
        `;

        // Header vừa render lại toàn bộ innerHTML nên cần gắn lại nút "Tải App", nút QR & trạng thái máy chủ
        mountPwaInstallButton();
        mountMobileQrButton();
        mountServerHealthBadge();
    };

    render();
    appStore.subscribe(render);
    onThemeChanged(render);
    onNotificationStateChange(render);

    container.addEventListener('click', (event) => {
        if (event.target.closest('[data-action="toggle-desktop-notifications"]')) {
            toggleDesktopNotifications();
            return;
        }
        if (event.target.closest('[data-action="navbar-open-palette"]')) {
            openCommandPalette();
            return;
        }
        if (event.target.closest('[data-action="navbar-toggle-theme"]')) {
            toggleTheme();
            return;
        }
        if (event.target.closest('[data-action="navbar-logout"]')) {
            appStore.clearAuthSession();
            window.location.href = ROUTES.LOGIN;
        }
    });
}
