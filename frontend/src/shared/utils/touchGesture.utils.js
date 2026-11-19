/**
 * Tiện ích nhận diện cử chỉ cảm ứng (Chạm nhẹ / Vuốt ngang) bằng touchstart + touchend thuần.
 * Gắn 1 lần lên container cha (Event Delegation) nên vẫn hoạt động khi nội dung bên trong được render lại.
 * Thuật toán: so sánh độ dời ngang (dx) và dọc (dy) giữa lúc chạm và lúc nhấc tay:
 * - |dx| đủ lớn và lấn át |dy| => Vuốt trái / phải
 * - Gần như không dịch chuyển và thời gian ngắn => Chạm nhẹ (Tap)
 */

const DEFAULT_SWIPE_MIN_DISTANCE_PX = 50;
const SWIPE_DOMINANCE_RATIO = 1.3;
const TAP_MAX_DISTANCE_PX = 10;
const TAP_MAX_DURATION_MS = 350;

function classifyGesture(dx, dy, elapsedMs, swipeMinDistance) {
    const absX = Math.abs(dx);
    const absY = Math.abs(dy);

    if (absX >= swipeMinDistance && absX > absY * SWIPE_DOMINANCE_RATIO) {
        return dx < 0 ? 'swipe-left' : 'swipe-right';
    }
    if (absX <= TAP_MAX_DISTANCE_PX && absY <= TAP_MAX_DISTANCE_PX && elapsedMs <= TAP_MAX_DURATION_MS) {
        return 'tap';
    }
    return 'none';
}

/**
 * @param {HTMLElement} containerEl Phần tử cha nhận sự kiện cảm ứng
 * @param {object} options
 * @param {string|null} options.targetSelector Chỉ nhận cử chỉ bắt đầu bên trong phần tử khớp selector này
 * @param {(targetEl: Element) => void} [options.onSwipeLeft]
 * @param {(targetEl: Element) => void} [options.onSwipeRight]
 * @param {(targetEl: Element) => void} [options.onTap] Khi có, click giả lập sau Tap sẽ bị chặn để không xử lý 2 lần
 * @param {number} [options.swipeMinDistance]
 * @returns {() => void} Hàm gỡ bỏ toàn bộ listener
 */
export function bindTouchGestures(containerEl, {
    targetSelector = null,
    onSwipeLeft,
    onSwipeRight,
    onTap,
    swipeMinDistance = DEFAULT_SWIPE_MIN_DISTANCE_PX
} = {}) {
    if (!containerEl) {
        return () => {};
    }

    let touchStart = null;

    const handleTouchStart = (event) => {
        if (event.touches.length !== 1) {
            touchStart = null;
            return;
        }
        const targetEl = targetSelector ? event.target.closest(targetSelector) : containerEl;
        if (!targetEl || !containerEl.contains(targetEl)) {
            touchStart = null;
            return;
        }
        const touch = event.touches[0];
        touchStart = { x: touch.clientX, y: touch.clientY, time: Date.now(), targetEl };
    };

    const handleTouchEnd = (event) => {
        if (!touchStart) return;
        const touch = event.changedTouches[0];
        const { x, y, time, targetEl } = touchStart;
        touchStart = null;

        const gesture = classifyGesture(touch.clientX - x, touch.clientY - y, Date.now() - time, swipeMinDistance);
        const handler = {
            'swipe-left': onSwipeLeft,
            'swipe-right': onSwipeRight,
            tap: onTap
        }[gesture];

        if (!handler) return;
        // Chặn sự kiện click giả lập do trình duyệt sinh ra sau cử chỉ, tránh xử lý trùng lặp
        if (event.cancelable) {
            event.preventDefault();
        }
        handler(targetEl);
    };

    const handleTouchCancel = () => {
        touchStart = null;
    };

    containerEl.addEventListener('touchstart', handleTouchStart, { passive: true });
    containerEl.addEventListener('touchend', handleTouchEnd, { passive: false });
    containerEl.addEventListener('touchcancel', handleTouchCancel, { passive: true });

    return () => {
        containerEl.removeEventListener('touchstart', handleTouchStart);
        containerEl.removeEventListener('touchend', handleTouchEnd);
        containerEl.removeEventListener('touchcancel', handleTouchCancel);
    };
}
