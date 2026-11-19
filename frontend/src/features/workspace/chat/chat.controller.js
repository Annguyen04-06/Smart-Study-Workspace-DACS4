/**
 * Cột 2 Workspace — Controller điều phối Thảo luận Thời gian thực, Ghim Kiến thức (Pinned Messages),
 * Nhập Giọng nói Tiếng Việt (Web Speech Recognition), Đọc thành tiếng (speechSynthesis) và @bot Streaming thật.
 *
 * Luồng tốc độ cao:
 * - Optimistic UI: tin nhắn / ghim / cảm xúc hiện ngay 0ms, tự khớp với dữ liệu thật từ SignalR, lỗi mạng thì
 *   tự hoàn tác + báo Toast.
 * - @bot Streaming: BotStreamStart (hiện bong bóng) -> BotStreamChunk (vẽ chữ bằng requestAnimationFrame, chỉ tự cuộn
 *   khi đang ở gần đáy) -> BotStreamComplete (dựng Markdown + KaTeX + Highlight 1 lần, gắn huy hiệu ⚡ tốc độ).
 * - Delta Sync: mở lại App / có mạng / SignalR vừa kết nối lại => chỉ tải các tin mới hơn tin cuối đang có.
 */

import { signalrClient } from '../../../core/realtime/signalrClient.js';
import { offlineDb } from '../../../core/storage/offlineDb.service.js';
import { enhanceMarkdownContainer } from '../../../shared/components/markdownViewer.component.js';
import { sendDesktopNotification } from '../../../shared/components/notification.service.js';
import { toast } from '../../../shared/components/toast.component.js';
import {
    copyTextToClipboard,
    escapeHtml,
    qs
} from '../../../shared/utils/dom.utils.js';
import { prefersReducedMotion } from '../../../shared/utils/spotlight.utils.js';
import { chatApi, DELTA_SYNC_PAGE_SIZE } from './chat.api.js';
import {
    renderChatSkeletons,
    renderMessageItem,
    renderPendingOwnMessage,
    renderPinnedMessagesBanner,
    renderStreamingBotCard
} from './message.view.js';

const NEAR_BOTTOM_THRESHOLD_PX = 140;

function createClientMessageId() {
    return `c${Date.now().toString(36)}${Math.random().toString(36).slice(2, 10)}`;
}

function createElementFromHtml(html) {
    const wrapper = document.createElement('div');
    wrapper.innerHTML = html;
    return wrapper.firstElementChild;
}

function withToggledReaction(msg, emoji, userId) {
    const reactions = Object.fromEntries(
        Object.entries(msg.reactions || {}).map(([key, ids]) => [key, Array.isArray(ids) ? [...ids] : []])
    );
    const users = reactions[emoji] || [];
    const hasReacted = users.some((id) => Number(id) === Number(userId));
    const nextUsers = hasReacted
        ? users.filter((id) => Number(id) !== Number(userId))
        : [...users, Number(userId)];
    if (nextUsers.length > 0) {
        reactions[emoji] = nextUsers;
    } else {
        delete reactions[emoji];
    }
    return { ...msg, reactions };
}

