/**
 * Controller trang Đăng ký tài khoản Sinh viên (pages/auth/register.html).
 */

import { ROUTES } from '../../core/config/app.config.js';
import { redirectGuestIfLoggedIn } from '../../core/guards/routeGuard.js';
import { appStore } from '../../core/state/appStore.js';
import { initOtaAutoSync } from '../../core/sync/otaUpdater.service.js';
import { toast } from '../../shared/components/toast.component.js';
import { qs } from '../../shared/utils/dom.utils.js';
import { initTheme, toggleTheme } from '../../shared/utils/theme.utils.js';
import {
    isValidEmail,
    isValidPassword,
    isValidStudentCode
} from '../../shared/utils/validation.utils.js';
import { authApi } from './auth.api.js';

function initRegisterPage() {
    initTheme();
    initOtaAutoSync();
    if (redirectGuestIfLoggedIn()) {
        return;
    }

    qs('#register-theme-toggle')?.addEventListener('click', () => toggleTheme());

    const homeLink = qs('#register-home-link');
    const loginLink = qs('#register-login-link');
    if (homeLink) homeLink.href = ROUTES.LANDING;
    if (loginLink) loginLink.href = ROUTES.LOGIN;

    const form = qs('#register-form');
    const submitBtn = qs('#register-submit-btn');

    form?.addEventListener('submit', async (event) => {
        event.preventDefault();

        const studentCode = qs('#reg-student-code')?.value.trim() || '';
        const fullName = qs('#reg-full-name')?.value.trim() || '';
        const email = qs('#reg-email')?.value.trim() || '';
        const password = qs('#reg-password')?.value || '';
        const confirmPassword = qs('#reg-confirm-password')?.value || '';

        if (!isValidStudentCode(studentCode)) {
            toast.warning('Mã số sinh viên phải từ 3–20 ký tự (chữ cái, chữ số, gạch ngang).');
            return;
        }
        if (fullName.length < 2) {
            toast.warning('Vui lòng nhập đầy đủ Họ và tên sinh viên.');
            return;
        }
        if (!isValidEmail(email)) {
            toast.warning('Định dạng Email không hợp lệ.');
            return;
        }
        if (!isValidPassword(password)) {
            toast.warning('Mật khẩu phải có ít nhất 6 ký tự.');
            return;
        }
        if (password !== confirmPassword) {
            toast.warning('Mật khẩu xác nhận không khớp.');
            return;
        }

        if (submitBtn) {
            submitBtn.disabled = true;
            submitBtn.textContent = 'Đang khởi tạo tài khoản...';
        }

        try {
            const authData = await authApi.register({
                studentCode,
                fullName,
                email,
                password,
                confirmPassword
            });

            appStore.setAuthSession(authData);
            toast.success('Đăng ký tài khoản sinh viên thành công!');

            setTimeout(() => {
                window.location.href = ROUTES.STUDENT_DASHBOARD;
            }, 250);
        } catch (err) {
            toast.error(err.message || 'Không thể đăng ký tài khoản.');
            if (submitBtn) {
                submitBtn.disabled = false;
                submitBtn.textContent = 'Tạo tài khoản Sinh viên';
            }
        }
    });
}

document.addEventListener('DOMContentLoaded', initRegisterPage);
