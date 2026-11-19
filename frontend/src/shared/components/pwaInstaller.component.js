/**
 * Component quản lý Progressive Web App (PWA):
 * 1. Đăng ký Service Worker (chỉ trong Secure Context: HTTPS hoặc localhost).
 * 2. Gắn nút "Tải App" vào slot #pwa-install-container trên Header (máy tính lẫn điện thoại).
 * 3. Luồng cài đặt theo nền tảng:
 *    - Android / Windows / macOS (Chrome, Edge): bắt beforeinstallprompt -> deferredPrompt.prompt() cài 1 chạm.
 *    - iPhone / iPad (Safari): mở Bottom Sheet hướng dẫn 2 bước Chia sẻ -> Thêm vào MH chính.
 *    - Trình duyệt khác / HTTP LAN: Bottom Sheet hướng dẫn cài thủ công qua menu trình duyệt.
 * Tự động ẩn nút khi ứng dụng đã chạy ở chế độ Standalone (đã cài đặt).
 */

import { APP_CONFIG } from '../../core/config/app.config.js';
import { closeBottomSheet, openBottomSheet } from './bottomSheet.component.js';
import {
    renderInstallButtonContent,
    renderIosInstallGuide,
    renderManualInstallGuide
} from './pwaInstallGuide.view.js';
import { toast } from './toast.component.js';

const INSTALL_SLOT_ID = 'pwa-install-container';
const INSTALL_BUTTON_CLASS = 'pwa-install-btn';

let deferredPrompt = null;
let isInitialized = false;
let hasInstalledInSession = false;

/**
 * Kiểm tra ứng dụng hiện có đang chạy ở chế độ Standalone (đã cài đặt) không.
 */
export function isPwaInstalled() {
    return hasInstalledInSession
        || window.matchMedia('(display-mode: standalone)').matches
        || window.matchMedia('(display-mode: fullscreen)').matches
        || window.navigator.standalone === true;
}

/**
 * Nhận diện iPhone / iPad (kể cả iPadOS 13+ giả lập User-Agent của macOS).
 */
export function isIosDevice() {
    const userAgent = navigator.userAgent || '';
    const isIphoneOrIpad = /iPad|iPhone|iPod/.test(userAgent);
    const isIpadOsDesktopMode = navigator.platform === 'MacIntel' && navigator.maxTouchPoints > 1;
    return isIphoneOrIpad || isIpadOsDesktopMode;
}

function isIosSafariBrowser() {
    return isIosDevice() && !/CriOS|FxiOS|EdgiOS|OPiOS|GSA/i.test(navigator.userAgent || '');
}

function isAndroidDevice() {
    return /Android/i.test(navigator.userAgent || '');
}

function resolveServiceWorkerUrl() {
    return `${APP_CONFIG.FRONTEND_ROOT}/sw.js`;
}

function registerServiceWorker(swPath) {
    // Service Worker chỉ hoạt động trong Secure Context (HTTPS qua Cloudflare hoặc http://localhost)
    if (!('serviceWorker' in navigator) || !window.isSecureContext) {
        return;
    }

    const register = () => {
        navigator.serviceWorker.register(swPath || resolveServiceWorkerUrl())
            .then((registration) => {
                console.log('[PWA] Service Worker đăng ký thành công:', registration.scope);
            })
            .catch((error) => {
                console.warn('[PWA] Đăng ký Service Worker thất bại:', error);
            });
    };

    if (document.readyState === 'complete') {
        register();
    } else {
        window.addEventListener('load', register, { once: true });
    }
}

function removeInstallButtons() {
    document.querySelectorAll(`.${INSTALL_BUTTON_CLASS}`).forEach((btn) => btn.remove());
}

