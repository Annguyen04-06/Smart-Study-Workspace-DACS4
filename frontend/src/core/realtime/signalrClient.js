/**
 * Singleton quản lý vòng đời kết nối SignalR WebSockets tới StudyRoomHub (/hubs/study-room).
 * Đồng bộ Presence, Chat/@bot Streaming, Group Pomodoro Timer và Pinned Messages thời gian thực.
 *
 * Tối ưu độ trễ:
 * - Kết nối WebSocket trực tiếp (skipNegotiation) bỏ 1 vòng HTTP negotiate; mạng chặn WebSocket thì tự quay về
 *   cơ chế negotiate chuẩn (WebSockets -> Server-Sent Events -> Long Polling).
 * - Tự kết nối lại nhanh theo nhịp 0 / 0.5 / 1.5 / 3 / 5 giây (giữ 5 giây/lần tối đa 2 phút), sau đó dừng
 *   để tiết kiệm pin; resume() khôi phục ngay khi người dùng mở lại App / có mạng trở lại.
 * - Phát sự kiện nội bộ 'Reconnected' sau khi đã tham gia lại phòng để các Controller chạy Delta Sync.
 */

import { APP_CONFIG, STORAGE_KEYS } from '../config/app.config.js';

const RECONNECT_DELAYS_MS = [0, 500, 1500, 3000, 5000];
const RECONNECT_GIVE_UP_AFTER_MS = 120_000;

const SERVER_EVENTS = [
    'OnlineUsersUpdated',
    'UserTyping',
    'ReceiveMessage',
    'BotThinking',
    'BotStreamStart',
    'BotStreamChunk',
    'BotStreamComplete',
    'BotStreamFailed',
    'ServerVersion',
    'WorkspaceResourceUpdated',
    'PomodoroStateUpdated',
    'MessagePinUpdated',
    'ReceiveRoomReaction',
    'MessageReactionsUpdated',
    'WhiteboardStateSynced',
    'ReceiveDrawStroke',
    'ReceiveCursorMove',
    'WhiteboardCleared'
];

const reconnectPolicy = {
    nextRetryDelayInMilliseconds(retryContext) {
        if (retryContext.elapsedMilliseconds > RECONNECT_GIVE_UP_AFTER_MS) {
            return null;
        }
        const index = Math.min(retryContext.previousRetryCount, RECONNECT_DELAYS_MS.length - 1);
        return RECONNECT_DELAYS_MS[index];
    }
};

class SignalRRealtimeClient {
    constructor() {
        this._connection = null;
        this._handlers = new Map();
        this._activeRoomId = null;
        this._preferDirectWebSocket = true;
        this._connectPromise = null;
    }

    on(eventName, callback) {
        if (!this._handlers.has(eventName)) {
            this._handlers.set(eventName, new Set());
        }
        this._handlers.get(eventName).add(callback);
        return () => {
            this._handlers.get(eventName)?.delete(callback);
        };
    }

    _emit(eventName, payload) {
        const listeners = this._handlers.get(eventName);
        if (!listeners) {
            return;
        }
        for (const fn of listeners) {
            try {
                fn(payload);
            } catch (err) {
                console.error(`[SignalR Event Error: ${eventName}]`, err);
            }
        }
    }

    _buildConnection(signalRGlobal, useDirectWebSocket) {
        const options = {
            accessTokenFactory: () => localStorage.getItem(STORAGE_KEYS.ACCESS_TOKEN) || ''
        };
        if (useDirectWebSocket) {
            options.skipNegotiation = true;
            options.transport = signalRGlobal.HttpTransportType.WebSockets;
        }

        const connection = new signalRGlobal.HubConnectionBuilder()
            .withUrl(APP_CONFIG.HUB_URL, options)
            .withAutomaticReconnect(reconnectPolicy)
            .configureLogging(signalRGlobal.LogLevel.Warning)
            .build();

        this._registerServerEvents(connection);
        return connection;
    }

    _registerServerEvents(connection) {
        for (const evt of SERVER_EVENTS) {
            connection.on(evt, (payload) => this._emit(evt, payload));
        }

        connection.onreconnecting(() => {
            this._emit('ConnectionStateChanged', { state: 'reconnecting' });
        });

        connection.onreconnected(async () => {
            this._emit('ConnectionStateChanged', { state: 'connected' });
            await this._rejoinActiveRoom();
        });

        connection.onclose(() => {
            this._emit('ConnectionStateChanged', { state: 'disconnected' });
        });
    }

    async _rejoinActiveRoom() {
        if (!this._activeRoomId) {
            return;
        }
        try {
            await this._connection.invoke('JoinRoom', this._activeRoomId);
            this._emit('Reconnected', { roomId: this._activeRoomId });
        } catch (err) {
            console.warn('Không thể tự động tham gia lại phòng sau khi Reconnect:', err);
        }
    }

