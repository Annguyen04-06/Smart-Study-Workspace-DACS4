/**
 * Tiện ích thao tác DOM an toàn, hàm chống tấn công XSS tuyệt đối (escapeHtml) và debounce.
 */

export function qs(selector, root = document) {
    return root.querySelector(selector);
}

export function qsa(selector, root = document) {
    return Array.from(root.querySelectorAll(selector));
}

const HTML_ESCAPE_MAP = {
    '&': '&amp;',
    '<': '&lt;',
    '>': '&gt;',
    '"': '&quot;',
    "'": '&#39;'
};

/**
 * Mã hóa toàn bộ ký tự đặc biệt HTML từ dữ liệu người dùng nhập để triệt tiêu lỗ hổng XSS.
 */
export function escapeHtml(value) {
    if (value === null || value === undefined) {
        return '';
    }
    return String(value).replace(/[&<>"']/g, (ch) => HTML_ESCAPE_MAP[ch]);
}

/**
 * Trì hoãn gọi hàm khi người dùng đang gõ ô tìm kiếm.
 */
export function debounce(fn, waitMs = 300) {
    let timerId = null;
    return function debounced(...args) {
        if (timerId) {
            clearTimeout(timerId);
        }
        timerId = setTimeout(() => {
            fn.apply(this, args);
        }, waitMs);
    };
}

/**
 * Rút trích 2 chữ cái đầu của Họ tên để tạo Avatar chữ viết tắt khi chưa có ảnh đại diện.
 */
export function getInitials(fullName) {
    if (!fullName || typeof fullName !== 'string') {
        return 'SV';
    }
    const parts = fullName.trim().split(/\s+/).filter(Boolean);
    if (parts.length === 0) {
        return 'SV';
    }
    if (parts.length === 1) {
        return parts[0].slice(0, 2).toUpperCase();
    }
    return `${parts[0][0]}${parts[parts.length - 1][0]}`.toUpperCase();
}

/**
 * Sao chép chuỗi văn bản vào bộ nhớ tạm hệ thống (Clipboard).
 */
export async function copyTextToClipboard(text) {
    if (navigator.clipboard && window.isSecureContext) {
        await navigator.clipboard.writeText(text);
        return true;
    }
    const tempArea = document.createElement('textarea');
    tempArea.value = text;
    tempArea.style.position = 'fixed';
    tempArea.style.left = '-9999px';
    document.body.appendChild(tempArea);
    tempArea.select();
    document.execCommand('copy');
    tempArea.remove();
    return true;
}