export function createChatController({ roomId, currentUser, roomName = '', onSaveToScratchpad = null }) {
    const messagesListEl = qs('#chat-messages-list');
    const pinnedContainerEl = qs('#ws-pinned-banner-container');
    const scrollViewportEl = qs('#chat-scroll-viewport');
    const chatFormEl = qs('#chat-input-form');
    const textareaEl = qs('#chat-message-input');
    const voiceBtnEl = qs('#ws-voice-input-btn');
    const typingBannerEl = qs('#chat-typing-indicator');
    const botThinkingBannerEl = qs('#chat-bot-thinking');
    const promptChipsEl = qs('#chat-prompt-chips');
    const loadOlderBtn = qs('#btn-load-older-messages');

    let oldestMessageId = null;
    let newestMessageId = null;
    let hasMoreHistory = true;
    let isLoadingHistory = false;
    let pinnedMessages = [];
    let isPinnedExpanded = false;
    let activeSpeechMessageId = null;
    let recognition = null;
    let isRecordingVoice = false;
    let isDeltaSyncRunning = false;
    let isDeltaSyncQueued = false;

    const messagesById = new Map();
    const pendingSends = new Map();   // clientMessageId -> { content, element }
    const botStreams = new Map();     // tempId -> { element, textEl, text, isPaintScheduled }
    const typingUsers = new Map();
    let localTypingTimeout = null;
    let isLocalTypingSent = false;

    const isNearBottom = () => {
        if (!scrollViewportEl) return true;
        const distance = scrollViewportEl.scrollHeight - scrollViewportEl.scrollTop - scrollViewportEl.clientHeight;
        return distance <= NEAR_BOTTOM_THRESHOLD_PX;
    };

    const scrollToComfortableBottom = (smooth = false, isBotCard = false) => {
        if (!scrollViewportEl) return;
        if (isBotCard && messagesListEl?.lastElementChild) {
            // Cuộn sao cho phần đầu câu trả lời mới của @bot nằm ở tầm mắt thoải mái nhất
            messagesListEl.lastElementChild.scrollIntoView({
                behavior: 'smooth',
                block: 'nearest'
            });
            return;
        }
        scrollViewportEl.scrollTo({
            top: scrollViewportEl.scrollHeight,
            behavior: smooth && !prefersReducedMotion() ? 'smooth' : 'auto'
        });
    };

    const rememberMessage = (msg) => {
        messagesById.set(msg.id, msg);
        if (oldestMessageId === null || msg.id < oldestMessageId) {
            oldestMessageId = msg.id;
        }
        if (newestMessageId === null || msg.id > newestMessageId) {
            newestMessageId = msg.id;
        }
    };

    const createMessageElement = (msg, options = {}) =>
        createElementFromHtml(renderMessageItem(msg, currentUser.id, options));

    const renderPinnedBanner = () => {
        if (!pinnedContainerEl) return;
        pinnedContainerEl.innerHTML = renderPinnedMessagesBanner(pinnedMessages, isPinnedExpanded);
        enhanceMarkdownContainer(pinnedContainerEl);
    };

    const refreshPinnedMessages = async () => {
        try {
            pinnedMessages = await chatApi.getPinnedMessages(roomId);
            offlineDb.savePinnedMessages(roomId, pinnedMessages).catch(() => {});
            renderPinnedBanner();
        } catch {
            // Đọc lại tin nhắn đã ghim từ bộ nhớ đệm ngoại tuyến IndexedDB
            const cached = await offlineDb.getCachedPinnedMessages(roomId);
            if (cached.length > 0) {
                pinnedMessages = cached;
                renderPinnedBanner();
            }
        }
    };

    const updateMessageInDom = (updatedMsg) => {
        messagesById.set(updatedMsg.id, updatedMsg);
        const existingNode = messagesListEl?.querySelector(`[data-message-id="${updatedMsg.id}"]`);
        if (existingNode) {
            const replacement = createMessageElement(updatedMsg, { isRealtimeNew: false });
            if (replacement) {
                existingNode.replaceWith(replacement);
                enhanceMarkdownContainer(replacement);
            }
        }
    };

    // Cập nhật trạng thái ghim trên tin nhắn + thanh "Kiến thức Đã Ghim" (dùng cho cả Optimistic lẫn dữ liệu thật)
    const applyPinState = (msg) => {
        updateMessageInDom(msg);
        pinnedMessages = msg.isPinned
            ? [msg, ...pinnedMessages.filter((m) => m.id !== msg.id)]
            : pinnedMessages.filter((m) => m.id !== msg.id);
        offlineDb.savePinnedMessages(roomId, pinnedMessages).catch(() => {});
        renderPinnedBanner();
    };

    const togglePinForMessage = async (messageId) => {
        const original = messagesById.get(messageId) || pinnedMessages.find((m) => m.id === messageId);
        const previousPinned = pinnedMessages;
        if (original) {
            applyPinState({ ...original, isPinned: !original.isPinned });
        }

        try {
            if (signalrClient.isConnected()) {
                await signalrClient.togglePinMessage(roomId, messageId);
            } else {
                applyPinState(await chatApi.togglePinMessage(roomId, messageId));
            }
        } catch (err) {
            if (original) {
                updateMessageInDom(original);
                pinnedMessages = previousPinned;
                renderPinnedBanner();
            }
            toast.error(err.message || 'Không thể cập nhật trạng thái ghim tin nhắn.');
        }
    };

    const toggleReactionForMessage = async (messageId, emoji) => {
        const original = messagesById.get(messageId);
        if (original) {
            updateMessageInDom(withToggledReaction(original, emoji, currentUser.id));
        }

        try {
            if (signalrClient.isConnected()) {
                await signalrClient.toggleMessageReaction(roomId, messageId, emoji);
            } else {
                updateMessageInDom(await chatApi.toggleMessageReaction(roomId, messageId, emoji));
            }
        } catch (err) {
            if (original) {
                updateMessageInDom(original);
            }
            toast.error(err.message || 'Không thể cập nhật cảm xúc tin nhắn.');
        }
    };

    const spawnFloatingEmojiBurst = (emoji) => {
        if (prefersReducedMotion()) return;
        const anchorEl = qs('#ws-quick-reaction-bar') || chatFormEl;
        const rect = anchorEl ? anchorEl.getBoundingClientRect() : { right: window.innerWidth - 80, top: window.innerHeight - 90, width: 120 };
        const baseX = rect.left ? rect.left + rect.width * 0.65 : rect.right - 60;
        const baseY = rect.top - 12;

        for (let i = 0; i < 3; i++) {
            const particle = document.createElement('span');
            particle.className = 'floating-emoji-burst';
            particle.textContent = emoji;

            const offsetX = (Math.random() - 0.5) * 56;
            const swayA = `${((Math.random() - 0.5) * 38).toFixed(1)}px`;
            const swayB = `${((Math.random() - 0.5) * 46).toFixed(1)}px`;
            const swayC = `${((Math.random() - 0.5) * 32).toFixed(1)}px`;
            const dur = `${(1.85 + Math.random() * 0.65).toFixed(2)}s`;

            particle.style.left = `${Math.max(24, Math.min(window.innerWidth - 48, baseX + offsetX))}px`;
            particle.style.top = `${Math.max(80, baseY)}px`;
            particle.style.setProperty('--sway-a', swayA);
            particle.style.setProperty('--sway-b', swayB);
            particle.style.setProperty('--sway-c', swayC);
            particle.style.setProperty('--burst-dur', dur);
            particle.style.animationDelay = `${i * 95}ms`;

            document.body.appendChild(particle);
            particle.addEventListener('animationend', () => particle.remove(), { once: true });
        }
    };

    // ------------------------------------------------------------------
    // Optimistic UI: tin nhắn của chính mình
    // ------------------------------------------------------------------
    const showPendingMessage = (clientId, content) => {
        if (!messagesListEl) return;
        qs('#chat-empty-hint', messagesListEl)?.remove();
        const element = createElementFromHtml(renderPendingOwnMessage({ clientId, content }));
        messagesListEl.appendChild(element);
        pendingSends.set(clientId, { content, element });
        scrollToComfortableBottom(true, false);
    };

    const confirmPendingMessage = (clientId, savedMsg) => {
        const pending = pendingSends.get(clientId);
        pendingSends.delete(clientId);
        if (!savedMsg || messagesById.has(savedMsg.id)) {
            pending?.element?.remove();
            return;
        }

        rememberMessage(savedMsg);
        const element = createMessageElement(savedMsg, { isRealtimeNew: false });
        if (pending?.element?.isConnected) {
            pending.element.replaceWith(element);
        } else {
            messagesListEl?.appendChild(element);
        }
        enhanceMarkdownContainer(element);
    };

    const rollbackPendingMessage = (clientId, content) => {
        pendingSends.get(clientId)?.element?.remove();
        pendingSends.delete(clientId);
        if (textareaEl && !textareaEl.value.trim()) {
            textareaEl.value = content;
            textareaEl.dispatchEvent(new Event('input'));
        }
    };

    // Tin của chính mình về qua Delta Sync (không có clientMessageId) => khớp với bong bóng đang gửi cùng nội dung
    const findPendingIdByContent = (content) => {
        const normalized = String(content || '').trim();
        for (const [clientId, pending] of pendingSends) {
            if (pending.content === normalized) {
                return clientId;
            }
        }
        return null;
    };

    const handleSubmitMessage = async () => {
        const content = textareaEl?.value.trim() || '';
        if (!content) return;

        textareaEl.value = '';
        textareaEl.style.height = 'auto';

        if (isLocalTypingSent) {
            isLocalTypingSent = false;
            signalrClient.sendTypingStatus(roomId, false).catch(() => {});
        }

        const clientId = createClientMessageId();
        showPendingMessage(clientId, content);

        try {
            const savedMsg = signalrClient.isConnected()
                ? await signalrClient.sendChatMessage(roomId, content, clientId)
                : await chatApi.postMessageFallback(roomId, content);
            confirmPendingMessage(clientId, savedMsg);
        } catch (err) {
            rollbackPendingMessage(clientId, content);
            toast.error(err.message || 'Gửi tin nhắn thất bại. Nội dung đã được giữ lại để bạn gửi lại.');
        }
    };

    // ------------------------------------------------------------------
    // @bot Streaming thật
    // ------------------------------------------------------------------
    const ensureBotStreamCard = (tempId, createdAt) => {
        const existing = botStreams.get(tempId);
        if (existing || !messagesListEl) {
            return existing;
        }

        qs('#chat-empty-hint', messagesListEl)?.remove();
        const shouldStickToBottom = isNearBottom();
        const element = createElementFromHtml(renderStreamingBotCard({
            tempId,
            createdAt: createdAt || new Date().toISOString()
        }));
        messagesListEl.appendChild(element);

        const stream = {
            element,
            textEl: element.querySelector('[data-stream-text]'),
            text: '',
            isPaintScheduled: false
        };
        botStreams.set(tempId, stream);
        if (shouldStickToBottom) {
            scrollToComfortableBottom(true, false);
        }
        return stream;
    };

    // Gộp mọi cụm chữ đến trong cùng 1 khung hình thành 1 lần vẽ (60FPS, không giật khi mạng dồn gói)
    const scheduleStreamPaint = (stream) => {
        if (stream.isPaintScheduled) return;
        stream.isPaintScheduled = true;
        window.requestAnimationFrame(() => {
            stream.isPaintScheduled = false;
            if (!stream.element.isConnected || !stream.textEl) return;
            const shouldStickToBottom = isNearBottom();
            stream.textEl.textContent = stream.text;
            if (shouldStickToBottom && scrollViewportEl) {
                scrollViewportEl.scrollTop = scrollViewportEl.scrollHeight;
            }
        });
    };

    const handleBotStreamComplete = (payload) => {
        const stream = botStreams.get(payload.tempId);
        botStreams.delete(payload.tempId);

        const msg = payload.message || {
            id: payload.messageId,
            roomId: Number(roomId),
            senderId: null,
            senderName: 'Trợ giảng AI (@bot)',
            isAiBot: true,
            isPinned: false,
            content: payload.fullContent,
            reactions: {},
            createdAt: new Date().toISOString()
        };

        if (messagesById.has(msg.id)) {
            stream?.element?.remove();
            return;
        }

        const shouldStickToBottom = isNearBottom();
        rememberMessage(msg);
        const finalElement = createMessageElement(msg, {
            isRealtimeNew: false,
            responseStats: { firstChunkMs: payload.firstChunkMs, durationMs: payload.durationMs }
        });
        if (stream?.element?.isConnected) {
            stream.element.replaceWith(finalElement);
        } else {
            messagesListEl?.appendChild(finalElement);
        }
        enhanceMarkdownContainer(finalElement);
        if (shouldStickToBottom) {
            scrollToComfortableBottom(false, false);
        }

        // Bắn thông báo hệ điều hành khi người dùng đang thu nhỏ App / chuyển cửa sổ khác
        sendDesktopNotification({
            title: `🤖 Trợ giảng @bot vừa trả lời (${roomName || 'Phòng học'})`,
            body: msg.content,
            tag: `ssw-bot-msg-${roomId}`
        });
    };

    // Bỏ các khung stream "mồ côi" khi câu trả lời hoàn chỉnh đã về qua Delta Sync (lỡ BotStreamComplete lúc mất mạng)
    const removeOldestBotStreamCard = () => {
        const oldestEntry = botStreams.entries().next();
        if (oldestEntry.done) return;
        const [tempId, stream] = oldestEntry.value;
        stream.element?.remove();
        botStreams.delete(tempId);
    };

    const toggleSpeakMessage = (messageId, buttonEl) => {
        if (!('speechSynthesis' in window)) {
            toast.warning('Trình duyệt hiện tại không hỗ trợ đọc giọng nói.');
            return;
        }

        if (activeSpeechMessageId === messageId && window.speechSynthesis.speaking) {
            window.speechSynthesis.cancel();
            activeSpeechMessageId = null;
            buttonEl.innerHTML = '<span>🔊 Đọc thành tiếng</span>';
            return;
        }

        window.speechSynthesis.cancel();
        const msg = messagesById.get(messageId);
        if (!msg) return;

        // Làm sạch ký hiệu Markdown & LaTeX để giọng đọc tiếng Việt tự nhiên
        const plainSpeech = String(msg.content || '')
            .replace(/```[\s\S]*?```/g, ' Đoạn mã nguồn minh họa. ')
            .replace(/\$\$[\s\S]*?\$\$/g, ' công thức toán học ')
            .replace(/\$[^$\n]+?\$/g, ' biểu thức ')
            .replace(/[#*_>|`~-]/g, ' ')
            .replace(/\s+/g, ' ')
            .trim();

        const utterance = new SpeechSynthesisUtterance(plainSpeech);
        utterance.lang = 'vi-VN';
        utterance.rate = 1.02;

        const voices = window.speechSynthesis.getVoices();
        const viVoice = voices.find((v) => v.lang.toLowerCase().startsWith('vi'));
        if (viVoice) {
            utterance.voice = viVoice;
        }

        activeSpeechMessageId = messageId;
        buttonEl.innerHTML = '<span class="text-emerald-600 font-bold">⏹ Dừng đọc</span>';

        const resetBtn = () => {
            activeSpeechMessageId = null;
            buttonEl.innerHTML = '<span>🔊 Đọc thành tiếng</span>';
        };
        utterance.onend = resetBtn;
        utterance.onerror = resetBtn;
        window.speechSynthesis.speak(utterance);
    };

    const initVietnameseVoiceInput = () => {
        const SpeechRec = window.SpeechRecognition || window.webkitSpeechRecognition;
        if (!voiceBtnEl) return;

        if (!SpeechRec) {
            voiceBtnEl.addEventListener('click', () => {
                toast.warning('Trình duyệt này chưa hỗ trợ Web Speech API (hãy dùng Chrome hoặc Edge).');
            });
            return;
        }

        recognition = new SpeechRec();
        recognition.lang = 'vi-VN';
        recognition.interimResults = false;
        recognition.maxAlternatives = 1;

        recognition.onstart = () => {
            isRecordingVoice = true;
            voiceBtnEl.classList.add('voice-recording-active');
            toast.info('Đang lắng nghe giọng nói tiếng Việt của bạn...', 2200);
        };

        recognition.onresult = (event) => {
            const transcript = event.results?.[0]?.[0]?.transcript?.trim() || '';
            if (transcript && textareaEl) {
                const currentVal = textareaEl.value.trim();
                textareaEl.value = currentVal ? `${currentVal} ${transcript}` : `@bot ${transcript}`;
                textareaEl.focus();
                textareaEl.dispatchEvent(new Event('input'));
            }
        };

        recognition.onerror = () => {
            toast.warning('Không nhận diện được âm thanh. Vui lòng kiểm tra quyền Micro.');
        };

        recognition.onend = () => {
            isRecordingVoice = false;
            voiceBtnEl.classList.remove('voice-recording-active');
        };

        voiceBtnEl.addEventListener('click', () => {
            if (isRecordingVoice) {
                recognition.stop();
            } else {
                try {
                    recognition.start();
                } catch {
                    recognition.stop();
                }
            }
        });
    };

    const updateTypingBanner = () => {
        if (!typingBannerEl) return;
        const names = Array.from(typingUsers.values());
        if (names.length === 0) {
            typingBannerEl.classList.add('hidden');
            typingBannerEl.innerHTML = '';
            return;
        }
        const text = names.length === 1
            ? `${escapeHtml(names[0])} đang soạn tin nhắn`
            : `${names.length} thành viên đang soạn tin nhắn`;
        typingBannerEl.classList.remove('hidden');
        typingBannerEl.innerHTML = `
            <span class="inline-flex items-center gap-1.5 text-xs text-slate-500 dark:text-slate-400">
                <span>${text}</span>
                <span class="inline-flex items-center gap-0.5 text-indigo-500">
                    <span class="typing-dot"></span><span class="typing-dot"></span><span class="typing-dot"></span>
                </span>
            </span>
        `;
    };

    const appendIncomingMessage = (msg) => {
        if (!messagesListEl || !msg || messagesById.has(msg.id)) {
            return;
        }

        // Tin của chính mình: thay bong bóng "đang gửi" bằng tin thật thay vì hiện trùng 2 lần
        const isMine = Number(msg.senderId) === Number(currentUser.id);
        const pendingId = msg.clientMessageId && pendingSends.has(msg.clientMessageId)
            ? msg.clientMessageId
            : (isMine ? findPendingIdByContent(msg.content) : null);
        if (pendingId) {
            confirmPendingMessage(pendingId, msg);
            return;
        }

        rememberMessage(msg);
        qs('#chat-empty-hint', messagesListEl)?.remove();

        const element = createMessageElement(msg, { isRealtimeNew: true });
        if (element) {
            messagesListEl.appendChild(element);
            enhanceMarkdownContainer(element);
        }
        scrollToComfortableBottom(true, Boolean(msg.isAiBot));
    };

    const loadInitialMessages = async () => {
        if (!messagesListEl) return;
        isLoadingHistory = true;
        messagesListEl.innerHTML = renderChatSkeletons();

        try {
            const [batch, initialPinned] = await Promise.all([
                chatApi.getMessages(roomId, { pageSize: 35 }),
                chatApi.getPinnedMessages(roomId)
            ]);

            pinnedMessages = initialPinned;
            offlineDb.savePinnedMessages(roomId, initialPinned).catch(() => {});
            renderPinnedBanner();

            messagesById.clear();
            botStreams.clear();
            pendingSends.clear();
            oldestMessageId = null;
            newestMessageId = null;

            if (batch.length === 0) {
                hasMoreHistory = false;
                loadOlderBtn?.classList.add('hidden');
                messagesListEl.innerHTML = `
                    <div id="chat-empty-hint" class="py-12 text-center">
                        <p class="text-sm font-bold text-slate-700 dark:text-slate-200">Bắt đầu cuộc thảo luận học thuật!</p>
                        <p class="mt-1 text-xs text-slate-500">Gõ <code class="rounded bg-indigo-50 px-1.5 py-0.5 font-mono text-indigo-700">@bot</code> hoặc bấm nút Micro để hỏi Trợ giảng AI.</p>
                    </div>
                `;
                return;
            }

            hasMoreHistory = batch.length >= 35;
            loadOlderBtn?.classList.toggle('hidden', !hasMoreHistory);

            messagesListEl.innerHTML = batch
                .map((m) => {
                    rememberMessage(m);
                    return renderMessageItem(m, currentUser.id, { isRealtimeNew: false });
                })
                .join('');

            enhanceMarkdownContainer(messagesListEl);
            scrollToComfortableBottom(false, false);
        } catch (err) {
            const cachedPinned = await offlineDb.getCachedPinnedMessages(roomId);
            if (cachedPinned.length > 0) {
                pinnedMessages = cachedPinned;
                renderPinnedBanner();
            }
            messagesListEl.innerHTML = `
                <div id="chat-empty-hint" class="py-10 text-center space-y-2">
                    <p class="text-sm font-bold text-amber-700 dark:text-amber-300">Đang ở chế độ Ngoại tuyến</p>
                    <p class="text-xs text-slate-500 dark:text-slate-400">Chat thời gian thực tạm ngưng khi mất mạng. Bạn vẫn có thể xem <strong>Tin nhắn đã ghim</strong> ở thanh trên, đọc <strong>Tóm tắt PDF</strong>, ghi <strong>Sổ tay</strong> và lật <strong>Thẻ 3D Flashcards</strong>!</p>
                </div>
            `;
        } finally {
            isLoadingHistory = false;
        }
    };

    const loadOlderMessages = async () => {
        if (isLoadingHistory || !hasMoreHistory || !oldestMessageId || !messagesListEl || !scrollViewportEl) {
            return;
        }
        isLoadingHistory = true;
        const previousScrollHeight = scrollViewportEl.scrollHeight;

        try {
            const olderBatch = await chatApi.getMessages(roomId, {
                beforeId: oldestMessageId,
                pageSize: 25
            });

            if (olderBatch.length === 0) {
                hasMoreHistory = false;
                loadOlderBtn?.classList.add('hidden');
                return;
            }

            hasMoreHistory = olderBatch.length >= 25;
            loadOlderBtn?.classList.toggle('hidden', !hasMoreHistory);

            const htmlChunk = olderBatch
                .filter((m) => !messagesById.has(m.id))
                .map((m) => {
                    rememberMessage(m);
                    return renderMessageItem(m, currentUser.id, { isRealtimeNew: false });
                })
                .join('');

            messagesListEl.insertAdjacentHTML('afterbegin', htmlChunk);
            enhanceMarkdownContainer(messagesListEl);
            scrollViewportEl.scrollTop = scrollViewportEl.scrollHeight - previousScrollHeight;
        } catch (err) {
            toast.error(err.message || 'Không thể tải tin nhắn cũ hơn.');
        } finally {
            isLoadingHistory = false;
        }
    };

    // ------------------------------------------------------------------
    // Delta Sync khi mở lại App / có mạng / SignalR kết nối lại
    // ------------------------------------------------------------------
    const syncMissedMessages = async () => {
        if (!messagesListEl || isLoadingHistory) return;
        if (isDeltaSyncRunning) {
            isDeltaSyncQueued = true;
            return;
        }

        isDeltaSyncRunning = true;
        try {
            const missed = await chatApi.getMessagesAfter(roomId, newestMessageId || 0);
            if (missed.length >= DELTA_SYNC_PAGE_SIZE) {
                // Lỡ quá nhiều tin (mở lại sau thời gian dài) => tải lại gọn khung chat thay vì chèn hàng trăm tin
                await loadInitialMessages();
                return;
            }

            for (const msg of missed) {
                if (msg.isAiBot && !messagesById.has(msg.id)) {
                    removeOldestBotStreamCard();
                }
                appendIncomingMessage(msg);
            }
            if (missed.length > 0) {
                refreshPinnedMessages().catch(() => {});
            }
        } catch {
            // Vẫn ngoại tuyến: giữ nguyên giao diện, lần mở lại / có mạng kế tiếp sẽ đồng bộ tiếp
        } finally {
            isDeltaSyncRunning = false;
            if (isDeltaSyncQueued) {
                isDeltaSyncQueued = false;
                syncMissedMessages();
            }
        }
    };

    const resumeRealtimeAndSync = async () => {
        await signalrClient.resume().catch(() => false);
        await syncMissedMessages();
    };

    const bindEvents = () => {
        enhanceMarkdownContainer(messagesListEl);
        initVietnameseVoiceInput();

        chatFormEl?.addEventListener('submit', (e) => {
            e.preventDefault();
            handleSubmitMessage();
        });

        textareaEl?.addEventListener('keydown', (e) => {
            if (e.key === 'Enter' && !e.shiftKey) {
                e.preventDefault();
                handleSubmitMessage();
            }
        });

        textareaEl?.addEventListener('input', () => {
            textareaEl.style.height = 'auto';
            textareaEl.style.height = `${Math.min(textareaEl.scrollHeight, 132)}px`;

            if (!isLocalTypingSent) {
                isLocalTypingSent = true;
                signalrClient.sendTypingStatus(roomId, true).catch(() => {});
            }

            if (localTypingTimeout) clearTimeout(localTypingTimeout);
            localTypingTimeout = setTimeout(() => {
                isLocalTypingSent = false;
                signalrClient.sendTypingStatus(roomId, false).catch(() => {});
            }, 1800);
        });

        loadOlderBtn?.addEventListener('click', loadOlderMessages);

        scrollViewportEl?.addEventListener('scroll', () => {
            if (scrollViewportEl.scrollTop <= 24 && hasMoreHistory && !isLoadingHistory) {
                loadOlderMessages();
            }
        });

        promptChipsEl?.addEventListener('click', (e) => {
            const chip = e.target.closest('[data-prompt-template]');
            if (!chip || !textareaEl) return;
            textareaEl.value = chip.dataset.promptTemplate || '@bot ';
            textareaEl.focus();
            textareaEl.dispatchEvent(new Event('input'));
        });

        // Các nút hành động trên tin nhắn (@bot Copy, Ghim kiến thức, Đọc thành tiếng)
        messagesListEl?.addEventListener('click', async (e) => {
            const actionBtn = e.target.closest('[data-msg-action]');
            if (!actionBtn) return;

            const action = actionBtn.dataset.msgAction;
            const msgId = Number(actionBtn.dataset.messageId);

            if (action === 'copy-bot') {
                const msg = messagesById.get(msgId);
                if (!msg) return;
                await copyTextToClipboard(msg.content);
                const prev = actionBtn.innerHTML;
                actionBtn.innerHTML = '<span class="text-emerald-600 font-bold">Đã chép câu trả lời ✓</span>';
                setTimeout(() => { actionBtn.innerHTML = prev; }, 1800);
                return;
            }

            if (action === 'pin') {
                await togglePinForMessage(msgId);
                return;
            }

            if (action === 'react') {
                const emoji = actionBtn.dataset.emoji;
                if (emoji) {
                    await toggleReactionForMessage(msgId, emoji);
                }
                return;
            }

            if (action === 'speak-bot') {
                toggleSpeakMessage(msgId, actionBtn);
                return;
            }

            if (action === 'save-to-scratchpad') {
                const msg = messagesById.get(msgId);
                if (!msg) return;
                if (typeof onSaveToScratchpad === 'function') {
                    await onSaveToScratchpad({
                        title: `Giải đáp của Trợ giảng @bot`,
                        markdownContent: msg.content
                    });
                    const prev = actionBtn.innerHTML;
                    actionBtn.innerHTML = '<span class="text-emerald-600 font-bold">Đã lưu Sổ tay ✓</span>';
                    setTimeout(() => { actionBtn.innerHTML = prev; }, 1800);
                }
            }
        });

        // Thanh thả cảm xúc nổi nhanh đồng bộ toàn phòng: hiện hiệu ứng ngay trên máy mình (0ms),
        // máy chủ phát cho các thành viên khác
        qs('#ws-quick-reaction-bar')?.addEventListener('click', async (e) => {
            const btn = e.target.closest('[data-room-reaction]');
            if (!btn) return;
            const emoji = btn.dataset.roomReaction;
            if (!emoji) return;

            spawnFloatingEmojiBurst(emoji);
            if (signalrClient.isConnected()) {
                signalrClient.sendRoomReaction(roomId, emoji).catch(() => {});
            }
        });

        // Các nút hành động trên Thanh Pinned Messages Banner
        pinnedContainerEl?.addEventListener('click', async (e) => {
            const btn = e.target.closest('[data-pin-banner-action]');
            if (!btn) return;

            const act = btn.dataset.pinBannerAction;
            if (act === 'toggle-expand') {
                isPinnedExpanded = !isPinnedExpanded;
                renderPinnedBanner();
            } else if (act === 'jump') {
                const msgId = Number(btn.dataset.messageId);
                const targetEl = messagesListEl?.querySelector(`[data-message-id="${msgId}"]`);
                if (targetEl) {
                    isPinnedExpanded = false;
                    renderPinnedBanner();
                    targetEl.scrollIntoView({ behavior: 'smooth', block: 'center' });
                    targetEl.classList.add('ring-2', 'ring-amber-400', 'rounded-2xl');
                    setTimeout(() => targetEl.classList.remove('ring-2', 'ring-amber-400', 'rounded-2xl'), 2000);
                } else {
                    toast.info('Tin nhắn này nằm ở trang lịch sử cũ hơn.');
                }
            } else if (act === 'unpin') {
                await togglePinForMessage(Number(btn.dataset.messageId));
            }
        });

        // Lắng nghe sự kiện SignalR thời gian thực
        signalrClient.on('ReceiveMessage', (msg) => {
            if (Number(msg.roomId) !== Number(roomId)) return;
            if (msg.senderId) {
                typingUsers.delete(msg.senderId);
                updateTypingBanner();
            }
            appendIncomingMessage(msg);

            if (msg.senderId && Number(msg.senderId) !== Number(currentUser.id)) {
                sendDesktopNotification({
                    title: `💬 ${msg.senderName || 'Thành viên'} trong ${roomName || 'Phòng học'}`,
                    body: msg.content,
                    tag: `ssw-chat-msg-${roomId}`
                });
            }
        });

        signalrClient.on('BotStreamStart', (payload) => {
            if (Number(payload?.roomId) !== Number(roomId)) return;
            botThinkingBannerEl?.classList.add('hidden');
            ensureBotStreamCard(payload.tempId, payload.createdAt);
        });

        signalrClient.on('BotStreamChunk', (payload) => {
            if (Number(payload?.roomId) !== Number(roomId) || !payload.chunk) return;
            const stream = ensureBotStreamCard(payload.tempId, null);
            if (!stream) return;
            stream.text += payload.chunk;
            scheduleStreamPaint(stream);
        });

        signalrClient.on('BotStreamComplete', (payload) => {
            if (Number(payload?.roomId) !== Number(roomId)) return;
            handleBotStreamComplete(payload);
        });

        signalrClient.on('BotStreamFailed', (payload) => {
            if (Number(payload?.roomId) !== Number(roomId)) return;
            botStreams.get(payload.tempId)?.element?.remove();
            botStreams.delete(payload.tempId);
            toast.warning(payload.message || 'Trợ giảng AI chưa trả lời được, vui lòng thử lại.');
        });

        signalrClient.on('MessagePinUpdated', (payload) => {
            if (Number(payload.roomId) !== Number(roomId) || !payload.message) return;
            const updated = payload.message;
            const wasPinned = Boolean(pinnedMessages.find((m) => m.id === updated.id));
            applyPinState(updated);
            if (updated.isPinned && !wasPinned) {
                toast.info(`${payload.actorName} đã ghim một kiến thức quan trọng.`, 2200);
            }
        });

        signalrClient.on('ReceiveRoomReaction', (payload) => {
            if (Number(payload?.roomId) !== Number(roomId) || !payload.emoji) return;
            if (Number(payload.userId) === Number(currentUser.id)) return; // Đã hiện ngay lúc bấm
            spawnFloatingEmojiBurst(payload.emoji);
        });

        signalrClient.on('MessageReactionsUpdated', (payload) => {
            if (Number(payload?.roomId) !== Number(roomId) || !payload.message) return;
            updateMessageInDom(payload.message);
        });

        signalrClient.on('UserTyping', (payload) => {
            if (Number(payload.roomId) !== Number(roomId) || Number(payload.userId) === Number(currentUser.id)) {
                return;
            }
            if (payload.isTyping) {
                typingUsers.set(payload.userId, payload.fullName);
            } else {
                typingUsers.delete(payload.userId);
            }
            updateTypingBanner();
        });

        signalrClient.on('BotThinking', (payload) => {
            if (Number(payload.roomId) !== Number(roomId) || !botThinkingBannerEl) return;
            // Khi đã có bong bóng stream thì không cần thêm dải "đang suy nghĩ"
            if (payload.isThinking && botStreams.size === 0) {
                botThinkingBannerEl.classList.remove('hidden');
                scrollToComfortableBottom(true, false);
            } else {
                botThinkingBannerEl.classList.add('hidden');
            }
        });

        // Delta Sync tức thì khi SignalR vừa tham gia lại phòng, khi mở lại App hoặc vừa có mạng trở lại
        signalrClient.on('Reconnected', () => syncMissedMessages());
        document.addEventListener('visibilitychange', () => {
            if (document.visibilityState === 'visible') {
                resumeRealtimeAndSync();
            }
        });
        window.addEventListener('online', () => resumeRealtimeAndSync());
    };

    return {
        async init() {
            bindEvents();
            await loadInitialMessages();
        },
        focusBotPrompt(customPrompt = null) {
            if (!textareaEl) return;
            if (customPrompt) {
                textareaEl.value = customPrompt.trim().toLowerCase().startsWith('@bot')
                    ? customPrompt
                    : `@bot ${customPrompt}`;
            } else if (!textareaEl.value.trim().toLowerCase().startsWith('@bot')) {
                textareaEl.value = '@bot ';
            }
            textareaEl.focus();
            textareaEl.dispatchEvent(new Event('input'));
        }
    };
}
