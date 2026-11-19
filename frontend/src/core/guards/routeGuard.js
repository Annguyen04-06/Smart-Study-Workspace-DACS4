/**
 * Các hàm bảo vệ điều hướng trang (Route Guards) dựa trên JWT và Role của người dùng.
 */

import { ROUTES } from '../config/app.config.js';
import { appStore } from '../state/appStore.js';

/**
 * Yêu cầu người dùng phải đăng nhập hợp lệ trước khi xem trang nội bộ.
 */
export function requireAuth() {
    const { token, user } = appStore.getState();
    if (!token || !user) {
        window.location.replace(ROUTES.LOGIN);
        return null;
    }
    return user;
}

/**
 * Yêu cầu quyền Quản trị viên (Role === 'Admin') cho trang Admin Portal.
 */
export function requireAdmin() {
    const user = requireAuth();
    if (!user) {
        return null;
    }
    if (String(user.role).toLowerCase() !== 'admin') {
        window.location.replace(ROUTES.STUDENT_DASHBOARD);
        return null;
    }
    return user;
}

/**
 * Nếu người dùng đã đăng nhập mà truy cập trang Login/Register thì tự chuyển về Dashboard tương ứng.
 */
export function redirectGuestIfLoggedIn() {
    const { token, user } = appStore.getState();
    if (!token || !user) {
        return false;
    }
    const target = String(user.role).toLowerCase() === 'admin'
        ? ROUTES.ADMIN_DASHBOARD
        : ROUTES.STUDENT_DASHBOARD;
    window.location.replace(target);
    return true;
}
