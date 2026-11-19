/**
 * Controller cho Trang chủ Flagship (index.html):
 * Điều phối bộ hiệu ứng hiện đại:
 * 1. Scroll Blur-Reveal (IntersectionObserver) + Đếm số động (Animated Number Counters)
 * 2. Nghiêng 3D theo chuột (3D Perspective Tilt) trên cửa sổ Hero Workspace
 * 3. Spotlight ánh sáng bám theo con trỏ trên các thẻ Bento
 * 4. Chuyển Tab tương tác trực tiếp (Chat @bot / Thi Quiz / Bảng xếp hạng) + Lật thẻ 3D mẫu + Đồng hồ Pomodoro sống.
 */

import { ROUTES } from '../../core/config/app.config.js';
import { appStore } from '../../core/state/appStore.js';
import { initOtaAutoSync } from '../../core/sync/otaUpdater.service.js';
import { qs, qsa } from '../../shared/utils/dom.utils.js';
import { initInteractiveEffects } from '../../shared/utils/spotlight.utils.js';
import {
    animateNumberCounter,
    initTheme,
    runViewTransition,
    toggleTheme
} from '../../shared/utils/theme.utils.js';
import { initPwaInstaller } from '../../shared/components/pwaInstaller.component.js';

const DEMO_FLASHCARDS = [
    {
        topic: 'Chuyên đề #DSA202 • Đồ thị',
        front: 'Độ phức tạp thời gian của thuật toán Dijkstra cài đặt bằng Binary Min-Heap là bao nhiêu?',
        back: 'O((|V| + |E|) log |V|) trên danh sách kề, với điều kiện bắt buộc mọi cạnh có trọng số không âm w(u, v) ≥ 0.'
    },
    {
        topic: 'Chuyên đề #NET101 • Mạng máy tính',
        front: 'Công thức xác định kích thước cửa sổ gửi hiệu dụng trong giao thức TCP?',
        back: 'W_effective = min(rwnd, cwnd) — kết hợp kiểm soát luồng bộ đệm bên nhận (rwnd) và kiểm soát tắc nghẽn mạng (cwnd).'
    },
    {
        topic: 'Chuyên đề #SEC308 • An toàn thông tin',
        front: 'Vì sao băm mật khẩu bằng BCrypt an toàn vượt trội so với băm nhanh không Salt?',
        back: 'BCrypt sinh Salt ngẫu nhiên 128-bit cho từng tài khoản (vô hiệu hóa Rainbow Table) và điều chỉnh được độ trễ Work Factor chống dò vét cạn.'
    }
];

function setupNavigationLinks() {
    const { token, user } = appStore.getState();
    const isAuthenticated = Boolean(token && user);
    const dashboardUrl = isAuthenticated && String(user.role).toLowerCase() === 'admin'
        ? ROUTES.ADMIN_DASHBOARD
        : ROUTES.STUDENT_DASHBOARD;

    const navActions = qs('#landing-nav-actions');
    if (navActions) {
        navActions.innerHTML = isAuthenticated
            ? `
                <a href="${dashboardUrl}" class="btn-primary-glow inline-flex items-center gap-2 rounded-xl px-4 py-2 text-xs font-extrabold">
                    <span>Mở Workspace</span>
                    <svg class="w-3.5 h-3.5" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2.3"><path stroke-linecap="round" stroke-linejoin="round" d="M13 7l5 5m0 0l-5 5m5-5H6"/></svg>
                </a>
              `
            : `
                <a href="${ROUTES.LOGIN}" class="rounded-xl px-3.5 py-2 text-xs font-bold text-slate-700 dark:text-slate-200 hover:bg-slate-100 dark:hover:bg-slate-800 transition">Đăng nhập</a>
                <a href="${ROUTES.REGISTER}" class="btn-primary-glow hidden sm:inline-flex items-center gap-1.5 rounded-xl px-4 py-2 text-xs font-extrabold">
                    <span>Đăng ký ngay</span>
                </a>
              `;
    }

    const primaryCta = qs('#hero-primary-cta');
    const secondaryCta = qs('#hero-secondary-cta');
    if (primaryCta) {
        primaryCta.href = isAuthenticated ? dashboardUrl : ROUTES.LOGIN;
        if (isAuthenticated) {
            const span = primaryCta.querySelector('span');
            if (span) span.textContent = 'Tiếp tục phiên học của bạn';
        }
    }
    if (secondaryCta) {
        secondaryCta.href = isAuthenticated ? ROUTES.STUDENT_PROFILE : ROUTES.REGISTER;
        if (isAuthenticated) {
            secondaryCta.textContent = 'Xem Hồ sơ & Lịch sử Thi';
        }
    }

    qsa('[data-auth-cta]').forEach((link) => {
        link.href = isAuthenticated ? dashboardUrl : ROUTES.LOGIN;
    });
}

