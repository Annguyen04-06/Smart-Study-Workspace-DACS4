/**
 * Bộ đồng bộ phiên bản tự động phía Client (OTA Auto-Sync Engine):
 * - Tự động kiểm tra GET /api/system/version?_t=... khi mở App, khi chuyển tab quay lại (visibilitychange),
 *   khi vừa có mạng trở lại (online) và định kỳ mỗi 60 giây.
 * - Đồng thời gọi registration.update() trên Service Worker và lắng nghe controllerchange.
 * - Khi máy chủ có bản build/update mới: hiện banner thông báo mượt mà góc trên màn hình,
 *   xóa sạch toàn bộ Cache API, cập nhật smartstudy_app_version và reload tự động sau 1.2 giây.
 * - Không làm mất dữ liệu: nếu người dùng đang bận (đang thi Quiz có [data-ota-guard], đang soạn dở
 *   tin nhắn/ô nhập) thì hoãn lại và tự cập nhật ngay khi rảnh (kiểm tra lại mỗi 5 giây).
 * - OTA qua SignalR: Hub đẩy sự kiện ServerVersion mỗi lần kết nối/kết nối lại => đồng bộ ngay sau khi server
 *   vừa build bản mới, không phải chờ chu kỳ 60 giây.
 */

import { APP_CONFIG } from '../config/app.config.js';
import { httpClient } from '../http/httpClient.js';
import { signalrClient } from '../realtime/signalrClient.js';

const VERSION_STORAGE_KEY = 'smartstudy_app_version';
const RELOAD_GUARD_KEY = 'smartstudy_ota_last_reload_ts';
const POLL_INTERVAL_MS = 60_000;
const RELOAD_DELAY_MS = 1_200;
const RELOAD_COOLDOWN_MS = 15_000;
const BUSY_RETRY_MS = 5_000;
const NON_TEXT_INPUT_TYPES = new Set(['button', 'checkbox', 'radio', 'submit', 'reset', 'range', 'file', 'color']);

let isInitialized = false;
let isSyncingUpdate = false;
let pollTimerId = null;
let deferredRetryTimerId = null;

/**
 * Người dùng đang bận nếu: trang có vùng được bảo vệ [data-ota-guard] (bài thi đang làm)
 * hoặc đang focus vào ô nhập liệu đã có nội dung (tin nhắn/ghi chú đang soạn dở).
 */
function isUserBusy() {
    if (document.querySelector('[data-ota-guard]')) {
        return true;
    }
    const active = document.activeElement;
    if (!active) {
        return false;
    }
    const isTextInput = active.tagName === 'INPUT' && !NON_TEXT_INPUT_TYPES.has(String(active.type).toLowerCase());
    const isEditable = active.isContentEditable || active.tagName === 'TEXTAREA' || isTextInput;
    const currentText = active.isContentEditable ? active.textContent : active.value;
    return isEditable && String(currentText || '').trim().length > 0;
}

function showOtaPendingNotice() {
    if (document.getElementById('ssw-ota-pending-banner')) {
        return;
    }
    const notice = document.createElement('div');
    notice.id = 'ssw-ota-pending-banner';
    notice.setAttribute('role', 'status');
    notice.className = 'fixed bottom-24 md:bottom-5 left-1/2 -translate-x-1/2 z-[120] w-[calc(100%-1.5rem)] max-w-sm rounded-xl border border-indigo-400/40 bg-slate-900/95 px-3.5 py-2.5 text-[11px] font-semibold text-indigo-100 shadow-xl backdrop-blur-xl animate-slide-fade';
    notice.textContent = 'Đã có phiên bản mới — giao diện sẽ tự cập nhật ngay khi bạn hoàn tất bài làm hoặc tin nhắn đang soạn.';
    document.body.appendChild(notice);
}

function scheduleDeferredSync(newVersion, releaseNotes, { showNotice = true } = {}) {
    if (showNotice) {
        showOtaPendingNotice();
    }
    if (deferredRetryTimerId) {
        return;
    }
    deferredRetryTimerId = window.setTimeout(() => {
        deferredRetryTimerId = null;
        executeSyncReload(newVersion, releaseNotes);
    }, BUSY_RETRY_MS);
}

function isReloadWithinCooldown() {
    try {
        const lastTs = Number(sessionStorage.getItem(RELOAD_GUARD_KEY) || 0);
        return lastTs > 0 && Date.now() - lastTs < RELOAD_COOLDOWN_MS;
    } catch {
        return false;
    }
}

function markReloadTimestamp() {
    try {
        sessionStorage.setItem(RELOAD_GUARD_KEY, String(Date.now()));
    } catch {
        // Bỏ qua nếu trình duyệt chặn sessionStorage
    }
}

