/**
 * Controller trang Khôi phục Mật khẩu 3 bước (pages/auth/forgot-password.html):
 * Bước 1: Gửi yêu cầu nhận mã OTP 6 chữ số (qua Google Gmail SMTP hoặc mã trực tiếp)
 * Bước 2: Nhập / dán 6 chữ số OTP kèm đếm ngược gửi lại
 * Bước 3: Đặt mật khẩu mới có thanh đánh giá độ mạnh trực quan.
 */

import { ROUTES } from '../../core/config/app.config.js';
import { redirectGuestIfLoggedIn } from '../../core/guards/routeGuard.js';
import { initOtaAutoSync } from '../../core/sync/otaUpdater.service.js';
import { toast } from '../../shared/components/toast.component.js';
import { qs, qsa } from '../../shared/utils/dom.utils.js';
import { initTheme, runViewTransition, toggleTheme } from '../../shared/utils/theme.utils.js';
import { isValidEmail, isValidPassword } from '../../shared/utils/validation.utils.js';
import { authApi } from './auth.api.js';

let targetEmail = '';
let verifiedOtpCode = '';
let resendTimerId = null;

function setActiveStep(stepNumber) {
    runViewTransition(() => {
        qs('#fp-step1-form')?.classList.toggle('hidden', stepNumber !== 1);
        qs('#fp-step2-form')?.classList.toggle('hidden', stepNumber !== 2);
        qs('#fp-step3-form')?.classList.toggle('hidden', stepNumber !== 3);

        qsa('[data-step-indicator]').forEach((el) => {
            const idx = Number(el.dataset.stepIndicator);
            if (idx === stepNumber) {
                el.className = 'rounded-xl border border-indigo-500 bg-indigo-50 dark:bg-indigo-500/20 px-3 py-2 text-center transition';
            } else if (idx < stepNumber) {
                el.className = 'rounded-xl border border-emerald-400/70 bg-emerald-50/70 dark:bg-emerald-500/15 px-3 py-2 text-center transition';
            } else {
                el.className = 'rounded-xl border border-slate-200 dark:border-slate-800 bg-white/70 dark:bg-slate-900/60 px-3 py-2 text-center opacity-60 transition';
            }
        });
    });
}

function startResendCountdown(seconds = 60) {
    const resendBtn = qs('#fp-resend-btn');
    if (!resendBtn) return;

    if (resendTimerId) {
        clearInterval(resendTimerId);
    }

    let remaining = seconds;
    resendBtn.disabled = true;
    resendBtn.textContent = `Gửi lại mã (${remaining}s)`;

    resendTimerId = setInterval(() => {
        remaining -= 1;
        if (remaining <= 0) {
            clearInterval(resendTimerId);
            resendTimerId = null;
            resendBtn.disabled = false;
            resendBtn.textContent = 'Gửi lại mã mới';
        } else {
            resendBtn.textContent = `Gửi lại mã (${remaining}s)`;
        }
    }, 1000);
}

function fillOtpInputs(codeString) {
    const digits = String(codeString || '').replace(/\D/g, '').slice(0, 6);
    const inputs = qsa('[data-otp-index]');
    inputs.forEach((inp, idx) => {
        inp.value = digits[idx] || '';
    });
    if (digits.length === 6) {
        inputs[5]?.focus();
    }
}

function readOtpInputs() {
    return qsa('[data-otp-index]')
        .map((inp) => inp.value.trim())
        .join('');
}

function computePasswordStrength(pwd) {
    if (!pwd) return { score: 0, label: 'Nhập tối thiểu 6 ký tự.', color: 'bg-slate-200 dark:bg-slate-800' };
    let score = 0;
    if (pwd.length >= 6) score += 1;
    if (pwd.length >= 9) score += 1;
    if (/[A-Z]/.test(pwd) && /[0-9]/.test(pwd)) score += 1;
    if (/[^A-Za-z0-9]/.test(pwd)) score += 1;

    const configs = [
        { label: 'Mật khẩu quá ngắn', color: 'bg-rose-500' },
        { label: 'Độ mạnh: Trung bình', color: 'bg-amber-500' },
        { label: 'Độ mạnh: Khá tốt', color: 'bg-sky-500' },
        { label: 'Độ mạnh: Tốt', color: 'bg-indigo-500' },
        { label: 'Độ mạnh: Rất an toàn ✓', color: 'bg-emerald-500' }
    ];
    return { score: Math.max(1, score), ...configs[score] };
}

