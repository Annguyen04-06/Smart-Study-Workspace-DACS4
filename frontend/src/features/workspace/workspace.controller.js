/**
 * Master Orchestrator của giao diện 3-Pane Workspace SPA (pages/student/workspace.html):
 * Kết nối Cột 1 (Midnight Sidebar) — Cột 2 (Realtime Chat, Pinned Banner, Voice Input & Group Pomodoro)
 * — Cột 3 (AI Study Studio: PDF / Quiz / 3D Flashcards / Room Leaderboard) + Command Palette Ctrl+K.
 * Trên điện thoại (< 768px) giao diện chuyển sang dạng Native App: Bottom Navigation + Drawer thành viên.
 */

import { ROUTES, buildWorkspaceUrl } from '../../core/config/app.config.js';
import { requireAuth } from '../../core/guards/routeGuard.js';
import { signalrClient } from '../../core/realtime/signalrClient.js';
import { appStore } from '../../core/state/appStore.js';
import { offlineDb } from '../../core/storage/offlineDb.service.js';
import { initOtaAutoSync } from '../../core/sync/otaUpdater.service.js';
import {
    initCommandPalette,
    openCommandPalette,
    registerWorkspacePaletteActions
} from '../../shared/components/commandPalette.component.js';
import { createAmbientAudioController } from '../../shared/components/ambientAudio.component.js';
import { createMobileWorkspaceNav } from '../../shared/components/mobileWorkspaceNav.component.js';
import {
    onNotificationStateChange,
    renderNotificationBellMarkup,
    sendDesktopNotification,
    toggleDesktopNotifications
} from '../../shared/components/notification.service.js';
import { mountMobileQrButton } from '../../shared/components/mobileAccessQr.component.js';
import { initPwaInstaller } from '../../shared/components/pwaInstaller.component.js';
import {
    initServerHealthMonitor,
    onServerStatusChange
} from '../../shared/components/serverHealth.component.js';
import { toast } from '../../shared/components/toast.component.js';
import {
    copyTextToClipboard,
    escapeHtml,
    getInitials,
    qs,
    qsa
} from '../../shared/utils/dom.utils.js';
import {
    applyStaggerEntrance,
    bind3DTiltCards,
    initInteractiveEffects
} from '../../shared/utils/spotlight.utils.js';
import {
    initTheme,
    runViewTransition,
    toggleTheme
} from '../../shared/utils/theme.utils.js';
import { roomsApi } from '../dashboard/rooms.api.js';
import { createChatController } from './chat/chat.controller.js';
import { createFlashcardsController } from './flashcards/flashcards.controller.js';
import {
    renderLeaderboardSkeletons,
    renderRoomLeaderboard
} from './leaderboard/leaderboard.view.js';
import { createMaterialsController } from './materials/materials.controller.js';
import { renderRoomMembersList } from './members/members.view.js';
import { createPomodoroController } from './pomodoro/pomodoro.controller.js';
import { createQuizController } from './quiz/quiz.controller.js';
import { createScratchpadController } from './scratchpad/scratchpad.controller.js';
import { createWhiteboardController } from './whiteboard/whiteboard.controller.js';

async function resolveTargetRoomId() {
    const params = new URLSearchParams(window.location.search);
    const paramId = Number(params.get('roomId'));
    if (paramId > 0) {
        return paramId;
    }

    const myRooms = await roomsApi.getMyRooms();
    if (myRooms.length > 0) {
        window.history.replaceState({}, '', buildWorkspaceUrl(myRooms[0].id));
        return myRooms[0].id;
    }

    toast.info('Hãy chọn hoặc tham gia một phòng học trước.');
    window.location.replace(ROUTES.STUDENT_DASHBOARD);
    return null;
}

