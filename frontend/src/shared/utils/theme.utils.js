/**
 * Quản lý Giao diện Sáng / Tối (Dark / Light Mode chuẩn Linear & Discord),
 * Hiệu ứng chuyển cảnh SPA (View Transitions), Số chạy tăng dần (CountUp) và Pháo giấy Confetti.
 */

const THEME_STORAGE_KEY = 'ssw_theme_mode';
const themeListeners = new Set();

export function getCurrentTheme() {
    const saved = localStorage.getItem(THEME_STORAGE_KEY);
    if (saved === 'dark' || saved === 'light') {
        return saved;
    }
    return window.matchMedia?.('(prefers-color-scheme: dark)').matches ? 'dark' : 'light';
}

export function applyTheme(theme) {
    const normalized = theme === 'dark' ? 'dark' : 'light';
    document.documentElement.classList.toggle('dark', normalized === 'dark');
    localStorage.setItem(THEME_STORAGE_KEY, normalized);
    for (const fn of themeListeners) {
        fn(normalized);
    }
}

export function initTheme() {
    applyTheme(getCurrentTheme());
}

export function toggleTheme() {
    const next = getCurrentTheme() === 'dark' ? 'light' : 'dark';
    runViewTransition(() => applyTheme(next));
    return next;
}

export function onThemeChanged(listener) {
    themeListeners.add(listener);
    return () => themeListeners.delete(listener);
}

/**
 * Thực thi cập nhật DOM mượt như React SPA bằng View Transitions API (kèm fallback CSS fadeInUp 180ms).
 */
export function runViewTransition(domMutator, containerEl = null) {
    if (typeof document.startViewTransition === 'function') {
        document.startViewTransition(() => {
            domMutator();
        });
        return;
    }
    domMutator();
    if (containerEl) {
        containerEl.classList.remove('spa-fade-in');
        void containerEl.offsetWidth;
        containerEl.classList.add('spa-fade-in');
    }
}

/**
 * Hiệu ứng số chạy tăng dần mượt mà (Animated Number Counter) cho các thẻ KPI.
 */
export function animateNumberCounter(element, targetValue, { decimals = 0, suffix = '', durationMs = 680 } = {}) {
    if (!element) return;
    const end = Number(targetValue) || 0;
    const startTime = performance.now();
    const formatter = new Intl.NumberFormat('vi-VN', {
        minimumFractionDigits: decimals,
        maximumFractionDigits: decimals
    });

    const step = (now) => {
        const progress = Math.min(1, (now - startTime) / durationMs);
        // EaseOutCubic
        const eased = 1 - Math.pow(1 - progress, 3);
        const current = end * eased;
        element.textContent = `${formatter.format(decimals > 0 ? current : Math.round(current))}${suffix}`;
        if (progress < 1) {
            requestAnimationFrame(step);
        } else {
            element.textContent = `${formatter.format(end)}${suffix}`;
        }
    };

    requestAnimationFrame(step);
}

/**
 * Bắn pháo giấy chúc mừng khi đạt >= 8.0 điểm Quiz hoặc thuộc 100% bộ thẻ 3D.
 */
export function triggerCelebrationConfetti() {
    if (typeof window.confetti !== 'function') {
        return;
    }
    window.confetti({
        particleCount: 85,
        spread: 72,
        origin: { y: 0.62 },
        colors: ['#4f46e5', '#8b5cf6', '#10b981', '#f59e0b', '#ec4899']
    });
}
