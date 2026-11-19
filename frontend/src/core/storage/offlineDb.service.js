/**
 * Kho lưu trữ ngoại tuyến (Offline-First Engine) sử dụng IndexedDB thuần không thư viện ngoài.
 * Tự động đồng bộ ngầm (Background Sync Cache) khi đang online:
 * - Danh sách phòng học đã tham gia & chi tiết phòng
 * - Tin nhắn đã ghim (Pinned Messages)
 * - Bản tóm tắt AI & Sơ đồ tư duy của tài liệu PDF
 * - Toàn bộ bộ Thẻ ghi nhớ 3D Flashcards + Hàng đợi đồng bộ (Sync Queue) mức độ nhớ khi mất mạng
 * - Lịch sử làm bài Quiz & lời giải thích chi tiết từ AI
 * - Sổ tay ghi chú Markdown cá nhân (Personal Markdown Scratchpad) theo từng phòng học
 */

const DB_NAME = 'SmartStudyOfflineDb';
const DB_VERSION = 1;

const STORES = Object.freeze({
    ROOMS: 'rooms',
    ROOM_DETAILS: 'roomDetails',
    PINNED_MESSAGES: 'pinnedMessages',
    MATERIALS: 'materials',
    FLASHCARD_DECKS: 'flashcardDecks',
    QUIZ_ATTEMPTS: 'quizAttempts',
    SYNC_QUEUE: 'syncQueue',
    SCRATCHPADS: 'scratchpads'
});

let dbPromise = null;

function isIndexedDbAvailable() {
    return typeof window !== 'undefined' && 'indexedDB' in window;
}

function openDatabase() {
    if (!isIndexedDbAvailable()) {
        return Promise.resolve(null);
    }
    if (dbPromise) {
        return dbPromise;
    }

    dbPromise = new Promise((resolve) => {
        try {
            const request = window.indexedDB.open(DB_NAME, DB_VERSION);

            request.onupgradeneeded = (event) => {
                const db = event.target.result;

                if (!db.objectStoreNames.contains(STORES.ROOMS)) {
                    db.createObjectStore(STORES.ROOMS, { keyPath: 'id' });
                }
                if (!db.objectStoreNames.contains(STORES.ROOM_DETAILS)) {
                    db.createObjectStore(STORES.ROOM_DETAILS, { keyPath: 'id' });
                }
                if (!db.objectStoreNames.contains(STORES.PINNED_MESSAGES)) {
                    db.createObjectStore(STORES.PINNED_MESSAGES, { keyPath: 'roomId' });
                }
                if (!db.objectStoreNames.contains(STORES.MATERIALS)) {
                    db.createObjectStore(STORES.MATERIALS, { keyPath: 'roomId' });
                }
                if (!db.objectStoreNames.contains(STORES.FLASHCARD_DECKS)) {
                    const fcStore = db.createObjectStore(STORES.FLASHCARD_DECKS, { keyPath: 'id' });
                    fcStore.createIndex('byRoomId', 'roomId', { unique: false });
                }
                if (!db.objectStoreNames.contains(STORES.QUIZ_ATTEMPTS)) {
                    db.createObjectStore(STORES.QUIZ_ATTEMPTS, { keyPath: 'attemptId' });
                }
                if (!db.objectStoreNames.contains(STORES.SYNC_QUEUE)) {
                    db.createObjectStore(STORES.SYNC_QUEUE, { keyPath: 'id', autoIncrement: true });
                }
                if (!db.objectStoreNames.contains(STORES.SCRATCHPADS)) {
                    db.createObjectStore(STORES.SCRATCHPADS, { keyPath: 'roomId' });
                }
            };

            request.onsuccess = () => resolve(request.result);
            request.onerror = () => {
                console.warn('[OfflineDB] Không thể mở IndexedDB:', request.error);
                resolve(null);
            };
        } catch (err) {
            console.warn('[OfflineDB] Trình duyệt hạn chế IndexedDB:', err);
            resolve(null);
        }
    });

    return dbPromise;
}

async function runTx(storeName, mode, operation) {
    const db = await openDatabase();
    if (!db) return null;

    return new Promise((resolve) => {
        try {
            const tx = db.transaction(storeName, mode);
            const store = tx.objectStore(storeName);
            let opResult;

            const req = operation(store);
            if (req && typeof req === 'object' && 'onsuccess' in req) {
                req.onsuccess = () => {
                    opResult = req.result;
                };
                req.onerror = () => {
                    opResult = null;
                };
            }

            tx.oncomplete = () => resolve(opResult ?? true);
            tx.onerror = () => resolve(null);
            tx.onabort = () => resolve(null);
        } catch {
            resolve(null);
        }
    });
}

