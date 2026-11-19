/**
 * Component giám sát trạng thái kết nối máy chủ thực tế (Server Health Monitor):
 * - Hiển thị chấm trạng thái tinh tế trên Navbar & Workspace Header:
 *   + Xanh lá (Online • VPS Connected / Local Connected)
 *   + Hổ phách (Offline Mode)
 * - Khi mất kết nối máy chủ: hiển thị banner Offline Study Mode dịu mắt (không hiện trang trắng lỗi)
 *   giúp sinh viên tiếp tục ôn Flashcards 3D, đọc lời giải Quiz và tóm tắt PDF đã lưu trong IndexedDB.
 * - Khi máy chủ online trở lại: tự động đồng bộ hàng đợi Flashcard Sync Queue & kết nối lại SignalR!
 */

import { APP_CONFIG } from '../../core/config/app.config.js';
import { httpClient } from '../../core/http/httpClient.js';
import { offlineDb } from '../../core/storage/offlineDb.service.js';
import { toast } from './toast.component.js';

const HEALTH_POLL_INTERVAL_MS = 25_000;
const HEALTH_TIMEOUT_MS = 4_000;
const BANNER_ID = 'ssw-offline-study-banner';

let isInitialized = false;
let isServerOnline = navigator.onLine;
let pollTimerId = null;
const listeners = new Set();

function resolveOnlineLabel() {
    const host = window.location.hostname.toLowerCase();
    const isLocal = host === 'localhost' || host === '127.0.0.1';
    return isLocal ? 'Online • Connected' : 'Online • VPS Connected';
}

function renderStatusBadgeMarkup(online) {
    if (online) {
        const label = resolveOnlineLabel();
        return `
            <span class="inline-flex items-center gap-1.5 rounded-full border border-emerald-200/90 dark:border-emerald-500/30 bg-emerald-50/90 dark:bg-emerald-500/15 px-2.5 py-1 text-[11px] font-semibold text-emerald-700 dark:text-emerald-300 shadow-xs"
                title="Máy chủ đang hoạt động ổn định (${label})">
                <span class="relative flex h-2 w-2">
                    <span class="absolute inline-flex h-full w-full animate-ping rounded-full bg-emerald-400 opacity-75"></span>
                    <span class="relative inline-flex h-2 w-2 rounded-full bg-emerald-500"></span>
                </span>
                <span class="hidden xl:inline">${label}</span>
                <span class="xl:hidden">Online</span>
            </span>
        `;
    }

    return `
        <span class="inline-flex items-center gap-1.5 rounded-full border border-amber-300/90 dark:border-amber-500/40 bg-amber-50/95 dark:bg-amber-500/20 px-2.5 py-1 text-[11px] font-bold text-amber-800 dark:text-amber-200 shadow-xs"
            title="Mất kết nối máy chủ — Đang dùng dữ liệu học tập đã lưu trong IndexedDB">
            <span class="h-2 w-2 rounded-full bg-amber-500 animate-pulse"></span>
            <span>Offline Mode</span>
        </span>
    `;
}

function syncOfflineBanner(online) {
    const existing = document.getElementById(BANNER_ID);

    if (online) {
        existing?.remove();
        return;
    }

    if (existing) {
        return;
    }

    const banner = document.createElement('div');
    banner.id = BANNER_ID;
    banner.setAttribute('role', 'status');
    banner.className = 'sticky top-0 z-40 border-b border-amber-300/80 dark:border-amber-500/35 bg-gradient-to-r from-amber-50 via-orange-50 to-amber-50 dark:from-amber-950/90 dark:via-slate-900/95 dark:to-amber-950/90 px-4 py-2 text-center text-xs font-medium text-amber-900 dark:text-amber-200 backdrop-blur-md shadow-xs';
    banner.innerHTML = `
        <div class="mx-auto flex max-w-6xl flex-wrap items-center justify-center gap-2">
            <span class="inline-flex h-2 w-2 rounded-full bg-amber-500 animate-pulse"></span>
            <span>
                <strong>Bạn đang ở chế độ Ngoại tuyến</strong> — Vẫn có thể ôn tập Flashcards 3D, xem lại Lời giải Quiz, Sổ tay Ghi chú và Tóm tắt bài giảng đã lưu.
            </span>
            <button type="button" data-action="retry-server-health"
                class="rounded-lg border border-amber-400/70 bg-white/80 dark:bg-slate-800 px-2.5 py-0.5 text-[11px] font-bold text-amber-900 dark:text-amber-200 hover:bg-white transition">
                Thử kết nối lại
            </button>
        </div>
    `;

    banner.querySelector('[data-action="retry-server-health"]')?.addEventListener('click', () => {
        checkServerHealthNow();
    });

    document.body.prepend(banner);
}

