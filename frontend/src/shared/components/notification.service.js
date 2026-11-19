/**
 * Dịch vụ Thông báo Đẩy Hệ điều hành Thời gian thực (Desktop & Mobile Web Notifications API):
 * - Cho phép người dùng bật/tắt thông báo bằng biểu tượng chuông trên Navbar / Workspace Header.
 * - Khi người dùng thu nhỏ App hoặc chuyển sang ứng dụng khác, tự động hiển thị thông báo gốc
 *   của Windows / macOS / Android kèm âm thanh nhẹ cho:
 *   + Trợ giảng @bot vừa hoàn tất câu trả lời
 *   + Đồng hồ Group Pomodoro kết thúc phiên tập trung 25 phút / nghỉ giải lao 5 phút
 *   + Có tin nhắn mới quan trọng hoặc tài liệu / đề thi mới trong phòng học.
 */

import { APP_CONFIG } from '../../core/config/app.config.js';
import { toast } from './toast.component.js';

const NOTIF_PREF_KEY = 'ssw_desktop_notifications_enabled';
const subscribers = new Set();

function playSoftNotificationTone() {
    try {
        const AudioCtx = window.AudioContext || window.webkitAudioContext;
        if (!AudioCtx) return;
        const ctx = new AudioCtx();
        const osc = ctx.createOscillator();
        const gain = ctx.createGain();
        osc.type = 'sine';
        osc.frequency.setValueAtTime(784, ctx.currentTime); // G5
        osc.frequency.exponentialRampToValueAtTime(1046.5, ctx.currentTime + 0.14); // C6
        gain.gain.setValueAtTime(0.0001, ctx.currentTime);
        gain.gain.exponentialRampToValueAtTime(0.08, ctx.currentTime + 0.02);
        gain.gain.exponentialRampToValueAtTime(0.0001, ctx.currentTime + 0.28);
        osc.connect(gain);
        gain.connect(ctx.destination);
        osc.start(ctx.currentTime);
        osc.stop(ctx.currentTime + 0.3);
    } catch {
        // Bỏ qua nếu trình duyệt chưa có tương tác người dùng
    }
}

export function isNotificationSupported() {
    return typeof window !== 'undefined' && 'Notification' in window;
}

export function getNotificationPermission() {
    if (!isNotificationSupported()) return 'unsupported';
    return window.Notification.permission;
}

export function isNotificationsEnabled() {
    if (!isNotificationSupported()) return false;
    if (window.Notification.permission !== 'granted') return false;
    return localStorage.getItem(NOTIF_PREF_KEY) !== 'false';
}

function emitStateChanged() {
    const enabled = isNotificationsEnabled();
    subscribers.forEach((fn) => {
        try {
            fn(enabled);
        } catch {
            // Bỏ qua lỗi callback
        }
    });
}

export function onNotificationStateChange(callback) {
    subscribers.add(callback);
    return () => subscribers.delete(callback);
}

/**
 * Bật / tắt thông báo đẩy và xin quyền từ hệ điều hành nếu chưa cấp.
 */
export async function toggleDesktopNotifications() {
    if (!isNotificationSupported()) {
        toast.warning('Trình duyệt hiện tại chưa hỗ trợ Web Notifications API.');
        return false;
    }

    if (window.Notification.permission === 'denied') {
        toast.warning('Bạn đã chặn quyền Thông báo của trình duyệt. Hãy bật lại trong biểu tượng ổ khóa trên thanh địa chỉ.');
        return false;
    }

    if (window.Notification.permission === 'default') {
        const permission = await window.Notification.requestPermission();
        if (permission !== 'granted') {
            toast.info('Chưa bật thông báo đẩy hệ điều hành.');
            emitStateChanged();
            return false;
        }
        localStorage.setItem(NOTIF_PREF_KEY, 'true');
        emitStateChanged();
        toast.success('Đã bật Thông báo Đẩy Thời gian thực cho Smart Study Workspace!');
        sendDesktopNotification({
            title: 'Smart Study Workspace',
            body: 'Thông báo đẩy đã sẵn sàng! Bạn sẽ nhận tin khi @bot trả lời hoặc Pomodoro kết thúc.',
            tag: 'ssw-notif-welcome',
            force: true,
            withSound: true
        });
        return true;
    }

    const currentlyEnabled = isNotificationsEnabled();
    localStorage.setItem(NOTIF_PREF_KEY, currentlyEnabled ? 'false' : 'true');
    emitStateChanged();

    if (currentlyEnabled) {
        toast.info('Đã tạm tắt Thông báo Đẩy trên thiết bị này.');
    } else {
        toast.success('Đã bật lại Thông báo Đẩy Thời gian thực!');
        playSoftNotificationTone();
    }
    return !currentlyEnabled;
}

