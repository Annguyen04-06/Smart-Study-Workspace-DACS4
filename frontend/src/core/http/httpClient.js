/**
 * Lớp HttpClient tập trung cho toàn bộ cuộc gọi REST API tới .NET 8 Backend Core.
 * Tự động gắn Header Authorization Bearer JWT, bóc tách cấu trúc ApiResponse và xử lý lỗi 401/403.
 */

import { APP_CONFIG, ROUTES, STORAGE_KEYS } from '../config/app.config.js';
import { appStore } from '../state/appStore.js';

export class ApiError extends Error {
    constructor(message, statusCode = 400, errors = []) {
        super(message);
        this.name = 'ApiError';
        this.statusCode = statusCode;
        this.errors = errors;
    }
}

class HttpClient {
    constructor(baseUrl) {
        this._baseUrl = baseUrl.replace(/\/$/, '');
    }

    async get(endpoint, queryParams = null) {
        const url = this._buildUrl(endpoint, queryParams);
        return this._send(url, { method: 'GET' });
    }

    async post(endpoint, body = {}) {
        return this._send(this._buildUrl(endpoint), {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(body)
        });
    }

    async put(endpoint, body = {}) {
        return this._send(this._buildUrl(endpoint), {
            method: 'PUT',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(body)
        });
    }

    async patch(endpoint, body = {}) {
        return this._send(this._buildUrl(endpoint), {
            method: 'PATCH',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(body)
        });
    }

    async delete(endpoint) {
        return this._send(this._buildUrl(endpoint), { method: 'DELETE' });
    }

    async upload(endpoint, formData) {
        // Không tự gán Content-Type để trình duyệt tự đính kèm multipart boundary chuẩn xác
        return this._send(this._buildUrl(endpoint), {
            method: 'POST',
            body: formData
        });
    }

    _buildUrl(endpoint, queryParams = null) {
        const cleanEndpoint = endpoint.startsWith('/') ? endpoint : `/${endpoint}`;
        const url = new URL(`${this._baseUrl}${cleanEndpoint}`);
        if (queryParams && typeof queryParams === 'object') {
            Object.entries(queryParams).forEach(([key, value]) => {
                if (value !== undefined && value !== null && value !== '') {
                    url.searchParams.append(key, String(value));
                }
            });
        }
        return url.toString();
    }

    async _send(url, options) {
        const headers = new Headers(options.headers || {});
        const token = localStorage.getItem(STORAGE_KEYS.ACCESS_TOKEN);

        if (token) {
            headers.set('Authorization', `Bearer ${token}`);
        }

        let response;
        try {
            response = await fetch(url, {
                ...options,
                headers
            });
        } catch {
            throw new ApiError(
                `Không thể kết nối tới máy chủ Backend Core (${APP_CONFIG.FILE_BASE_URL}). Vui lòng kiểm tra dịch vụ đang chạy.`,
                0
            );
        }

        let payload = null;
        try {
            payload = await response.json();
        } catch {
            payload = null;
        }

        if (response.status === 401) {
            const isAuthPage = window.location.pathname.includes('/auth/login.html') ||
                               window.location.pathname.includes('/auth/register.html');
            appStore.clearAuthSession();
            if (!isAuthPage) {
                window.location.href = ROUTES.LOGIN;
            }
            throw new ApiError(payload?.message || 'Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.', 401);
        }

        if (!response.ok || (payload && payload.success === false)) {
            const errMessage = payload?.message || `Yêu cầu thất bại (HTTP ${response.status}).`;
            const errList = Array.isArray(payload?.errors) ? payload.errors : [];
            throw new ApiError(errMessage, response.status, errList);
        }

        return payload;
    }
}

export const httpClient = new HttpClient(APP_CONFIG.API_BASE_URL);
