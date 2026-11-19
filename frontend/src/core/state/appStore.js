/**
 * Reactive Store nhẹ viết bằng Vanilla JS thuần (Observer Pattern: subscribe / setState).
 * Đồng bộ trạng thái đăng nhập người dùng, phòng đang mở và danh sách thành viên trực tuyến.
 */

import { STORAGE_KEYS } from '../config/app.config.js';

function readPersistedUser() {
    try {
        const raw = localStorage.getItem(STORAGE_KEYS.CURRENT_USER);
        return raw ? JSON.parse(raw) : null;
    } catch {
        return null;
    }
}

class AppStore {
    constructor() {
        this._state = {
            token: localStorage.getItem(STORAGE_KEYS.ACCESS_TOKEN) || null,
            user: readPersistedUser(),
            activeRoom: null,
            onlineUsers: []
        };
        this._listeners = new Set();
    }

    getState() {
        return this._state;
    }

    setState(partialState) {
        this._state = { ...this._state, ...partialState };
        for (const listener of this._listeners) {
            listener(this._state);
        }
    }

    subscribe(listener) {
        this._listeners.add(listener);
        return () => this._listeners.delete(listener);
    }

    setAuthSession({ token, expiresAt, user }) {
        if (token) {
            localStorage.setItem(STORAGE_KEYS.ACCESS_TOKEN, token);
        }
        if (expiresAt) {
            localStorage.setItem(STORAGE_KEYS.TOKEN_EXPIRES_AT, expiresAt);
        }
        if (user) {
            localStorage.setItem(STORAGE_KEYS.CURRENT_USER, JSON.stringify(user));
        }
        this.setState({
            token: token ?? this._state.token,
            user: user ?? this._state.user
        });
    }

    updateCurrentUser(user) {
        localStorage.setItem(STORAGE_KEYS.CURRENT_USER, JSON.stringify(user));
        this.setState({ user });
    }

    clearAuthSession() {
        localStorage.removeItem(STORAGE_KEYS.ACCESS_TOKEN);
        localStorage.removeItem(STORAGE_KEYS.CURRENT_USER);
        localStorage.removeItem(STORAGE_KEYS.TOKEN_EXPIRES_AT);
        this.setState({
            token: null,
            user: null,
            activeRoom: null,
            onlineUsers: []
        });
    }
}

export const appStore = new AppStore();
