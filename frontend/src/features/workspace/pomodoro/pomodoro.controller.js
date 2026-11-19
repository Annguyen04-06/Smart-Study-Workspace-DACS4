/**
 * Điều phối Đồng hồ Pomodoro Học Nhóm Thời gian thực (Real-time Synced Group Pomodoro qua SignalR).
 * Đồng bộ từng giây cho toàn bộ thành viên trong phòng, phát chuông âm báo dịu nhẹ qua Web Audio API
 * và bắn pháo giấy chúc mừng khi hoàn thành phiên tập trung 25 phút.
 */

import { signalrClient } from '../../../core/realtime/signalrClient.js';
import { sendDesktopNotification } from '../../../shared/components/notification.service.js';
import { toast } from '../../../shared/components/toast.component.js';
import { qs } from '../../../shared/utils/dom.utils.js';
import { formatTimerMmSs } from '../../../shared/utils/format.utils.js';
import { triggerCelebrationConfetti } from '../../../shared/utils/theme.utils.js';

function playCompletionChime() {
    try {
        const AudioCtx = window.AudioContext || window.webkitAudioContext;
        if (!AudioCtx) return;
        const ctx = new AudioCtx();
        const notes = [523.25, 659.25, 783.99, 1046.5]; // C5 - E5 - G5 - C6 hợp âm vui tươi
        notes.forEach((freq, idx) => {
            const osc = ctx.createOscillator();
            const gain = ctx.createGain();
            osc.type = 'sine';
            osc.frequency.value = freq;
            gain.gain.setValueAtTime(0.0001, ctx.currentTime + idx * 0.14);
            gain.gain.exponentialRampToValueAtTime(0.14, ctx.currentTime + idx * 0.14 + 0.03);
            gain.gain.exponentialRampToValueAtTime(0.0001, ctx.currentTime + idx * 0.14 + 0.45);
            osc.connect(gain);
            gain.connect(ctx.destination);
            osc.start(ctx.currentTime + idx * 0.14);
            osc.stop(ctx.currentTime + idx * 0.14 + 0.48);
        });
    } catch {
        // Bỏ qua nếu trình duyệt chặn autoplay trước tương tác đầu tiên
    }
}

