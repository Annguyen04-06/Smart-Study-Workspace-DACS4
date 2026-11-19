/**
 * Tầng View hiển thị 4 bảng quản trị sạch đẹp (Người dùng, Phòng học, Kho Học liệu PDF, Lịch sử Hoạt động AI).
 */

import { resolveBackendFileUrl } from '../../core/config/app.config.js';
import { renderEmptyState } from '../../shared/components/emptyState.component.js';
import { escapeHtml } from '../../shared/utils/dom.utils.js';
import {
    formatDateTime,
    formatFileSize
} from '../../shared/utils/format.utils.js';

export function renderAdminUsersTable(users = [], currentAdminId = 0) {
    if (!users.length) {
        return renderEmptyState({ title: 'Không tìm thấy người dùng', description: 'Thử thay đổi bộ lọc tìm kiếm.' });
    }

    const rows = users
        .map((u) => {
            const isSelf = Number(u.id) === Number(currentAdminId);
            const statusBadge = u.isLocked
                ? `<span class="rounded-full bg-rose-100 dark:bg-rose-500/20 px-2.5 py-0.5 text-[11px] font-semibold text-rose-700 dark:text-rose-300">Đã khóa</span>`
                : `<span class="rounded-full bg-emerald-100 dark:bg-emerald-500/20 px-2.5 py-0.5 text-[11px] font-semibold text-emerald-700 dark:text-emerald-300">Hoạt động</span>`;

            return `
                <tr class="hover:bg-slate-50/80 dark:hover:bg-slate-800/50">
                    <td class="px-4 py-3">
                        <p class="text-sm font-semibold text-slate-900 dark:text-slate-100">${escapeHtml(u.fullName)}</p>
                        <p class="text-xs text-slate-500 dark:text-slate-400">${escapeHtml(u.email)}</p>
                    </td>
                    <td class="px-4 py-3 font-mono text-xs text-slate-600 dark:text-slate-300">${escapeHtml(u.studentCode || '—')}</td>
                    <td class="px-4 py-3 text-xs font-semibold ${u.role === 'Admin' ? 'text-indigo-700 dark:text-indigo-300' : 'text-slate-600 dark:text-slate-300'}">${u.role === 'Admin' ? 'Quản trị viên' : 'Sinh viên'}</td>
                    <td class="px-4 py-3 text-xs text-slate-600 dark:text-slate-300">${u.joinedRoomsCount} phòng</td>
                    <td class="px-4 py-3">${statusBadge}</td>
                    <td class="px-4 py-3 text-xs text-slate-500 dark:text-slate-400">${escapeHtml(formatDateTime(u.createdAt))}</td>
                    <td class="px-4 py-3 text-right">
                        ${isSelf
                            ? `<span class="text-xs text-slate-400 italic">Tài khoản hiện tại</span>`
                            : `<button type="button" data-admin-action="toggle-user-lock" data-user-id="${u.id}"
                                   class="rounded-lg border px-3 py-1.5 text-xs font-semibold transition ${
                                       u.isLocked
                                           ? 'border-emerald-200 dark:border-emerald-800 bg-emerald-50 dark:bg-emerald-950/40 text-emerald-700 dark:text-emerald-300 hover:bg-emerald-600 hover:text-white'
                                           : 'border-rose-200 dark:border-rose-800 bg-rose-50 dark:bg-rose-950/40 text-rose-700 dark:text-rose-300 hover:bg-rose-600 hover:text-white'
                                   }">
                                   ${u.isLocked ? 'Mở khóa' : 'Khóa TK'}
                               </button>`}
                    </td>
                </tr>
            `;
        })
        .join('');

    return wrapTable(
        ['Họ tên & Email', 'MSSV', 'Vai trò', 'Tham gia', 'Trạng thái', 'Ngày tạo', 'Thao tác'],
        rows
    );
}

