/**
 * Tầng API của phân hệ Xác thực (Auth).
 */

import { httpClient } from '../../core/http/httpClient.js';

export const authApi = {
    async login(credentials) {
        const res = await httpClient.post('/auth/login', credentials);
        return res.data;
    },

    async register(payload) {
        const res = await httpClient.post('/auth/register', payload);
        return res.data;
    },

    async getMe() {
        const res = await httpClient.get('/auth/me');
        return res.data;
    },

    async forgotPassword(email) {
        const res = await httpClient.post('/auth/forgot-password', { email });
        return res.data;
    },

    async verifyResetOtp(payload) {
        return httpClient.post('/auth/verify-reset-otp', payload);
    },

    async resetPassword(payload) {
        return httpClient.post('/auth/reset-password', payload);
    }
};
