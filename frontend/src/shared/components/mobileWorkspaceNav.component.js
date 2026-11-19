/**
 * Điều phối trải nghiệm Native Mobile App cho trang Phòng học (workspace.html) khi màn hình < 768px:
 * - Thanh Bottom Navigation 5 nút: Chat & @bot | Tài liệu PDF | Luyện Quiz | Flashcards 3D | Phòng & TV
 * - Drawer trượt từ cạnh trái (Mã mời, Pomodoro, Thành viên Online) + lớp phủ mờ + vuốt trái / Esc để đóng
 * - Di chuyển widget Pomodoro giữa Header (máy tính) và Drawer (điện thoại) theo matchMedia
 * - Chấm đỏ báo tin nhắn mới trên tab Chat khi người dùng đang ở tab công cụ khác
 * Trên máy tính (>= 768px) các lớp CSS mobile không có hiệu lực nên bố cục 3 cột giữ nguyên.
 */

import { bindTouchGestures } from '../utils/touchGesture.utils.js';

const MOBILE_MEDIA_QUERY = '(max-width: 767px)';
const CHAT_PANE = 'chat';
const MEMBERS_ITEM = 'members';
const STUDIO_TABS = new Set(['materials', 'quiz', 'flashcards', 'notes', 'whiteboard', 'leaderboard']);
const CHAT_NEAR_BOTTOM_PX = 120;

const NAV_ITEMS = [
    {
        key: CHAT_PANE,
        label: 'Chat & @bot',
        icon: '<svg fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2"><path stroke-linecap="round" stroke-linejoin="round" d="M8 10h.01M12 10h.01M16 10h.01M21 12c0 4.418-4.03 8-9 8a9.86 9.86 0 01-4.255-.949L3 20l1.395-3.72C3.512 15.042 3 13.574 3 12c0-4.418 4.03-8 9-8s9 3.582 9 8z"/></svg>'
    },
    {
        key: 'materials',
        label: 'Tài liệu PDF',
        icon: '<svg fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2"><path stroke-linecap="round" stroke-linejoin="round" d="M9 12h6m-6 4h6m2 5H7a2 2 0 01-2-2V5a2 2 0 012-2h5.586a1 1 0 01.707.293l5.414 5.414a1 1 0 01.293.707V19a2 2 0 01-2 2z"/></svg>'
    },
    {
        key: 'quiz',
        label: 'Luyện Quiz',
        icon: '<svg fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2"><path stroke-linecap="round" stroke-linejoin="round" d="M9 5H7a2 2 0 00-2 2v12a2 2 0 002 2h10a2 2 0 002-2V7a2 2 0 00-2-2h-2M9 5a2 2 0 002 2h2a2 2 0 002-2M9 5a2 2 0 012-2h2a2 2 0 012 2m-6 9l2 2 4-4"/></svg>'
    },
    {
        key: 'flashcards',
        label: 'Flashcards 3D',
        icon: '<svg fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2"><path stroke-linecap="round" stroke-linejoin="round" d="M19 11H5m14 0a2 2 0 012 2v6a2 2 0 01-2 2H5a2 2 0 01-2-2v-6a2 2 0 012-2m14 0V9a2 2 0 00-2-2M5 11V9a2 2 0 012-2m0 0V5a2 2 0 012-2h6a2 2 0 012 2v2M7 7h10"/></svg>'
    },
    {
        key: MEMBERS_ITEM,
        label: 'Phòng & TV',
        icon: '<svg fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2"><path stroke-linecap="round" stroke-linejoin="round" d="M17 20h5v-2a3 3 0 00-5.356-1.857M17 20H7m10 0v-2c0-.656-.126-1.283-.356-1.857M7 20H2v-2a3 3 0 015.356-1.857M7 20v-2c0-.656.126-1.283.356-1.857m0 0a5.002 5.002 0 019.288 0M15 7a3 3 0 11-6 0 3 3 0 016 0zm6 3a2 2 0 11-4 0 2 2 0 014 0zM7 10a2 2 0 11-4 0 2 2 0 014 0z"/></svg>'
    }
];

