/**
 * Smart Study Workspace - Universal Dynamic Origin Configuration.
 * Cấu hình trung tâm của tầng Frontend (API URLs, SignalR Hub, File Storage & Routes).
 * Backend C# All-in-One phục vụ cả giao diện lẫn API trên cổng 5000 VÀ 5500, nên mặc định luôn dùng
 * chính window.location.origin: http://localhost:5500, http://localhost:5000, LAN Wi-Fi (192.168.x.x:5500),
 * Docker Nginx (80/443) và Cloudflare HTTPS Tunnel (https://xxxx.trycloudflare.com) đều cùng origin.
 * Đường dẫn gốc Frontend tính qua import.meta.url để chuyển trang chuẩn xác dù mở Live Server
 * tại thư mục gốc dự án hay bên trong thư mục /frontend.
 */

// Chỉ các Dev Server tĩnh độc lập (VS Code Live Server khi 5500 đã bận, Vite...) mới cần trỏ sang Backend cổng 5000
const DEV_STATIC_SERVER_PORTS = new Set(['5501', '3000', '5173']);
const BACKEND_DEV_PORT = '5000';

const resolveBackendBaseOrigin = () => {
    const { protocol, hostname, port, origin } = globalThis.location;
    if (DEV_STATIC_SERVER_PORTS.has(port)) {
        return `${protocol}//${hostname}:${BACKEND_DEV_PORT}`;
    }
    // Cổng 5000/5500 của Backend All-in-One, Docker Nginx hoặc HTTPS Tunnel: dùng chính origin hiện tại
    return origin;
};

export const BASE_ORIGIN = resolveBackendBaseOrigin();
export const API_BASE_URL = `${BASE_ORIGIN}/api`;
export const HUB_URL = `${BASE_ORIGIN}/hubs/study-room`;
export const FILE_BASE_URL = BASE_ORIGIN;

const resolvedFrontendRoot = new URL('../../../', import.meta.url).pathname.replace(/\/$/, '');

export const STORAGE_KEYS = Object.freeze({
    ACCESS_TOKEN: 'ssw_access_token',
    CURRENT_USER: 'ssw_current_user',
    TOKEN_EXPIRES_AT: 'ssw_token_expires_at'
});

export const ROUTES = Object.freeze({
    LANDING: `${resolvedFrontendRoot}/index.html`,
    LOGIN: `${resolvedFrontendRoot}/pages/auth/login.html`,
    REGISTER: `${resolvedFrontendRoot}/pages/auth/register.html`,
    FORGOT_PASSWORD: `${resolvedFrontendRoot}/pages/auth/forgot-password.html`,
    STUDENT_DASHBOARD: `${resolvedFrontendRoot}/pages/student/dashboard.html`,
    STUDENT_WORKSPACE: `${resolvedFrontendRoot}/pages/student/workspace.html`,
    STUDENT_PROFILE: `${resolvedFrontendRoot}/pages/student/profile.html`,
    ADMIN_DASHBOARD: `${resolvedFrontendRoot}/pages/admin/dashboard.html`
});

export const APP_CONFIG = Object.freeze({
    APP_NAME: 'Smart Study Workspace',
    BASE_ORIGIN,
    API_BASE_URL,
    HUB_URL,
    FILE_BASE_URL,
    FRONTEND_ROOT: resolvedFrontendRoot,
    ROUTES
});

/**
 * Ghép đường dẫn tương đối từ Backend (ví dụ /uploads/materials/abc.pdf) thành URL tuyệt đối.
 */
export function resolveBackendFileUrl(relativeOrFullUrl) {
    if (!relativeOrFullUrl) {
        return '';
    }
    if (/^https?:\/\//i.test(relativeOrFullUrl)) {
        return relativeOrFullUrl;
    }
    const cleanPath = relativeOrFullUrl.startsWith('/') ? relativeOrFullUrl : `/${relativeOrFullUrl}`;
    return `${FILE_BASE_URL}${cleanPath}`;
}

/**
 * Xây dựng URL mở không gian học tập 3 cột của một phòng học cụ thể.
 */
export function buildWorkspaceUrl(roomId) {
    return `${ROUTES.STUDENT_WORKSPACE}?roomId=${encodeURIComponent(roomId)}`;
}

export default APP_CONFIG;