async function promptNativeInstall() {
    const promptEvent = deferredPrompt;
    try {
        await promptEvent.prompt();
    } catch (error) {
        // Trình duyệt từ chối hộp thoại gốc (ví dụ thiếu thao tác người dùng) -> hướng dẫn cài thủ công
        console.warn('[PWA] Không mở được hộp thoại cài đặt gốc:', error);
        openManualInstallGuide();
        return;
    }
    // Mỗi BeforeInstallPromptEvent chỉ dùng được 1 lần sau khi đã hiển thị thành công
    deferredPrompt = null;

    const { outcome } = await promptEvent.userChoice;
    if (outcome === 'accepted') {
        toast.success('Đang cài đặt Smart Study Workspace vào thiết bị của bạn...');
    } else {
        toast.info('Bạn có thể cài đặt lại bất cứ lúc nào bằng nút "Tải App".');
    }
}

function openIosInstallGuide() {
    openBottomSheet({
        title: 'Cài Smart Study lên iPhone / iPad',
        subtitle: 'Chỉ 2 bước — không cần App Store',
        contentHtml: renderIosInstallGuide({
            isSafari: isIosSafariBrowser(),
            isSecureContext: window.isSecureContext
        })
    });
}

function openManualInstallGuide() {
    openBottomSheet({
        title: 'Cài Smart Study như ứng dụng',
        subtitle: 'Trình duyệt chưa sẵn sàng cài 1 chạm — làm theo hướng dẫn dưới đây',
        contentHtml: renderManualInstallGuide({
            isAndroid: isAndroidDevice(),
            isSecureContext: window.isSecureContext
        })
    });
}

async function handleInstallClick() {
    if (deferredPrompt) {
        await promptNativeInstall();
        return;
    }
    if (isIosDevice()) {
        openIosInstallGuide();
        return;
    }
    openManualInstallGuide();
}

function createInstallButton() {
    const btn = document.createElement('button');
    btn.type = 'button';
    btn.className = `${INSTALL_BUTTON_CLASS} group relative inline-flex items-center gap-1.5 whitespace-nowrap rounded-xl border border-indigo-400/30 bg-gradient-to-r from-indigo-600 to-violet-600 px-2.5 sm:px-3 py-1.5 text-xs font-bold text-white shadow-md shadow-indigo-500/25 hover:from-indigo-500 hover:to-violet-500 hover:shadow-indigo-500/40 transition-all duration-200`;
    btn.title = 'Cài đặt Smart Study Workspace như một ứng dụng trên thiết bị';
    btn.setAttribute('aria-label', 'Tải ứng dụng Smart Study Workspace');
    btn.innerHTML = renderInstallButtonContent();
    btn.addEventListener('click', handleInstallClick);
    return btn;
}

/**
 * Gắn (hoặc gắn lại sau khi Header render lại) nút "Tải App" vào slot #pwa-install-container.
 */
export function mountPwaInstallButton() {
    if (isPwaInstalled()) {
        return;
    }
    const slot = document.getElementById(INSTALL_SLOT_ID);
    if (!slot || slot.querySelector(`.${INSTALL_BUTTON_CLASS}`)) {
        return;
    }
    slot.appendChild(createInstallButton());
}

/**
 * Khởi tạo Service Worker, lắng nghe sự kiện cài đặt và hiển thị nút "Tải App".
 * Gọi lại nhiều lần an toàn (chỉ đăng ký listener 1 lần).
 * @param {string|null} swPath Đường dẫn tùy chỉnh tới sw.js (mặc định tự tính theo thư mục gốc Frontend)
 */
export function initPwaInstaller(swPath = null) {
    if (isInitialized) {
        mountPwaInstallButton();
        return;
    }
    isInitialized = true;

    registerServiceWorker(swPath);

    window.addEventListener('beforeinstallprompt', (event) => {
        // Giữ lại lời mời cài đặt để kích hoạt đúng lúc người dùng bấm "Tải App"
        event.preventDefault();
        deferredPrompt = event;
        mountPwaInstallButton();
    });

    window.addEventListener('appinstalled', () => {
        deferredPrompt = null;
        hasInstalledInSession = true;
        removeInstallButtons();
        closeBottomSheet();
        toast.success('Đã cài đặt Smart Study Workspace thành công vào thiết bị của bạn!');
    });

    mountPwaInstallButton();
}
