/**
 * Dịch vụ Đồng hồ đếm ngược độc lập cho kỳ thi trắc nghiệm trực tuyến.
 * Đảm bảo tính chính xác theo mốc thời gian thực (Date.now) và tự động kích hoạt nộp bài khi hết giờ.
 */

export class QuizTimerService {
    constructor() {
        this._intervalId = null;
        this._startedAtMs = 0;
        this._durationSeconds = 0;
    }

    start(durationMinutes, { onTick, onTimeExpired }) {
        this.stop();
        this._durationSeconds = Math.max(60, Math.floor(Number(durationMinutes) * 60));
        this._startedAtMs = Date.now();

        onTick?.(this._durationSeconds);

        this._intervalId = window.setInterval(() => {
            const elapsed = this.getElapsedSeconds();
            const remaining = Math.max(0, this._durationSeconds - elapsed);
            onTick?.(remaining);

            if (remaining <= 0) {
                this.stop();
                onTimeExpired?.();
            }
        }, 1000);
    }

    getElapsedSeconds() {
        if (!this._startedAtMs) {
            return 0;
        }
        return Math.max(1, Math.floor((Date.now() - this._startedAtMs) / 1000));
    }

    stop() {
        if (this._intervalId !== null) {
            clearInterval(this._intervalId);
            this._intervalId = null;
        }
    }

    isRunning() {
        return this._intervalId !== null;
    }
}
