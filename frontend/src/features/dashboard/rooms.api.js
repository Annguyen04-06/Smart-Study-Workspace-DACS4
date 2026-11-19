/**
 * Tầng API cho phân hệ Phòng học Nhóm, Tham gia bằng InviteCode và Bảng xếp hạng Leaderboard.
 */

import { httpClient } from '../../core/http/httpClient.js';

export const roomsApi = {
    async getRooms(filter = {}) {
        const res = await httpClient.get('/rooms', filter);
        return res.data;
    },

    async getMyRooms() {
        const res = await httpClient.get('/rooms/my');
        return res.data;
    },

    async getRoomDetail(roomId) {
        const res = await httpClient.get(`/rooms/${roomId}`);
        return res.data;
    },

    async getOnlineUsers(roomId) {
        const res = await httpClient.get(`/rooms/${roomId}/online-users`);
        return res.data;
    },

    async getLeaderboard(roomId) {
        const res = await httpClient.get(`/rooms/${roomId}/leaderboard`);
        return res.data || [];
    },

    async createRoom(payload) {
        const res = await httpClient.post('/rooms', payload);
        return res.data;
    },

    async joinByInviteCode(inviteCode) {
        const res = await httpClient.post('/rooms/join', { inviteCode });
        return res.data;
    },

    async joinByRoomId(roomId) {
        const res = await httpClient.post(`/rooms/${roomId}/join`);
        return res.data;
    },

    async leaveRoom(roomId) {
        return httpClient.delete(`/rooms/${roomId}/leave`);
    },

    async deleteRoom(roomId) {
        return httpClient.delete(`/rooms/${roomId}`);
    }
};
