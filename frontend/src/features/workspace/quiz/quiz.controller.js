/**
 * Cột 3 Tab 2 — Controller điều phối Sinh đề Trắc nghiệm AI, Làm bài đếm ngược,
 * bắn pháo giấy Confetti khi đạt >= 8.0 điểm và Xuất Phiếu Ôn Tập In Ấn.
 */

import { offlineDb } from '../../../core/storage/offlineDb.service.js';
import { enhanceMarkdownContainer } from '../../../shared/components/markdownViewer.component.js';
import { toast } from '../../../shared/components/toast.component.js';
import { escapeHtml, qs } from '../../../shared/utils/dom.utils.js';
import { formatTimerMmSs } from '../../../shared/utils/format.utils.js';
import { exportQuizReviewSheet } from '../../../shared/utils/printExport.utils.js';
import { applyStaggerEntrance } from '../../../shared/utils/spotlight.utils.js';
import { triggerCelebrationConfetti } from '../../../shared/utils/theme.utils.js';
import {
    mountWeaknessRadarChart,
    renderWeaknessRadarMarkup,
    renderWeaknessRadarSkeleton
} from '../../profile/weaknessRadar.view.js';
import { quizApi } from './quiz.api.js';
import { QuizTimerService } from './quizTimer.service.js';
import {
    renderActiveQuizExam,
    renderQuizMaterialOptions,
    renderQuizResultReview,
    renderQuizzesList
} from './quiz.view.js';

