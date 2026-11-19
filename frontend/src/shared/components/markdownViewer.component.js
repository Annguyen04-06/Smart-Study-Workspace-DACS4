/**
 * Bộ Render Học thuật Đẳng cấp (.ai-prose Pipeline 4 Bước):
 * - Bước 1 (Protect Math): Tách tạm toàn bộ khối toán $$...$$, \[...\], $...$, \(...\) thành @@MATH_BLOCK_i@@
 *   trước khi đưa vào marked.parse() để không bao giờ bị mất dấu _, *, \.
 * - Bước 2 (Custom IDE Code Block & XSS Sanitizer): Khung code có 3 chấm màu macOS + nhãn ngôn ngữ
 *   (C#, PYTHON, SQL, JS...) + nút Copy code (đổi thành "Đã sao chép ✓" trong 2s) + Highlight.js.
 * - Bước 3 (Table Wrapper): Tự động bọc mọi thẻ <table> trong <div class="ai-table-wrapper"> cuộn ngang mượt.
 * - Bước 4 (Restore & Render KaTeX): Render trực tiếp katex.renderToString(latex, { displayMode, throwOnError: false }).
 */

import { copyTextToClipboard, escapeHtml } from '../utils/dom.utils.js';

const LANGUAGE_LABELS = Object.freeze({
    cs: 'C#',
    csharp: 'C#',
    dotnet: 'C#',
    py: 'PYTHON',
    python: 'PYTHON',
    sql: 'SQL',
    tsql: 'T-SQL',
    js: 'JAVASCRIPT',
    javascript: 'JAVASCRIPT',
    ts: 'TYPESCRIPT',
    typescript: 'TYPESCRIPT',
    html: 'HTML',
    css: 'CSS',
    json: 'JSON',
    bash: 'BASH',
    sh: 'SHELL',
    powershell: 'POWERSHELL',
    cpp: 'C++',
    c: 'C',
    java: 'JAVA'
});

function formatLanguageBadge(rawLang) {
    const clean = String(rawLang || '').trim().toLowerCase();
    if (!clean) return 'CODE';
    return LANGUAGE_LABELS[clean] || clean.toUpperCase();
}

function highlightCodeSafely(rawCode, rawLang) {
    const lang = String(rawLang || '').trim().toLowerCase();
    if (window.hljs) {
        try {
            if (lang && window.hljs.getLanguage?.(lang)) {
                return window.hljs.highlight(rawCode, { language: lang, ignoreIllegals: true }).value;
            }
            return window.hljs.highlightAuto(rawCode).value;
        } catch {
            return escapeHtml(rawCode);
        }
    }
    return escapeHtml(rawCode);
}

function buildIdeCodeBlockHtml(rawCode, rawLang) {
    const langBadge = formatLanguageBadge(rawLang);
    const highlighted = highlightCodeSafely(rawCode, rawLang);

    return `
        <div class="ai-code-wrapper">
            <div class="ai-code-header">
                <div class="flex items-center gap-2">
                    <span class="inline-flex items-center gap-1.5">
                        <span class="h-2.5 w-2.5 rounded-full bg-rose-500/90"></span>
                        <span class="h-2.5 w-2.5 rounded-full bg-amber-400/90"></span>
                        <span class="h-2.5 w-2.5 rounded-full bg-emerald-500/90"></span>
                    </span>
                    <span class="ml-1.5 font-mono text-[11px] font-bold tracking-wider text-slate-300">${escapeHtml(langBadge)}</span>
                </div>
                <button type="button" data-copy-code class="ai-copy-code-btn inline-flex items-center gap-1.5 rounded-md bg-white/[0.07] px-2.5 py-1 text-[11px] font-semibold text-slate-200 hover:bg-white/15 hover:text-white transition">
                    <span>Copy code</span>
                </button>
            </div>
            <pre><code class="hljs">${highlighted}</code></pre>
        </div>
    `;
}

function sanitizeAndWrapTables(dirtyHtml) {
    const parser = new DOMParser();
    const doc = parser.parseFromString(dirtyHtml, 'text/html');

    const forbiddenTags = ['script', 'iframe', 'object', 'embed', 'form', 'input', 'style', 'link', 'meta'];
    forbiddenTags.forEach((tag) => {
        doc.querySelectorAll(tag).forEach((el) => el.remove());
    });

    doc.querySelectorAll('*').forEach((el) => {
        Array.from(el.attributes).forEach((attr) => {
            const name = attr.name.toLowerCase();
            const val = attr.value.toLowerCase().trim();
            if (
                name.startsWith('on') ||
                (name === 'href' && val.startsWith('javascript:')) ||
                (name === 'src' && val.startsWith('javascript:'))
            ) {
                el.removeAttribute(attr.name);
            }
        });
    });

    // Bước 3: Bọc mọi thẻ <table> trong <div class="ai-table-wrapper"> cuộn ngang mượt mà
    doc.querySelectorAll('table').forEach((tableEl) => {
        if (tableEl.parentElement?.classList.contains('ai-table-wrapper')) {
            return;
        }
        const wrapper = doc.createElement('div');
        wrapper.className = 'ai-table-wrapper';
        tableEl.parentNode?.insertBefore(wrapper, tableEl);
        wrapper.appendChild(tableEl);
    });

    return doc.body.innerHTML;
}