async function initWorkspace() {
    initTheme();
    initInteractiveEffects();
    initCommandPalette();
    initPwaInstaller();
    initOtaAutoSync();
    initServerHealthMonitor();
    mountMobileQrButton();

    // Gắn nút Chuông Thông báo Đẩy trên Header Workspace
    const notifBellSlot = qs('#ws-notif-bell-slot');
    const renderWorkspaceNotifBell = () => {
        if (notifBellSlot) {
            notifBellSlot.innerHTML = renderNotificationBellMarkup();
        }
    };
    renderWorkspaceNotifBell();
    onNotificationStateChange(renderWorkspaceNotifBell);
    notifBellSlot?.addEventListener('click', (e) => {
        if (e.target.closest('[data-action="toggle-desktop-notifications"]')) {
            toggleDesktopNotifications();
        }
    });

    const ambientAudio = createAmbientAudioController();
    ambientAudio.init();

    const currentUser = requireAuth();
    if (!currentUser) return;

    qsa('[data-nav-dashboard]').forEach((el) => {
        el.href = ROUTES.STUDENT_DASHBOARD;
    });

    const profileLink = qs('#ws-profile-link');
    if (profileLink) profileLink.href = ROUTES.STUDENT_PROFILE;

    const userInitialsEl = qs('#ws-dock-initials');
    const userNameEl = qs('#ws-dock-fullname');
    const userCodeEl = qs('#ws-dock-code');
    if (userInitialsEl) userInitialsEl.textContent = getInitials(currentUser.fullName);
    if (userNameEl) userNameEl.textContent = currentUser.fullName;
    if (userCodeEl) userCodeEl.textContent = currentUser.studentCode || currentUser.role;

    qs('#ws-theme-toggle-btn')?.addEventListener('click', () => toggleTheme());
    qs('#ws-open-palette-btn')?.addEventListener('click', () => openCommandPalette());

    const roomId = await resolveTargetRoomId();
    if (!roomId) return;

    let roomDetail;
    let myJoinedRooms = [];
    let onlineUserIds = new Set([currentUser.id]);
    let activeRightTab = 'materials';
    let rightPaneMode = 'normal';

    try {
        [roomDetail, myJoinedRooms] = await Promise.all([
            roomsApi.getRoomDetail(roomId),
            roomsApi.getMyRooms()
        ]);
        // Đồng bộ ngầm chi tiết phòng & danh sách phòng vào IndexedDB cho Offline-First Study Mode
        offlineDb.saveRoomDetail(roomDetail).catch(() => {});
        offlineDb.saveRooms(myJoinedRooms).catch(() => {});
    } catch (err) {
        // Nếu mất kết nối máy chủ: nạp phòng đã lưu trong IndexedDB thay vì báo lỗi văng ra ngoài!
        const [cachedDetail, cachedRooms] = await Promise.all([
            offlineDb.getCachedRoomDetail(roomId),
            offlineDb.getCachedRooms()
        ]);
        if (cachedDetail) {
            roomDetail = cachedDetail;
            myJoinedRooms = cachedRooms.length > 0 ? cachedRooms : [cachedDetail];
            toast.info('Đang mở phòng học ở chế độ Ngoại tuyến từ dữ liệu IndexedDB.');
        } else {
            toast.error(err.message || 'Bạn không có quyền truy cập phòng học này.');
            setTimeout(() => {
                window.location.href = ROUTES.STUDENT_DASHBOARD;
            }, 900);
            return;
        }
    }

    appStore.setState({ activeRoom: roomDetail });

    const renderRoomChrome = () => {
        qsa('[data-room-title]').forEach((el) => {
            el.textContent = roomDetail.name;
        });

        qsa('[data-room-subject]').forEach((el) => {
            el.textContent = roomDetail.subjectCategory;
        });

        const inviteEl = qs('#ws-invite-code-text');
        if (inviteEl) inviteEl.textContent = `#${roomDetail.inviteCode}`;

        const switcherEl = qs('#ws-room-switcher');
        if (switcherEl) {
            switcherEl.innerHTML = myJoinedRooms
                .map(
                    (r) => `<option value="${r.id}" class="bg-slate-900 text-white" ${Number(r.id) === Number(roomId) ? 'selected' : ''}>
                                ${escapeHtml(r.name)}
                            </option>`
                )
                .join('');
        }

        const roomActionBtn = qs('#ws-room-danger-btn');
        if (roomActionBtn) {
            const isOwner = roomDetail.myRoleInRoom === 'Owner';
            roomActionBtn.textContent = isOwner ? 'Xóa phòng' : 'Rời phòng';
            roomActionBtn.dataset.mode = isOwner ? 'delete' : 'leave';
        }
    };

    const renderMembersSidebar = () => {
        const membersContainer = qs('#ws-members-list');
        const onlineCounterEl = qs('#ws-online-counter');
        if (membersContainer) {
            membersContainer.innerHTML = renderRoomMembersList(
                roomDetail.members || [],
                onlineUserIds,
                currentUser.id
            );
        }
        if (onlineCounterEl) {
            onlineCounterEl.textContent = `${onlineUserIds.size}/${(roomDetail.members || []).length} Online`;
        }
    };

    const refreshLeaderboard = async () => {
        const lbContainer = qs('#leaderboard-stage-container');
        if (!lbContainer) return;
        try {
            const items = await roomsApi.getLeaderboard(roomId);
            lbContainer.innerHTML = renderRoomLeaderboard(items, currentUser.id);
            bind3DTiltCards(lbContainer);
            const listHost = lbContainer.querySelector('[data-lb-list]');
            if (listHost) {
                applyStaggerEntrance(listHost);
            }
        } catch (err) {
            lbContainer.innerHTML = `<p class="text-xs text-rose-500">${escapeHtml(err.message)}</p>`;
        }
    };

    renderRoomChrome();
    renderMembersSidebar();

    const rightPaneEl = qs('#ws-right-pane');
    const expandBtnLabel = qs('#ws-expand-label');
    const restorePaneBtn = qs('#ws-restore-pane-btn');

    const applyRightPaneLayout = (mode) => {
        rightPaneMode = mode;
        if (!rightPaneEl) return;

        rightPaneEl.classList.remove('hidden', 'lg:w-[425px]', 'xl:w-[455px]', 'lg:w-[55vw]');
        restorePaneBtn?.classList.add('hidden');

        if (mode === 'collapsed') {
            rightPaneEl.classList.add('hidden');
            restorePaneBtn?.classList.remove('hidden');
        } else if (mode === 'expanded') {
            rightPaneEl.classList.add('lg:w-[55vw]');
            if (expandBtnLabel) expandBtnLabel.textContent = 'Thu nhỏ';
        } else {
            rightPaneEl.classList.add('lg:w-[425px]', 'xl:w-[455px]');
            if (expandBtnLabel) expandBtnLabel.textContent = 'Mở rộng 55%';
        }
    };

    qs('#ws-toggle-expand-btn')?.addEventListener('click', () => {
        applyRightPaneLayout(rightPaneMode === 'expanded' ? 'normal' : 'expanded');
    });

    qs('#ws-collapse-pane-btn')?.addEventListener('click', () => {
        applyRightPaneLayout('collapsed');
    });

    restorePaneBtn?.addEventListener('click', () => {
        applyRightPaneLayout('normal');
    });

    // Điều hướng Native Mobile App (< 768px): Bottom Navigation 5 nút + Drawer Phòng & Thành viên
    const mobileNav = createMobileWorkspaceNav({
        leftPaneEl: qs('#ws-left-pane'),
        centerPaneEl: qs('.workspace-pane-center'),
        rightPaneEl,
        chatScrollViewportEl: qs('#chat-scroll-viewport'),
        pomodoroWidgetEl: qs('#ws-pomo-widget'),
        pomodoroDesktopSlotEl: qs('#ws-pomo-desktop-slot'),
        pomodoroMobileSlotEl: qs('#ws-pomo-mobile-slot'),
        onSelectStudioTab: (tabName) => switchRightTab(tabName)
    });
    mobileNav.init();

    const switchRightTab = (tabName) => {
        activeRightTab = tabName;
        mobileNav.syncStudioTab(tabName);
        if (rightPaneMode === 'collapsed') {
            applyRightPaneLayout('normal');
        }

        runViewTransition(() => {
            qsa('[data-ws-tab]').forEach((btn) => {
                const active = btn.dataset.wsTab === tabName;
                btn.className = active
                    ? 'flex-1 inline-flex items-center justify-center gap-1 whitespace-nowrap rounded-xl bg-white dark:bg-slate-800 px-2 py-2 text-[11px] font-bold text-indigo-700 dark:text-indigo-300 shadow-sm ring-1 ring-slate-200/70 dark:ring-slate-700 transition'
                    : 'flex-1 inline-flex items-center justify-center gap-1 whitespace-nowrap rounded-xl px-2 py-2 text-[11px] font-semibold text-slate-500 dark:text-slate-400 hover:text-slate-900 dark:hover:text-white transition';
            });

            qsa('[data-ws-panel]').forEach((panel) => {
                const show = panel.dataset.wsPanel === tabName;
                panel.classList.toggle('hidden', !show);
                if (show) {
                    panel.classList.add('spa-fade-in');
                }
            });
        });

        if (tabName === 'leaderboard') {
            refreshLeaderboard();
        } else if (tabName === 'whiteboard') {
            if (rightPaneMode === 'normal') {
                applyRightPaneLayout('expanded');
            }
            whiteboardController.onTabActivated();
        } else if (tabName === 'notes') {
            if (rightPaneMode === 'normal') {
                applyRightPaneLayout('expanded');
            }
        }
    };

    qsa('[data-ws-tab]').forEach((btn) => {
        btn.addEventListener('click', () => switchRightTab(btn.dataset.wsTab));
    });

    // Nút mở nhanh Sổ tay Ghi chú Markdown chia đôi màn hình trên Header Workspace
    qs('#ws-scratchpad-quick-btn')?.addEventListener('click', () => {
        switchRightTab('notes');
    });

    // Khởi tạo Pomodoro, Whiteboard, Scratchpad, Quiz, Flashcards, Materials và Chat Controllers
    const pomodoroController = createPomodoroController({ roomId });
    pomodoroController.init();

    const whiteboardController = createWhiteboardController({
        roomId,
        currentUser,
        roomName: roomDetail.name
    });
    whiteboardController.init();

    const scratchpadController = createScratchpadController({
        roomId,
        roomName: roomDetail.name,
        inviteCode: roomDetail.inviteCode
    });

    let chatController = null;

    const quizController = createQuizController({
        roomId,
        onExpandPane: (expand) => {
            if (expand && rightPaneMode === 'normal') {
                applyRightPaneLayout('expanded');
            }
        },
        onQuizSubmitted: () => refreshLeaderboard(),
        onAskBotPrompt: (promptText) => {
            chatController?.focusBotPrompt(promptText);
        }
    });

    const flashcardsController = createFlashcardsController({
        roomId,
        isTabActive: () => activeRightTab === 'flashcards' && rightPaneMode !== 'collapsed'
    });

    const materialsController = createMaterialsController({
        roomId,
        roomName: roomDetail.name,
        currentUser,
        isRoomOwner: roomDetail.myRoleInRoom === 'Owner',
        onMaterialsChanged: (updatedMaterials) => {
            quizController.syncMaterialDropdown(updatedMaterials);
            flashcardsController.syncMaterialDropdown(updatedMaterials);
        },
        onRequestGenerateQuiz: (materialId) => {
            quizController.syncMaterialDropdown(materialsController.getMaterials(), materialId);
            switchRightTab('quiz');
        },
        onRequestGenerateFlashcard: (materialId) => {
            flashcardsController.syncMaterialDropdown(materialsController.getMaterials(), materialId);
            switchRightTab('flashcards');
        },
        onExpandPane: () => applyRightPaneLayout('expanded')
    });

    chatController = createChatController({
        roomId,
        currentUser,
        roomName: roomDetail.name,
        onSaveToScratchpad: async ({ title, markdownContent }) => {
            await scratchpadController.appendSnippet({ title, markdownContent });
            switchRightTab('notes');
            toast.success('Đã chép gọn câu trả lời của @bot sang Sổ tay Ôn thi Markdown!');
        }
    });

    qs('#ws-insert-bot-prefix')?.addEventListener('click', () => chatController.focusBotPrompt());

    // Đăng ký các lệnh điều khiển nhanh phòng học vào Command Palette (Ctrl + K)
    registerWorkspacePaletteActions(() => [
        {
            group: 'Không gian Phòng học Hiện tại',
            title: 'Đặt câu hỏi nhanh cho Trợ giảng @bot',
            subtitle: 'Chèn cú pháp @bot và trỏ chuột vào ô thảo luận',
            badge: '@bot',
            run: () => chatController.focusBotPrompt()
        },
        {
            group: 'Không gian Phòng học Hiện tại',
            title: 'Bắt đầu Đồng hồ Pomodoro Nhóm 25 Phút',
            subtitle: 'Đồng bộ đếm ngược tập trung cho toàn bộ thành viên đang online',
            badge: '25:00',
            run: () => pomodoroController.startFocus25()
        },
        {
            group: 'Không gian Phòng học Hiện tại',
            title: 'Mở Tab Kho Học liệu PDF & Tóm tắt AI',
            subtitle: 'Đọc trực tiếp PDF hoặc in phiếu ôn tập',
            badge: 'PDF',
            run: () => switchRightTab('materials')
        },
        {
            group: 'Không gian Phòng học Hiện tại',
            title: 'Mở Tab Thi Trắc nghiệm AI',
            subtitle: 'Luyện đề có đồng hồ đếm ngược & lời giải chi tiết',
            badge: 'Quiz',
            run: () => switchRightTab('quiz')
        },
        {
            group: 'Không gian Phòng học Hiện tại',
            title: 'Mở Tab Thẻ Ghi nhớ 3D (Flashcards)',
            subtitle: 'Lật thẻ bằng phím Space và đánh giá Spaced Repetition 1-2-3',
            badge: '3D',
            run: () => switchRightTab('flashcards')
        },
        {
            group: 'Không gian Phòng học Hiện tại',
            title: 'Mở Bảng trắng Học nhóm Trực tiếp (Collaborative Whiteboard)',
            subtitle: 'Vẽ sơ đồ nhóm thời gian thực kèm con trỏ màu Figma',
            badge: 'Canvas',
            run: () => switchRightTab('whiteboard')
        },
        {
            group: 'Không gian Phòng học Hiện tại',
            title: 'Bật / Tắt Chế độ Tập trung Sâu (Zen Focus Mode)',
            subtitle: 'Ẩn thanh bên gây xao nhãng & bật âm thanh mưa rơi nhẹ + Brown Noise',
            badge: 'Zen',
            run: () => ambientAudio.toggleZenMode()
        },
        {
            group: 'Không gian Phòng học Hiện tại',
            title: 'Mở Sổ tay Ghi chú Markdown Chia đôi Màn hình',
            subtitle: 'Ghi chú ôn thi tự lưu IndexedDB, chép từ @bot và xuất file .MD / PDF',
            badge: 'Sổ tay',
            run: () => switchRightTab('notes')
        },
        {
            group: 'Không gian Phòng học Hiện tại',
            title: 'Chạy AI Smart Weakness Radar (Phân tích lỗ hổng kiến thức)',
            subtitle: 'Quét các câu trả lời sai trong phòng học và sinh đề 5 câu vá lỗ hổng',
            badge: 'Radar',
            run: () => {
                switchRightTab('quiz');
                quizController.runWeaknessRadar();
            }
        },
        {
            group: 'Không gian Phòng học Hiện tại',
            title: 'Xem Bảng xếp hạng Thi đua Phòng học (Leaderboard)',
            subtitle: 'Vinh danh Top 3 Vương miện Vàng / Bạc / Đồng',
            badge: 'Top 3',
            run: () => switchRightTab('leaderboard')
        }
    ]);

    const lbStage = qs('#leaderboard-stage-container');
    if (lbStage) lbStage.innerHTML = renderLeaderboardSkeletons();

    const loadedMaterials = await materialsController.init();
    await Promise.all([
        chatController.init(),
        quizController.init(loadedMaterials),
        flashcardsController.init(loadedMaterials),
        scratchpadController.init(),
        refreshLeaderboard()
    ]);

    // Hỗ trợ mở thẳng Tab hoặc Đề thi vá lỗ hổng AI từ trang Hồ sơ (URL params: tab, autoQuizId, botPrompt)
    const urlParams = new URLSearchParams(window.location.search);
    const requestedTab = urlParams.get('tab');
    const autoQuizId = Number(urlParams.get('autoQuizId') || 0);
    const presetBotPrompt = urlParams.get('botPrompt');

    if (requestedTab && ['materials', 'quiz', 'flashcards', 'notes', 'whiteboard', 'leaderboard'].includes(requestedTab)) {
        switchRightTab(requestedTab);
    }
    if (autoQuizId > 0) {
        switchRightTab('quiz');
        await quizController.startExam(autoQuizId);
    }
    if (presetBotPrompt) {
        chatController.focusBotPrompt(presetBotPrompt);
    }

    const connBadgeEl = qs('#ws-signalr-badge');
    const setConnBadge = (state) => {
        if (!connBadgeEl) return;
        // Trên điện thoại chỉ hiện chấm trạng thái để nhường chỗ cho tên phòng học
        if (state === 'connected') {
            connBadgeEl.className = 'inline-flex shrink-0 items-center gap-1.5 whitespace-nowrap rounded-full bg-emerald-50 dark:bg-emerald-500/15 border border-emerald-200/90 dark:border-emerald-500/30 px-2 sm:px-2.5 py-0.5 text-[11px] font-semibold text-emerald-700 dark:text-emerald-300';
            connBadgeEl.innerHTML = `<span class="h-1.5 w-1.5 rounded-full bg-emerald-500 animate-pulse"></span><span class="hidden sm:inline">Realtime Live</span>`;
            connBadgeEl.title = 'Realtime Live';
        } else if (state === 'reconnecting') {
            connBadgeEl.className = 'inline-flex shrink-0 items-center gap-1.5 whitespace-nowrap rounded-full bg-amber-50 border border-amber-200 px-2 sm:px-2.5 py-0.5 text-[11px] font-semibold text-amber-700';
            connBadgeEl.innerHTML = `<span class="h-1.5 w-1.5 rounded-full bg-amber-500 animate-ping"></span><span class="hidden sm:inline">Đang kết nối...</span>`;
            connBadgeEl.title = 'Đang kết nối lại...';
        } else {
            connBadgeEl.className = 'inline-flex shrink-0 items-center gap-1.5 whitespace-nowrap rounded-full bg-slate-100 dark:bg-slate-800 px-2.5 py-0.5 text-[11px] font-medium text-slate-500';
            connBadgeEl.textContent = 'Offline';
            connBadgeEl.title = 'Mất kết nối thời gian thực';
        }
    };

    signalrClient.on('ConnectionStateChanged', ({ state }) => setConnBadge(state));

    // Báo chấm đỏ trên tab Chat (điện thoại) khi có tin nhắn mới từ người khác hoặc @bot
    signalrClient.on('ReceiveMessage', (msg) => {
        if (Number(msg.roomId) !== Number(roomId) || Number(msg.senderId) === Number(currentUser.id)) return;
        mobileNav.notifyChatActivity();
    });
    signalrClient.on('BotStreamComplete', (payload) => {
        if (Number(payload?.roomId) !== Number(roomId)) return;
        mobileNav.notifyChatActivity();
    });

    signalrClient.on('OnlineUsersUpdated', async (payload) => {
        if (Number(payload.roomId) !== Number(roomId)) return;
        const list = payload.onlineUsers || [];
        onlineUserIds = new Set(list.map((u) => Number(u.userId)));

        const knownMemberIds = new Set((roomDetail.members || []).map((m) => Number(m.userId)));
        if (list.some((u) => !knownMemberIds.has(Number(u.userId)))) {
            try {
                roomDetail = await roomsApi.getRoomDetail(roomId);
            } catch {
                // Giữ nguyên nếu chưa làm mới được
            }
        }
        renderMembersSidebar();
    });

    signalrClient.on('WorkspaceResourceUpdated', async (payload) => {
        if (Number(payload.roomId) !== Number(roomId)) return;
        if (payload.resourceType === 'Material') {
            await materialsController.refreshMaterials();
            toast.info(`${payload.actorName} vừa tải lên tài liệu PDF "${payload.resourceTitle}".`);
            sendDesktopNotification({
                title: `📄 Tài liệu mới trong ${roomDetail.name}`,
                body: `${payload.actorName} vừa tải lên PDF "${payload.resourceTitle}".`,
                tag: `ssw-res-mat-${roomId}`
            });
        } else if (payload.resourceType === 'Quiz') {
            await quizController.refreshQuizzes();
            toast.info(`${payload.actorName} vừa tạo đề trắc nghiệm AI "${payload.resourceTitle}".`);
            sendDesktopNotification({
                title: `🎯 Đề thi mới trong ${roomDetail.name}`,
                body: `${payload.actorName} vừa biên soạn đề trắc nghiệm "${payload.resourceTitle}".`,
                tag: `ssw-res-quiz-${roomId}`
            });
        } else if (payload.resourceType === 'FlashcardDeck') {
            await flashcardsController.refreshDecks();
            toast.info(`${payload.actorName} vừa tạo bộ thẻ 3D "${payload.resourceTitle}".`);
            sendDesktopNotification({
                title: `🗂️ Bộ Flashcards 3D mới trong ${roomDetail.name}`,
                body: `${payload.actorName} vừa tạo bộ thẻ "${payload.resourceTitle}".`,
                tag: `ssw-res-fc-${roomId}`
            });
        }
    });

    const connectRealtimeRoom = async () => {
        try {
            await signalrClient.joinRoom(roomId);
            setConnBadge('connected');
        } catch {
            setConnBadge('disconnected');
            try {
                const initialOnline = await roomsApi.getOnlineUsers(roomId);
                onlineUserIds = new Set(initialOnline.map((u) => Number(u.userId)));
                renderMembersSidebar();
            } catch {
                // Bỏ qua khi đang hoàn toàn ngoại tuyến
            }
        }
    };

    await connectRealtimeRoom();

    // Khi máy chủ vừa online trở lại sau khi mất mạng: tự kết nối lại SignalR và đồng bộ tài nguyên phòng
    onServerStatusChange(async (online) => {
        if (!online) {
            setConnBadge('disconnected');
            return;
        }
        await connectRealtimeRoom();
        materialsController.refreshMaterials().catch(() => {});
        quizController.refreshQuizzes().catch(() => {});
        flashcardsController.refreshDecks().catch(() => {});
    });

    qs('#ws-copy-invite-btn')?.addEventListener('click', async () => {
        await copyTextToClipboard(roomDetail.inviteCode);
        toast.success(`Đã sao chép mã mời #${roomDetail.inviteCode}!`);
    });

    qs('#ws-room-switcher')?.addEventListener('change', (e) => {
        const targetId = Number(e.target.value);
        if (targetId && targetId !== Number(roomId)) {
            window.location.href = buildWorkspaceUrl(targetId);
        }
    });

    qs('#ws-room-danger-btn')?.addEventListener('click', async (e) => {
        const mode = e.currentTarget.dataset.mode;
        if (mode === 'delete') {
            if (!window.confirm(`Xóa vĩnh viễn phòng học "${roomDetail.name}" và toàn bộ tài liệu bên trong?`)) return;
            try {
                await roomsApi.deleteRoom(roomId);
                toast.success('Đã xóa phòng học.');
                window.location.href = ROUTES.STUDENT_DASHBOARD;
            } catch (err) {
                toast.error(err.message);
            }
        } else {
            if (!window.confirm(`Bạn muốn rời khỏi phòng học "${roomDetail.name}"?`)) return;
            try {
                await roomsApi.leaveRoom(roomId);
                toast.info('Đã rời khỏi phòng học.');
                window.location.href = ROUTES.STUDENT_DASHBOARD;
            } catch (err) {
                toast.error(err.message);
            }
        }
    });

    qs('#ws-mobile-members-btn')?.addEventListener('click', () => mobileNav.toggleDrawer());

    // Máy tính bảng (768px – 1023px): bật/tắt Cột 3 dạng flex để các tab bên trong hiển thị đúng bố cục
    qs('#ws-mobile-tools-btn')?.addEventListener('click', () => {
        if (!rightPaneEl) return;
        const willShow = rightPaneEl.classList.contains('hidden');
        rightPaneEl.classList.toggle('hidden', !willShow);
        rightPaneEl.classList.toggle('flex', willShow);
    });
}

document.addEventListener('DOMContentLoaded', initWorkspace);