export function createQuizController({ roomId, onExpandPane, onQuizSubmitted, onAskBotPrompt = null }) {
    const createSectionEl = qs('#quiz-create-section');
    const weaknessSectionEl = qs('#quiz-weakness-radar-section');
    const weaknessBodyEl = qs('#ws-quiz-weakness-body');
    const runWeaknessBtn = qs('#ws-run-weakness-radar-btn');
    const generateFormEl = qs('#generate-quiz-form');
    const materialSelectEl = qs('#quiz-material-select');
    const generateBtn = qs('#quiz-generate-btn');
    const stageContainerEl = qs('#quiz-stage-container');

    const timer = new QuizTimerService();
    let quizzes = [];
    let activeQuiz = null;
    let lastAttemptResult = null;
    let selectedAnswers = new Map();
    let remainingSec = 0;
    let isSubmitting = false;

    const showListStage = () => {
        timer.stop();
        activeQuiz = null;
        selectedAnswers.clear();
        createSectionEl?.classList.remove('hidden');
        weaknessSectionEl?.classList.remove('hidden');
        if (stageContainerEl) {
            stageContainerEl.innerHTML = renderQuizzesList(quizzes);
            applyStaggerEntrance(stageContainerEl);
        }
    };

    const runWeaknessRadar = async () => {
        if (!weaknessBodyEl) return;
        weaknessBodyEl.innerHTML = renderWeaknessRadarSkeleton();
        if (runWeaknessBtn) {
            runWeaknessBtn.disabled = true;
            runWeaknessBtn.textContent = 'Đang quét...';
        }
        try {
            const analysis = await quizApi.analyzeWeakness(roomId);
            weaknessBodyEl.innerHTML = renderWeaknessRadarMarkup(analysis, {
                compact: true,
                canvasId: 'ws-quiz-weakness-chart'
            });
            mountWeaknessRadarChart('ws-quiz-weakness-chart', analysis.subjectRadar || []);
        } catch (err) {
            weaknessBodyEl.innerHTML = `<p class="text-xs text-amber-700 dark:text-amber-300">${escapeHtml(err.message || 'Cần kết nối máy chủ để phân tích lỗ hổng AI.')}</p>`;
        } finally {
            if (runWeaknessBtn) {
                runWeaknessBtn.disabled = false;
                runWeaknessBtn.textContent = 'Quét lại AI';
            }
        }
    };

    const refreshQuizzes = async () => {
        try {
            quizzes = await quizApi.getRoomQuizzes(roomId);
        } catch {
            quizzes = [];
        }
        if (!activeQuiz && stageContainerEl) {
            stageContainerEl.innerHTML = renderQuizzesList(quizzes);
            applyStaggerEntrance(stageContainerEl);
        }
        return quizzes;
    };

    const startExam = async (quizId) => {
        try {
            const takeData = await quizApi.getQuizForTaking(quizId);
            activeQuiz = takeData;
            selectedAnswers = new Map();
            createSectionEl?.classList.add('hidden');
            weaknessSectionEl?.classList.add('hidden');
            onExpandPane?.(true);

            remainingSec = takeData.durationMinutes * 60;
            if (stageContainerEl) {
                stageContainerEl.innerHTML = renderActiveQuizExam(activeQuiz, selectedAnswers, remainingSec);
            }

            timer.start(takeData.durationMinutes, {
                onTick: (secLeft) => {
                    remainingSec = secLeft;
                    const badge = qs('#quiz-timer-badge');
                    if (badge) {
                        badge.textContent = formatTimerMmSs(secLeft);
                        if (secLeft <= 60) {
                            badge.className = 'rounded-lg px-2.5 py-1 font-mono text-sm font-bold bg-rose-100 text-rose-700 animate-pulse';
                        }
                    }
                },
                onTimeExpired: () => {
                    toast.warning('Đã hết thời gian làm bài! Hệ thống đang tự động nộp bài của bạn.');
                    submitActiveExam(true);
                }
            });
        } catch (err) {
            toast.error(err.message || 'Không thể tải đề thi trắc nghiệm.');
        }
    };

    const submitActiveExam = async (autoSubmitted = false) => {
        if (!activeQuiz || isSubmitting) return;
        isSubmitting = true;
        const elapsedSeconds = timer.getElapsedSeconds();
        timer.stop();

        const answersPayload = activeQuiz.questions.map((q) => ({
            questionId: q.id,
            selectedOption: selectedAnswers.get(q.id) || null
        }));

        try {
            const result = await quizApi.submitQuiz(activeQuiz.id, {
                timeSpentSeconds: elapsedSeconds,
                answers: answersPayload
            });

            activeQuiz = null;
            lastAttemptResult = result;
            // Lưu lời giải thích AI của bài thi vừa nộp xuống IndexedDB phục vụ xem lại khi Offline
            offlineDb.saveQuizAttemptDetail(result).catch(() => {});
            if (stageContainerEl) {
                stageContainerEl.innerHTML = renderQuizResultReview(result);
                enhanceMarkdownContainer(stageContainerEl);
            }

            const numericScore = Number(result.score) || 0;
            if (numericScore >= 8.0) {
                triggerCelebrationConfetti();
                toast.success(`🎉 Xuất sắc! Bạn đạt ${numericScore.toFixed(2)}/10 điểm và được cộng điểm trên Bảng xếp hạng!`);
            } else if (!autoSubmitted) {
                toast.info(`Hoàn thành bài thi! Điểm số: ${numericScore.toFixed(2)}/10.`);
            }

            quizzes = await quizApi.getRoomQuizzes(roomId);
            onQuizSubmitted?.();
        } catch (err) {
            toast.error(err.message || 'Nộp bài thi thất bại.');
        } finally {
            isSubmitting = false;
        }
    };

    const bindEvents = () => {
        enhanceMarkdownContainer(stageContainerEl);

        generateFormEl?.addEventListener('submit', async (e) => {
            e.preventDefault();
            const materialId = Number(materialSelectEl?.value || 0);
            const questionCount = Number(qs('#quiz-question-count')?.value || 5);
            const durationMinutes = Number(qs('#quiz-duration-minutes')?.value || 10);
            const difficulty = qs('#quiz-difficulty-select')?.value || 'Medium';

            if (!materialId) {
                toast.warning('Vui lòng tải lên và chọn một tài liệu PDF nguồn.');
                return;
            }

            if (generateBtn) {
                generateBtn.disabled = true;
                generateBtn.textContent = 'Gemini đang biên soạn đề...';
            }

            try {
                const createdQuiz = await quizApi.generateQuiz(roomId, {
                    materialId,
                    questionCount,
                    durationMinutes,
                    difficulty
                });
                toast.success(`Đã tạo đề "${createdQuiz.title}"!`);
                await refreshQuizzes();
            } catch (err) {
                toast.error(err.message || 'Sinh đề trắc nghiệm thất bại.');
            } finally {
                if (generateBtn) {
                    generateBtn.disabled = false;
                    generateBtn.textContent = '✨ Sinh Đề bằng Gemini AI';
                }
            }
        });

        runWeaknessBtn?.addEventListener('click', runWeaknessRadar);

        weaknessBodyEl?.addEventListener('click', async (e) => {
            const btn = e.target.closest('[data-weakness-action]');
            if (!btn) return;
            const action = btn.dataset.weaknessAction;

            if (action === 'refresh') {
                await runWeaknessRadar();
                return;
            }

            if (action === 'ask-bot') {
                const promptText = btn.dataset.prompt || '';
                if (promptText && typeof onAskBotPrompt === 'function') {
                    onAskBotPrompt(promptText);
                }
                return;
            }

            if (action === 'generate-remedial') {
                const matId = Number(btn.dataset.materialId || materialSelectEl?.value || 0) || null;
                let focusTopics = [];
                try {
                    focusTopics = JSON.parse(btn.dataset.focusTopics || '[]');
                } catch {
                    focusTopics = [];
                }

                btn.disabled = true;
                const prevHtml = btn.innerHTML;
                btn.innerHTML = '<span>⏳ AI đang soạn 5 câu vá lỗ hổng...</span>';
                try {
                    const created = await quizApi.generateRemedialQuiz({
                        roomId: Number(roomId),
                        materialId: matId,
                        focusTopics,
                        questionCount: 5
                    });
                    toast.success(`Đã tạo xong "${created.title}" — Bắt đầu thi ngay!`);
                    await refreshQuizzes();
                    await startExam(created.id);
                } catch (err) {
                    toast.error(err.message || 'Không thể sinh đề Quiz vá lỗ hổng.');
                    btn.disabled = false;
                    btn.innerHTML = prevHtml;
                }
            }
        });

        stageContainerEl?.addEventListener('click', (e) => {
            const btn = e.target.closest('[data-quiz-action]');
            if (!btn) return;

            const action = btn.dataset.quizAction;

            if (action === 'start') {
                startExam(Number(btn.dataset.quizId));
                return;
            }

            if (action === 'select-option' && activeQuiz) {
                const qId = Number(btn.dataset.questionId);
                const optKey = btn.dataset.optionKey;
                selectedAnswers.set(qId, optKey);
                stageContainerEl.innerHTML = renderActiveQuizExam(activeQuiz, selectedAnswers, remainingSec);
                return;
            }

            if (action === 'cancel-exam') {
                if (window.confirm('Bạn đang trong thời gian làm bài. Bạn có chắc muốn hủy lượt làm này?')) {
                    showListStage();
                }
                return;
            }

            if (action === 'submit-exam') {
                submitActiveExam(false);
                return;
            }

            if (action === 'print-sheet' && lastAttemptResult) {
                exportQuizReviewSheet(lastAttemptResult);
                return;
            }

            if (action === 'back-to-list') {
                showListStage();
            }
        });

        window.addEventListener('beforeunload', (event) => {
            if (timer.isRunning()) {
                event.preventDefault();
                event.returnValue = '';
            }
        });
    };

    return {
        async init(initialMaterials = []) {
            bindEvents();
            this.syncMaterialDropdown(initialMaterials);
            await refreshQuizzes();
        },
        syncMaterialDropdown(materials = [], preselectedId = null) {
            if (materialSelectEl) {
                materialSelectEl.innerHTML = renderQuizMaterialOptions(materials, preselectedId);
            }
        },
        refreshQuizzes,
        startExam,
        runWeaknessRadar,
        isExamRunning: () => timer.isRunning()
    };
}