/**
 * Gắn / cập nhật lại chấm trạng thái máy chủ vào mọi slot #server-health-slot trên trang.
 */
export function mountServerHealthBadge() {
    document.querySelectorAll('[data-server-health-slot], #server-health-slot').forEach((slot) => {
        slot.innerHTML = renderStatusBadgeMarkup(isServerOnline);
    });
}

async function flushQueuedOfflineMutations() {
    try {
        const result = await offlineDb.flushSyncQueue({
            FLASHCARD_MASTERY: async ({ cardId, masteryLevel }) => {
                await httpClient.patch(`/flashcards/${cardId}/mastery`, { masteryLevel });
            }
        });
        if (result.syncedCount > 0) {
            toast.success(`Đã tự động đồng bộ ${result.syncedCount} tiến độ Flashcard ngoại tuyến lên máy chủ!`);
        }
    } catch {
        // Sẽ tự thử lại trong chu kỳ kế tiếp
    }
}

function setOnlineStatus(nextOnline) {
    const changed = isServerOnline !== nextOnline;
    isServerOnline = nextOnline;

    mountServerHealthBadge();
    syncOfflineBanner(nextOnline);

    if (changed) {
        listeners.forEach((cb) => {
            try {
                cb(nextOnline);
            } catch {
                // Bỏ qua lỗi từ subscriber
            }
        });

        if (nextOnline) {
            toast.success('Đã khôi phục kết nối máy chủ — Đang đồng bộ dữ liệu học tập!', 2500);
            flushQueuedOfflineMutations();
        }
    }
}

export async function checkServerHealthNow() {
    if (!navigator.onLine) {
        setOnlineStatus(false);
        return false;
    }

    const controller = new AbortController();
    const timeoutId = window.setTimeout(() => controller.abort(), HEALTH_TIMEOUT_MS);

    try {
        const res = await fetch(`${APP_CONFIG.API_BASE_URL}/health?_t=${Date.now()}`, {
            method: 'GET',
            cache: 'no-store',
            signal: controller.signal
        });
        window.clearTimeout(timeoutId);
        const healthy = res.ok;
        setOnlineStatus(healthy);
        return healthy;
    } catch {
        window.clearTimeout(timeoutId);
        setOnlineStatus(false);
        return false;
    }
}

export function getServerOnlineStatus() {
    return isServerOnline;
}

export function onServerStatusChange(callback) {
    listeners.add(callback);
    return () => listeners.delete(callback);
}

/**
 * Khởi động dịch vụ giám sát sức khỏe máy chủ (an toàn khi gọi lặp lại).
 */
export function initServerHealthMonitor() {
    mountServerHealthBadge();

    if (isInitialized) {
        return;
    }
    isInitialized = true;

    window.addEventListener('online', () => {
        checkServerHealthNow();
    });
    window.addEventListener('offline', () => {
        setOnlineStatus(false);
    });

    document.addEventListener('visibilitychange', () => {
        if (document.visibilityState === 'visible') {
            checkServerHealthNow();
        }
    });

    // Kiểm tra ban đầu và đồng bộ hàng đợi ngoại tuyến nếu đã có sẵn
    checkServerHealthNow().then((online) => {
        if (online) {
            flushQueuedOfflineMutations();
        }
    });

    pollTimerId = window.setInterval(() => {
        if (document.visibilityState === 'visible') {
            checkServerHealthNow();
        }
    }, HEALTH_POLL_INTERVAL_MS);
}

export function stopServerHealthMonitor() {
    if (pollTimerId) {
        clearInterval(pollTimerId);
        pollTimerId = null;
    }
}
