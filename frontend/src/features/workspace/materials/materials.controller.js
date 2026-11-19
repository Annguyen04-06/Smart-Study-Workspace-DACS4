/**
 * Cột 3 Tab 1 — Controller quản lý Tải lên PDF, hiển thị bản Tóm tắt AI, nhúng PDF Preview,
 * Xuất/In Phiếu Ôn Tập và chuyển tiếp nhanh tài liệu sang Tab Sinh đề Quiz / Flashcards.
 */

import { offlineDb } from '../../../core/storage/offlineDb.service.js';
import { enhanceMarkdownContainer } from '../../../shared/components/markdownViewer.component.js';
import { toast } from '../../../shared/components/toast.component.js';
import { qs } from '../../../shared/utils/dom.utils.js';
import { exportMaterialSummarySheet } from '../../../shared/utils/printExport.utils.js';
import { applyStaggerEntrance } from '../../../shared/utils/spotlight.utils.js';
import { validatePdfFile } from '../../../shared/utils/validation.utils.js';
import {
    renderMaterialInspector,
    renderMaterialsList,
    renderMaterialsSkeletons
} from './materialItem.view.js';
import { materialsApi } from './materials.api.js';
import {
    mountInteractiveMindmap,
    renderMindmapInspectorShell
} from './mindmapViewer.component.js';