    async connect() {
        const signalRGlobal = window.signalR;
        if (!signalRGlobal) {
            throw new Error('Thư viện Microsoft SignalR chưa được nạp trên trang.');
        }
        if (this.isConnected()) {
            return;
        }
        // Gộp các lời gọi connect() đồng thời vào 1 lần khởi động duy nhất
        if (!this._connectPromise) {
            this._connectPromise = this._startConnection(signalRGlobal).finally(() => {
                this._connectPromise = null;
            });
        }
        await this._connectPromise;
    }

    async _startConnection(signalRGlobal) {
        if (!this._connection) {
            this._connection = this._buildConnection(signalRGlobal, this._preferDirectWebSocket);
        }
        if (this._connection.state !== signalRGlobal.HubConnectionState.Disconnected) {
            return;
        }

        try {
            await this._connection.start();
        } catch (err) {
            if (!this._preferDirectWebSocket) {
                throw err;
            }
            // Proxy/mạng chặn WebSocket trực tiếp -> dùng cơ chế negotiate chuẩn cho phần còn lại của phiên
            this._preferDirectWebSocket = false;
            this._connection = this._buildConnection(signalRGlobal, false);
            await this._connection.start();
        }
        this._emit('ConnectionStateChanged', { state: 'connected' });
    }

    /**
     * Khôi phục kết nối khi App vừa được mở lại / có mạng trở lại (kết nối có thể đã bị hệ điều hành cắt).
     * Trả về true nếu đang kết nối được tới phòng hiện tại.
     */
    async resume() {
        if (!this._activeRoomId || !window.signalR) {
            return false;
        }
        if (this.isConnected()) {
            return true;
        }
        if (this._connection && this._connection.state !== window.signalR.HubConnectionState.Disconnected) {
            return false; // Đang tự kết nối lại theo reconnectPolicy
        }
        await this.connect();
        await this._rejoinActiveRoom();
        return this.isConnected();
    }

    async joinRoom(roomId) {
        const numericRoomId = Number(roomId);
        await this.connect();

        if (this._activeRoomId && this._activeRoomId !== numericRoomId) {
            await this.leaveRoom(this._activeRoomId);
        }

        this._activeRoomId = numericRoomId;
        await this._connection.invoke('JoinRoom', numericRoomId);
    }

    async leaveRoom(roomId) {
        if (!this.isConnected()) {
            return;
        }
        const numericRoomId = Number(roomId);
        await this._connection.invoke('LeaveRoom', numericRoomId);
        if (this._activeRoomId === numericRoomId) {
            this._activeRoomId = null;
        }
    }

    async sendTypingStatus(roomId, isTyping) {
        if (!this.isConnected()) {
            return;
        }
        await this._connection.invoke('SendTypingStatus', Number(roomId), Boolean(isTyping));
    }

    /**
     * Gửi tin nhắn kèm mã tạm clientMessageId (Optimistic UI). Trả về tin nhắn thật đã lưu trên máy chủ.
     */
    async sendChatMessage(roomId, content, clientMessageId) {
        await this.connect();
        return this._connection.invoke('SendChatMessage', Number(roomId), content, clientMessageId);
    }

    async togglePinMessage(roomId, messageId) {
        await this.connect();
        await this._connection.invoke('TogglePinMessage', Number(roomId), Number(messageId));
    }

    async startPomodoro(roomId, modeMinutes = 25) {
        await this.connect();
        await this._connection.invoke('StartPomodoro', Number(roomId), Number(modeMinutes));
    }

    async pausePomodoro(roomId) {
        await this.connect();
        await this._connection.invoke('PausePomodoro', Number(roomId));
    }

    async resetPomodoro(roomId, modeMinutes = 25) {
        await this.connect();
        await this._connection.invoke('ResetPomodoro', Number(roomId), Number(modeMinutes));
    }

    async sendRoomReaction(roomId, emoji) {
        await this.connect();
        await this._connection.invoke('SendRoomReaction', Number(roomId), String(emoji));
    }

    async toggleMessageReaction(roomId, messageId, emoji) {
        await this.connect();
        await this._connection.invoke('ToggleMessageReaction', Number(roomId), Number(messageId), String(emoji));
    }

    async requestWhiteboardState(roomId) {
        if (!this.isConnected()) return;
        await this._connection.invoke('RequestWhiteboardState', Number(roomId));
    }

    async sendDrawStroke(roomId, strokeData) {
        if (!this.isConnected()) return;
        await this._connection.invoke('SendDrawStroke', Number(roomId), strokeData);
    }

    async sendCursorMove(roomId, normX, normY) {
        if (!this.isConnected()) return;
        await this._connection.invoke('SendCursorMove', Number(roomId), Number(normX), Number(normY));
    }

    async clearWhiteboard(roomId) {
        await this.connect();
        await this._connection.invoke('ClearWhiteboard', Number(roomId));
    }

    isConnected() {
        return Boolean(
            this._connection &&
            window.signalR &&
            this._connection.state === window.signalR.HubConnectionState.Connected
        );
    }
}

export const signalrClient = new SignalRRealtimeClient();
