/**
 * Cột 3 Tab 3 — Controller điều phối Lật thẻ 3D (Click / Space / Chạm nhẹ), chuyển thẻ bằng phím mũi tên
 * hoặc vuốt ngang trên điện thoại (touchstart + touchend), phím 1/2/3 đánh giá Spaced Repetition
 * (Optimistic UI: chuyển thẻ ngay, lưu nền, tự hoàn tác khi lỗi) và bắn pháo giấy Confetti khi thuộc 100% bộ thẻ.
 */

import { offlineDb } from '../../../core/storage/offlineDb.service.js';
import { toast } from '../../../shared/components/toast.component.js';
import { qs } from '../../../shared/utils/dom.utils.js';
import {
    applyStaggerEntrance,
    bind3DTiltCards
} from '../../../shared/utils/spotlight.utils.js';
import { triggerCelebrationConfetti } from '../../../shared/utils/theme.utils.js';
import { bindTouchGestures } from '../../../shared/utils/touchGesture.utils.js';
import { renderQuizMaterialOptions } from '../quiz/quiz.view.js';
import {
    renderActiveFlashcardPlayer,
    renderFlashcardDecksList
} from './flashcard.view.js';
import { flashcardsApi } from './flashcards.api.js';

export function createFlashcardsController({ roomId, isTabActive }) {
    const createSectionEl = qs('#fc-create-section');
    const generateFormEl = qs('#generate-fc-form');
    const materialSelectEl = qs('#fc-material-select');
    const generateBtn = qs('#fc-generate-btn');
    const stageContainerEl = qs('#fc-stage-container');

    let decks = [];
    let activeDeck = null;
    let workingCards = [];
    let currentCardIndex = 0;
    let isFlipped = false;

    /**
     * @param {'next'|'prev'|null} enterDirection Hướng trượt vào của thẻ mới (hiệu ứng vuốt chuyển thẻ)
     */
    const renderPlayerOrList = (enterDirection = null) => {
        if (!stageContainerEl) return;
        bind3DTiltCards(stageContainerEl, '[data-tilt-card]', 7.5);
        if (!activeDeck) {
            createSectionEl?.classList.remove('hidden');
            stageContainerEl.innerHTML = renderFlashcardDecksList(decks);
            applyStaggerEntrance(stageContainerEl);
            return;
        }
        createSectionEl?.classList.add('hidden');
        stageContainerEl.innerHTML = renderActiveFlashcardPlayer(
            activeDeck,
            workingCards,
            currentCardIndex,
            isFlipped
        );
        if (enterDirection) {
            stageContainerEl.querySelector('.flashcard-scene')
                ?.classList.add(enterDirection === 'next' ? 'fc-swipe-enter-next' : 'fc-swipe-enter-prev');
        }
    };

    const toggleFlip = () => {
        isFlipped = !isFlipped;
        qs('#flashcard-3d-box')?.classList.toggle('is-flipped', isFlipped);
    };

    // Chuyển tới thẻ có chỉ số targetIndex (bỏ qua nếu vượt biên), luôn úp lại mặt trước
    const goToCard = (targetIndex, enterDirection) => {
        if (targetIndex < 0 || targetIndex >= workingCards.length || targetIndex === currentCardIndex) {
            return;
        }
        currentCardIndex = targetIndex;
        isFlipped = false;
        renderPlayerOrList(enterDirection);
    };

    const goToNextCard = () => goToCard(currentCardIndex + 1, 'next');
    const goToPrevCard = () => goToCard(currentCardIndex - 1, 'prev');

    const refreshDecks = async () => {
        try {
            decks = await flashcardsApi.getRoomDecks(roomId);
            offlineDb.saveRoomDecksSummary(roomId, decks).catch(() => {});
        } catch {
            decks = await offlineDb.getCachedRoomDecks(roomId);
        }
        if (!activeDeck) {
            renderPlayerOrList();
        }
        return decks;
    };

    const openDeckPlayer = async (deckId) => {
        try {
            const detail = await flashcardsApi.getDeckDetail(deckId);
            offlineDb.saveFlashcardDeckDetail(roomId, detail).catch(() => {});
            activeDeck = detail;
            workingCards = [...(detail.cards || [])];
            currentCardIndex = 0;
            isFlipped = false;
            renderPlayerOrList();
        } catch (err) {
            // Chế độ Ngoại tuyến: nạp toàn bộ thẻ 3D đã mở xem trước đó từ IndexedDB
            const cachedDeck = await offlineDb.getCachedDeckDetail(deckId);
            if (cachedDeck) {
                activeDeck = cachedDeck;
                workingCards = [...(cachedDeck.cards || [])];
                currentCardIndex = 0;
                isFlipped = false;
                renderPlayerOrList();
                toast.info('Đang ôn bộ thẻ 3D từ bộ nhớ đệm ngoại tuyến IndexedDB.', 2200);
                return;
            }
            toast.error(err.message || 'Không thể mở bộ thẻ ghi nhớ.');
        }
    };

    const shuffleCards = () => {
        for (let i = workingCards.length - 1; i > 0; i--) {
            const j = Math.floor(Math.random() * (i + 1));
            [workingCards[i], workingCards[j]] = [workingCards[j], workingCards[i]];
        }
        currentCardIndex = 0;
        isFlipped = false;
        renderPlayerOrList();
        toast.info('Đã xáo trộn ngẫu nhiên các thẻ ghi nhớ.', 1800);
    };

    const advanceAfterRating = () => {
        const allMastered = workingCards.length > 0 && workingCards.every((c) => Number(c.masteryLevel) === 2);
        if (allMastered) {
            triggerCelebrationConfetti();
            toast.success('🏆 Tuyệt đỉnh! Bạn đã thuộc 100% toàn bộ thẻ trong bộ học liệu này!');
        }
        if (currentCardIndex < workingCards.length - 1) {
            goToNextCard();
        } else {
            renderPlayerOrList();
        }
    };

    /**
     * Optimistic UI: ghi nhận mức độ nhớ và chuyển thẻ ngay (0ms), máy chủ xác nhận ở nền.
     * Mất mạng => đưa vào Sync Queue IndexedDB; máy chủ từ chối => hoàn tác đánh giá và báo Toast.
     */
    const updateCurrentCardMastery = async (masteryLevel) => {
        const card = workingCards[currentCardIndex];
        if (!card) return;

        const level = Number(masteryLevel);
        const cardIndex = currentCardIndex;
        const deckId = activeDeck?.id || null;

        workingCards[cardIndex] = { ...card, masteryLevel: level };
        if (deckId) {
            offlineDb.updateCachedCardMastery(deckId, card.id, level).catch(() => {});
        }
        advanceAfterRating();

        try {
            const updated = await flashcardsApi.updateCardMastery(card.id, level);
            if (workingCards[cardIndex]?.id === card.id) {
                workingCards[cardIndex] = updated;
            }
            refreshDecks().catch(() => {});
        } catch (err) {
            if (err?.statusCode === 0 || !navigator.onLine) {
                await offlineDb.enqueueSyncAction('FLASHCARD_MASTERY', {
                    cardId: card.id,
                    masteryLevel: level,
                    deckId
                });
                toast.info('Đã lưu tiến độ thẻ ngoại tuyến — sẽ tự đồng bộ khi có mạng trở lại!', 2000);
                return;
            }

            if (workingCards[cardIndex]?.id === card.id) {
                workingCards[cardIndex] = card;
            }
            if (deckId) {
                offlineDb.updateCachedCardMastery(deckId, card.id, card.masteryLevel).catch(() => {});
            }
            if (activeDeck?.id === deckId) {
                renderPlayerOrList();
            }
            toast.error(`${err.message || 'Không thể lưu mức độ ghi nhớ.'} Đã hoàn tác đánh giá thẻ.`);
        }
    };

    const bindEvents = () => {
        generateFormEl?.addEventListener('submit', async (e) => {
            e.preventDefault();
            const materialId = Number(materialSelectEl?.value || 0);
            const cardCount = Number(qs('#fc-card-count')?.value || 10);

            if (!materialId) {
                toast.warning('Vui lòng tải lên và chọn một tài liệu PDF nguồn.');
                return;
            }

            if (generateBtn) {
                generateBtn.disabled = true;
                generateBtn.textContent = 'Gemini đang trích xuất thẻ...';
            }

            try {
                const createdDeck = await flashcardsApi.generateDeck(roomId, {
                    materialId,
                    cardCount
                });
                toast.success(`Đã tạo bộ thẻ "${createdDeck.title}"!`);
                await refreshDecks();
                await openDeckPlayer(createdDeck.id);
            } catch (err) {
                toast.error(err.message || 'Sinh bộ thẻ Flashcard thất bại.');
            } finally {
                if (generateBtn) {
                    generateBtn.disabled = false;
                    generateBtn.textContent = '✨ Tạo Bộ Thẻ bằng AI';
                }
            }
        });

        stageContainerEl?.addEventListener('click', (e) => {
            const target = e.target.closest('[data-fc-action]');
            if (!target) return;

            const action = target.dataset.fcAction;

            if (action === 'open-deck') {
                openDeckPlayer(Number(target.dataset.deckId));
                return;
            }

            if (action === 'close-deck') {
                activeDeck = null;
                workingCards = [];
                renderPlayerOrList();
                return;
            }

            if (action === 'flip-card') {
                toggleFlip();
                return;
            }

            if (action === 'prev-card') {
                goToPrevCard();
                return;
            }

            if (action === 'next-card') {
                goToNextCard();
                return;
            }

            if (action === 'shuffle-cards') {
                shuffleCards();
                return;
            }

            if (action === 'rate-mastery') {
                updateCurrentCardMastery(Number(target.dataset.mastery));
            }
        });

        // Hỗ trợ phím Space lật thẻ, mũi tên Trái/Phải chuyển thẻ, và phím 1/2/3 đánh giá Spaced Repetition
        document.addEventListener('keydown', (event) => {
            if (!activeDeck || !isTabActive?.()) return;
            const activeTag = document.activeElement?.tagName?.toLowerCase();
            if (activeTag === 'input' || activeTag === 'textarea' || activeTag === 'select') {
                return;
            }

            if (event.code === 'Space') {
                event.preventDefault();
                toggleFlip();
            } else if (event.key === 'ArrowRight') {
                goToNextCard();
            } else if (event.key === 'ArrowLeft') {
                goToPrevCard();
            } else if (event.key === '1') {
                updateCurrentCardMastery(0);
            } else if (event.key === '2') {
                updateCurrentCardMastery(1);
            } else if (event.key === '3') {
                updateCurrentCardMastery(2);
            }
        });

        // Cảm ứng trên thẻ 3D: chạm nhẹ để lật, vuốt trái sang thẻ tiếp theo, vuốt phải về thẻ trước
        bindTouchGestures(stageContainerEl, {
            targetSelector: '#flashcard-3d-box',
            onTap: toggleFlip,
            onSwipeLeft: goToNextCard,
            onSwipeRight: goToPrevCard
        });
    };

    return {
        async init(initialMaterials = []) {
            bindEvents();
            this.syncMaterialDropdown(initialMaterials);
            await refreshDecks();
        },
        syncMaterialDropdown(materials = [], preselectedId = null) {
            if (materialSelectEl) {
                materialSelectEl.innerHTML = renderQuizMaterialOptions(materials, preselectedId);
            }
        },
        refreshDecks
    };
}