export function createMaterialsController({
    roomId,
    roomName,
    currentUser,
    isRoomOwner,
    onMaterialsChanged,
    onRequestGenerateQuiz,
    onRequestGenerateFlashcard,
    onExpandPane
}) {
    const listEl = qs('#materials-list-container');
    const inspectorEl = qs('#material-inspector-container');
    const uploadFormEl = qs('#upload-material-form');
    const fileInputEl = qs('#material-file-input');
    const titleInputEl = qs('#material-title-input');
    const uploadSubmitBtn = qs('#material-upload-btn');

    let materials = [];
    let selectedMaterialId = null;
    let inspectorMode = 'summary';

    const canManageAll = Boolean(isRoomOwner || String(currentUser.role).toLowerCase() === 'admin');

    const renderAll = () => {
        if (listEl) {
            listEl.innerHTML = renderMaterialsList(materials, {
                selectedId: selectedMaterialId,
                canManageAll,
                currentUserId: currentUser.id
            });
            applyStaggerEntrance(listEl);
        }

        if (inspectorEl && inspectorMode !== 'mindmap') {
            const activeMat = materials.find((m) => Number(m.id) === Number(selectedMaterialId)) || null;
            inspectorEl.innerHTML = renderMaterialInspector(activeMat, inspectorMode);
            enhanceMarkdownContainer(inspectorEl);
        } else if (inspectorEl && !selectedMaterialId) {
            inspectorEl.innerHTML = '';
        }
    };

    const openMindmapViewer = async (matId, forceRegenerate = false) => {
        const activeMat = materials.find((m) => Number(m.id) === Number(matId));
        if (!activeMat || !inspectorEl) return;

        selectedMaterialId = matId;
        inspectorMode = 'mindmap';
        onExpandPane?.(true);

        if (listEl) {
            listEl.innerHTML = renderMaterialsList(materials, {
                selectedId: selectedMaterialId,
                canManageAll,
                currentUserId: currentUser.id
            });
        }

        // Nếu đã có sẵn mã Mermaid trong bộ nhớ và không ép tạo lại -> render tức thì 0ms
        if (!forceRegenerate && activeMat.aiMindmap) {
            inspectorEl.innerHTML = renderMindmapInspectorShell(activeMat, { isLoading: false });
            await mountInteractiveMindmap(inspectorEl, activeMat.aiMindmap, activeMat.title);
            return;
        }

        inspectorEl.innerHTML = renderMindmapInspectorShell(activeMat, { isLoading: true });
        try {
            const result = await materialsApi.generateMindmap(matId, forceRegenerate);
            activeMat.aiMindmap = result.mermaidCode;
            inspectorEl.innerHTML = renderMindmapInspectorShell(activeMat, { isLoading: false });
            await mountInteractiveMindmap(inspectorEl, result.mermaidCode, activeMat.title);
            if (forceRegenerate) {
                toast.success('Đã tổng hợp lại Sơ đồ Tư duy Kiến thức AI!');
            }
        } catch (err) {
            toast.error(err.message || 'Không thể sinh sơ đồ tư duy lúc này.');
            inspectorMode = 'summary';
            renderAll();
        }
    };

    const refreshMaterials = async () => {
        if (listEl && materials.length === 0) {
            listEl.innerHTML = renderMaterialsSkeletons();
        }
        try {
            materials = await materialsApi.getRoomMaterials(roomId);
            // Tự động sao lưu bản tóm tắt AI & sơ đồ tư duy xuống IndexedDB cho Offline-First Mode
            offlineDb.saveRoomMaterials(roomId, materials).catch(() => {});
        } catch {
            materials = await offlineDb.getCachedRoomMaterials(roomId);
        }
        if (selectedMaterialId && !materials.some((m) => Number(m.id) === Number(selectedMaterialId))) {
            selectedMaterialId = materials[0]?.id ?? null;
        } else if (!selectedMaterialId && materials.length > 0) {
            selectedMaterialId = materials[0].id;
            inspectorMode = 'summary';
        }
        renderAll();
        onMaterialsChanged?.(materials);
        return materials;
    };

    const bindEvents = () => {
        fileInputEl?.addEventListener('change', () => {
            const file = fileInputEl.files?.[0];
            if (file && titleInputEl && !titleInputEl.value.trim()) {
                titleInputEl.value = file.name.replace(/\.pdf$/i, '');
            }
        });

        uploadFormEl?.addEventListener('submit', async (e) => {
            e.preventDefault();
            const file = fileInputEl?.files?.[0];
            const check = validatePdfFile(file, 25);
            if (!check.valid) {
                toast.warning(check.message);
                return;
            }

            const title = titleInputEl?.value.trim() || file.name.replace(/\.pdf$/i, '');

            if (uploadSubmitBtn) {
                uploadSubmitBtn.disabled = true;
                uploadSubmitBtn.textContent = 'Đang bóc tách & tóm tắt AI...';
            }

            try {
                const uploaded = await materialsApi.uploadMaterial(roomId, file, title);
                uploadFormEl.reset();
                selectedMaterialId = uploaded.id;
                inspectorMode = 'summary';
                toast.success(`Đã tải lên và tóm tắt "${uploaded.title}"!`);
                await refreshMaterials();
            } catch (err) {
                toast.error(err.message || 'Tải lên tài liệu PDF thất bại.');
            } finally {
                if (uploadSubmitBtn) {
                    uploadSubmitBtn.disabled = false;
                    uploadSubmitBtn.textContent = 'Tải lên & Tóm tắt Học thuật AI';
                }
            }
        });

        const handleActionClick = async (event) => {
            const btn = event.target.closest('[data-mat-action]');
            if (!btn) return;

            const action = btn.dataset.matAction;
            const matId = Number(btn.dataset.matId);

            if (action === 'close-inspector') {
                selectedMaterialId = null;
                renderAll();
                return;
            }

            if (action === 'summary') {
                selectedMaterialId = matId;
                inspectorMode = 'summary';
                renderAll();
                return;
            }

            if (action === 'mindmap') {
                await openMindmapViewer(matId, false);
                return;
            }

            if (action === 'regenerate-mindmap') {
                await openMindmapViewer(matId, true);
                return;
            }

            if (action === 'preview') {
                selectedMaterialId = matId;
                inspectorMode = 'preview';
                onExpandPane?.(true);
                renderAll();
                return;
            }

            if (action === 'print-summary') {
                const mat = materials.find((m) => Number(m.id) === matId);
                if (mat) {
                    exportMaterialSummarySheet(mat, roomName);
                }
                return;
            }

            if (action === 'quick-quiz') {
                onRequestGenerateQuiz?.(matId);
                return;
            }

            if (action === 'quick-flashcard') {
                onRequestGenerateFlashcard?.(matId);
                return;
            }

            if (action === 'delete') {
                if (!window.confirm('Bạn có chắc chắn muốn xóa tài liệu PDF này và các đề thi liên quan?')) {
                    return;
                }
                try {
                    await materialsApi.deleteMaterial(matId);
                    toast.success('Đã xóa học liệu PDF.');
                    await refreshMaterials();
                } catch (err) {
                    toast.error(err.message || 'Không thể xóa học liệu.');
                }
            }
        };

        listEl?.addEventListener('click', handleActionClick);
        inspectorEl?.addEventListener('click', handleActionClick);
    };

    return {
        async init() {
            bindEvents();
            return refreshMaterials();
        },
        refreshMaterials,
        getMaterials: () => materials
    };
}