function renderNavMarkup() {
    return NAV_ITEMS.map((item) => `
        <button type="button" class="mobile-nav-item" data-mobile-nav="${item.key}" aria-label="${item.label}">
            <span class="mobile-nav-icon-wrap">
                ${item.icon}
                ${item.key === CHAT_PANE ? '<span class="mobile-nav-badge hidden" data-mobile-chat-badge></span>' : ''}
            </span>
            <span class="mobile-nav-label">${item.label}</span>
        </button>
    `).join('');
}

/**
 * @param {object} deps
 * @param {HTMLElement} deps.leftPaneEl Cột 1 (trở thành Drawer trên điện thoại)
 * @param {HTMLElement} deps.centerPaneEl Cột 2 (Chat & @bot)
 * @param {HTMLElement} deps.rightPaneEl Cột 3 (AI Study Studio)
 * @param {HTMLElement} [deps.chatScrollViewportEl] Vùng cuộn tin nhắn để giữ vị trí cuộn khi đổi tab
 * @param {HTMLElement} [deps.pomodoroWidgetEl] Widget Pomodoro dùng chung
 * @param {HTMLElement} [deps.pomodoroDesktopSlotEl] Vị trí Pomodoro trên Header (máy tính)
 * @param {HTMLElement} [deps.pomodoroMobileSlotEl] Vị trí Pomodoro trong Drawer (điện thoại)
 * @param {(tabName: string) => void} deps.onSelectStudioTab Chuyển tab công cụ của Cột 3
 */
