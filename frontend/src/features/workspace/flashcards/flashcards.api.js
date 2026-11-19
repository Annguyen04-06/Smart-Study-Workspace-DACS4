/**
 * Cột 3 Tab 3 — Tầng API quản lý Bộ thẻ Ghi nhớ 3D và đánh giá mức độ nhớ Spaced Repetition.
 */

import { httpClient } from '../../../core/http/httpClient.js';

export const flashcardsApi = {
    async getRoomDecks(roomId) {
        const res = await httpClient.get(`/rooms/${roomId}/flashcards`);
        return res.data || [];
    },

    async generateDeck(roomId, payload) {
        const res = await httpClient.post(`/rooms/${roomId}/flashcards/generate`, payload);
        return res.data;
    },

    async getDeckDetail(deckId) {
        const res = await httpClient.get(`/flashcards/decks/${deckId}`);
        return res.data;
    },

    async updateCardMastery(cardId, masteryLevel) {
        const res = await httpClient.patch(`/flashcards/cards/${cardId}/mastery`, { masteryLevel });
        return res.data;
    }
};