function showOtaSyncBanner(newVersion, releaseNotes = '') {
    const existing = document.getElementById('ssw-ota-sync-banner');
    if (existing) {
        return;
    }

    const banner = document.createElement('div');
    banner.id = 'ssw-ota-sync-banner';
    banner.setAttribute('role', 'status');
    banner.setAttribute('aria-live', 'polite');
    banner.className = 'fixed top-3 left-1/2 -translate-x-1/2 z-[120] w-[calc(100%-1.5rem)] max-w-md rounded-2xl border border-indigo-400/40 bg-slate-900/95 text-white px-4 py-3 shadow-2xl shadow-indigo-950/50 backdrop-blur-xl animate-slide-fade';

    const safeNotes = releaseNotes
        ? `<p class="mt-0.5 text-[11px] text-indigo-200/90 line-clamp-1"></p>`
        : '';

    banner.innerHTML = `
        <div class="flex items-center gap-3">
            <span class="flex h-8 w-8 shrink-0 items-center justify-center rounded-xl bg-gradient-to-br from-indigo-500 to-violet-600 shadow-md">
                <svg class="h-4 w-4 animate-spin text-white" fill="none" viewBox="0 0 24 24">
                    <circle class="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" stroke-width="3"></circle>
                    <path class="opacity-90" fill="currentColor" d="M4 12a8 8 0 018-8v3a5 5 0 00-5 5H4z"></path>
                </svg>
            </span>
            <div class="min-w-0 flex-1">
                <p class="text-xs font-extrabold tracking-tight text-white">
                    Phát hiện phiên bản mới — Đang tự động đồng bộ giao diện...
                </p>
                ${safeNotes}
            </div>
            <span class="shrink-0 rounded-lg bg-indigo-500/25 border border-indigo-400/30 px-2 py-0.5 font-mono text-[10px] font-bold text-indigo-200" data-ota-ver></span>
        </div>
    `;

    const verBadge = banner.querySelector('[data-ota-ver]');
    if (verBadge) {
        verBadge.textContent = String(newVersion || '').slice(0, 18);
    }
    const notesEl = banner.querySelector('p.line-clamp-1');
    if (notesEl && releaseNotes) {
        notesEl.textContent = releaseNotes;
    }

    document.body.appendChild(banner);
}

async function clearAllBrowserCaches() {
    if (!('caches' in window)) {
        return;
    }
    try {
        const keys = await caches.keys();
        await Promise.all(keys.map((key) => caches.delete(key)));
    } catch (err) {
        console.warn('[OTA Auto-Sync] Không thể xóa hết Cache API:', err);
    }
}

async function notifyServiceWorkerForceSync() {
    if (!('serviceWorker' in navigator)) {
        return;
    }
    try {
        const registration = await navigator.serviceWorker.getRegistration();
        if (registration?.active) {
            registration.active.postMessage({ type: 'FORCE_SYNC_UPDATE' });
        }
        if (registration?.waiting) {
            registration.waiting.postMessage({ type: 'SKIP_WAITING' });
        }
        await registration?.update();
    } catch {
        // Không làm gián đoạn quá trình reload nếu SW chưa sẵn sàng
    }
}

async function fetchRemoteBuildVersion() {
    const ts = Date.now();

    try {
        // Tham số _t chống mọi lớp cache trung gian; máy chủ cũng trả Cache-Control: no-store
        const payload = await httpClient.get('/system/version', { _t: ts });
        const data = payload?.data;
        if (data?.version) {
            return {
                version: String(data.version),
                updatedAt: data.updatedAt || null,
                releaseNotes: data.releaseNotes || ''
            };
        }
    } catch {
        // Thử phương án dự phòng đọc trực tiếp /version.json nếu đang chạy static dev server
    }

    try {
        const fallbackUrl = `${APP_CONFIG.FRONTEND_ROOT}/version.json?_t=${ts}`;
        const res = await fetch(fallbackUrl, { method: 'GET', cache: 'no-store' });
        if (res.ok) {
            const data = await res.json();
            if (data?.version) {
                return {
                    version: String(data.version),
                    updatedAt: data.updatedAt || null,
                    releaseNotes: data.releaseNotes || ''
                };
            }
        }
    } catch {
        // Đang offline hoàn toàn -> giữ nguyên bản hiện tại
    }

    return null;
}

