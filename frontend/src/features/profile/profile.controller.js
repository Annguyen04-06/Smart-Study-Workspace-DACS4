/**
 * Controller trang Hồ sơ Cá nhân Sinh viên (pages/student/profile.html):
 * Cập nhật thông tin cá nhân, tải lên ảnh đại diện Avatar, đổi mật khẩu và xem lại chi tiết bài thi cũ.
 */

import { ROUTES, buildWorkspaceUrl, resolveBackendFileUrl } from '../../core/config/app.config.js';
import { requireAuth } from '../../core/guards/routeGuard.js';
import { appStore } from '../../core/state/appStore.js';
import { offlineDb } from '../../core/storage/offlineDb.service.js';
import { openModal } from '../../shared/components/modal.component.js';
import { mountNavbar } from '../../shared/components/navbar.component.js';
import { toast } from '../../shared/components/toast.component.js';
import { copyTextToClipboard, escapeHtml, getInitials, qs } from '../../shared/utils/dom.utils.js';
import { initInteractiveEffects } from '../../shared/utils/spotlight.utils.js';
import { animateNumberCounter } from '../../shared/utils/theme.utils.js';
import { isValidPassword, validateAvatarFile } from '../../shared/utils/validation.utils.js';
import {
    renderAttemptHistoryTable,
    renderAttemptReviewModalContent
} from './attemptHistory.view.js';
import { profileApi } from './profile.api.js';
import {
    mountWeaknessRadarChart,
    renderWeaknessRadarMarkup,
    renderWeaknessRadarSkeleton
} from './weaknessRadar.view.js';

function renderProfileHeader(profile) {
    const avatarWrap = qs('#profile-avatar-preview');
    if (avatarWrap) {
        avatarWrap.innerHTML = profile.avatarUrl
            ? `<img src="${escapeHtml(resolveBackendFileUrl(profile.avatarUrl))}" alt="Avatar" class="h-20 w-20 rounded-2xl object-cover ring-4 ring-indigo-50" />`
            : `<div class="flex h-20 w-20 items-center justify-center rounded-2xl bg-indigo-600 text-2xl font-bold text-white ring-4 ring-indigo-50">${escapeHtml(getInitials(profile.fullName))}</div>`;
    }

    const nameEl = qs('#profile-display-name');
    const emailEl = qs('#profile-display-email');
    const fullNameInput = qs('#profile-fullname-input');
    const studentCodeInput = qs('#profile-studentcode-input');

    if (nameEl) nameEl.textContent = profile.fullName;
    if (emailEl) emailEl.textContent = `${profile.email} • ${profile.role === 'Admin' ? 'Quản trị viên' : (profile.studentCode || 'Sinh viên')}`;
    if (fullNameInput) fullNameInput.value = profile.fullName || '';
    if (studentCodeInput) studentCodeInput.value = profile.studentCode || '';

    const kpiRooms = qs('#kpi-joined-rooms');
    const kpiQuizzes = qs('#kpi-completed-quizzes');
    const kpiScore = qs('#kpi-avg-score');
    animateNumberCounter(kpiRooms, profile.joinedRoomsCount ?? 0);
    animateNumberCounter(kpiQuizzes, profile.completedQuizzesCount ?? 0);
    animateNumberCounter(kpiScore, Number(profile.averageQuizScore || 0), { decimals: 2, suffix: ' / 10' });
}

