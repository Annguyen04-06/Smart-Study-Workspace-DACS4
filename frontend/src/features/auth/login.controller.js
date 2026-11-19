/**
 * Controller trang Đăng nhập hiện đại (pages/auth/login.html):
 * Hỗ trợ đổi giao diện Sáng/Tối, nút Ẩn/Hiện mật khẩu và chuyển hướng Quên mật khẩu.
 * Lưu ý bảo mật: tuyệt đối không ghi tài khoản/mật khẩu mẫu vào mã nguồn Frontend vì trình duyệt
 * tải được toàn bộ file JS (nhất là khi hệ thống mở ra Internet qua Cloudflare Tunnel).
 */

import { ROUTES } from '../../core/config/app.config.js';
import { redirectGuestIfLoggedIn } from '../../core/guards/routeGuard.js';
import { appStore } from '../../core/state/appStore.js';
import { initOtaAutoSync } from '../../core/sync/otaUpdater.service.js';
import { mountMobileQrButton } from '../../shared/components/mobileAccessQr.component.js';
import { initPwaInstaller } from '../../shared/components/pwaInstaller.component.js';
import { toast } from '../../shared/components/toast.component.js';
import { qs } from '../../shared/utils/dom.utils.js';
import { initInteractiveEffects } from '../../shared/utils/spotlight.utils.js';
import { initTheme, toggleTheme } from '../../shared/utils/theme.utils.js';
import { isValidEmail } from '../../shared/utils/validation.utils.js';
import { authApi } from './auth.api.js';

function initLoginPage() {
    initTheme();
    initPwaInstaller();
    initOtaAutoSync();
    initInteractiveEffects();
    if (redirectGuestIfLoggedIn()) {
        return;
    }

    const homeLink = qs('#login-home-link');
    const backHomeLink = qs('#login-back-home');
    const registerLink = qs('#login-register-link');
    const forgotLink = qs('#login-forgot-link');

    if (homeLink) homeLink.href = ROUTES.LANDING;
    if (backHomeLink) backHomeLink.href = ROUTES.LANDING;
    if (registerLink) registerLink.href = ROUTES.REGISTER;
    if (forgotLink) forgotLink.href = ROUTES.FORGOT_PASSWORD;

    qs('#login-theme-toggle')?.addEventListener('click', () => toggleTheme());
    mountMobileQrButton();

    const form = qs('#login-form');
    const emailInput = qs('#login-email');
    const passwordInput = qs('#login-password');
    const submitBtn = qs('#login-submit-btn');

    // Nút con mắt Ẩn / Hiện mật khẩu
    qs('#toggle-login-password')?.addEventListener('click', () => {
        if (!passwordInput) return;
        passwordInput.type = passwordInput.type === 'password' ? 'text' : 'password';
        passwordInput.focus();
    });

    const setBusy = (busy) => {
        if (!submitBtn) return;
        submitBtn.disabled = busy;
        submitBtn.innerHTML = busy
            ? `<span>Đang đăng nhập...</span>`
            : `<span>Đăng nhập vào Workspace</span><svg class="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2.3"><path stroke-linecap="round" stroke-linejoin="round" d="M13 7l5 5m0 0l-5 5m5-5H6"/></svg>`;
    };

    const executeLogin = async (email, password) => {
        if (!isValidEmail(email)) {
            toast.warning('Vui lòng nhập địa chỉ Email hợp lệ.');
            emailInput?.focus();
            return;
        }
        if (!password) {
            toast.warning('Vui lòng nhập mật khẩu đăng nhập.');
            passwordInput?.focus();
            return;
        }

        setBusy(true);
        try {
            const authData = await authApi.login({ email: email.trim(), password });
            appStore.setAuthSession(authData);
            toast.success(`Xin chào ${authData.user.fullName}!`);

            const destination = String(authData.user.role).toLowerCase() === 'admin'
                ? ROUTES.ADMIN_DASHBOARD
                : ROUTES.STUDENT_DASHBOARD;

            setTimeout(() => {
                window.location.href = destination;
            }, 180);
        } catch (err) {
            toast.error(err.message || 'Đăng nhập thất bại.');
            setBusy(false);
        }
    };

    form?.addEventListener('submit', async (event) => {
        event.preventDefault();
        await executeLogin(emailInput?.value || '', passwordInput?.value || '');
    });
}

document.addEventListener('DOMContentLoaded', initLoginPage);
