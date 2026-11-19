/**
 * Hệ thống Hiệu ứng Ánh sáng & Chuyển động Vi mô 60FPS (Mouse-Tracking Spotlight, 3D Tilt & Ripple):
 * - Sử dụng requestAnimationFrame + Event Delegation duy nhất trên toàn tài liệu để đạt 60FPS mượt mà.
 * - Tự động vô hiệu hóa độ nghiêng và sóng gợn khi hệ điều hành bật prefers-reduced-motion.
 */

let isGlobalSpotlightMounted = false;

export function prefersReducedMotion() {
    return Boolean(window.matchMedia?.('(prefers-reduced-motion: reduce)').matches);
}

/**
 * Kích hoạt quầng sáng bám tọa độ chuột (--mouse-x, --mouse-y) cho mọi thẻ .spotlight-surface / .spotlight-card
 * và hiệu ứng gợn sóng (Ripple) trên các nút tương tác chính.
 */
export function initInteractiveEffects() {
    if (isGlobalSpotlightMounted) {
        return;
    }
    isGlobalSpotlightMounted = true;

    let rafPending = false;
    let lastPointerEvent = null;

    document.addEventListener(
        'pointermove',
        (event) => {
            if (prefersReducedMotion()) return;
            lastPointerEvent = event;
            if (rafPending) return;

            rafPending = true;
            window.requestAnimationFrame(() => {
                rafPending = false;
                if (!lastPointerEvent) return;

                const target = lastPointerEvent.target instanceof Element
                    ? lastPointerEvent.target.closest('.spotlight-surface, .spotlight-card')
                    : null;
                if (!target) return;

                const rect = target.getBoundingClientRect();
                const x = lastPointerEvent.clientX - rect.left;
                const y = lastPointerEvent.clientY - rect.top;

                target.style.setProperty('--mouse-x', `${x.toFixed(1)}px`);
                target.style.setProperty('--mouse-y', `${y.toFixed(1)}px`);
                target.style.setProperty('--spot-x', `${x.toFixed(1)}px`);
                target.style.setProperty('--spot-y', `${y.toFixed(1)}px`);
            });
        },
        { passive: true }
    );

    // Hiệu ứng Ripple mượt mà khi nhấn vào nút bấm
    document.addEventListener(
        'pointerdown',
        (event) => {
            if (prefersReducedMotion() || event.button !== 0) return;
            const btn = event.target instanceof Element
                ? event.target.closest('.btn-primary-glow, [data-ripple]')
                : null;
            if (!btn || btn.disabled) return;

            btn.classList.add('ripple-host');
            const rect = btn.getBoundingClientRect();
            const size = Math.max(rect.width, rect.height) * 0.9;
            const wave = document.createElement('span');
            wave.className = 'ui-ripple-wave';
            wave.style.width = `${size}px`;
            wave.style.height = `${size}px`;
            wave.style.left = `${event.clientX - rect.left}px`;
            wave.style.top = `${event.clientY - rect.top}px`;

            btn.appendChild(wave);
            wave.addEventListener('animationend', () => wave.remove(), { once: true });
        },
        { passive: true }
    );
}

/**
 * Gắn độ nghiêng 3D (3D Tilt) mượt theo vị trí chuột cho các thẻ [data-tilt-card] bên trong container.
 */
export function bind3DTiltCards(containerEl, selector = '[data-tilt-card]', maxDeg = 6.5) {
    if (!containerEl || containerEl.dataset.tiltBound === 'true') {
        return;
    }
    containerEl.dataset.tiltBound = 'true';

    let activeCard = null;
    let rafId = null;

    const resetCard = (card) => {
        if (!card) return;
        card.classList.add('is-resetting');
        card.style.setProperty('--tilt-rx', '0deg');
        card.style.setProperty('--tilt-ry', '0deg');
    };

    containerEl.addEventListener(
        'pointermove',
        (event) => {
            if (prefersReducedMotion()) return;
            const card = event.target instanceof Element ? event.target.closest(selector) : null;

            if (activeCard && activeCard !== card) {
                resetCard(activeCard);
                activeCard = null;
            }

            if (!card) return;
            activeCard = card;

            const clientX = event.clientX;
            const clientY = event.clientY;

            if (rafId) return;
            rafId = window.requestAnimationFrame(() => {
                rafId = null;
                if (!activeCard) return;

                const rect = activeCard.getBoundingClientRect();
                if (rect.width <= 0 || rect.height <= 0) return;

                const normX = (clientX - rect.left) / rect.width - 0.5;
                const normY = (clientY - rect.top) / rect.height - 0.5;

                const rotateY = normX * maxDeg * 2;
                const rotateX = -normY * maxDeg * 2;

                activeCard.classList.remove('is-resetting');
                activeCard.style.setProperty('--tilt-rx', `${rotateX.toFixed(2)}deg`);
                activeCard.style.setProperty('--tilt-ry', `${rotateY.toFixed(2)}deg`);
            });
        },
        { passive: true }
    );

    containerEl.addEventListener(
        'pointerleave',
        () => {
            if (activeCard) {
                resetCard(activeCard);
                activeCard = null;
            }
        },
        { passive: true }
    );
}

/**
 * Gán chỉ số trượt tuần tự (stagger delay 35ms mỗi phần tử) cho danh sách vừa render.
 */
export function applyStaggerEntrance(containerEl, itemSelector = ':scope > *', maxCap = 24) {
    if (!containerEl || prefersReducedMotion()) return;
    const items = containerEl.querySelectorAll(itemSelector);
    items.forEach((el, idx) => {
        el.style.setProperty('--stagger-index', String(Math.min(idx, maxCap)));
        el.classList.add('stagger-entrance');
    });
}