async function initProfilePage() {
    initInteractiveEffects();
    const sessionUser = requireAuth();
    if (!sessionUser) return;

    mountNavbar('#app-navbar', 'profile');

    const radarContainer = qs('#profile-weakness-radar-container');
    const runRadarBtn = qs('#btn-run-weakness-radar');
    let latestWeaknessData = null;

    const loadWeaknessRadar = async () => {
        if (!radarContainer) return;
        radarContainer.innerHTML = renderWeaknessRadarSkeleton();
        if (runRadarBtn) {
            runRadarBtn.disabled = true;
            runRadarBtn.textContent = 'AI đang chẩn đoán...';
        }
        try {
            const analysis = await profileApi.analyzeWeakness(null);
            latestWeaknessData = analysis;
            radarContainer.innerHTML = renderWeaknessRadarMarkup(analysis, {
                compact: false,
                canvasId: 'profile-weakness-chart'
            });
            mountWeaknessRadarChart('profile-weakness-chart', analysis.subjectRadar || []);
        } catch (err) {
            radarContainer.innerHTML = `
                <div class="rounded-xl border border-amber-200 dark:border-amber-500/30 bg-amber-50/70 dark:bg-amber-500/10 p-4 text-xs text-amber-800 dark:text-amber-200">
                    ${escapeHtml(err.message || 'Cần kết nối máy chủ để chạy chẩn đoán AI Smart Weakness Radar.')}
                </div>
            `;
        } finally {
            if (runRadarBtn) {
                runRadarBtn.disabled = false;
                runRadarBtn.textContent = '✨ Quét & Chẩn đoán AI';
            }
        }
    };

    try {
        const [freshProfile, attempts] = await Promise.all([
            profileApi.getProfile(),
            profileApi.getMyQuizAttempts()
        ]);

        appStore.updateCurrentUser(freshProfile);
        renderProfileHeader(freshProfile);

        // Lưu ngầm danh sách lịch sử thi xuống IndexedDB để xem lại khi mất mạng
        offlineDb.saveQuizAttemptsList(attempts).catch(() => { });

        const historyContainer = qs('#attempt-history-container');
        if (historyContainer) {
            historyContainer.innerHTML = renderAttemptHistoryTable(attempts);
        }

        // Tự động chạy Radar chẩn đoán nếu sinh viên đã có lượt làm bài
        if (attempts.length > 0) {
            loadWeaknessRadar();
        } else if (radarContainer) {
            radarContainer.innerHTML = `
                <div class="rounded-xl border border-dashed border-indigo-200 dark:border-indigo-500/30 bg-indigo-50/40 dark:bg-indigo-500/5 p-4 text-center text-xs text-slate-600 dark:text-slate-300">
                    Nhấn <strong>"✨ Quét &amp; Chẩn đoán AI"</strong> hoặc hoàn thành 1 đề Quiz trong phòng học để kích hoạt Radar điểm mạnh – điểm yếu!
                </div>
            `;
        }
    } catch (err) {
        // Offline Fallback: hiển thị hồ sơ từ session + lịch sử thi đã lưu trong IndexedDB
        renderProfileHeader(sessionUser);
        const cachedAttempts = await offlineDb.getCachedQuizAttempts();
        const historyContainer = qs('#attempt-history-container');
        if (historyContainer) {
            historyContainer.innerHTML = renderAttemptHistoryTable(cachedAttempts);
        }
        if (cachedAttempts.length > 0) {
            toast.info(`Đang ở chế độ Ngoại tuyến — Đã nạp ${cachedAttempts.length} lịch sử bài thi từ bộ nhớ đệm.`);
        } else {
            toast.error(err.message || 'Không thể tải dữ liệu hồ sơ.');
        }
    }

    runRadarBtn?.addEventListener('click', loadWeaknessRadar);

    // Xử lý sự kiện trên AI Smart Weakness Radar (Phân tích lại / Hỏi @bot / Tạo ngay đề Quiz 5 câu vá lỗ hổng)
    radarContainer?.addEventListener('click', async (event) => {
        const actionBtn = event.target.closest('[data-weakness-action]');
        if (!actionBtn) return;

        const action = actionBtn.dataset.weaknessAction;
        if (action === 'refresh') {
            await loadWeaknessRadar();
            return;
        }

        if (action === 'ask-bot') {
            const promptText = actionBtn.dataset.prompt || '';
            await copyTextToClipboard(promptText);
            const targetRoomId = latestWeaknessData?.suggestedRoomId;
            if (targetRoomId) {
                toast.success('Đã sao chép câu hỏi @bot — đang chuyển sang phòng học...');
                window.location.href = `${buildWorkspaceUrl(targetRoomId)}&botPrompt=${encodeURIComponent(promptText)}`;
            } else {
                toast.success('Đã sao chép câu hỏi gợi ý @bot vào bộ nhớ tạm!');
            }
            return;
        }

        if (action === 'generate-remedial') {
            const roomId = Number(actionBtn.dataset.roomId || 0) || null;
            const materialId = Number(actionBtn.dataset.materialId || 0) || null;
            let focusTopics = [];
            try {
                focusTopics = JSON.parse(actionBtn.dataset.focusTopics || '[]');
            } catch {
                focusTopics = [];
            }

            actionBtn.disabled = true;
            const prevHtml = actionBtn.innerHTML;
            actionBtn.innerHTML = '<span>⏳ Gemini AI đang biên soạn đề 5 câu vá lỗ hổng...</span>';

            try {
                const createdQuiz = await profileApi.generateRemedialQuiz({
                    roomId,
                    materialId,
                    focusTopics,
                    questionCount: 5
                });
                toast.success(`Đã tạo xong "${createdQuiz.title}"! Đang mở phòng thi ngay...`);
                window.location.href = `${buildWorkspaceUrl(createdQuiz.roomId)}&tab=quiz&autoQuizId=${createdQuiz.id}`;
            } catch (err) {
                toast.error(err.message || 'Không thể tạo đề Quiz vá lỗ hổng lúc này.');
                actionBtn.disabled = false;
                actionBtn.innerHTML = prevHtml;
            }
        }
    });

    // 1. Đổi ảnh đại diện Avatar
    const avatarInput = qs('#avatar-file-input');
    avatarInput?.addEventListener('change', async (event) => {
        const file = event.target.files?.[0];
        if (!file) return;

        const check = validateAvatarFile(file);
        if (!check.valid) {
            toast.warning(check.message);
            avatarInput.value = '';
            return;
        }

        try {
            toast.info('Đang tải lên ảnh đại diện mới...', 1800);
            const updatedProfile = await profileApi.uploadAvatar(file);
            appStore.updateCurrentUser(updatedProfile);
            renderProfileHeader(updatedProfile);
            toast.success('Đã cập nhật ảnh đại diện thành công!');
        } catch (err) {
            toast.error(err.message || 'Tải lên ảnh đại diện thất bại.');
        } finally {
            avatarInput.value = '';
        }
    });

    // 2. Cập nhật Họ tên & MSSV
    qs('#profile-info-form')?.addEventListener('submit', async (event) => {
        event.preventDefault();
        const fullName = qs('#profile-fullname-input')?.value.trim() || '';
        const studentCode = qs('#profile-studentcode-input')?.value.trim() || '';

        if (fullName.length < 2) {
            toast.warning('Họ và tên phải có ít nhất 2 ký tự.');
            return;
        }

        try {
            const updated = await profileApi.updateProfile({ fullName, studentCode: studentCode || null });
            appStore.updateCurrentUser(updated);
            renderProfileHeader(updated);
            toast.success('Đã lưu thông tin hồ sơ cá nhân.');
        } catch (err) {
            toast.error(err.message || 'Cập nhật hồ sơ thất bại.');
        }
    });

    // 3. Đổi mật khẩu
    const pwdForm = qs('#change-password-form');
    pwdForm?.addEventListener('submit', async (event) => {
        event.preventDefault();
        const currentPassword = qs('#pwd-current')?.value || '';
        const newPassword = qs('#pwd-new')?.value || '';
        const confirmNewPassword = qs('#pwd-confirm')?.value || '';

        if (!isValidPassword(newPassword)) {
            toast.warning('Mật khẩu mới phải từ 6 ký tự trở lên.');
            return;
        }
        if (newPassword !== confirmNewPassword) {
            toast.warning('Xác nhận mật khẩu mới không khớp.');
            return;
        }

        try {
            await profileApi.changePassword({ currentPassword, newPassword, confirmNewPassword });
            pwdForm.reset();
            toast.success('Đổi mật khẩu đăng nhập thành công!');
        } catch (err) {
            toast.error(err.message || 'Không thể đổi mật khẩu.');
        }
    });

    // 4. Mở Modal Xem lại lời giải chi tiết bài thi cũ
    qs('#attempt-history-container')?.addEventListener('click', async (event) => {
        const btn = event.target.closest('[data-action="view-attempt-detail"]');
        if (!btn) return;

        const attemptId = btn.dataset.attemptId;
        const bodyEl = qs('#attempt-review-modal-body');
        if (bodyEl) {
            bodyEl.innerHTML = `<div class="space-y-3 py-4"><div class="skeleton-box h-16 w-full"></div><div class="skeleton-box h-28 w-full"></div></div>`;
        }
        openModal('attempt-review-modal');

        try {
            const detail = await profileApi.getAttemptDetail(attemptId);
            // Lưu lời giải chi tiết xuống IndexedDB để sinh viên xem lại được cả khi mất mạng
            offlineDb.saveQuizAttemptDetail(detail).catch(() => { });
            if (bodyEl) {
                bodyEl.innerHTML = renderAttemptReviewModalContent(detail);
            }
        } catch (err) {
            const cachedDetail = await offlineDb.getCachedQuizAttemptDetail(attemptId);
            if (cachedDetail && bodyEl) {
                bodyEl.innerHTML = renderAttemptReviewModalContent(cachedDetail);
                toast.info('Đang hiển thị lời giải chi tiết đã lưu ngoại tuyến.');
                return;
            }
            if (bodyEl) {
                bodyEl.innerHTML = `<p class="text-sm text-rose-600">${escapeHtml(err.message)}</p>`;
            }
        }
    });
}

document.addEventListener('DOMContentLoaded', initProfilePage);
