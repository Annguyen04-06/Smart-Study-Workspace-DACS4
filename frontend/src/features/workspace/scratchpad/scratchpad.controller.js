/**
 * Cột 3 — Controller quản lý Sổ tay Ghi chú Cá nhân (Personal Markdown Scratchpad):
 * - Lưu tự động tức thì vào IndexedDB theo từng roomId (hoạt động cả khi Offline 100%).
 * - Hỗ trợ nhận đoạn trích 1-click ("Lưu vào Sổ tay") từ mọi câu trả lời của Trợ giảng @bot.
 * - Hỗ trợ xuất file .md chuẩn hoặc in PDF sạch đẹp qua printExport.utils.js.
 */

import { offlineDb } from '../../../core/storage/offlineDb.service.js';
import { enhanceMarkdownContainer } from '../../../shared/components/markdownViewer.component.js';
import { toast } from '../../../shared/components/toast.component.js';
import { debounce, qs } from '../../../shared/utils/dom.utils.js';
import { exportScratchpadSheet } from '../../../shared/utils/printExport.utils.js';
import {
    renderScratchpadPreviewContent,
    renderScratchpadShell
} from './scratchpad.view.js';

export function createScratchpadController({ roomId, roomName, inviteCode = '' }) {
    const hostEl = qs('#scratchpad-stage-container');

    let content = '';
    let viewMode = 'split'; // 'edit' | 'split' | 'preview'
    let saveStatus = 'Đã đồng bộ IndexedDB';

    const setStatusText = (text) => {
        saveStatus = text;
        const statusEl = qs('#scratchpad-save-status');
        if (statusEl) statusEl.textContent = text;
    };

    const persistNow = async () => {
        setStatusText('Đang lưu...');
        await offlineDb.saveScratchpad(roomId, roomName, content);
        const nowStr = new Date().toLocaleTimeString('vi-VN', { hour: '2-digit', minute: '2-digit' });
        setStatusText(`Đã lưu tức thì (${nowStr})`);
    };

    const debouncedPersist = debounce(persistNow, 350);

    const refreshLivePreview = () => {
        const previewEl = qs('#scratchpad-preview-pane');
        if (previewEl) {
            previewEl.innerHTML = renderScratchpadPreviewContent(content);
            enhanceMarkdownContainer(previewEl);
        }
        const wordEl = qs('#scratchpad-word-count');
        if (wordEl) {
            const count = content.trim() ? content.trim().split(/\s+/).length : 0;
            wordEl.textContent = `${count} từ`;
        }
    };

    const render = () => {
        if (!hostEl) return;
        hostEl.innerHTML = renderScratchpadShell({
            roomName,
            content,
            mode: viewMode,
            saveStatus
        });
        enhanceMarkdownContainer(hostEl);
    };

    const insertMarkdownSnippet = (type) => {
        const textarea = qs('#scratchpad-textarea');
        if (!textarea) return;

        const templates = {
            heading: '\n## Chủ đề trọng tâm\n',
            bold: '**khái niệm quan trọng**',
            math: '\n$$\nT(n) = O(n \\log n)\n$$\n',
            code: '\n```python\n# Mã nguồn minh họa\n```\n',
            checklist: '\n- [ ] Ôn lại định nghĩa cốt lõi\n- [ ] Làm bài tập vận dụng\n'
        };

        const snippet = templates[type] || '';
        const start = textarea.selectionStart ?? content.length;
        const end = textarea.selectionEnd ?? content.length;
        content = content.slice(0, start) + snippet + content.slice(end);
        textarea.value = content;
        textarea.focus();
        const cursor = start + snippet.length;
        textarea.setSelectionRange(cursor, cursor);
        refreshLivePreview();
        debouncedPersist();
    };

    const downloadMarkdownFile = () => {
        if (!content.trim()) {
            toast.warning('Sổ tay hiện đang trống, hãy ghi chú hoặc lưu kiến thức từ @bot trước.');
            return;
        }
        const slug = String(inviteCode || `room-${roomId}`).toLowerCase().replace(/[^a-z0-9-_]/g, '');
        const header = `# Sổ tay Ôn thi — ${roomName}\n> Xuất từ Smart Study Workspace ngày ${new Date().toLocaleString('vi-VN')}\n\n---\n\n`;
        const blob = new Blob([header + content], { type: 'text/markdown;charset=utf-8' });
        const url = URL.createObjectURL(blob);
        const link = document.createElement('a');
        link.href = url;
        link.download = `so-tay-on-thi-${slug}.md`;
        document.body.appendChild(link);
        link.click();
        link.remove();
        URL.revokeObjectURL(url);
        toast.success(`Đã tải xuống so-tay-on-thi-${slug}.md!`);
    };

    const bindEvents = () => {
        hostEl?.addEventListener('input', (e) => {
            if (e.target?.id !== 'scratchpad-textarea') return;
            content = e.target.value;
            setStatusText('Đang soạn...');
            refreshLivePreview();
            debouncedPersist();
        });

        hostEl?.addEventListener('click', async (e) => {
            const modeBtn = e.target.closest('[data-sp-mode]');
            if (modeBtn) {
                viewMode = modeBtn.dataset.spMode || 'split';
                render();
                return;
            }

            const insertBtn = e.target.closest('[data-sp-insert]');
            if (insertBtn) {
                if (viewMode === 'preview') {
                    viewMode = 'split';
                    render();
                }
                insertMarkdownSnippet(insertBtn.dataset.spInsert);
                return;
            }

            const actionBtn = e.target.closest('[data-sp-action]');
            if (!actionBtn) return;

            const action = actionBtn.dataset.spAction;
            if (action === 'export-md') {
                downloadMarkdownFile();
            } else if (action === 'export-pdf') {
                if (!content.trim()) {
                    toast.warning('Sổ tay hiện đang trống để in PDF.');
                    return;
                }
                exportScratchpadSheet({ roomName, content });
            } else if (action === 'clear') {
                if (!content.trim()) return;
                if (!window.confirm('Xóa toàn bộ nội dung Sổ tay ghi chú của phòng học này?')) return;
                content = '';
                await persistNow();
                render();
                toast.info('Đã làm mới Sổ tay ghi chú.');
            }
        });
    };

    return {
        async init() {
            bindEvents();
            const saved = await offlineDb.getScratchpad(roomId);
            if (saved?.content) {
                content = saved.content;
                const savedTime = saved.updatedAt
                    ? new Date(saved.updatedAt).toLocaleTimeString('vi-VN', { hour: '2-digit', minute: '2-digit' })
                    : '';
                saveStatus = savedTime ? `Đã nạp từ IndexedDB (${savedTime})` : 'Đã nạp từ IndexedDB';
            }
            render();
        },

        /**
         * Chép gọn một đoạn giải thích/công thức từ @bot vào Sổ tay cá nhân và lưu ngay xuống IndexedDB.
         */
        async appendSnippet({ title = 'Ghi chú từ Trợ giảng @bot', markdownContent = '' }) {
            const cleanBody = String(markdownContent || '').trim();
            if (!cleanBody) return;

            const timestamp = new Date().toLocaleTimeString('vi-VN', { hour: '2-digit', minute: '2-digit' });
            const block = `### ✨ ${title} (${timestamp})\n\n${cleanBody}\n`;
            content = content.trim() ? `${content.trim()}\n\n---\n\n${block}` : block;

            await persistNow();
            render();

            const previewPane = qs('#scratchpad-preview-pane');
            if (previewPane) {
                previewPane.scrollTop = previewPane.scrollHeight;
            }
        }
    };
}
