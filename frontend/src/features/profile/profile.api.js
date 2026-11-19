/**
 * Tầng API cho phân hệ Hồ sơ Cá nhân, Đổi Avatar/Mật khẩu và Lịch sử Thi Trắc nghiệm.
 */

import { httpClient } from '../../core/http/httpClient.js';

export const profileApi = {
    async getProfile() {
        const res = await httpClient.get('/auth/me');
        return res.data;
    },

    async updateProfile(payload) {
        const res = await httpClient.put('/auth/profile', payload);
        return res.data;
    },

    async changePassword(payload) {
        return httpClient.put('/auth/change-password', payload);
    },

    async uploadAvatar(file) {
        const formData = new FormData();
        formData.append('file', file);
        const res = await httpClient.upload('/auth/avatar', formData);
        return res.data;
    },

    async getMyQuizAttempts() {
        const res = await httpClient.get('/quizzes/my-attempts');
        return res.data;
    },

    async getAttemptDetail(attemptId) {
        const res = await httpClient.get(`/quizzes/attempts/${attemptId}`);
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