export function createPomodoroController({ roomId }) {
    const timeTextEl = qs('#ws-pomo-time');
    const modeBadgeEl = qs('#ws-pomo-mode');
    const toggleBtnEl = qs('#ws-pomo-toggle-btn');
    const switchModeBtnEl = qs('#ws-pomo-switch-mode-btn');
    const resetBtnEl = qs('#ws-pomo-reset-btn');

    let currentMode = 'Focus'; // 'Focus' (25p) | 'ShortBreak' (5p)
    let isRunning = false;
    let remainingSeconds = 25 * 60;
    let targetEndClientMs = null;
    let tickInterval = null;

    const renderWidget = () => {
        if (timeTextEl) {
            timeTextEl.textContent = formatTimerMmSs(remainingSeconds);
        }
        if (modeBadgeEl) {
            const isFocus = currentMode === 'Focus';
            modeBadgeEl.textContent = isFocus ? 'Focus 25p' : 'Nghỉ 5p';
            modeBadgeEl.className = isFocus
                ? 'rounded-md bg-indigo-500/15 px-1.5 py-0.5 text-[10px] font-bold text-indigo-600 dark:text-indigo-300'
                : 'rounded-md bg-emerald-500/15 px-1.5 py-0.5 text-[10px] font-bold text-emerald-600 dark:text-emerald-300';
        }
        if (toggleBtnEl) {
            toggleBtnEl.innerHTML = isRunning
                ? `<span title="Tạm dừng Pomodoro">⏸</span>`
                : `<span title="Bắt đầu Pomodoro chung cho cả phòng">▶</span>`;
            toggleBtnEl.className = isRunning
                ? 'flex h-6 w-6 items-center justify-center rounded-lg bg-amber-500 text-[11px] font-bold text-white hover:bg-amber-600 transition'
                : 'flex h-6 w-6 items-center justify-center rounded-lg bg-indigo-600 text-[11px] font-bold text-white hover:bg-indigo-700 transition';
        }
    };

    const clearLocalTick = () => {
        if (tickInterval) {
            clearInterval(tickInterval);
            tickInterval = null;
        }
    };

    const applyServerState = (state) => {
        if (!state || Number(state.roomId) !== Number(roomId)) return;

        clearLocalTick();
        currentMode = state.mode || 'Focus';
        isRunning = state.status === 'Running';
        remainingSeconds = Math.max(0, Number(state.remainingSeconds) || 0);

        if (isRunning && state.endTimeUtc) {
            const serverNow = state.serverNowUtc ? new Date(state.serverNowUtc).getTime() : Date.now();
            const endUtcMs = new Date(state.endTimeUtc).getTime();
            const driftAdjustedRemainingMs = Math.max(0, endUtcMs - serverNow);
            targetEndClientMs = Date.now() + driftAdjustedRemainingMs;

            tickInterval = window.setInterval(() => {
                const left = Math.max(0, Math.ceil((targetEndClientMs - Date.now()) / 1000));
                remainingSeconds = left;
                renderWidget();

                if (left <= 0) {
                    clearLocalTick();
                    isRunning = false;
                    remainingSeconds = currentMode === 'Focus' ? 25 * 60 : 5 * 60;
                    renderWidget();
                    playCompletionChime();
                    if (currentMode === 'Focus') {
                        triggerCelebrationConfetti();
                        toast.success('🎉 Tuyệt vời! Cả phòng vừa hoàn thành trọn vẹn phiên tập trung Pomodoro 25 phút!');
                        sendDesktopNotification({
                            title: '⏰ Pomodoro 25 phút đã hoàn tất!',
                            body: 'Tuyệt vời! Cả nhóm vừa kết thúc phiên tập trung sâu — hãy nghỉ giải lao 5 phút nhé.',
                            tag: `ssw-pomo-${roomId}`,
                            force: true
                        });
                    } else {
                        toast.info('⏰ Hết giờ giải lao 5 phút! Sẵn sàng quay lại phiên học mới nhé.');
                        sendDesktopNotification({
                            title: '🔔 Hết giờ nghỉ giải lao Pomodoro!',
                            body: '5 phút thư giãn đã kết thúc — sẵn sàng bắt đầu phiên tập trung 25 phút tiếp theo.',
                            tag: `ssw-pomo-${roomId}`,
                            force: true
                        });
                    }
                }
            }, 1000);
        }

        renderWidget();
    };

    const bindEvents = () => {
        signalrClient.on('PomodoroStateUpdated', applyServerState);

        toggleBtnEl?.addEventListener('click', async () => {
            try {
                if (isRunning) {
                    await signalrClient.pausePomodoro(roomId);
                } else {
                    const mins = currentMode === 'Focus' ? 25 : 5;
                    await signalrClient.startPomodoro(roomId, mins);
                }
            } catch {
                toast.warning('Cần kết nối SignalR trực tuyến để đồng bộ Pomodoro nhóm.');
            }
        });

        switchModeBtnEl?.addEventListener('click', async () => {
            const nextMinutes = currentMode === 'Focus' ? 5 : 25;
            try {
                await signalrClient.resetPomodoro(roomId, nextMinutes);
            } catch {
                currentMode = nextMinutes === 5 ? 'ShortBreak' : 'Focus';
                remainingSeconds = nextMinutes * 60;
                renderWidget();
            }
        });

        resetBtnEl?.addEventListener('click', async () => {
            const mins = currentMode === 'Focus' ? 25 : 5;
            try {
                await signalrClient.resetPomodoro(roomId, mins);
            } catch {
                isRunning = false;
                remainingSeconds = mins * 60;
                renderWidget();
            }
        });
    };

    return {
        init() {
            bindEvents();
            renderWidget();
        },
        startFocus25() {
            signalrClient.startPomodoro(roomId, 25).catch(() => {});
        }
    };
}
