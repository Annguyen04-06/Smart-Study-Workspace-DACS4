/**
 * Tầng View vẽ 2 biểu đồ trực quan bằng Chart.js trên Admin Dashboard:
 * 1. Biểu đồ Cột chồng (Stacked Bar): Lượng Prompt & Completion Token Gemini trong 14 ngày gần nhất.
 * 2. Biểu đồ Vành khuyên (Doughnut): Tỷ trọng lượt gọi giữa 4 nhóm tính năng AI.
 */

let tokenChartInstance = null;
let featureChartInstance = null;

export function renderAdminAnalyticsCharts(dailyTokenUsage = [], featureBreakdown = []) {
    const ChartGlobal = window.Chart;
    if (!ChartGlobal) {
        return;
    }

    const isDark = document.documentElement.classList.contains('dark');
    const tickColor = isDark ? '#94a3b8' : '#64748b';
    const gridColor = isDark ? 'rgba(51, 65, 85, 0.45)' : 'rgba(226, 232, 240, 0.75)';
    const borderCutoutColor = isDark ? '#0f172a' : '#ffffff';

    const tokenCanvas = document.getElementById('admin-token-7days-chart');
    if (tokenCanvas) {
        if (tokenChartInstance) {
            tokenChartInstance.destroy();
        }

        const labels = dailyTokenUsage.map((d) => d.dateLabel);
        const requestSeries = dailyTokenUsage.map((d) => d.requestCount ?? 0);

        tokenChartInstance = new ChartGlobal(tokenCanvas, {
            type: 'bar',
            data: {
                labels,
                datasets: [
                    {
                        label: 'Lượt hỗ trợ học tập AI',
                        data: requestSeries,
                        backgroundColor: '#6366f1',
                        hoverBackgroundColor: '#4f46e5',
                        borderRadius: 8
                    }
                ]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                plugins: {
                    legend: {
                        position: 'top',
                        labels: { color: tickColor, usePointStyle: true, boxWidth: 8, font: { family: 'Inter', size: 12 } }
                    }
                },
                scales: {
                    x: { ticks: { color: tickColor }, grid: { display: false } },
                    y: { beginAtZero: true, ticks: { color: tickColor, precision: 0 }, grid: { color: gridColor } }
                }
            }
        });
    }

    const featureCanvas = document.getElementById('admin-feature-ratio-chart');
    if (featureCanvas) {
        if (featureChartInstance) {
            featureChartInstance.destroy();
        }

        const labels = featureBreakdown.map((f) => f.displayName);
        const counts = featureBreakdown.map((f) => f.requestCount);
        const hasAnyData = counts.some((c) => c > 0);

        featureChartInstance = new ChartGlobal(featureCanvas, {
            type: 'doughnut',
            data: {
                labels: hasAnyData ? labels : ['Chưa phát sinh truy vấn'],
                datasets: [
                    {
                        data: hasAnyData ? counts : [1],
                        backgroundColor: hasAnyData
                            ? ['#4f46e5', '#10b981', '#f59e0b', '#a855f7']
                            : ['#e2e8f0'],
                        borderWidth: 2,
                        borderColor: borderCutoutColor
                    }
                ]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                cutout: '64%',
                plugins: {
                    legend: {
                        position: 'bottom',
                        labels: { color: tickColor, usePointStyle: true, padding: 14, font: { family: 'Inter', size: 12 } }
                    }
                }
            }
        });
    }
}
