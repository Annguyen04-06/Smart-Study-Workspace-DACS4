/**
 * Controller Cổng Quản trị viên (pages/admin/dashboard.html):
 * Điều phối 4 thẻ KPI, 2 biểu đồ Chart.js và 4 Tab Quản trị (Người dùng, Phòng học, Kho PDF, Nhật ký AI).
 */

import { requireAdmin } from '../../core/guards/routeGuard.js';
import { mountNavbar } from '../../shared/components/navbar.component.js';
import { toast } from '../../shared/components/toast.component.js';
import { debounce, qs, qsa } from '../../shared/utils/dom.utils.js';
import { initInteractiveEffects } from '../../shared/utils/spotlight.utils.js';
import {
    animateNumberCounter,
    runViewTransition
} from '../../shared/utils/theme.utils.js';
import { adminApi } from './admin.api.js';
import { renderAdminAnalyticsCharts } from './adminCharts.view.js';
import {
    renderAdminAiLogsTable,
    renderAdminMaterialsTable,
    renderAdminRoomsTable,
    renderAdminUsersTable
} from './adminTables.view.js';

let activeTab = 'users';
let searchKeyword = '';
let currentAdminUser = null;

async function loadOverviewAndCharts() {
    const overview = await adminApi.getOverview();

    const setKpi = (selector, value) => {
        const el = qs(selector);
        if (el) el.textContent = value;
    };

    animateNumberCounter(qs('#admin-kpi-users'), overview.totalUsers);
    setKpi('#admin-kpi-users-sub', `${overview.activeStudents} sinh viên hoạt động • ${overview.lockedUsers} bị khóa`);

    animateNumberCounter(qs('#admin-kpi-rooms'), overview.activeRooms);
    setKpi('#admin-kpi-rooms-sub', `Trên tổng số ${overview.totalRooms} phòng (có phòng chờ duyệt)`);

    animateNumberCounter(qs('#admin-kpi-resources'), overview.totalMaterials + overview.totalQuizzes + overview.totalFlashcardDecks);
    setKpi('#admin-kpi-resources-sub', `${overview.totalMaterials} PDF • ${overview.totalQuizzes} Đề thi • ${overview.totalFlashcardDecks} Bộ thẻ`);

    animateNumberCounter(qs('#admin-kpi-tokens'), overview.totalAiRequests);
    setKpi('#admin-kpi-tokens-sub', `Tỷ lệ phản hồi thành công ${overview.successRatePercent}%`);

    renderAdminAnalyticsCharts(overview.dailyTokenUsage || [], overview.featureBreakdown || []);
}

async function loadActiveTabTable() {
    const container = qs('#admin-table-container');
    if (!container) return;

    container.innerHTML = `<div class="space-y-2.5"><div class="skeleton-box h-12 w-full"></div><div class="skeleton-box h-12 w-full"></div><div class="skeleton-box h-12 w-full"></div></div>`;

    try {
        let html = '';
        if (activeTab === 'users') {
            const paged = await adminApi.getUsers({ search: searchKeyword, pageSize: 25 });
            html = renderAdminUsersTable(paged.items || [], currentAdminUser.id);
        } else if (activeTab === 'rooms') {
            const paged = await adminApi.getRooms({ search: searchKeyword, pageSize: 25 });
            html = renderAdminRoomsTable(paged.items || []);
        } else if (activeTab === 'materials') {
            const paged = await adminApi.getMaterials({ search: searchKeyword, pageSize: 25 });
            html = renderAdminMaterialsTable(paged.items || []);
        } else if (activeTab === 'ailogs') {
            const featureFilter = qs('#admin-feature-filter')?.value || '';
            const paged = await adminApi.getAiLogs({ featureType: featureFilter, pageSize: 35 });
            html = renderAdminAiLogsTable(paged.items || []);
        }
        runViewTransition(() => {
            container.innerHTML = html;
        }, container);
    } catch (err) {
        toast.error(err.message || 'Không thể tải dữ liệu bảng quản trị.');
    }
}