export const offlineDb = {
    // =========================================================================
    // 1. PHÒNG HỌC & CHI TIẾT PHÒNG
    // =========================================================================
    async saveRooms(rooms = []) {
        if (!Array.isArray(rooms) || rooms.length === 0) return;
        const db = await openDatabase();
        if (!db) return;

        return new Promise((resolve) => {
            const tx = db.transaction(STORES.ROOMS, 'readwrite');
            const store = tx.objectStore(STORES.ROOMS);
            const now = Date.now();
            for (const room of rooms) {
                if (room?.id) {
                    store.put({ ...room, _cachedAt: now });
                }
            }
            tx.oncomplete = () => resolve(true);
            tx.onerror = () => resolve(false);
        });
    },

    async getCachedRooms() {
        const items = await runTx(STORES.ROOMS, 'readonly', (store) => store.getAll());
        return Array.isArray(items) ? items : [];
    },

    async saveRoomDetail(roomDetail) {
        if (!roomDetail?.id) return;
        await runTx(STORES.ROOM_DETAILS, 'readwrite', (store) =>
            store.put({ ...roomDetail, id: Number(roomDetail.id), _cachedAt: Date.now() })
        );
    },

    async getCachedRoomDetail(roomId) {
        return runTx(STORES.ROOM_DETAILS, 'readonly', (store) => store.get(Number(roomId)));
    },

    // =========================================================================
    // 2. TIN NHẮN ĐÃ GHIM (PINNED MESSAGES)
    // =========================================================================
    async savePinnedMessages(roomId, messages = []) {
        await runTx(STORES.PINNED_MESSAGES, 'readwrite', (store) =>
            store.put({ roomId: Number(roomId), messages, updatedAt: Date.now() })
        );
    },

    async getCachedPinnedMessages(roomId) {
        const record = await runTx(STORES.PINNED_MESSAGES, 'readonly', (store) => store.get(Number(roomId)));
        return Array.isArray(record?.messages) ? record.messages : [];
    },

    // =========================================================================
    // 3. HỌC LIỆU PDF & TÓM TẮT AI + MINDMAP
    // =========================================================================
    async saveRoomMaterials(roomId, materials = []) {
        await runTx(STORES.MATERIALS, 'readwrite', (store) =>
            store.put({ roomId: Number(roomId), items: materials, updatedAt: Date.now() })
        );
    },

    async getCachedRoomMaterials(roomId) {
        const record = await runTx(STORES.MATERIALS, 'readonly', (store) => store.get(Number(roomId)));
        return Array.isArray(record?.items) ? record.items : [];
    },

    // =========================================================================
    // 4. THẺ GHI NHỚ 3D FLASHCARDS (CACHE + CẬP NHẬT NGOẠI TUYẾN)
    // =========================================================================
    async saveRoomDecksSummary(roomId, decks = []) {
        if (!Array.isArray(decks)) return;
        const db = await openDatabase();
        if (!db) return;

        // Giữ lại danh sách cards đã lưu trước đó nếu có để không bị ghi đè mất nội dung thẻ 3D
        const existingList = await this.getCachedRoomDecks(roomId);
        const existingMap = new Map(existingList.map((d) => [Number(d.id), d]));

        return new Promise((resolve) => {
            const tx = db.transaction(STORES.FLASHCARD_DECKS, 'readwrite');
            const store = tx.objectStore(STORES.FLASHCARD_DECKS);
            const now = Date.now();

            for (const deck of decks) {
                if (!deck?.id) continue;
                const prev = existingMap.get(Number(deck.id));
                store.put({
                    ...prev,
                    ...deck,
                    id: Number(deck.id),
                    roomId: Number(roomId),
                    cards: prev?.cards || deck.cards || null,
                    updatedAt: now
                });
            }
            tx.oncomplete = () => resolve(true);
            tx.onerror = () => resolve(false);
        });
    },

    async saveFlashcardDeckDetail(roomId, deckDetail) {
        if (!deckDetail?.id) return;
        await runTx(STORES.FLASHCARD_DECKS, 'readwrite', (store) =>
            store.put({
                ...deckDetail,
                id: Number(deckDetail.id),
                roomId: Number(roomId || deckDetail.roomId),
                updatedAt: Date.now()
            })
        );
    },

    async getCachedRoomDecks(roomId) {
        const db = await openDatabase();
        if (!db) return [];

        return new Promise((resolve) => {
            try {
                const tx = db.transaction(STORES.FLASHCARD_DECKS, 'readonly');
                const idx = tx.objectStore(STORES.FLASHCARD_DECKS).index('byRoomId');
                const req = idx.getAll(Number(roomId));
                req.onsuccess = () => resolve(Array.isArray(req.result) ? req.result : []);
                req.onerror = () => resolve([]);
            } catch {
                resolve([]);
            }
        });
    },

    async getCachedDeckDetail(deckId) {
        const record = await runTx(STORES.FLASHCARD_DECKS, 'readonly', (store) => store.get(Number(deckId)));
        return record && Array.isArray(record.cards) ? record : null;
    },

    async updateCachedCardMastery(deckId, cardId, masteryLevel) {
        const deck = await runTx(STORES.FLASHCARD_DECKS, 'readonly', (store) => store.get(Number(deckId)));
        if (!deck || !Array.isArray(deck.cards)) return null;

        let updatedCard = null;
        deck.cards = deck.cards.map((c) => {
            if (Number(c.id) === Number(cardId)) {
                updatedCard = { ...c, masteryLevel: Number(masteryLevel) };
                return updatedCard;
            }
            return c;
        });

        deck.masteredCount = deck.cards.filter((c) => Number(c.masteryLevel) === 2).length;
        deck.updatedAt = Date.now();
        await runTx(STORES.FLASHCARD_DECKS, 'readwrite', (store) => store.put(deck));
        return updatedCard;
    },

    // =========================================================================
    // 5. LỊCH SỬ THI QUIZ & LỜI GIẢI AI
    // =========================================================================
    async saveQuizAttemptsList(attempts = []) {
        if (!Array.isArray(attempts)) return;
        const db = await openDatabase();
        if (!db) return;

        const existing = await this.getCachedQuizAttempts();
        const existingMap = new Map(existing.map((a) => [Number(a.attemptId), a]));

        return new Promise((resolve) => {
            const tx = db.transaction(STORES.QUIZ_ATTEMPTS, 'readwrite');
            const store = tx.objectStore(STORES.QUIZ_ATTEMPTS);
            const now = Date.now();

            for (const item of attempts) {
                if (!item?.attemptId) continue;
                const prev = existingMap.get(Number(item.attemptId));
                store.put({
                    ...prev,
                    ...item,
                    attemptId: Number(item.attemptId),
                    details: prev?.details || item.details || null,
                    updatedAt: now
                });
            }
            tx.oncomplete = () => resolve(true);
            tx.onerror = () => resolve(false);
        });
    },

    async saveQuizAttemptDetail(detail) {
        if (!detail?.attemptId) return;
        await runTx(STORES.QUIZ_ATTEMPTS, 'readwrite', (store) =>
            store.put({
                ...detail,
                attemptId: Number(detail.attemptId),
                updatedAt: Date.now()
            })
        );
    },

    async getCachedQuizAttempts() {
        const items = await runTx(STORES.QUIZ_ATTEMPTS, 'readonly', (store) => store.getAll());
        if (!Array.isArray(items)) return [];
        return items.sort((a, b) => new Date(b.completedAt || 0) - new Date(a.completedAt || 0));
    },

    async getCachedQuizAttemptDetail(attemptId) {
        const record = await runTx(STORES.QUIZ_ATTEMPTS, 'readonly', (store) => store.get(Number(attemptId)));
        return record && Array.isArray(record.details) ? record : null;
    },

    // =========================================================================
    // 6. HÀNG ĐỢI ĐỒNG BỘ NGOẠI TUYẾN (SYNC QUEUE)
    // =========================================================================
    async enqueueSyncAction(type, payload) {
        await runTx(STORES.SYNC_QUEUE, 'readwrite', (store) =>
            store.add({
                type,
                payload,
                createdAt: Date.now()
            })
        );
    },

    async getPendingSyncActions() {
        const items = await runTx(STORES.SYNC_QUEUE, 'readonly', (store) => store.getAll());
        return Array.isArray(items) ? items : [];
    },

    async removeSyncAction(id) {
        await runTx(STORES.SYNC_QUEUE, 'readwrite', (store) => store.delete(Number(id)));
    },

    /**
     * Đẩy tuần tự toàn bộ hàng đợi đồng bộ lên máy chủ khi có mạng trở lại.
     */
    async flushSyncQueue(handlers = {}) {
        const pending = await this.getPendingSyncActions();
        if (!pending.length) {
            return { syncedCount: 0, remainingCount: 0 };
        }

        let syncedCount = 0;
        for (const item of pending) {
            const handler = handlers[item.type];
            if (!handler) continue;

            try {
                await handler(item.payload);
                await this.removeSyncAction(item.id);
                syncedCount += 1;
            } catch (err) {
                // Nếu vẫn lỗi mạng (status 0) thì dừng vòng lặp để thử lại sau; lỗi 4xx bỏ qua khỏi hàng đợi
                if (err?.statusCode === 0) {
                    break;
                }
                await this.removeSyncAction(item.id);
            }
        }

        const remaining = await this.getPendingSyncActions();
        return { syncedCount, remainingCount: remaining.length };
    },

    // =========================================================================
    // 7. SỔ TAY GHI CHÚ CÁ NHÂN (PERSONAL MARKDOWN SCRATCHPAD)
    // =========================================================================
    async saveScratchpad(roomId, roomName, content) {
        const key = Number(roomId);
        const record = {
            roomId: key,
            roomName: roomName || `Phòng #${key}`,
            content: String(content || ''),
            updatedAt: Date.now()
        };
        await runTx(STORES.SCRATCHPADS, 'readwrite', (store) => store.put(record));
        return record;
    },

    async getScratchpad(roomId) {
        return runTx(STORES.SCRATCHPADS, 'readonly', (store) => store.get(Number(roomId)));
    }
};