/**
 * Phát thông báo hệ điều hành (mặc định chỉ bắn khi App đang ở nền / mất focus, trừ khi force = true).
 */
export function sendDesktopNotification({
    title,
    body,
    tag = 'smartstudy-event',
    force = false,
    withSound = true,
    onClick = null
}) {
    if (!isNotificationsEnabled()) {
        return false;
    }

    const isBackground = document.hidden || !document.hasFocus();
    if (!force && !isBackground) {
        return false;
    }

    try {
        if (withSound) {
            playSoftNotificationTone();
        }

        const iconUrl = `${APP_CONFIG.FRONTEND_ROOT}/src/assets/icons/icon-192.png`;
        const cleanBody = String(body || '')
            .replace(/[#*`>_~]/g, '')
            .replace(/\s+/g, ' ')
            .trim()
            .slice(0, 160);

        const notif = new window.Notification(title || 'Smart Study Workspace', {
            body: cleanBody,
            icon: iconUrl,
            badge: iconUrl,
            tag,
            renotify: true
        });

        notif.onclick = () => {
            window.focus();
            notif.close();
            if (typeof onClick === 'function') {
                onClick();
            }
        };

        window.setTimeout(() => notif.close(), 7000);
        return true;
    } catch (err) {
        console.warn('[NotificationService] Không thể hiển thị thông báo gốc:', err);
        return false;
    }
}

/**
 * Tạo chuỗi HTML nút chuông Bật/Tắt Thông báo đồng bộ cho Navbar và Workspace Header.
 */
export function renderNotificationBellMarkup() {
    const enabled = isNotificationsEnabled();
    const denied = getNotificationPermission() === 'denied';

    const title = denied
        ? 'Thông báo đang bị trình duyệt chặn (bấm để xem hướng dẫn)'
        : enabled
            ? 'Đang bật Thông báo Đẩy (@bot, Pomodoro, Đề thi mới) — Bấm để tắt'
            : 'Bật Thông báo Đẩy Thời gian thực cho Desktop & Mobile';

    const btnClass = enabled
        ? 'relative flex h-9 w-9 items-center justify-center rounded-xl border border-indigo-300 dark:border-indigo-500/40 bg-indigo-50 dark:bg-indigo-500/20 text-indigo-600 dark:text-indigo-300 hover:bg-indigo-100 transition'
        : 'relative flex h-9 w-9 items-center justify-center rounded-xl border border-slate-200 dark:border-slate-800 bg-white dark:bg-slate-900 text-slate-500 dark:text-slate-400 hover:border-indigo-300 hover:text-indigo-600 transition';

    return `
        <button type="button" data-action="toggle-desktop-notifications" class="${btnClass}" title="${title}" aria-label="${title}">
            <svg class="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
                ${enabled
                    ? '<path stroke-linecap="round" stroke-linejoin="round" d="M15 17h5l-1.405-1.405A2.032 2.032 0 0118 14.158V11a6.002 6.002 0 00-4-5.659V5a2 2 0 10-4 0v.341C7.67 6.165 6 8.388 6 11v3.159c0 .538-.214 1.055-.595 1.436L4 17h5m6 0v1a3 3 0 11-6 0v-1m6 0H9"/>'
                    : '<path stroke-linecap="round" stroke-linejoin="round" d="M15 17h5l-1.405-1.405A2.032 2.032 0 0118 14.158V11a6.002 6.002 0 00-4-5.659V5a2 2 0 10-4 0v.341C7.67 6.165 6 8.388 6 11v3.159c0 .538-.214 1.055-.595 1.436L4 17h5m6 0v1a3 3 0 11-6 0v-1m6 0H9M3 3l18 18"/>'}
            </svg>
            ${enabled ? '<span class="absolute top-1.5 right-1.5 h-2 w-2 rounded-full bg-emerald-500 ring-2 ring-white dark:ring-slate-900"></span>' : ''}
        </button>
    `;
}