async function executeSyncReload(newVersion, releaseNotes) {
    if (isSyncingUpdate) {
        return;
    }
    // Đang bận (thi/soạn tin) hoặc vừa tải lại < 15 giây (chống vòng lặp) -> hẹn thử lại thay vì bỏ qua bản mới
    const userBusy = isUserBusy();
    if (userBusy || isReloadWithinCooldown()) {
        scheduleDeferredSync(newVersion, releaseNotes, { showNotice: userBusy });
        return;
    }
    isSyncingUpdate = true;

    document.getElementById('ssw-ota-pending-banner')?.remove();
    showOtaSyncBanner(newVersion, releaseNotes);
    markReloadTimestamp();

    try {
        localStorage.setItem(VERSION_STORAGE_KEY, newVersion);
    } catch {
        // Bỏ qua lỗi quota storage
    }

    await Promise.allSettled([
        notifyServiceWorkerForceSync(),
        clearAllBrowserCaches()
    ]);

    window.setTimeout(() => {
        window.location.reload();
    }, RELOAD_DELAY_MS);
}

/**
 * Kiểm tra phiên bản mới nhất trên máy chủ và tự động cập nhật giao diện nếu có thay đổi.
 */
export async function checkForOtaUpdate() {
    if (isSyncingUpdate || !navigator.onLine) {
        return false;
    }

    // Luôn yêu cầu Service Worker kiểm tra bản sw.js mới nhất
    if ('serviceWorker' in navigator && window.isSecureContext) {
        navigator.serviceWorker.getRegistration()
            .then((reg) => reg?.update())
            .catch(() => {});
    }

    const remote = await fetchRemoteBuildVersion();
    if (!remote?.version) {
        return false;
    }

    const savedVersion = localStorage.getItem(VERSION_STORAGE_KEY);
    if (!savedVersion) {
        // Lần đầu tiên mở App: ghi nhận mã phiên bản hiện tại, không reload dư thừa
        localStorage.setItem(VERSION_STORAGE_KEY, remote.version);
        return false;
    }

    if (savedVersion !== remote.version) {
        console.log(`[OTA Auto-Sync] Phát hiện bản mới: ${savedVersion} -> ${remote.version}`);
        await executeSyncReload(remote.version, remote.releaseNotes);
        return true;
    }

    return false;
}

/**
 * Khởi tạo OTA Auto-Sync Engine (an toàn khi gọi nhiều lần trên cùng trang).
 */
export function initOtaAutoSync() {
    if (isInitialized) {
        return;
    }
    isInitialized = true;

    // 1. Lắng nghe Service Worker controllerchange (khi SW mới tiếp quản trang)
    if ('serviceWorker' in navigator && window.isSecureContext) {
        let hadController = Boolean(navigator.serviceWorker.controller);
        navigator.serviceWorker.addEventListener('controllerchange', () => {
            if (!hadController) {
                hadController = true;
                return;
            }
            if (!isSyncingUpdate && !isReloadWithinCooldown()) {
                checkForOtaUpdate();
            }
        });
    }

    // 2. Kiểm tra ngay khi mở App (trì hoãn nhẹ 900ms để không cản First Paint)
    window.setTimeout(() => {
        checkForOtaUpdate();
    }, 900);

    // 3. Kiểm tra mỗi khi người dùng mở lại App / chuyển tab quay lại
    document.addEventListener('visibilitychange', () => {
        if (document.visibilityState === 'visible') {
            checkForOtaUpdate();
        }
    });

    // 4. Kiểm tra ngay khi thiết bị vừa có kết nối mạng trở lại
    window.addEventListener('online', () => {
        checkForOtaUpdate();
    });

    // 5. Định kỳ mỗi 60 giây kiểm tra nền
    pollTimerId = window.setInterval(() => {
        if (document.visibilityState === 'visible') {
            checkForOtaUpdate();
        }
    }, POLL_INTERVAL_MS);

    // 6. SignalR (trang Workspace) báo mã phiên bản ngay khi kết nối lại Hub
    signalrClient.on('ServerVersion', (payload) => applyServerVersionHint(payload?.version));
}

/**
 * Nhận mã phiên bản server đẩy qua SignalR: khác bản đang cài thì kiểm tra lại qua API (nguồn chuẩn) và đồng bộ ngay.
 */
export function applyServerVersionHint(serverVersion) {
    const savedVersion = localStorage.getItem(VERSION_STORAGE_KEY);
    if (!serverVersion || !savedVersion || savedVersion === serverVersion) {
        return;
    }
    checkForOtaUpdate();
}

export function getInstalledAppVersion() {
    return localStorage.getItem(VERSION_STORAGE_KEY) || 'dev';
}

export function stopOtaAutoSync() {
    if (pollTimerId) {
        clearInterval(pollTimerId);
        pollTimerId = null;
    }
}
