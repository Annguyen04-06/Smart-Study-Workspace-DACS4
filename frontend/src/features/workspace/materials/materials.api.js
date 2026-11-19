/**
 * Cột 3 Tab 1 — Tầng API quản lý Kho Học liệu PDF và gọi dịch vụ bóc tách tóm tắt AI.
 */

import { httpClient } from '../../../core/http/httpClient.js';

export const materialsApi = {
    async getRoomMaterials(roomId) {
        const res = await httpClient.get(`/rooms/${roomId}/materials`);
        return res.data || [];
    },

    async getMaterialDetail(materialId) {
        const res = await httpClient.get(`/materials/${materialId}`);
        return res.data;
    },

    async uploadMaterial(roomId, file, title) {
        const formData = new FormData();
        formData.append('file', file);
        if (title) {
            formData.append('title', title);
        }
        const res = await httpClient.upload(`/rooms/${roomId}/materials`, formData);
        return res.data;
    },

    async generateMindmap(materialId, forceRegenerate = false) {
        const res = await httpClient.post(`/materials/${materialId}/mindmap`, {
            forceRegenerate: Boolean(forceRegenerate)
        });
        return res.data;
    },

    async deleteMaterial(materialId) {
        return httpClient.delete(`/materials/${materialId}`);
    }
};
