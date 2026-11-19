/**
 * Tầng API cho Cổng Quản trị viên (Admin Analytics & Moderation Portal).
 */

import { httpClient } from '../../core/http/httpClient.js';

export const adminApi = {
    async getOverview() {
        const res = await httpClient.get('/admin/overview');
        return res.data;
    },

    async getUsers(params = {}) {
        const res = await httpClient.get('/admin/users', params);
        return res.data;
    },

    async toggleUserLock(userId) {
        const res = await httpClient.patch(`/admin/users/${userId}/toggle-lock`);
        return res.data;
    },

    async getRooms(params = {}) {
        const res = await httpClient.get('/admin/rooms', params);
        return res.data;
    },

    async updateRoomStatus(roomId, status) {
        const res = await httpClient.patch(`/admin/rooms/${roomId}/status`, { status });
        return res.data;
    },

    async deleteRoom(roomId) {
        return httpClient.delete(`/admin/rooms/${roomId}`);
    },

    async getMaterials(params = {}) {
        const res = await httpClient.get('/admin/materials', params);
        return res.data;
    },

    async toggleMaterialApproval(materialId) {
        const res = await httpClient.patch(`/admin/materials/${materialId}/toggle-approval`);
        return res.data;
    },

    async deleteMaterial(materialId) {
        return httpClient.delete(`/admin/materials/${materialId}`);
    },

    async getAiLogs(params = {}) {
        const res = await httpClient.get('/admin/ai-logs', params);
        return res.data;
    }
};