async function initAdminDashboard() {
    initInteractiveEffects();
    currentAdminUser = requireAdmin();
    if (!currentAdminUser) return;

    mountNavbar('#app-navbar', 'admin');

    try {
        await Promise.all([loadOverviewAndCharts(), loadActiveTabTable()]);
    } catch (err) {
        toast.error(err.message || 'Không thể tải bảng điều khiển Quản trị viên.');
    }

    // Chuyển đổi giữa 4 Tab quản trị
    qsa('[data-admin-tab]').forEach((tabBtn) => {
        tabBtn.addEventListener('click', () => {
            activeTab = tabBtn.dataset.adminTab;
            qsa('[data-admin-tab]').forEach((btn) => {
                const isActive = btn === tabBtn;
                btn.className = isActive
                    ? 'rounded-xl bg-indigo-600 px-4 py-2 text-xs font-semibold text-white shadow-xs transition'
                    : 'rounded-xl px-4 py-2 text-xs font-semibold text-slate-600 dark:text-slate-300 hover:bg-slate-100 dark:hover:bg-slate-800 transition';
            });

            qs('#admin-search-wrap')?.classList.toggle('hidden', activeTab === 'ailogs');
            qs('#admin-ailog-filter-wrap')?.classList.toggle('hidden', activeTab !== 'ailogs');
            loadActiveTabTable();
        });
    });

    qs('#admin-search-input')?.addEventListener(
        'input',
        debounce((e) => {
            searchKeyword = e.target.value.trim();
            loadActiveTabTable();
        }, 280)
    );

    qs('#admin-feature-filter')?.addEventListener('change', () => {
        loadActiveTabTable();
    });

    qs('#admin-refresh-btn')?.addEventListener('click', async () => {
        await Promise.all([loadOverviewAndCharts(), loadActiveTabTable()]);
        toast.info('Đã làm mới số liệu hệ thống.', 1800);
    });

    // Event Delegation cho các nút thao tác trên cả 4 bảng
    const tableContainer = qs('#admin-table-container');

    tableContainer?.addEventListener('click', async (e) => {
        const btn = e.target.closest('[data-admin-action]');
        if (!btn || btn.tagName === 'SELECT') return;

        const action = btn.dataset.adminAction;

        try {
            if (action === 'toggle-user-lock') {
                const updated = await adminApi.toggleUserLock(Number(btn.dataset.userId));
                toast.success(updated.isLocked ? `Đã khóa tài khoản "${updated.fullName}".` : `Đã mở khóa "${updated.fullName}".`);
                await Promise.all([loadOverviewAndCharts(), loadActiveTabTable()]);
            } else if (action === 'delete-room') {
                if (!window.confirm('Xác nhận xóa vĩnh viễn phòng học này và các dữ liệu bên trong?')) return;
                await adminApi.deleteRoom(Number(btn.dataset.roomId));
                toast.success('Đã xóa phòng học.');
                await Promise.all([loadOverviewAndCharts(), loadActiveTabTable()]);
            } else if (action === 'toggle-material-approval') {
                const updated = await adminApi.toggleMaterialApproval(Number(btn.dataset.materialId));
                toast.success(updated.isApproved ? 'Đã phê duyệt hiển thị tài liệu PDF.' : 'Đã tạm ẩn tài liệu PDF.');
                await loadActiveTabTable();
            } else if (action === 'delete-material') {
                if (!window.confirm('Xóa vĩnh viễn tệp PDF này khỏi máy chủ?')) return;
                await adminApi.deleteMaterial(Number(btn.dataset.materialId));
                toast.success('Đã xóa tệp học liệu PDF.');
                await Promise.all([loadOverviewAndCharts(), loadActiveTabTable()]);
            }
        } catch (err) {
            toast.error(err.message || 'Thao tác quản trị thất bại.');
        }
    });

    tableContainer?.addEventListener('change', async (e) => {
        const selectEl = e.target.closest('select[data-admin-action="change-room-status"]');
        if (!selectEl) return;

        try {
            const updated = await adminApi.updateRoomStatus(Number(selectEl.dataset.roomId), selectEl.value);
            toast.success(`Phòng "${updated.name}" đã chuyển sang trạng thái ${updated.status}.`);
            await loadOverviewAndCharts();
        } catch (err) {
            toast.error(err.message || 'Không thể đổi trạng thái phòng.');
            await loadActiveTabTable();
        }
    });
}

document.addEventListener('DOMContentLoaded', initAdminDashboard);
