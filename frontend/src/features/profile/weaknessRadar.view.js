/**
 * View hiển thị tính năng AI Smart Weakness Radar (Phân tích Lỗ hổng Kiến thức & Lộ trình Ôn tập AI):
 * - Biểu đồ Radar (hoặc Horizontal Bar nếu < 3 môn) bằng Chart.js + Thanh tiến độ từng môn
 * - Thẻ "Chẩn đoán từ Trợ giảng AI" liệt kê đúng 3 mảng kiến thức hay nhầm nhất
 * - Nút "Tạo ngay đề Quiz 5 câu vá lỗ hổng" dùng chung cho cả profile.html và Tab Quiz trong workspace.html.
 */

import { escapeHtml } from '../../shared/utils/dom.utils.js';

const chartInstances = new WeakMap();

export function renderWeaknessRadarSkeleton() {
    return `
        <div class="space-y-4 py-2">
            <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
                <div class="skeleton-box h-56 w-full rounded-2xl"></div>
                <div class="space-y-3">
                    <div class="skeleton-box h-16 w-full rounded-xl"></div>
                    <div class="skeleton-box h-16 w-full rounded-xl"></div>
                    <div class="skeleton-box h-16 w-full rounded-xl"></div>
                </div>
            </div>
            <div class="skeleton-box h-28 w-full rounded-2xl"></div>
        </div>
    `;
}

function getSeverityBadge(severity) {
    const map = {
        High: {
            label: 'Ưu tiên cao',
            cls: 'bg-rose-100 text-rose-700 border-rose-200 dark:bg-rose-500/20 dark:text-rose-300 dark:border-rose-500/35'
        },
        Medium: {
            label: 'Cần củng cố',
            cls: 'bg-amber-100 text-amber-800 border-amber-200 dark:bg-amber-500/20 dark:text-amber-300 dark:border-amber-500/35'
        },
        Low: {
            label: 'Ôn nhẹ lại',
            cls: 'bg-sky-100 text-sky-700 border-sky-200 dark:bg-sky-500/20 dark:text-sky-300 dark:border-sky-500/35'
        }
    };
    return map[severity] || map.Medium;
}

function getAccuracyBarColor(percent) {
    if (percent >= 80) return 'bg-emerald-500';
    if (percent >= 60) return 'bg-indigo-500';
    if (percent >= 40) return 'bg-amber-500';
    return 'bg-rose-500';
}

/**
 * Render toàn bộ giao diện AI Smart Weakness Radar.
 * @param {object} data Kết quả từ POST /api/quizzes/analyze-weakness
 * @param {{ compact?: boolean, canvasId?: string }} options compact=true khi đặt trong Cột 3 Workspace
 */
