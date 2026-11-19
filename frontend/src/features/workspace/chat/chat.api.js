/**
 * Cột 2 Workspace — Tầng API tải phân trang lịch sử tin nhắn, Delta Sync tin mới, tin nhắn đã ghim và gửi tin dự phòng.
 */

import { httpClient } from '../../../core/http/httpClient.js';

export const DELTA_SYNC_PAGE_SIZE = 100;

export const chatApi = {
    async getMessages(roomId, { beforeId = null, pageSize = 35 } = {}) {
        const res = await httpClient.get(`/rooms/${roomId}/messages`, {
            beforeId: beforeId || undefined,
            pageSize
        });
        return res.data || [];
    },

    /**
     * Delta Sync: chỉ lấy các tin nhắn có id lớn hơn afterId (tăng dần) khi mở lại App / vừa kết nối lại.
     */
    async getMessagesAfter(roomId, afterId) {
        const res = await httpClient.get(`/rooms/${roomId}/messages`, {
            afterId,
            pageSize: DELTA_SYNC_PAGE_SIZE
        });
        return res.data || [];
    },

    async getPinnedMessages(roomId) {
        const res = await httpClient.get(`/rooms/${roomId}/messages/pinned`);
        return res.data || [];
    },

    async togglePinMessage(roomId, messageId) {
        const res = await httpClient.patch(`/rooms/${roomId}/messages/${messageId}/pin`);
        return res.data;
    },

    async toggleMessageReaction(roomId, messageId, emoji) {
        const res = await httpClient.post(`/rooms/${roomId}/messages/${messageId}/reactions`, { emoji });
        return res.data;
    },

    async postMessageFallback(roomId, content) {
        const res = await httpClient.post(`/rooms/${roomId}/messages`, { content });
        return res.data;
    }
};
