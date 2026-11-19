/**
 * Controller trang Dashboard Phòng học Sinh viên (pages/student/dashboard.html):
 * Điều phối tìm kiếm, lọc môn học, tạo phòng mới, tham gia nhanh bằng InviteCode và chuyển sang Workspace.
 */

import { buildWorkspaceUrl } from '../../core/config/app.config.js';
import { requireAuth } from '../../core/guards/routeGuard.js';
import { offlineDb } from '../../core/storage/offlineDb.service.js';
import { registerWorkspacePaletteActions } from '../../shared/components/commandPalette.component.js';
import { renderEmptyState } from '../../shared/components/emptyState.component.js';
import { closeModal, openModal } from '../../shared/components/modal.component.js';
import { mountNavbar } from '../../shared/components/navbar.component.js';
import { onServerStatusChange } from '../../shared/components/serverHealth.component.js';
import { toast } from '../../shared/components/toast.component.js';
import {
    copyTextToClipboard,
    debounce,
    escapeHtml,
    qs,
    qsa
} from '../../shared/utils/dom.utils.js';
import {
    applyStaggerEntrance,
    initInteractiveEffects
} from '../../shared/utils/spotlight.utils.js';
import { runViewTransition } from '../../shared/utils/theme.utils.js';
import { renderRoomCard, renderRoomSkeletons } from './roomCard.view.js';
import { roomsApi } from './rooms.api.js';

const state = {
    search: '',
    subjectCategory: '',
    onlyJoined: false,
    pageNumber: 1,
    pageSize: 30
};

async function loadRoomsGrid() {
    const gridEl = qs('#rooms-grid');
    const summaryEl = qs('#rooms-count-summary');
    if (!gridEl) return;

    gridEl.innerHTML = renderRoomSkeletons(6);

    try {
        const paged = await roomsApi.getRooms(state);
        const items = paged.items || [];

        // Lưu ngầm danh sách phòng học vào IndexedDB phục vụ Offline-First Mode
        offlineDb.saveRooms(items).catch(() => {});

        if (summaryEl) {
            summaryEl.textContent = `Hiển thị ${items.length} / ${paged.totalCount} phòng học`;
        }

        registerWorkspacePaletteActions(() => [
            {
                group: 'Không gian Phòng học',
                title: 'Khởi tạo Phòng học Nhóm mới',
                subtitle: 'Tạo phòng học mới theo môn học và mời bạn bè tham gia',
                badge: 'Tạo mới',
                run: () => openModal('create-room-modal')
            }
        ]);

        if (items.length === 0) {
            runViewTransition(() => {
                gridEl.className = 'mt-6';
                gridEl.innerHTML = renderEmptyState({
                    title: 'Không tìm thấy phòng học phù hợp',
                    description: 'Hãy thử đổi từ khóa tìm kiếm, nhập mã mời từ bạn bè (ví dụ: NET101) hoặc khởi tạo phòng học nhóm mới.',
                    actionLabel: 'Tạo phòng học mới',
                    actionId: 'open-create-room-modal'
                });
            });
            return;
        }

        runViewTransition(() => {
            gridEl.className = 'mt-6 grid grid-cols-1 gap-5 sm:grid-cols-2 lg:grid-cols-3';
            gridEl.innerHTML = items.map(renderRoomCard).join('');
            applyStaggerEntrance(gridEl);
        });
    } catch (err) {
        // Khi mất kết nối máy chủ: đọc lại danh sách phòng đã lưu trong IndexedDB để tiếp tục ôn tập ngoại tuyến
        const cachedRooms = await offlineDb.getCachedRooms();
        const filteredCached = cachedRooms.filter((r) => {
            if (state.onlyJoined && !r.isMember) return false;
            if (state.subjectCategory && r.subjectCategory !== state.subjectCategory) return false;
            if (state.search) {
                const kw = state.search.toLowerCase();
                return (
                    String(r.name || '').toLowerCase().includes(kw) ||
                    String(r.subjectCategory || '').toLowerCase().includes(kw) ||
                    String(r.inviteCode || '').toLowerCase().includes(kw)
                );
            }
            return true;
        });

        if (filteredCached.length > 0) {
            if (summaryEl) {
                summaryEl.textContent = `Chế độ Ngoại tuyến — Đang hiển thị ${filteredCached.length} phòng đã lưu trong bộ nhớ đệm`;
            }
            gridEl.className = 'mt-6 grid grid-cols-1 gap-5 sm:grid-cols-2 lg:grid-cols-3';
            gridEl.innerHTML = filteredCached.map(renderRoomCard).join('');
            applyStaggerEntrance(gridEl);
            return;
        }

        gridEl.className = 'mt-6';
        gridEl.innerHTML = renderEmptyState({
            title: 'Chưa tải được danh sách phòng học',
            description: err.message || 'Không thể kết nối tới máy chủ. Vui lòng tải lại trang sau giây lát.'
        });
    }
}