export function renderWeaknessRadarMarkup(data, { compact = false, canvasId = 'weakness-radar-chart' } = {}) {
    if (!data) {
        return '';
    }

    const subjects = data.subjectRadar || [];
    const weaknesses = data.topWeaknesses || [];
    const roadmap = data.studyRoadmap || [];
    const hasAttempts = Number(data.totalAttemptsAnalyzed || 0) > 0;

    const progressBarsHtml = subjects.length > 0
        ? subjects.map((s) => {
            const pct = Math.max(0, Math.min(100, Number(s.accuracyPercent) || 0));
            return `
                <div class="space-y-1">
                    <div class="flex items-center justify-between gap-2 text-xs">
                        <span class="font-semibold text-slate-700 dark:text-slate-200 truncate" title="${escapeHtml(s.roomName)}">
                            ${escapeHtml(s.subjectCategory)}
                        </span>
                        <span class="shrink-0 font-mono text-[11px] font-bold ${pct >= 75 ? 'text-emerald-600 dark:text-emerald-400' : pct >= 50 ? 'text-amber-600 dark:text-amber-400' : 'text-rose-600 dark:text-rose-400'}">
                            ${pct.toFixed(1)}% (${s.correctCount}/${s.totalQuestions})
                        </span>
                    </div>
                    <div class="h-2 w-full overflow-hidden rounded-full bg-slate-100 dark:bg-slate-800">
                        <div class="h-full rounded-full transition-all duration-500 ${getAccuracyBarColor(pct)}" style="width: ${pct}%"></div>
                    </div>
                </div>
            `;
        }).join('')
        : `<p class="text-xs text-slate-500 dark:text-slate-400 italic">Chưa có dữ liệu điểm số theo chủ đề. Hãy hoàn thành ít nhất 1 đề Quiz để biểu đồ hiển thị.</p>`;

    const gapsHtml = weaknesses.length > 0
        ? weaknesses.map((gap, idx) => {
            const sev = getSeverityBadge(gap.severity);
            return `
                <div class="rounded-xl border border-slate-200/90 dark:border-slate-800 bg-white/95 dark:bg-slate-900/90 p-3.5 space-y-2 shadow-xs">
                    <div class="flex items-start justify-between gap-2">
                        <div class="min-w-0">
                            <span class="inline-block text-[10px] font-bold uppercase tracking-wider text-indigo-600 dark:text-indigo-400">
                                Lỗ hổng #${idx + 1} &bull; ${escapeHtml(gap.subjectCategory)}
                            </span>
                            <h4 class="text-xs font-extrabold text-slate-900 dark:text-white leading-snug">
                                ${escapeHtml(gap.topicName)}
                            </h4>
                        </div>
                        <span class="shrink-0 rounded-full border px-2 py-0.5 text-[10px] font-bold ${sev.cls}">
                            ${sev.label} (${gap.mistakeCount} câu)
                        </span>
                    </div>
                    <p class="text-xs text-slate-600 dark:text-slate-300 leading-relaxed">
                        <strong class="text-slate-800 dark:text-slate-100">Nguyên nhân:</strong> ${escapeHtml(gap.rootCause)}
                    </p>
                    <p class="text-xs text-indigo-700 dark:text-indigo-300 bg-indigo-50/70 dark:bg-indigo-500/10 rounded-lg px-2.5 py-1.5 leading-relaxed">
                        <strong>Hướng vá nhanh:</strong> ${escapeHtml(gap.studyAction)}
                    </p>
                    ${gap.recommendedPrompt ? `
                        <div class="flex items-center justify-between gap-2 pt-1">
                            <span class="truncate font-mono text-[10px] text-slate-500 dark:text-slate-400">${escapeHtml(gap.recommendedPrompt)}</span>
                            <button type="button" data-weakness-action="ask-bot" data-prompt="${escapeHtml(gap.recommendedPrompt)}"
                                class="shrink-0 rounded-lg border border-indigo-200 dark:border-indigo-500/35 bg-white dark:bg-slate-800 px-2 py-1 text-[10px] font-bold text-indigo-700 dark:text-indigo-300 hover:bg-indigo-50 transition">
                                Hỏi @bot →
                            </button>
                        </div>
                    ` : ''}
                </div>
            `;
        }).join('')
        : `
            <div class="rounded-xl border border-emerald-200/80 dark:border-emerald-500/30 bg-emerald-50/60 dark:bg-emerald-500/10 p-4 text-xs text-emerald-800 dark:text-emerald-200">
                ${hasAttempts
                    ? '🎉 Tuyệt vời! Bạn chưa để lọt lỗ hổng kiến thức nghiêm trọng nào. Hãy thử sức thêm đề mức độ Khó (Hard)!'
                    : '📌 Làm xong ít nhất 1 đề Quiz để Trợ giảng AI tự động bắt bệnh đúng 3 mảng kiến thức bạn hay nhầm nhất.'}
            </div>
        `;

    const roadmapHtml = roadmap.length > 0
        ? `
            <div class="rounded-xl border border-slate-200/80 dark:border-slate-800 bg-slate-50/80 dark:bg-slate-900/60 p-3.5">
                <p class="text-[11px] font-extrabold uppercase tracking-wider text-slate-600 dark:text-slate-300 mb-2">🗺️ Lộ trình Ôn tập Đề xuất</p>
                <ol class="space-y-1.5 text-xs text-slate-700 dark:text-slate-300">
                    ${roadmap.map((step) => `<li class="flex items-start gap-2"><span class="mt-1 h-1.5 w-1.5 shrink-0 rounded-full bg-indigo-500"></span><span>${escapeHtml(step)}</span></li>`).join('')}
                </ol>
            </div>
        `
        : '';

    const canCreateRemedial = Boolean(data.suggestedRoomId || data.suggestedMaterialId);
    const focusTopicsJson = escapeHtml(JSON.stringify(weaknesses.map((w) => w.topicName)));

    const gridLayout = compact ? 'grid-cols-1' : 'grid-cols-1 lg:grid-cols-12';
    const leftSpan = compact ? '' : 'lg:col-span-5';
    const rightSpan = compact ? '' : 'lg:col-span-7';

    return `
        <div class="space-y-4">
            <!-- Thanh tổng hợp nhanh -->
            <div class="grid grid-cols-3 gap-2.5">
                <div class="rounded-xl border border-slate-200/90 dark:border-slate-800 bg-slate-50/80 dark:bg-slate-900 px-3 py-2.5 text-center">
                    <p class="text-[10px] font-bold uppercase text-slate-500">Độ chính xác</p>
                    <p class="mt-0.5 font-mono text-base font-extrabold text-indigo-600 dark:text-indigo-400">${Number(data.overallAccuracyPercent || 0).toFixed(1)}%</p>
                </div>
                <div class="rounded-xl border border-emerald-200/80 dark:border-emerald-500/30 bg-emerald-50/60 dark:bg-emerald-500/10 px-3 py-2.5 text-center">
                    <p class="text-[10px] font-bold uppercase text-emerald-700 dark:text-emerald-300">Thế mạnh</p>
                    <p class="mt-0.5 truncate text-xs font-extrabold text-emerald-800 dark:text-emerald-200" title="${escapeHtml(data.strongestSubject)}">${escapeHtml(data.strongestSubject || '—')}</p>
                </div>
                <div class="rounded-xl border border-rose-200/80 dark:border-rose-500/30 bg-rose-50/60 dark:bg-rose-500/10 px-3 py-2.5 text-center">
                    <p class="text-[10px] font-bold uppercase text-rose-700 dark:text-rose-300">Cần vá gấp</p>
                    <p class="mt-0.5 truncate text-xs font-extrabold text-rose-800 dark:text-rose-200" title="${escapeHtml(data.prioritySubject)}">${escapeHtml(data.prioritySubject || '—')}</p>
                </div>
            </div>

            <div class="grid ${gridLayout} gap-4 items-start">
                <!-- Cột Radar / Thanh tiến độ chủ đề -->
                <div class="${leftSpan} rounded-2xl border border-slate-200/90 dark:border-slate-800 bg-white dark:bg-slate-900/90 p-4 space-y-3.5">
                    <div class="flex items-center justify-between gap-2">
                        <p class="text-xs font-extrabold text-slate-800 dark:text-white">📡 Biểu đồ Năng lực Theo Môn</p>
                        <span class="text-[10px] font-mono text-slate-400">${data.totalWrongAnswers || 0} câu sai / ${data.totalQuestionsAnswered || 0} câu</span>
                    </div>
                    ${subjects.length >= 2 ? `
                        <div class="relative ${compact ? 'h-48' : 'h-56'} w-full">
                            <canvas id="${escapeHtml(canvasId)}"></canvas>
                        </div>
                    ` : ''}
                    <div class="space-y-2.5 pt-1">
                        ${progressBarsHtml}
                    </div>
                </div>

                <!-- Cột Chẩn đoán từ Trợ giảng AI -->
                <div class="${rightSpan} rounded-2xl border border-indigo-200/90 dark:border-indigo-500/30 bg-gradient-to-br from-indigo-50/70 via-white to-violet-50/60 dark:from-indigo-950/40 dark:via-slate-900 dark:to-violet-950/30 p-4 space-y-3.5 shadow-sm">
                    <div class="flex items-start justify-between gap-3">
                        <div class="flex items-center gap-2.5">
                            <span class="flex h-9 w-9 shrink-0 items-center justify-center rounded-xl bg-gradient-to-br from-indigo-600 to-violet-600 text-white text-sm shadow-md">🧠</span>
                            <div>
                                <h3 class="text-xs font-extrabold uppercase tracking-wider text-indigo-700 dark:text-indigo-300">Chẩn đoán từ Trợ giảng AI</h3>
                                <p class="text-[11px] text-slate-500 dark:text-slate-400">Tổng hợp từ ${data.totalAttemptsAnalyzed || 0} lượt thi thực tế</p>
                            </div>
                        </div>
                        <button type="button" data-weakness-action="refresh"
                            class="shrink-0 rounded-lg border border-slate-200 dark:border-slate-700 bg-white dark:bg-slate-800 px-2.5 py-1 text-[11px] font-semibold text-slate-600 dark:text-slate-300 hover:border-indigo-300 hover:text-indigo-600 transition">
                            ↻ Phân tích lại
                        </button>
                    </div>

                    <p class="text-xs text-slate-700 dark:text-slate-200 leading-relaxed bg-white/80 dark:bg-slate-900/70 rounded-xl p-3 border border-indigo-100 dark:border-indigo-500/20">
                        ${escapeHtml(data.overallDiagnosis)}
                    </p>

                    <div class="space-y-2.5">
                        ${gapsHtml}
                    </div>

                    ${roadmapHtml}

                    <button type="button"
                        data-weakness-action="generate-remedial"
                        data-room-id="${data.suggestedRoomId || ''}"
                        data-material-id="${data.suggestedMaterialId || ''}"
                        data-focus-topics="${focusTopicsJson}"
                        ${canCreateRemedial ? '' : 'disabled'}
                        class="btn-primary-glow w-full rounded-xl py-2.5 px-4 text-xs font-extrabold flex items-center justify-center gap-2 disabled:opacity-50 disabled:cursor-not-allowed">
                        <span>⚡ Tạo ngay đề Quiz 5 câu vá lỗ hổng</span>
                        ${data.suggestedRoomName ? `<span class="hidden sm:inline opacity-85 font-normal">(${escapeHtml(data.suggestedRoomName.slice(0, 28))})</span>` : ''}
                    </button>
                </div>
            </div>
        </div>
    `;
}