function setupScrollRevealAndCounters() {
    const revealEls = qsa('.reveal-up:not(.is-visible)');
    const counterEls = qsa('[data-counter-target]');
    const triggeredCounters = new WeakSet();

    if (!('IntersectionObserver' in window)) {
        revealEls.forEach((el) => el.classList.add('is-visible'));
        counterEls.forEach((el) => {
            animateNumberCounter(el, Number(el.dataset.counterTarget || 0), {
                suffix: el.dataset.counterSuffix || ''
            });
        });
        return;
    }

    const observer = new IntersectionObserver(
        (entries) => {
            entries.forEach((entry) => {
                if (!entry.isIntersecting) return;
                entry.target.classList.add('is-visible');

                const counter = entry.target.matches('[data-counter-target]')
                    ? entry.target
                    : entry.target.querySelector('[data-counter-target]');

                if (counter && !triggeredCounters.has(counter)) {
                    triggeredCounters.add(counter);
                    animateNumberCounter(counter, Number(counter.dataset.counterTarget || 0), {
                        durationMs: 1100,
                        suffix: counter.dataset.counterSuffix || ''
                    });
                }

                observer.unobserve(entry.target);
            });
        },
        { threshold: 0.14 }
    );

    revealEls.forEach((el) => observer.observe(el));
}

function setupHero3DTiltAndTabs() {
    const stage = qs('#hero-tilt-stage');
    const windowEl = qs('#hero-tilt-window');

    if (stage && windowEl && window.matchMedia('(min-width: 1024px)').matches) {
        stage.addEventListener('mousemove', (e) => {
            const rect = stage.getBoundingClientRect();
            const relX = (e.clientX - rect.left) / rect.width - 0.5;
            const relY = (e.clientY - rect.top) / rect.height - 0.5;
            const rotateY = (relX * 5.5).toFixed(2);
            const rotateX = (-relY * 4.5).toFixed(2);
            windowEl.style.transform = `rotateX(${rotateX}deg) rotateY(${rotateY}deg)`;
        });

        stage.addEventListener('mouseleave', () => {
            windowEl.style.transform = 'rotateX(0deg) rotateY(0deg)';
        });
    }

    // Chuyển đổi trực tiếp 3 Tab bên trong mô hình Hero Workspace
    qsa('[data-hero-demo-tab]').forEach((tabBtn) => {
        tabBtn.addEventListener('click', () => {
            const target = tabBtn.dataset.heroDemoTab;
            runViewTransition(() => {
                qsa('[data-hero-demo-tab]').forEach((btn) => {
                    const active = btn === tabBtn;
                    btn.className = active
                        ? 'rounded-lg bg-white dark:bg-slate-900 px-3 py-1 text-[11px] font-bold text-indigo-700 dark:text-indigo-300 shadow-xs transition'
                        : 'rounded-lg px-3 py-1 text-[11px] font-semibold text-slate-600 dark:text-slate-300 hover:text-slate-900 dark:hover:text-white transition';
                });

                qsa('[data-hero-demo-panel]').forEach((panel) => {
                    const show = panel.dataset.heroDemoPanel === target;
                    panel.classList.toggle('hidden', !show);
                    if (show) panel.classList.add('spa-fade-in');
                });
            });
        });
    });

    // Đồng hồ Pomodoro mẫu đếm giây thực tế trên huy hiệu nổi
    const pomoEl = qs('#landing-live-pomo');
    if (pomoEl) {
        let totalSec = 24 * 60 + 59;
        setInterval(() => {
            totalSec = totalSec > 1200 ? totalSec - 1 : 25 * 60;
            const mm = String(Math.floor(totalSec / 60)).padStart(2, '0');
            const ss = String(totalSec % 60).padStart(2, '0');
            pomoEl.textContent = `${mm}:${ss}`;
        }, 1000);
    }
}

function setupSpotlightCardsAndDemoFlashcard() {
    qsa('.spotlight-card').forEach((card) => {
        card.addEventListener('mousemove', (e) => {
            const rect = card.getBoundingClientRect();
            card.style.setProperty('--spot-x', `${e.clientX - rect.left}px`);
            card.style.setProperty('--spot-y', `${e.clientY - rect.top}px`);
        });
    });

    let currentCardIdx = 0;
    const cardInner = qs('#landing-demo-card-inner');
    const topicEl = qs('#demo-fc-topic');
    const frontEl = qs('#demo-fc-front');
    const backEl = qs('#demo-fc-back');

    cardInner?.addEventListener('click', () => {
        cardInner.classList.toggle('is-flipped');
    });

    qs('#landing-next-demo-card')?.addEventListener('click', () => {
        currentCardIdx = (currentCardIdx + 1) % DEMO_FLASHCARDS.length;
        const item = DEMO_FLASHCARDS[currentCardIdx];
        cardInner?.classList.remove('is-flipped');
        setTimeout(() => {
            if (topicEl) topicEl.textContent = item.topic;
            if (frontEl) frontEl.textContent = item.front;
            if (backEl) backEl.textContent = item.back;
        }, 140);
    });
}

function initLandingPage() {
    initTheme();
    initPwaInstaller();
    initOtaAutoSync();
    initInteractiveEffects();
    qs('#landing-theme-toggle')?.addEventListener('click', () => toggleTheme());

    setupNavigationLinks();
    setupScrollRevealAndCounters();
    setupHero3DTiltAndTabs();
    setupSpotlightCardsAndDemoFlashcard();
}

document.addEventListener('DOMContentLoaded', initLandingPage);