/**
 * Chuyển chuỗi Markdown + LaTeX sang HTML chuẩn .ai-prose an toàn tuyệt đối.
 */
export function renderAcademicMarkdownHtml(rawMarkdown) {
    if (!rawMarkdown) {
        return '';
    }

    const mathBlocks = [];
    const stashMath = (latex, displayMode) => {
        const index = mathBlocks.length;
        mathBlocks.push({ latex: latex.trim(), displayMode });
        return `@@MATH_BLOCK_${index}@@`;
    };

    const codeBlocks = [];
    const stashCodeBlock = (code, lang) => {
        const index = codeBlocks.length;
        codeBlocks.push({ code, lang });
        return `\n\n@@CODE_BLOCK_${index}@@\n\n`;
    };

    // Tách trước các khối fenced code ```lang ... ``` để ký hiệu $ bên trong code không bị nhầm là LaTeX
    let working = String(rawMarkdown).replace(
        /```([a-zA-Z0-9_+#.-]*)\r?\n([\s\S]*?)```/g,
        (_, lang, code) => stashCodeBlock(code.replace(/\n$/, ''), lang)
    );

    // Bước 1 (Protect Math): Tách tạm $$...$$, \[...\], $...$, \(...\) thành @@MATH_BLOCK_i@@
    working = working
        .replace(/\$\$([\s\S]+?)\$\$/g, (_, expr) => stashMath(expr, true))
        .replace(/\\\[([\s\S]+?)\\\]/g, (_, expr) => stashMath(expr, true))
        .replace(/(?<!\$)\$(?!\$)([^$\n]+?)\$(?!\$)/g, (_, expr) => stashMath(expr, false))
        .replace(/\\\(([^\n]+?)\\\)/g, (_, expr) => stashMath(expr, false));

    let parsedHtml = '';
    if (window.marked && typeof window.marked.parse === 'function') {
        parsedHtml = window.marked.parse(working, {
            gfm: true,
            breaks: true
        });
    } else {
        parsedHtml = `<p>${escapeHtml(working).replace(/\n/g, '<br/>')}</p>`;
    }

    // Bước 2 & 3: Sanitize XSS + Bọc bảng trong .ai-table-wrapper
    let safeHtml = sanitizeAndWrapTables(parsedHtml);

    // Khôi phục các khối IDE Code đã tô màu cú pháp
    safeHtml = safeHtml.replace(/(?:<p>)?@@CODE_BLOCK_(\d+)@@(?:<\/p>)?/g, (_, idxStr) => {
        const item = codeBlocks[Number(idxStr)];
        return item ? buildIdeCodeBlockHtml(item.code, item.lang) : '';
    });

    // Bước 4 (Restore & Render KaTeX): Render sắc nét mọi công thức toán học
    safeHtml = safeHtml.replace(/@@MATH_BLOCK_(\d+)@@/g, (_, idxStr) => {
        const item = mathBlocks[Number(idxStr)];
        if (!item) return '';
        if (window.katex && typeof window.katex.renderToString === 'function') {
            try {
                const rendered = window.katex.renderToString(item.latex, {
                    displayMode: item.displayMode,
                    throwOnError: false
                });
                return item.displayMode
                    ? `<div class="ai-math-display">${rendered}</div>`
                    : rendered;
            } catch {
                return escapeHtml(item.displayMode ? `$$${item.latex}$$` : `$${item.latex}$`);
            }
        }
        return escapeHtml(item.displayMode ? `$$${item.latex}$$` : `$${item.latex}$`);
    });

    return `<div class="ai-prose">${safeHtml}</div>`;
}

/**
 * Gắn Event Delegation cho nút "Copy code" bên trong vùng chứa .ai-prose (đổi chữ thành "Đã sao chép ✓" màu xanh trong 2 giây).
 */
export function enhanceMarkdownContainer(containerEl) {
    if (!containerEl || containerEl.dataset.copyDelegated) {
        return;
    }

    containerEl.dataset.copyDelegated = 'true';
    containerEl.addEventListener('click', async (event) => {
        const btn = event.target.closest('[data-copy-code]');
        if (!btn) return;

        const codeNode = btn.closest('.ai-code-wrapper')?.querySelector('pre code');
        if (!codeNode) return;

        await copyTextToClipboard(codeNode.textContent || '');
        btn.innerHTML = `<span class="text-emerald-400 font-bold">Đã sao chép ✓</span>`;
        setTimeout(() => {
            btn.innerHTML = `<span>Copy code</span>`;
        }, 2000);
    });
}