/**
 * Khởi tạo biểu đồ Chart.js (Radar khi >= 3 môn, Bar ngang khi có 2 môn) trên canvas vừa render.
 */
export function mountWeaknessRadarChart(canvasId, subjectRadar = []) {
    const ChartLib = window.Chart;
    const canvas = document.getElementById(canvasId);
    if (!ChartLib || !canvas || !Array.isArray(subjectRadar) || subjectRadar.length < 2) {
        return;
    }

    const prevChart = chartInstances.get(canvas);
    if (prevChart) {
        prevChart.destroy();
    }

    const isDark = document.documentElement.classList.contains('dark');
    const gridColor = isDark ? 'rgba(148, 163, 184, 0.18)' : 'rgba(148, 163, 184, 0.28)';
    const labelColor = isDark ? '#cbd5e1' : '#334155';

    const labels = subjectRadar.map((s) =>
        s.subjectCategory.length > 18 ? `${s.subjectCategory.slice(0, 17)}…` : s.subjectCategory
    );
    const values = subjectRadar.map((s) => Number(s.accuracyPercent) || 0);

    const useRadar = subjectRadar.length >= 3;
    const chart = new ChartLib(canvas.getContext('2d'), {
        type: useRadar ? 'radar' : 'bar',
        data: {
            labels,
            datasets: [
                {
                    label: 'Độ chính xác (%)',
                    data: values,
                    backgroundColor: useRadar
                        ? 'rgba(99, 102, 241, 0.24)'
                        : values.map((v) => (v >= 75 ? 'rgba(16, 185, 129, 0.75)' : v >= 50 ? 'rgba(245, 158, 11, 0.75)' : 'rgba(244, 63, 94, 0.75)')),
                    borderColor: '#6366f1',
                    borderWidth: 2,
                    pointBackgroundColor: '#4f46e5',
                    pointBorderColor: '#ffffff',
                    pointRadius: 4,
                    borderRadius: useRadar ? 0 : 8
                }
            ]
        },
        options: {
            responsive: true,
            maintainAspectRatio: false,
            plugins: {
                legend: { display: false },
                tooltip: {
                    callbacks: {
                        label: (ctx) => ` Chính xác: ${ctx.raw}%`
                    }
                }
            },
            scales: useRadar
                ? {
                    r: {
                        min: 0,
                        max: 100,
                        ticks: { stepSize: 25, color: labelColor, backdropColor: 'transparent', font: { size: 10 } },
                        grid: { color: gridColor },
                        angleLines: { color: gridColor },
                        pointLabels: { color: labelColor, font: { size: 10, weight: '600' } }
                    }
                }
                : {
                    y: { min: 0, max: 100, grid: { color: gridColor }, ticks: { color: labelColor, font: { size: 10 } } },
                    x: { grid: { display: false }, ticks: { color: labelColor, font: { size: 10, weight: '600' } } }
                }
        }
    });

    chartInstances.set(canvas, chart);
}