function initDashboardPage() {
    initInteractiveEffects();
    const currentUser = requireAuth();
    if (!currentUser) return;

    mountNavbar('#app-navbar', 'dashboard');

    const welcomeEl = qs('#dashboard-welcome-name');
    if (welcomeEl) {
        welcomeEl.textContent = currentUser.fullName;
    }

    // 1. Tìm kiếm Debounce
    const searchInput = qs('#room-search-input');
    searchInput?.addEventListener(
        'input',
        debounce((e) => {
            state.search = e.target.value.trim();
            state.pageNumber = 1;
            loadRoomsGrid();
        }, 280)
    );

    // 2. Lọc theo chuyên ngành / môn học
    const subjectSelect = qs('#room-subject-filter');
    subjectSelect?.addEventListener('change', (e) => {
        state.subjectCategory = e.target.value;
        state.pageNumber = 1;
        loadRoomsGrid();
    });

    // 3. Chuyển đổi Tất cả phòng mở / Phòng đã tham gia
    qsa('[data-scope-tab]').forEach((tabBtn) => {
        tabBtn.addEventListener('click', () => {
            state.onlyJoined = tabBtn.dataset.scopeTab === 'joined';
            state.pageNumber = 1;

            qsa('[data-scope-tab]').forEach((btn) => {
                const isActive = btn === tabBtn;
                btn.className = isActive
                    ? 'rounded-lg bg-white dark:bg-slate-800 px-3.5 py-1.5 text-xs font-semibold text-indigo-700 dark:text-indigo-300 shadow-sm'
                    : 'rounded-lg px-3.5 py-1.5 text-xs font-medium text-slate-600 dark:text-slate-400 hover:text-slate-900 dark:hover:text-slate-100 transition';
            });

            loadRoomsGrid();
        });
    });

    // 4. Nút mở Modal Tạo phòng mới
    qs('#btn-open-create-room')?.addEventListener('click', () => openModal('create-room-modal'));

    // 5. Thanh nhập nhanh Mã mời InviteCode (hỗ trợ cả dạng có dấu # đầu chuỗi như #OS209 hoặc OS209)
    const quickJoinForm = qs('#quick-join-code-form');
    quickJoinForm?.addEventListener('submit', async (event) => {
        event.preventDefault();
        const codeInput = qs('#quick-invite-code-input');
        const rawCode = codeInput?.value || '';
        const cleanCode = rawCode.replace(/^#+/, '').replace(/\s+/g, '').toUpperCase();
        if (!cleanCode) {
            toast.warning('Vui lòng nhập mã mời phòng học (ví dụ: OS209, SEC308, CLD405).');
            codeInput?.focus();
            return;
        }

        try {
            const joinedRoom = await roomsApi.joinByInviteCode(cleanCode);
            toast.success(`Đã vào phòng "${joinedRoom.name}"!`);
            window.location.href = buildWorkspaceUrl(joinedRoom.id);
        } catch (err) {
            toast.error(err.message || 'Mã mời không hợp lệ.');
        }
    });

    // 6. Xử lý Form Tạo Phòng Học Mới
    const createForm = qs('#create-room-form');
    createForm?.addEventListener('submit', async (event) => {
        event.preventDefault();
        const name = qs('#create-room-name')?.value.trim() || '';
        const subjectCategory = qs('#create-room-subject')?.value.trim() || '';
        const description = qs('#create-room-desc')?.value.trim() || '';
        const submitBtn = qs('#create-room-submit-btn');

        if (name.length < 3) {
            toast.warning('Tên phòng học phải từ 3 ký tự trở lên.');
            return;
        }
        if (!subjectCategory) {
            toast.warning('Vui lòng chọn hoặc nhập chuyên ngành môn học.');
            return;
        }

        if (submitBtn) {
            submitBtn.disabled = true;
            submitBtn.textContent = 'Đang khởi tạo...';
        }

        try {
            const created = await roomsApi.createRoom({ name, subjectCategory, description });
            closeModal('create-room-modal');
            createForm.reset();
            toast.success(`Đã tạo phòng "${created.name}" (Mã mời: ${created.inviteCode}).`);
            window.location.href = buildWorkspaceUrl(created.id);
        } catch (err) {
            toast.error(err.message || 'Không thể tạo phòng học.');
        } finally {
            if (submitBtn) {
                submitBtn.disabled = false;
                submitBtn.textContent = 'Tạo phòng & Mở Workspace';
            }
        }
    });

    // 7. Event Delegation trên danh sách thẻ phòng học
    qs('#rooms-grid')?.addEventListener('click', async (event) => {
        const openModalTrigger = event.target.closest('[data-action="open-create-room-modal"]');
        if (openModalTrigger) {
            openModal('create-room-modal');
            return;
        }

        const copyBtn = event.target.closest('[data-action="copy-invite"]');
        if (copyBtn) {
            const code = copyBtn.dataset.inviteCode || '';
            await copyTextToClipboard(code);
            toast.success(`Đã sao chép mã mời #${escapeHtml(code)} vào bộ nhớ tạm.`);
            return;
        }

        const enterBtn = event.target.closest('[data-action="enter-room"]');
        if (enterBtn) {
            window.location.href = buildWorkspaceUrl(enterBtn.dataset.roomId);
            return;
        }

        const joinBtn = event.target.closest('[data-action="join-room"]');
        if (joinBtn) {
            const roomId = joinBtn.dataset.roomId;
            joinBtn.disabled = true;
            joinBtn.textContent = 'Đang vào...';
            try {
                const joined = await roomsApi.joinByRoomId(roomId);
                toast.success(`Chào mừng bạn vào phòng "${joined.name}"!`);
                window.location.href = buildWorkspaceUrl(joined.id);
            } catch (err) {
                toast.error(err.message || 'Không thể tham gia phòng học.');
                joinBtn.disabled = false;
                joinBtn.textContent = 'Tham gia ngay';
            }
        }
    });

    loadRoomsGrid();

    // Khi máy chủ vừa online trở lại sau khi mất mạng -> tự động đồng bộ lại danh sách phòng học
    onServerStatusChange((online) => {
        if (online) {
            loadRoomsGrid();
        }
    });
}

document.addEventListener('DOMContentLoaded', initDashboardPage);