export function renderAdminRoomsTable(rooms = []) {
    if (!rooms.length) {
        return renderEmptyState({ title: 'Không có phòng học nào', description: 'Chưa có phòng học khớp điều kiện lọc.' });
    }

    const rows = rooms
        .map((r) => {
            return `
                <tr class="hover:bg-slate-50/80 dark:hover:bg-slate-800/50">
                    <td class="px-4 py-3">
                        <p class="text-sm font-semibold text-slate-900 dark:text-slate-100">${escapeHtml(r.name)}</p>
                        <p class="text-xs text-slate-500 dark:text-slate-400">${escapeHtml(r.subjectCategory)} &bull; Trưởng phòng: ${escapeHtml(r.createdByName)}</p>
                    </td>
                    <td class="px-4 py-3 font-mono text-xs font-semibold text-indigo-700 dark:text-indigo-300">#${escapeHtml(r.inviteCode)}</td>
                    <td class="px-4 py-3 text-xs text-slate-600 dark:text-slate-300">${r.memberCount} TV / ${r.materialCount} PDF</td>
                    <td class="px-4 py-3">
                        <select data-admin-action="change-room-status" data-room-id="${r.id}"
                            class="rounded-lg border border-slate-200 dark:border-slate-700 bg-white dark:bg-slate-800 px-2.5 py-1 text-xs font-semibold text-slate-700 dark:text-slate-200">
                            <option value="Active" ${r.status === 'Active' ? 'selected' : ''}>Đang mở</option>
                            <option value="Pending" ${r.status === 'Pending' ? 'selected' : ''}>Chờ phê duyệt</option>
                            <option value="Closed" ${r.status === 'Closed' ? 'selected' : ''}>Đã đóng</option>
                        </select>
                    </td>
                    <td class="px-4 py-3 text-xs text-slate-500 dark:text-slate-400">${escapeHtml(formatDateTime(r.createdAt))}</td>
                    <td class="px-4 py-3 text-right">
                        <button type="button" data-admin-action="delete-room" data-room-id="${r.id}"
                            class="rounded-lg border border-rose-200 dark:border-rose-800 bg-rose-50 dark:bg-rose-950/40 px-3 py-1.5 text-xs font-semibold text-rose-700 dark:text-rose-300 hover:bg-rose-600 hover:text-white transition">
                            Xóa phòng
                        </button>
                    </td>
                </tr>
            `;
        })
        .join('');

    return wrapTable(
        ['Phòng học & Chuyên ngành', 'Mã mời', 'Quy mô', 'Trạng thái', 'Ngày tạo', 'Thao tác'],
        rows
    );
}

export function renderAdminMaterialsTable(materials = []) {
    if (!materials.length) {
        return renderEmptyState({ title: 'Kho học liệu trống', description: 'Chưa có tệp PDF nào được tải lên.' });
    }

    const rows = materials
        .map((m) => {
            const pdfUrl = resolveBackendFileUrl(m.fileUrl);
            return `
                <tr class="hover:bg-slate-50/80 dark:hover:bg-slate-800/50">
                    <td class="px-4 py-3">
                        <a href="${escapeHtml(pdfUrl)}" target="_blank" rel="noopener" class="text-sm font-semibold text-indigo-700 dark:text-indigo-300 hover:underline">
                            ${escapeHtml(m.title)}
                        </a>
                        <p class="text-xs text-slate-500 dark:text-slate-400">${escapeHtml(m.originalFileName)} &bull; Phòng: ${escapeHtml(m.roomName)}</p>
                    </td>
                    <td class="px-4 py-3 text-xs text-slate-600 dark:text-slate-300">${m.pageCount} trang (${escapeHtml(formatFileSize(m.fileSizeBytes))})</td>
                    <td class="px-4 py-3 text-xs text-slate-600 dark:text-slate-300">${escapeHtml(m.uploadedByName)}</td>
                    <td class="px-4 py-3">
                        <span class="rounded-full px-2.5 py-0.5 text-[11px] font-semibold ${
                            m.isApproved
                                ? 'bg-emerald-100 dark:bg-emerald-500/20 text-emerald-700 dark:text-emerald-300'
                                : 'bg-amber-100 dark:bg-amber-500/20 text-amber-800 dark:text-amber-300'
                        }">
                            ${m.isApproved ? 'Đã duyệt' : 'Đang ẩn'}
                        </span>
                    </td>
                    <td class="px-4 py-3 text-xs text-slate-500 dark:text-slate-400">${escapeHtml(formatDateTime(m.createdAt))}</td>
                    <td class="px-4 py-3 text-right space-x-1.5">
                        <button type="button" data-admin-action="toggle-material-approval" data-material-id="${m.id}"
                            class="rounded-lg border border-slate-200 dark:border-slate-700 bg-white dark:bg-slate-800 px-2.5 py-1.5 text-xs font-semibold text-slate-700 dark:text-slate-200 hover:bg-slate-100 dark:hover:bg-slate-700">
                            ${m.isApproved ? 'Tạm ẩn' : 'Phê duyệt'}
                        </button>
                        <button type="button" data-admin-action="delete-material" data-material-id="${m.id}"
                            class="rounded-lg border border-rose-200 dark:border-rose-800 bg-rose-50 dark:bg-rose-950/40 px-2.5 py-1.5 text-xs font-semibold text-rose-700 dark:text-rose-300 hover:bg-rose-600 hover:text-white transition">
                            Xóa
                        </button>
                    </td>
                </tr>
            `;
        })
        .join('');

    return wrapTable(
        ['Học liệu PDF & Phòng', 'Dung lượng', 'Người tải', 'Kiểm duyệt', 'Thời gian', 'Thao tác'],
        rows
    );
}

