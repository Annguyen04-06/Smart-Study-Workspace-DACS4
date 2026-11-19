/**
 * Tầng API lấy đường dẫn mở App trên điện thoại: GET /api/system/mobile-access
 * => { publicHttpsUrl, lanUrl, isTunnelActive, updatedAt }.
 */

import { httpClient } from '../../core/http/httpClient.js';

export const mobileAccessApi = {
    async getMobileAccess() {
        const res = await httpClient.get('/system/mobile-access', { _t: Date.now() });
        return res.data || null;
    }
};
