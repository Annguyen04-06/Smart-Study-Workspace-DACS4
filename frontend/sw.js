/**
 * Smart Study Workspace - Progressive Web App Service Worker (OTA Auto-Sync Engine v4)
 * Chiến lược:
 * - API REST (/api/...), SignalR (/hubs/...), file tải lên (/uploads/...), Swagger và version.json:
 *   Network-Only tuyệt đối (không bao giờ lưu cache dữ liệu động hay mã phiên bản).
 * - Tài nguyên giao diện cùng origin (HTML, JS, CSS, ảnh, icons):
 *   Network-First with Cache Fallback (dùng cache: 'no-store' khi hỏi server để luôn lấy bản mới nhất;
 *   chỉ dùng Cache khi thật sự mất mạng).
 * - Lắng nghe message FORCE_SYNC_UPDATE từ otaUpdater.service.js để xóa sạch toàn bộ cache ngay lập tức.
 */

const CACHE_VERSION = 'v4';
const CACHE_NAME = `smartstudy-cache-${CACHE_VERSION}`;
const NETWORK_TIMEOUT_MS = 4500;

const NETWORK_ONLY_PATH_SEGMENTS = ['/api/', '/hubs/', '/uploads/', '/swagger', '/version.json'];

const STATIC_ASSETS = [
    './',
    './index.html',
    './offline.html',
    './manifest.webmanifest',
    './src/assets/icons/icon.svg',
    './src/assets/icons/favicon.png',
    './src/assets/icons/apple-touch-icon.png',
    './src/assets/icons/icon-192.png',
    './src/assets/icons/icon-512.png',
    './src/assets/styles/variables.css',
    './src/assets/styles/components.css',
    './src/assets/styles/animations.css',
    './src/assets/styles/mobile-native.css',
    './src/core/config/app.config.js',
    './src/core/http/httpClient.js',
    './src/core/sync/otaUpdater.service.js',
    './src/core/storage/offlineDb.service.js',
    './src/shared/utils/dom.utils.js',
    './src/shared/utils/spotlight.utils.js',
    './src/shared/utils/touchGesture.utils.js',
    './src/shared/components/toast.component.js',
    './src/shared/components/navbar.component.js',
    './src/shared/components/serverHealth.component.js',
    './src/shared/components/notification.service.js',
    './src/shared/components/bottomSheet.component.js',
    './src/shared/components/pwaInstallGuide.view.js',
    './src/shared/components/pwaInstaller.component.js',
    './src/shared/components/mobileWorkspaceNav.component.js'
];

const OFFLINE_PAGE_URL = new URL('./offline.html', self.registration.scope).href;

self.addEventListener('install', (event) => {
    event.waitUntil(
        caches.open(CACHE_NAME)
            .then((cache) => cache.addAll(STATIC_ASSETS).catch((err) => {
                console.warn('[PWA SW] Không nạp được một số asset vào cache ban đầu:', err);
            }))
            .then(() => self.skipWaiting())
    );
});

// Các phiên bản Service Worker cũ chưa có OTA Auto-Sync (v1 còn dùng Stale-While-Revalidate => App kẹt giao diện cũ)
const LEGACY_PRE_OTA_CACHES = new Set(['smartstudy-cache-v1', 'smartstudy-cache-v2', 'smartstudy-cache-v3']);

self.addEventListener('activate', (event) => {
    event.waitUntil((async () => {
        const staleCacheNames = (await caches.keys()).filter((name) => name !== CACHE_NAME);
        await Promise.all(staleCacheNames.map((name) => {
            console.log('[PWA SW] Xóa sạch cache phiên bản cũ:', name);
            return caches.delete(name);
        }));
        await self.clients.claim();

        // Nâng cấp từ SW đời cũ: trang đang mở vẫn là giao diện cũ và không có OTA để tự làm mới
        // => buộc các cửa sổ App tải lại 1 lần để nhận ngay bản mới nhất (chỉ xảy ra một lần duy nhất)
        if (staleCacheNames.some((name) => LEGACY_PRE_OTA_CACHES.has(name))) {
            const windowClients = await self.clients.matchAll({ type: 'window' });
            await Promise.all(windowClients.map((client) => client.navigate(client.url).catch(() => null)));
        }
    })());
});

// Lắng nghe lệnh đồng bộ cưỡng bức từ OTA Auto-Sync Client
self.addEventListener('message', (event) => {
    const data = event.data;
    if (!data || typeof data !== 'object') {
        return;
    }

    if (data.type === 'SKIP_WAITING') {
        self.skipWaiting();
        return;
    }

    if (data.type === 'FORCE_SYNC_UPDATE') {
        event.waitUntil(
            caches.keys()
                .then((names) => Promise.all(names.map((name) => caches.delete(name))))
                .then(() => self.skipWaiting())
                .then(() => self.clients.matchAll({ type: 'window', includeUncontrolled: true }))
                .then((clients) => {
                    clients.forEach((client) => {
                        client.postMessage({ type: 'SYNC_CACHE_CLEARED', cacheVersion: CACHE_VERSION });
                    });
                })
        );
    }
});

function isNetworkOnlyRequest(url) {
    return NETWORK_ONLY_PATH_SEGMENTS.some((segment) => url.pathname.includes(segment));
}

function saveToCache(request, response) {
    if (response && response.status === 200 && response.type === 'basic') {
        const copy = response.clone();
        caches.open(CACHE_NAME).then((cache) => cache.put(request, copy));
    }
    return response;
}

/**
 * Network-First with Cache Fallback:
 * - Có mạng: fetch thẳng server với cache: 'no-store' để bỏ qua HTTP cache cũ, lưu bản mới nhất vào SW cache.
 * - Mất mạng / quá thời gian chờ: dùng bản đã lưu trong cache; điều hướng trang chưa có cache -> offline.html.
 */
async function networkFirst(request, { isNavigation }) {
    const freshRequest = new Request(request, { cache: 'no-store' });
    const networkPromise = fetch(freshRequest).then((response) => saveToCache(request, response));
    networkPromise.catch(() => {});

    const cachedResponse = await caches.match(request);

    try {
        if (!cachedResponse) {
            return await networkPromise;
        }
        const timeoutPromise = new Promise((resolve) => {
            setTimeout(() => resolve(cachedResponse), NETWORK_TIMEOUT_MS);
        });
        return await Promise.race([networkPromise, timeoutPromise]);
    } catch {
        if (cachedResponse) {
            return cachedResponse;
        }
        if (isNavigation) {
            const offlinePage = await caches.match(OFFLINE_PAGE_URL);
            if (offlinePage) {
                return offlinePage;
            }
        }
        return Response.error();
    }
}

self.addEventListener('fetch', (event) => {
    const { request } = event;

    // 1. Không can thiệp các request thay đổi dữ liệu (POST, PUT, PATCH, DELETE)
    if (request.method !== 'GET') {
        return;
    }

    const url = new URL(request.url);

    // 2. Tài nguyên CDN bên ngoài (Tailwind, KaTeX, SignalR, Chart.js...) để trình duyệt tự xử lý
    if (url.origin !== self.location.origin) {
        return;
    }

    // 3. Network-Only: API, SignalR Hub, file PDF/Avatar tải lên, Swagger, version.json
    if (isNetworkOnlyRequest(url)) {
        return;
    }

    // 4. HTML, JS, CSS, ảnh cục bộ: Network-First with Cache Fallback
    event.respondWith(networkFirst(request, { isNavigation: request.mode === 'navigate' }));
});