export function renderAdminAiLogsTable(logs = []) {
    if (!logs.length) {
        return renderEmptyState({ title: 'Chưa có hoạt động AI', description: 'Các lượt sử dụng trợ giảng và công cụ học tập sẽ hiển thị tại đây.' });
    }

    const featureMeta = {
        ExtractPdf: {
            label: 'Tóm tắt giáo trình PDF',
            cls: 'bg-emerald-50 dark:bg-emerald-500/15 text-emerald-700 dark:text-emerald-300 border-emerald-200 dark:border-emerald-500/30'
        },
        BotChat: {
            label: 'Hỏi đáp Trợ giảng @bot',
            cls: 'bg-indigo-50 dark:bg-indigo-500/15 text-indigo-700 dark:text-indigo-300 border-indigo-200 dark:border-indigo-500/30'
        },
        GenerateQuiz: {
            label: 'Biên soạn Đề trắc nghiệm',
            cls: 'bg-amber-50 dark:bg-amber-500/15 text-amber-800 dark:text-amber-300 border-amber-200 dark:border-amber-500/30'
        },
        GenerateFlashcards: {
            label: 'Tạo Bộ thẻ ghi nhớ 3D',
            cls: 'bg-purple-50 dark:bg-purple-500/15 text-purple-700 dark:text-purple-300 border-purple-200 dark:border-purple-500/30'
        },
        GenerateMindmap: {
            label: 'Sơ đồ Tư duy AI',
            cls: 'bg-cyan-50 dark:bg-cyan-500/15 text-cyan-700 dark:text-cyan-300 border-cyan-200 dark:border-cyan-500/30'
        },
        AnalyzeWeakness: {
            label: 'Phân tích Lỗ hổng AI',
            cls: 'bg-rose-50 dark:bg-rose-500/15 text-rose-700 dark:text-rose-300 border-rose-200 dark:border-rose-500/30'
        }
    };

    const rows = logs
        .map((l) => {
            const meta = featureMeta[l.featureType] || {
                label: 'Hỗ trợ học tập',
                cls: 'bg-slate-100 dark:bg-slate-800 text-slate-700 dark:text-slate-300 border-slate-200 dark:border-slate-700'
            };
            return `
                <tr class="hover:bg-slate-50/80 dark:hover:bg-slate-800/50">
                    <td class="px-4 py-3 text-xs text-slate-500 dark:text-slate-400">${escapeHtml(formatDateTime(l.createdAt))}</td>
                    <td class="px-4 py-3">
                        <p class="text-xs font-semibold text-slate-900 dark:text-slate-100">${escapeHtml(l.userFullName)}</p>
                    </td>
                    <td class="px-4 py-3 text-xs text-slate-600 dark:text-slate-300">${escapeHtml(l.roomName || 'Không gian chung')}</td>
                    <td class="px-4 py-3">
                        <span class="rounded-md border px-2.5 py-0.5 text-[11px] font-semibold ${meta.cls}">${escapeHtml(meta.label)}</span>
                    </td>
                    <td class="px-4 py-3 text-right">
                        <span class="rounded-full px-2.5 py-0.5 text-[11px] font-semibold ${
                            l.isSuccess
                                ? 'bg-emerald-100 dark:bg-emerald-500/20 text-emerald-700 dark:text-emerald-300'
                                : 'bg-rose-100 dark:bg-rose-500/20 text-rose-700 dark:text-rose-300'
                        }">
                            ${l.isSuccess ? 'Hoàn tất' : 'Gián đoạn'}
                        </span>
                    </td>
                </tr>
            `;
        })
        .join('');

    return wrapTable(
        ['Thời gian', 'Sinh viên thực hiện', 'Phòng học', 'Hoạt động Học tập', 'Trạng thái'],
        rows
    );
}

function wrapTable(headers, rowsHtml) {
    const ths = headers
        .map(
            (h, idx) =>
                `<th class="px-4 py-3 ${idx === headers.length - 1 ? 'text-right' : ''}">${escapeHtml(h)}</th>`
        )
        .join('');

    return `
        <div class="overflow-x-auto rounded-xl border border-slate-200 dark:border-slate-800">
            <table class="min-w-full divide-y divide-slate-200 dark:divide-slate-800 text-left">
                <thead class="bg-slate-50 dark:bg-slate-900/80 text-[11px] font-semibold uppercase tracking-wider text-slate-500 dark:text-slate-400">
                    <tr>${ths}</tr>
                </thead>
                <tbody class="divide-y divide-slate-100 dark:divide-slate-800/80 bg-white dark:bg-slate-900">${rowsHtml}</tbody>
            </table>
        </div>
    `;
}
