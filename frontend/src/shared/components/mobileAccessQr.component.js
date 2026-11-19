/**
 * Controller nút "Mở trên Điện thoại (QR HTTPS)" trên Laptop (>= 768px): gắn nút vào mọi [data-mobile-qr-slot],
 * mở Modal mã QR cỡ lớn trỏ thẳng vào link HTTPS Cloudflare Tunnel (đúng trang đang xem) kèm nút Copy.
 * Mã QR được tạo ngay trên trình duyệt (thư viện qrcode-generator nạp lười từ CDN khi mở Modal lần đầu),
 * không gửi đường dẫn riêng của máy chủ cho bất kỳ dịch vụ tạo QR bên ngoài nào.
 */

import { APP_CONFIG } from '../../core/config/app.config.js';
import { copyTextToClipboard } from '../utils/dom.utils.js';
import { mobileAccessApi } from './mobileAccess.api.js';
import { openModal } from './modal.component.js';
import {
    MOBILE_QR_MODAL_ID,
    renderMobileQrButton,
    renderMobileQrContent,
    renderMobileQrError,
    renderMobileQrLoading,
    renderMobileQrModalShell
} from './mobileAccessQr.view.js';
import { toast } from './toast.component.js';

const QR_LIBRARY_URL = 'https://cdnjs.cloudflare.com/ajax/libs/qrcode-generator/1.4.4/qrcode.min.js';
const QR_CELL_SIZE = 6;
const QR_MARGIN = 2;

let isInitialized = false;
let qrLibraryPromise = null;

function loadQrLibrary() {
    if (window.qrcode) {
        return Promise.resolve(window.qrcode);
    }
    if (!qrLibraryPromise) {
        qrLibraryPromise = new Promise((resolve, reject) => {
            const script = document.createElement('script');
            script.src = QR_LIBRARY_URL;
            script.async = true;
            script.onload = () => (window.qrcode ? resolve(window.qrcode) : reject(new Error('Thư viện tạo mã QR không hợp lệ.')));
            script.onerror = () => {
                qrLibraryPromise = null;
                reject(new Error('Không tải được thư viện tạo mã QR (kiểm tra kết nối Internet).'));
            };
            document.head.appendChild(script);
        });
    }
    return qrLibraryPromise;
}

// Giữ nguyên trang đang xem (ví dụ đúng phòng học) khi mở trên điện thoại
function buildDeepLink(baseUrl) {
    const { pathname, search } = window.location;
    const frontendRoot = APP_CONFIG.FRONTEND_ROOT || '';
    const relativePath = frontendRoot && pathname.startsWith(frontendRoot) ? pathname.slice(frontendRoot.length) : pathname;
    return `${baseUrl.replace(/\/$/, '')}${relativePath || '/'}${search}`;
}

function formatUpdatedAt(info) {
    if (!info?.updatedAt) {
        return 'Chưa có đường hầm HTTPS';
    }
    const time = new Date(info.updatedAt).toLocaleTimeString('vi-VN', { hour: '2-digit', minute: '2-digit', second: '2-digit' });
    return info.isTunnelActive ? `Đã kiểm chứng link lúc ${time}` : `Đường hầm ngừng phản hồi từ ${time}`;
}

function getModalBody() {
    if (!document.getElementById(MOBILE_QR_MODAL_ID)) {
        document.body.insertAdjacentHTML('beforeend', renderMobileQrModalShell());
    }
    return document.querySelector(`#${MOBILE_QR_MODAL_ID} [data-mobile-qr-body]`);
}

async function renderQrInto(container, text) {
    const qrcode = await loadQrLibrary();
    const qr = qrcode(0, 'M');
    qr.addData(text);
    qr.make();
    // SVG do thư viện sinh chỉ gồm các ô vuông (không chứa dữ liệu người dùng) nên gắn innerHTML an toàn
    container.innerHTML = qr.createSvgTag(QR_CELL_SIZE, QR_MARGIN);
}

async function loadAndRenderModalContent() {
    const body = getModalBody();
    if (!body) return;
    body.innerHTML = renderMobileQrLoading();

    try {
        const info = await mobileAccessApi.getMobileAccess();
        const baseUrl = info?.publicHttpsUrl || info?.lanUrl;
        if (!baseUrl) {
            body.innerHTML = renderMobileQrError('Chưa có đường hầm HTTPS và không xác định được IP mạng LAN của máy chủ. Hãy chạy run-local-vps.bat.');
            return;
        }

        const targetUrl = buildDeepLink(baseUrl);
        body.innerHTML = renderMobileQrContent({
            targetUrl,
            isHttps: Boolean(info.publicHttpsUrl),
            lanUrl: info.publicHttpsUrl && info.lanUrl ? buildDeepLink(info.lanUrl) : null,
            updatedAtLabel: formatUpdatedAt(info)
        });

        const qrContainer = body.querySelector('[data-mobile-qr-code]');
        if (qrContainer) {
            await renderQrInto(qrContainer, targetUrl);
        }
    } catch (err) {
        body.innerHTML = renderMobileQrError(err?.message || 'Máy chủ không phản hồi.');
    }
}

export async function openMobileQrModal() {
    getModalBody();
    openModal(MOBILE_QR_MODAL_ID);
    await loadAndRenderModalContent();
}

function bindGlobalEventsOnce() {
    if (isInitialized) return;
    isInitialized = true;

    document.addEventListener('click', async (event) => {
        if (event.target.closest('[data-action="open-mobile-qr"]')) {
            openMobileQrModal();
            return;
        }
        if (event.target.closest('[data-action="refresh-mobile-qr"]')) {
            loadAndRenderModalContent();
            return;
        }
        const copyBtn = event.target.closest('[data-action="copy-mobile-link"]');
        if (copyBtn) {
            await copyTextToClipboard(copyBtn.dataset.copyValue || '');
            copyBtn.textContent = 'Đã chép ✓';
            toast.success('Đã sao chép đường dẫn mở trên điện thoại.', 1800);
            window.setTimeout(() => { copyBtn.textContent = 'Copy'; }, 1800);
        }
    });
}

/**
 * Gắn nút QR vào mọi [data-mobile-qr-slot] (gọi lại an toàn mỗi khi Header render lại).
 */
export function mountMobileQrButton() {
    bindGlobalEventsOnce();
    document.querySelectorAll('[data-mobile-qr-slot]').forEach((slot) => {
        slot.innerHTML = renderMobileQrButton();
    });
}