function initForgotPasswordPage() {
    initTheme();
    initOtaAutoSync();
    if (redirectGuestIfLoggedIn()) {
        return;
    }

    const backLogin = qs('#fp-back-login');
    const homeLink = qs('#fp-home-link');
    if (backLogin) backLogin.href = ROUTES.LOGIN;
    if (homeLink) homeLink.href = ROUTES.LANDING;

    qs('#fp-theme-toggle')?.addEventListener('click', () => toggleTheme());

    const requestOtpForEmail = async (email) => {
        const res = await authApi.forgotPassword(email);
        targetEmail = email;

        const maskedEl = qs('#fp-masked-email');
        if (maskedEl) maskedEl.textContent = res.maskedEmail || email;

        fillOtpInputs('');
        setActiveStep(2);
        startResendCountdown(60);

        toast.success(`Đã gửi mã xác thực 6 chữ số tới hộp thư ${res.maskedEmail || email}!`);
        setTimeout(() => qsa('[data-otp-index]')[0]?.focus(), 120);
    };

    // Bước 1: Gửi Email nhận OTP
    qs('#fp-step1-form')?.addEventListener('submit', async (e) => {
        e.preventDefault();
        const email = qs('#fp-email-input')?.value.trim() || '';
        const btn = qs('#fp-step1-btn');

        if (!isValidEmail(email)) {
            toast.warning('Vui lòng nhập địa chỉ Email hợp lệ.');
            return;
        }

        if (btn) {
            btn.disabled = true;
            btn.textContent = 'Đang gửi mã xác thực...';
        }

        try {
            await requestOtpForEmail(email);
        } catch (err) {
            toast.error(err.message || 'Không thể tạo mã khôi phục mật khẩu.');
        } finally {
            if (btn) {
                btn.disabled = false;
                btn.textContent = 'Nhận Mã Xác Thực 6 Số →';
            }
        }
    });

    // Tự động chuyển ô khi gõ hoặc dán 6 chữ số OTP
    const otpInputs = qsa('[data-otp-index]');
    otpInputs.forEach((inp, idx) => {
        inp.addEventListener('input', (e) => {
            const val = e.target.value.replace(/\D/g, '');
            e.target.value = val.slice(-1);
            if (e.target.value && idx < otpInputs.length - 1) {
                otpInputs[idx + 1].focus();
            }
        });

        inp.addEventListener('keydown', (e) => {
            if (e.key === 'Backspace' && !inp.value && idx > 0) {
                otpInputs[idx - 1].focus();
            }
        });

        inp.addEventListener('paste', (e) => {
            e.preventDefault();
            const pasted = e.clipboardData?.getData('text') || '';
            fillOtpInputs(pasted);
        });
    });

    qs('#fp-change-email-btn')?.addEventListener('click', () => {
        setActiveStep(1);
    });

    qs('#fp-resend-btn')?.addEventListener('click', async () => {
        if (!targetEmail) return;
        try {
            await requestOtpForEmail(targetEmail);
        } catch (err) {
            toast.error(err.message || 'Không thể gửi lại mã.');
        }
    });

    // Bước 2: Xác minh OTP 6 chữ số
    qs('#fp-step2-form')?.addEventListener('submit', async (e) => {
        e.preventDefault();
        const code = readOtpInputs();
        const btn = qs('#fp-step2-btn');

        if (code.length !== 6) {
            toast.warning('Vui lòng nhập đủ 6 chữ số mã xác thực OTP.');
            return;
        }

        if (btn) {
            btn.disabled = true;
            btn.textContent = 'Đang kiểm tra mã OTP...';
        }

        try {
            await authApi.verifyResetOtp({ email: targetEmail, otpCode: code });
            verifiedOtpCode = code;
            toast.success('Xác thực mã OTP thành công! Hãy đặt mật khẩu mới.');
            setActiveStep(3);
            setTimeout(() => qs('#fp-new-password')?.focus(), 120);
        } catch (err) {
            toast.error(err.message || 'Mã xác thực OTP không hợp lệ.');
        } finally {
            if (btn) {
                btn.disabled = false;
                btn.textContent = 'Xác nhận Mã OTP →';
            }
        }
    });

    // Thanh đo độ mạnh mật khẩu & nút Ẩn/Hiện mật khẩu ở Bước 3
    const newPwdInput = qs('#fp-new-password');
    newPwdInput?.addEventListener('input', () => {
        const info = computePasswordStrength(newPwdInput.value);
        const bars = qsa('#fp-strength-bars span');
        bars.forEach((bar, i) => {
            bar.className = `h-1.5 rounded-full transition ${i < info.score ? info.color : 'bg-slate-200 dark:bg-slate-800'}`;
        });
        const label = qs('#fp-strength-label');
        if (label) label.textContent = info.label;
    });

    qs('#fp-toggle-pwd')?.addEventListener('click', () => {
        if (!newPwdInput) return;
        newPwdInput.type = newPwdInput.type === 'password' ? 'text' : 'password';
    });

    // Bước 3: Lưu mật khẩu mới
    qs('#fp-step3-form')?.addEventListener('submit', async (e) => {
        e.preventDefault();
        const newPassword = newPwdInput?.value || '';
        const confirmNewPassword = qs('#fp-confirm-password')?.value || '';
        const btn = qs('#fp-step3-btn');

        if (!isValidPassword(newPassword)) {
            toast.warning('Mật khẩu mới phải có ít nhất 6 ký tự.');
            return;
        }
        if (newPassword !== confirmNewPassword) {
            toast.warning('Xác nhận mật khẩu mới không khớp.');
            return;
        }

        if (btn) {
            btn.disabled = true;
            btn.textContent = 'Đang cập nhật mật khẩu...';
        }

        try {
            await authApi.resetPassword({
                email: targetEmail,
                otpCode: verifiedOtpCode,
                newPassword,
                confirmNewPassword
            });
            toast.success('Đặt lại mật khẩu thành công! Đang chuyển tới trang Đăng nhập...');
            setTimeout(() => {
                window.location.href = ROUTES.LOGIN;
            }, 900);
        } catch (err) {
            toast.error(err.message || 'Không thể đặt lại mật khẩu.');
            if (btn) {
                btn.disabled = false;
                btn.textContent = 'Lưu Mật khẩu Mới & Đăng nhập';
            }
        }
    });
}

document.addEventListener('DOMContentLoaded', initForgotPasswordPage);