export function createMobileWorkspaceNav({
    leftPaneEl,
    centerPaneEl,
    rightPaneEl,
    chatScrollViewportEl = null,
    pomodoroWidgetEl = null,
    pomodoroDesktopSlotEl = null,
    pomodoroMobileSlotEl = null,
    onSelectStudioTab
}) {
    const mediaQuery = window.matchMedia(MOBILE_MEDIA_QUERY);

    let navEl = null;
    let backdropEl = null;
    let activePane = CHAT_PANE;
    let isDrawerOpen = false;
    let hasUnreadChat = false;
    let chatScrollSnapshot = null;

    const isMobileViewport = () => mediaQuery.matches;

    const renderActiveState = () => {
        if (!navEl) return;
        const highlightKey = isDrawerOpen ? MEMBERS_ITEM : activePane;
        navEl.querySelectorAll('[data-mobile-nav]').forEach((btn) => {
            const isActive = btn.dataset.mobileNav === highlightKey;
            btn.classList.toggle('active', isActive);
            btn.setAttribute('aria-pressed', String(isActive));
        });
        navEl.querySelector('[data-mobile-chat-badge]')?.classList.toggle('hidden', !hasUnreadChat);
    };

    // Lưu vị trí cuộn của khung chat trước khi ẩn (display:none làm mất scrollTop trên một số trình duyệt)
    const snapshotChatScroll = () => {
        if (!chatScrollViewportEl || activePane !== CHAT_PANE) return;
        const { scrollTop, scrollHeight, clientHeight } = chatScrollViewportEl;
        chatScrollSnapshot = {
            scrollTop,
            wasNearBottom: scrollHeight - scrollTop - clientHeight < CHAT_NEAR_BOTTOM_PX
        };
    };

    const restoreChatScroll = (jumpToLatest) => {
        if (!chatScrollViewportEl || !chatScrollSnapshot) return;
        const { scrollTop, wasNearBottom } = chatScrollSnapshot;
        chatScrollSnapshot = null;
        requestAnimationFrame(() => {
            chatScrollViewportEl.scrollTop = wasNearBottom || jumpToLatest
                ? chatScrollViewportEl.scrollHeight
                : scrollTop;
        });
    };

    const applyPaneVisibility = () => {
        const showChat = activePane === CHAT_PANE;
        centerPaneEl?.classList.toggle('mobile-pane-hidden', !showChat);
        rightPaneEl?.classList.toggle('mobile-pane-hidden', showChat);
    };

    const setActivePane = (paneKey) => {
        if (paneKey === activePane) return;
        if (paneKey !== CHAT_PANE) {
            snapshotChatScroll();
        }
        activePane = paneKey;
        applyPaneVisibility();
        if (paneKey === CHAT_PANE) {
            const hadUnreadMessages = hasUnreadChat;
            hasUnreadChat = false;
            restoreChatScroll(hadUnreadMessages);
        }
    };

    const openDrawer = () => {
        if (!leftPaneEl || isDrawerOpen) return;
        isDrawerOpen = true;
        leftPaneEl.classList.add('mobile-drawer-open');
        backdropEl?.classList.add('active');
        renderActiveState();
    };

    const closeDrawer = () => {
        if (!isDrawerOpen) return;
        isDrawerOpen = false;
        leftPaneEl?.classList.remove('mobile-drawer-open');
        backdropEl?.classList.remove('active');
        renderActiveState();
    };

    const toggleDrawer = () => (isDrawerOpen ? closeDrawer() : openDrawer());

    const showChat = () => {
        closeDrawer();
        setActivePane(CHAT_PANE);
        renderActiveState();
    };

    // Đồng bộ trạng thái khi Cột 3 đổi tab từ nơi khác (Command Palette, nút "Sinh Quiz" trên thẻ tài liệu...)
    const syncStudioTab = (tabName) => {
        if (!STUDIO_TABS.has(tabName)) return;
        closeDrawer();
        setActivePane(tabName);
        renderActiveState();
    };

    const handleNavClick = (event) => {
        const btn = event.target.closest('[data-mobile-nav]');
        if (!btn) return;
        const key = btn.dataset.mobileNav;

        if (key === MEMBERS_ITEM) {
            toggleDrawer();
        } else if (key === CHAT_PANE) {
            showChat();
        } else {
            syncStudioTab(key);
            onSelectStudioTab?.(key);
        }
    };

    const relocatePomodoroWidget = () => {
        const targetSlot = isMobileViewport() ? pomodoroMobileSlotEl : pomodoroDesktopSlotEl;
        if (pomodoroWidgetEl && targetSlot && pomodoroWidgetEl.parentElement !== targetSlot) {
            targetSlot.appendChild(pomodoroWidgetEl);
        }
    };

    // Ô nhập có thuộc tính data-mobile-placeholder dùng gợi ý ngắn gọn trên điện thoại (tránh xuống dòng)
    const applyResponsivePlaceholders = () => {
        document.querySelectorAll('[data-mobile-placeholder]').forEach((el) => {
            if (el.dataset.desktopPlaceholder === undefined) {
                el.dataset.desktopPlaceholder = el.getAttribute('placeholder') || '';
            }
            el.setAttribute('placeholder', isMobileViewport() ? el.dataset.mobilePlaceholder : el.dataset.desktopPlaceholder);
        });
    };

    const handleViewportChange = () => {
        relocatePomodoroWidget();
        applyResponsivePlaceholders();
        if (!isMobileViewport()) {
            closeDrawer();
        }
    };

    const handleEscapeKey = (event) => {
        if (event.key === 'Escape') {
            closeDrawer();
        }
    };

    const mountChrome = () => {
        backdropEl = document.createElement('div');
        backdropEl.className = 'mobile-drawer-backdrop';
        backdropEl.setAttribute('aria-hidden', 'true');
        backdropEl.addEventListener('click', closeDrawer);

        navEl = document.createElement('nav');
        navEl.className = 'mobile-bottom-nav hidden';
        navEl.setAttribute('aria-label', 'Điều hướng Phòng học');
        navEl.innerHTML = renderNavMarkup();
        navEl.addEventListener('click', handleNavClick);

        document.body.append(backdropEl, navEl);
    };

    return {
        init() {
            mountChrome();
            applyPaneVisibility();
            renderActiveState();
            relocatePomodoroWidget();
            applyResponsivePlaceholders();

            leftPaneEl?.addEventListener('click', (event) => {
                if (event.target.closest('[data-mobile-drawer-close]')) {
                    closeDrawer();
                }
            });
            bindTouchGestures(leftPaneEl, { onSwipeLeft: closeDrawer });
            document.addEventListener('keydown', handleEscapeKey);
            mediaQuery.addEventListener('change', handleViewportChange);
        },
        openDrawer,
        closeDrawer,
        toggleDrawer,
        showChat,
        syncStudioTab,
        /**
         * Gọi khi có tin nhắn mới: hiện chấm đỏ trên tab Chat nếu người dùng đang xem tab khác trên điện thoại.
         */
        notifyChatActivity() {
            if (!isMobileViewport() || activePane === CHAT_PANE) return;
            hasUnreadChat = true;
            renderActiveState();
        }
    };
}
