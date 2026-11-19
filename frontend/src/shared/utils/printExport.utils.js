/**
 * Tiện ích Xuất / In Phiếu Ôn Tập Học Thuật (A4 Print / Save as PDF)
 * cho Bản tóm tắt AI của Học liệu PDF và Đề cương Câu hỏi Trắc nghiệm kèm Lời giải.
 */

import { renderAcademicMarkdownHtml } from '../components/markdownViewer.component.js';
import { escapeHtml } from './dom.utils.js';

function openPrintDocument(docTitle, subtitle, bodyHtml) {
    const printWin = window.open('', '_blank', 'width=920,height=780');
    if (!printWin) {
        window.print();
        return;
    }

    printWin.document.write(`<!DOCTYPE html>
<html lang="vi">
<head>
    <meta charset="UTF-8" />
    <title>${escapeHtml(docTitle)} — Smart Study Workspace</title>
    <link rel="stylesheet" href="https://fonts.googleapis.com/css2?family=Inter:wght@400;500;600;700;800&family=JetBrains+Mono:wght@400;500&display=swap" />
    <link rel="stylesheet" href="https://cdn.jsdelivr.net/npm/katex@0.16.11/dist/katex.min.css" />
    <style>
        body { font-family: 'Inter', sans-serif; color: #0f172a; max-width: 820px; margin: 28px auto; padding: 0 24px; line-height: 1.65; }
        .sheet-header { border-bottom: 2px solid #4f46e5; padding-bottom: 14px; margin-bottom: 22px; display: flex; justify-content: space-between; align-items: flex-end; }
        .brand { font-size: 11px; font-weight: 700; text-transform: uppercase; letter-spacing: 0.08em; color: #4f46e5; }
        h1 { font-size: 20px; margin: 4px 0 2px; color: #0f172a; }
        .sub { font-size: 12px; color: #64748b; }
        .q-block { border: 1px solid #cbd5e1; border-radius: 10px; padding: 14px 16px; margin-bottom: 14px; page-break-inside: avoid; }
        .q-title { font-weight: 700; font-size: 14px; margin-bottom: 8px; }
        .opt { font-size: 13px; margin: 3px 0; }
        .opt.correct { color: #047857; font-weight: 700; }
        .exp { margin-top: 8px; padding: 8px 12px; background: #eef2ff; border-left: 3px solid #4f46e5; font-size: 12.5px; border-radius: 0 6px 6px 0; }
        table { width: 100%; border-collapse: collapse; margin: 12px 0; font-size: 13px; }
        th, td { border: 1px solid #cbd5e1; padding: 7px 10px; text-align: left; }
        th { background: #eef2ff; font-weight: 700; }
        pre { background: #0f172a; color: #f8fafc; padding: 12px; border-radius: 8px; overflow-x: auto; font-family: 'JetBrains Mono', monospace; font-size: 12px; }
        code { font-family: 'JetBrains Mono', monospace; }
        .ai-code-header button { display: none; }
        @media print { .no-print { display: none !important; } body { margin: 10px auto; } }
    </style>
</head>
<body>
    <div class="no-print" style="margin-bottom:16px; text-align:right;">
        <button id="print-now-btn" style="background:#4f46e5;color:#fff;border:0;padding:8px 16px;border-radius:8px;font-weight:600;cursor:pointer;">
            In / Lưu thành PDF
        </button>
    </div>
    <div class="sheet-header">
        <div>
            <div class="brand">Smart Study Workspace &bull; Phiếu Ôn Tập Học Thuật</div>
            <h1>${escapeHtml(docTitle)}</h1>
            <div class="sub">${escapeHtml(subtitle)}</div>
        </div>
        <div class="sub">${new Date().toLocaleDateString('vi-VN')}</div>
    </div>
    <div>${bodyHtml}</div>
</body>
</html>`);
    printWin.document.close();
    printWin.addEventListener('load', () => {
        const btn = printWin.document.getElementById('print-now-btn');
        btn?.addEventListener('click', () => printWin.print());
        setTimeout(() => printWin.print(), 350);
    });
}

export function exportMaterialSummarySheet(material, roomName = '') {
    const summaryHtml = renderAcademicMarkdownHtml(material.aiSummary || material.extractedText || '');
    openPrintDocument(
        material.title,
        `Phòng học: ${roomName || 'Smart Study'} • ${material.pageCount} trang gốc`,
        summaryHtml
    );
}

export function exportQuizReviewSheet(result) {
    const cardsHtml = (result.details || [])
        .map((q) => {
            const opts = [
                ['A', q.optionA],
                ['B', q.optionB],
                ['C', q.optionC],
                ['D', q.optionD]
            ]
                .map(
                    ([k, v]) =>
                        `<div class="opt ${k === q.correctOption ? 'correct' : ''}">${k}. ${escapeHtml(v)} ${
                            k === q.correctOption ? '✓ (Đáp án đúng)' : ''
                        }</div>`
                )
                .join('');

            return `
                <div class="q-block">
                    <div class="q-title">Câu ${q.orderIndex}. ${escapeHtml(q.questionText)}</div>
                    ${opts}
                    <div class="exp"><strong>Giải thích AI:</strong> ${renderAcademicMarkdownHtml(q.explanation)}</div>
                </div>
            `;
        })
        .join('');

    openPrintDocument(
        result.quizTitle,
        `Kết quả: ${Number(result.score).toFixed(2)}/10 (${result.correctAnswersCount}/${result.totalQuestions} câu đúng)`,
        cardsHtml
    );
}

export function exportScratchpadSheet({ roomName = '', content = '' }) {
    const renderedHtml = renderAcademicMarkdownHtml(content || '');
    openPrintDocument(
        `Sổ tay Ôn thi — ${roomName || 'Smart Study Workspace'}`,
        `Tổng hợp ghi chú cá nhân & kiến thức lưu từ Trợ giảng AI (@bot)`,
        renderedHtml
    );
}
