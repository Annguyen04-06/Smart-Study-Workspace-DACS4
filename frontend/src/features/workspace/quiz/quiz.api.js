/**
 * Cột 3 Tab 2 — Tầng API cho tính năng Sinh đề Trắc nghiệm AI và Nộp bài chấm điểm thang 10.
 */

import { httpClient } from '../../../core/http/httpClient.js';

export const quizApi = {
    async getRoomQuizzes(roomId) {
        const res = await httpClient.get(`/rooms/${roomId}/quizzes`);
        return res.data || [];
    },

    async generateQuiz(roomId, payload) {
        const res = await httpClient.post(`/rooms/${roomId}/quizzes/generate`, payload);
        return res.data;
    },

    async getQuizForTaking(quizId) {
        const res = await httpClient.get(`/quizzes/${quizId}`);
        return res.data;
    },

    async submitQuiz(quizId, payload) {
        const res = await httpClient.post(`/quizzes/${quizId}/submit`, payload);
        return res.data;
    },

    async analyzeWeakness(roomId = null) {
        const res = await httpClient.post('/quizzes/analyze-weakness', roomId ? { roomId } : {});
        return res.data;
    },

    async generateRemedialQuiz(payload = {}) {
        const res = await httpClient.post('/quizzes/generate-remedial', {
            questionCount: 5,
            ...payload
        });
        return res.data;
    }
};
