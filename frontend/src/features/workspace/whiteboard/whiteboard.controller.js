/**
 * Cột 3 Tab 5 — Controller Bảng Trắng Học Nhóm Trực Tiếp (Collaborative Group Whiteboard):
 * - Vẽ mượt 60FPS trên HTML5 <canvas> hỗ trợ màn hình Retina/HiDPI.
 * - Throttle chuẩn 35ms cho SendCursorMove & SendDrawStroke qua SignalR.
 * - Hiển thị con trỏ chuột màu riêng biệt kèm nhãn tên sinh viên theo phong cách Figma.
 */

import { signalrClient } from '../../../core/realtime/signalrClient.js';
import { toast } from '../../../shared/components/toast.component.js';
import { escapeHtml, qs, qsa } from '../../../shared/utils/dom.utils.js';
import { renderWhiteboardStudioShell } from './whiteboard.view.js';

const THROTTLE_MS = 35;
const CURSOR_IDLE_TIMEOUT_MS = 7000;

export function createWhiteboardController({ roomId, currentUser, roomName }) {
    const mountEl = qs('#whiteboard-stage-container');

    let canvas = null;
    let ctx = null;
    let stageEl = null;
    let cursorsLayerEl = null;

    let currentTool = 'pen';
    let currentColor = '#4f46e5';
    let currentWidth = 2.5;

    let isDrawing = false;
    let activeStroke = null;
    let lastCursorSentAt = 0;
    let lastStreamSentAt = 0;
    let streamedPointOffset = 0;

    // Lưu lịch sử toàn bộ nét vẽ đã xác nhận để vẽ lại tức thì khi resize hoặc xem trước hình khối
    let committedStrokes = [];
    const remoteCursors = new Map();

    const getCanvasMetrics = () => {
        if (!canvas) return { width: 1, height: 1 };
        const rect = canvas.getBoundingClientRect();
        return {
            width: Math.max(1, rect.width),
            height: Math.max(1, rect.height),
            left: rect.left,
            top: rect.top
        };
    };

    const resizeCanvasToDisplaySize = () => {
        if (!canvas || !stageEl) return;
        const rect = stageEl.getBoundingClientRect();
        if (rect.width < 10 || rect.height < 10) return;

        const dpr = Math.min(window.devicePixelRatio || 1, 2);
        const targetW = Math.floor(rect.width * dpr);
        const targetH = Math.floor(rect.height * dpr);

        if (canvas.width !== targetW || canvas.height !== targetH) {
            canvas.width = targetW;
            canvas.height = targetH;
            ctx = canvas.getContext('2d');
            ctx.scale(dpr, dpr);
            redrawAllStrokes();
        }
    };

    const drawSingleStroke = (targetCtx, stroke, widthPx, heightPx) => {
        const pts = stroke?.points;
        if (!pts || pts.length === 0) return;

        targetCtx.save();
        const isEraser = stroke.tool === 'eraser';
        targetCtx.lineCap = 'round';
        targetCtx.lineJoin = 'round';
        targetCtx.lineWidth = isEraser ? Math.max(16, (stroke.width || 4) * 3.5) : (stroke.width || 2.5);

        if (isEraser) {
            targetCtx.globalCompositeOperation = 'destination-out';
            targetCtx.strokeStyle = 'rgba(0,0,0,1)';
        } else {
            targetCtx.globalCompositeOperation = 'source-over';
            targetCtx.strokeStyle = stroke.color || '#4f46e5';
            targetCtx.fillStyle = stroke.color || '#4f46e5';
        }

        const p0 = { x: pts[0].x * widthPx, y: pts[0].y * heightPx };
        const pLast = { x: pts[pts.length - 1].x * widthPx, y: pts[pts.length - 1].y * heightPx };

        if (stroke.tool === 'line') {
            targetCtx.beginPath();
            targetCtx.moveTo(p0.x, p0.y);
            targetCtx.lineTo(pLast.x, pLast.y);
            targetCtx.stroke();
        } else if (stroke.tool === 'arrow') {
            targetCtx.beginPath();
            targetCtx.moveTo(p0.x, p0.y);
            targetCtx.lineTo(pLast.x, pLast.y);
            targetCtx.stroke();

            const angle = Math.atan2(pLast.y - p0.y, pLast.x - p0.x);
            const headLen = 11 + (stroke.width || 2.5) * 1.4;
            targetCtx.beginPath();
            targetCtx.moveTo(pLast.x, pLast.y);
            targetCtx.lineTo(
                pLast.x - headLen * Math.cos(angle - Math.PI / 6),
                pLast.y - headLen * Math.sin(angle - Math.PI / 6)
            );
            targetCtx.lineTo(
                pLast.x - headLen * Math.cos(angle + Math.PI / 6),
                pLast.y - headLen * Math.sin(angle + Math.PI / 6)
            );
            targetCtx.closePath();
            targetCtx.fill();
        } else if (stroke.tool === 'rect') {
            targetCtx.strokeRect(p0.x, p0.y, pLast.x - p0.x, pLast.y - p0.y);
        } else if (stroke.tool === 'circle') {
            const cx = (p0.x + pLast.x) / 2;
            const cy = (p0.y + pLast.y) / 2;
            const rx = Math.max(2, Math.abs(pLast.x - p0.x) / 2);
            const ry = Math.max(2, Math.abs(pLast.y - p0.y) / 2);
            targetCtx.beginPath();
            targetCtx.ellipse(cx, cy, rx, ry, 0, 0, Math.PI * 2);
            targetCtx.stroke();
        } else {
            // Bút vẽ mượt Quadratic Curve hoặc Tẩy
            targetCtx.beginPath();
            targetCtx.moveTo(p0.x, p0.y);
            if (pts.length === 1) {
                targetCtx.arc(p0.x, p0.y, targetCtx.lineWidth / 2, 0, Math.PI * 2);
                targetCtx.fill();
            } else {
                for (let i = 1; i < pts.length; i++) {
                    const prev = { x: pts[i - 1].x * widthPx, y: pts[i - 1].y * heightPx };
                    const curr = { x: pts[i].x * widthPx, y: pts[i].y * heightPx };
                    const midX = (prev.x + curr.x) / 2;
                    const midY = (prev.y + curr.y) / 2;
                    targetCtx.quadraticCurveTo(prev.x, prev.y, midX, midY);
                }
                targetCtx.lineTo(pLast.x, pLast.y);
                targetCtx.stroke();
            }
        }

        targetCtx.restore();
    };

    const redrawAllStrokes = (previewStroke = null) => {
        if (!ctx || !canvas) return;
        const { width, height } = getCanvasMetrics();
        ctx.clearRect(0, 0, width, height);

        for (const s of committedStrokes) {
            drawSingleStroke(ctx, s, width, height);
        }
        if (previewStroke) {
            drawSingleStroke(ctx, previewStroke, width, height);
        }
    };

    const renderOrUpdateRemoteCursor = ({ userId, fullName, color, x, y }) => {
        if (!cursorsLayerEl || Number(userId) === Number(currentUser.id)) return;
        const { width, height } = getCanvasMetrics();
        const px = Math.max(0, Math.min(width - 8, x * width));
        const py = Math.max(0, Math.min(height - 8, y * height));

        let entry = remoteCursors.get(userId);
        if (!entry) {
            const el = document.createElement('div');
            el.className = 'absolute top-0 left-0 transition-transform duration-75 ease-out will-change-transform';
            el.innerHTML = `
                <svg class="w-4 h-4 drop-shadow" viewBox="0 0 24 24" fill="${escapeHtml(color || '#6366f1')}">
                    <path d="M4 2l16 10-7 2-3 7L4 2z" stroke="#ffffff" stroke-width="1.5" stroke-linejoin="round"/>
                </svg>
                <span class="ml-3 -mt-1 inline-block whitespace-nowrap rounded-full px-2 py-0.5 text-[10px] font-bold text-white shadow-md"
                      style="background-color: ${escapeHtml(color || '#6366f1')}">
                    ${escapeHtml(fullName || 'Thành viên')}
                </span>
            `;
            cursorsLayerEl.appendChild(el);
            entry = { el, timer: null };
            remoteCursors.set(userId, entry);
        }

        entry.el.style.transform = `translate3d(${px.toFixed(1)}px, ${py.toFixed(1)}px, 0)`;
        entry.el.style.opacity = '1';

        if (entry.timer) clearTimeout(entry.timer);
        entry.timer = window.setTimeout(() => {
            entry.el.style.opacity = '0';
        }, CURSOR_IDLE_TIMEOUT_MS);
    };

    const downloadBoardPng = () => {
        if (!canvas) return;
        const { width, height } = getCanvasMetrics();
        const exportCanvas = document.createElement('canvas');
        exportCanvas.width = width * 2;
        exportCanvas.height = height * 2;
        const exCtx = exportCanvas.getContext('2d');
        exCtx.scale(2, 2);

        const isDark = document.documentElement.classList.contains('dark');
        exCtx.fillStyle = isDark ? '#0f172a' : '#ffffff';
        exCtx.fillRect(0, 0, width, height);

        // Vẽ lưới chấm nhẹ thẩm mỹ trên ảnh xuất
        exCtx.fillStyle = isDark ? 'rgba(148,163,184,0.2)' : 'rgba(148,163,184,0.35)';
        for (let gx = 16; gx < width; gx += 20) {
            for (let gy = 16; gy < height; gy += 20) {
                exCtx.fillRect(gx, gy, 1.2, 1.2);
            }
        }

        // Vẽ các nét vẽ trên lớp phụ để hỗ trợ tẩy trong suốt không làm thủng nền giấy
        const strokeLayer = document.createElement('canvas');
        strokeLayer.width = width * 2;
        strokeLayer.height = height * 2;
        const sCtx = strokeLayer.getContext('2d');
        sCtx.scale(2, 2);
        for (const s of committedStrokes) {
            drawSingleStroke(sCtx, s, width, height);
        }

        exCtx.drawImage(strokeLayer, 0, 0, width, height);

        const link = document.createElement('a');
        const safeRoom = String(roomName || 'phong-hoc').replace(/[^a-zA-Z0-9_-]+/g, '-').toLowerCase();
        link.download = `bang-trang-${safeRoom}-${Date.now()}.png`;
        link.href = exportCanvas.toDataURL('image/png');
        link.click();
        toast.success('Đã tải ảnh chụp Bảng trắng (.png) về máy!');
    };

    const bindCanvasEvents = () => {
        if (!mountEl) return;
        mountEl.innerHTML = renderWhiteboardStudioShell();

        stageEl = qs('#wb-canvas-stage', mountEl);
        canvas = qs('#wb-main-canvas', mountEl);
        cursorsLayerEl = qs('#wb-remote-cursors', mountEl);

        const updateToolButtonsUi = () => {
            qsa('[data-wb-tool]', mountEl).forEach((btn) => {
                const active = btn.dataset.wbTool === currentTool;
                btn.className = active
                    ? 'rounded-lg bg-indigo-600 px-2.5 py-1 text-[11px] font-bold text-white shadow-xs transition'
                    : 'rounded-lg px-2 py-1 text-[11px] font-semibold text-slate-600 dark:text-slate-300 hover:bg-white dark:hover:bg-slate-700 transition';
            });
        };

        mountEl.addEventListener('click', async (e) => {
            const toolBtn = e.target.closest('[data-wb-tool]');
            if (toolBtn) {
                currentTool = toolBtn.dataset.wbTool;
                updateToolButtonsUi();
                return;
            }

            const colorBtn = e.target.closest('[data-wb-color]');
            if (colorBtn) {
                currentColor = colorBtn.dataset.wbColor;
                if (currentTool === 'eraser') {
                    currentTool = 'pen';
                    updateToolButtonsUi();
                }
                qsa('[data-wb-color]', mountEl).forEach((b) => {
                    const selected = b === colorBtn;
                    b.classList.toggle('ring-2', selected);
                    b.classList.toggle('ring-offset-1', selected);
                    b.classList.toggle('ring-indigo-500', selected);
                });
                return;
            }

            const widthBtn = e.target.closest('[data-wb-width]');
            if (widthBtn) {
                currentWidth = Number(widthBtn.dataset.wbWidth) || 2.5;
                qsa('[data-wb-width]', mountEl).forEach((b) => {
                    const selected = b === widthBtn;
                    b.className = selected
                        ? 'rounded-md bg-indigo-50 dark:bg-indigo-500/20 px-2 py-0.5 text-[10px] font-bold text-indigo-700 dark:text-indigo-300 ring-1 ring-indigo-400'
                        : 'rounded-md px-2 py-0.5 text-[10px] font-semibold text-slate-500 hover:bg-slate-100 dark:hover:bg-slate-800';
                });
                return;
            }

            const actionBtn = e.target.closest('[data-wb-action]');
            if (!actionBtn) return;

            if (actionBtn.dataset.wbAction === 'download') {
                downloadBoardPng();
            } else if (actionBtn.dataset.wbAction === 'clear') {
                if (!window.confirm('Xóa toàn bộ nét vẽ trên bảng trắng chung của phòng?')) return;
                committedStrokes = [];
                redrawAllStrokes();
                try {
                    await signalrClient.clearWhiteboard(roomId);
                } catch {
                    toast.info('Đã xóa bảng vẽ cục bộ.');
                }
            }
        });

        const getNormPoint = (evt) => {
            const { width, height, left, top } = getCanvasMetrics();
            return {
                x: Math.max(0, Math.min(1, (evt.clientX - left) / width)),
                y: Math.max(0, Math.min(1, (evt.clientY - top) / height))
            };
        };

        canvas?.addEventListener('pointerdown', (evt) => {
            if (evt.button !== 0) return;
            resizeCanvasToDisplaySize();
            canvas.setPointerCapture?.(evt.pointerId);

            const pt = getNormPoint(evt);
            isDrawing = true;
            streamedPointOffset = 0;
            lastStreamSentAt = performance.now();

            activeStroke = {
                id: `${currentUser.id}-${Date.now()}`,
                tool: currentTool,
                color: currentColor,
                width: currentWidth,
                points: [pt]
            };
            redrawAllStrokes(activeStroke);
        });

        canvas?.addEventListener('pointermove', (evt) => {
            const now = performance.now();
            const pt = getNormPoint(evt);

            // Throttle 35ms gửi tọa độ con trỏ chuột nhóm qua SignalR
            if (now - lastCursorSentAt >= THROTTLE_MS) {
                lastCursorSentAt = now;
                signalrClient.sendCursorMove(roomId, pt.x, pt.y).catch(() => {});
            }

            if (!isDrawing || !activeStroke) return;

            const isShape = ['line', 'rect', 'circle', 'arrow'].includes(activeStroke.tool);
            if (isShape) {
                activeStroke.points = [activeStroke.points[0], pt];
            } else {
                activeStroke.points.push(pt);
                // Stream từng phân đoạn mượt 35ms khi vẽ bút tự do hoặc tẩy
                if (now - lastStreamSentAt >= THROTTLE_MS && activeStroke.points.length - streamedPointOffset >= 3) {
                    const chunkPoints = activeStroke.points.slice(Math.max(0, streamedPointOffset - 1));
                    streamedPointOffset = activeStroke.points.length;
                    lastStreamSentAt = now;

                    const chunkStroke = {
                        id: `${activeStroke.id}-${streamedPointOffset}`,
                        tool: activeStroke.tool,
                        color: activeStroke.color,
                        width: activeStroke.width,
                        points: chunkPoints
                    };
                    signalrClient.sendDrawStroke(roomId, chunkStroke).catch(() => {});
                }
            }

            redrawAllStrokes(activeStroke);
        });

        const finishActiveStroke = () => {
            if (!isDrawing || !activeStroke) return;
            isDrawing = false;

            const finalStroke = activeStroke;
            activeStroke = null;
            committedStrokes.push(finalStroke);
            redrawAllStrokes();

            const isShape = ['line', 'rect', 'circle', 'arrow'].includes(finalStroke.tool);
            if (isShape || streamedPointOffset === 0) {
                signalrClient.sendDrawStroke(roomId, finalStroke).catch(() => {});
            } else if (finalStroke.points.length > streamedPointOffset) {
                const tailPoints = finalStroke.points.slice(Math.max(0, streamedPointOffset - 1));
                signalrClient.sendDrawStroke(roomId, {
                    ...finalStroke,
                    id: `${finalStroke.id}-end`,
                    points: tailPoints
                }).catch(() => {});
            }
        };

        canvas?.addEventListener('pointerup', finishActiveStroke);
        canvas?.addEventListener('pointercancel', finishActiveStroke);

        // Đồng bộ các sự kiện SignalR từ các thành viên khác trong phòng
        signalrClient.on('WhiteboardStateSynced', (payload) => {
            if (Number(payload?.roomId) !== Number(roomId)) return;
            committedStrokes = Array.isArray(payload.strokes) ? payload.strokes : [];
            redrawAllStrokes();
        });

        signalrClient.on('ReceiveDrawStroke', (payload) => {
            if (Number(payload?.roomId) !== Number(roomId) || !payload.stroke) return;
            committedStrokes.push(payload.stroke);
            if (canvas && ctx) {
                const { width, height } = getCanvasMetrics();
                drawSingleStroke(ctx, payload.stroke, width, height);
            }
        });

        signalrClient.on('ReceiveCursorMove', (payload) => {
            if (Number(payload?.roomId) !== Number(roomId)) return;
            renderOrUpdateRemoteCursor(payload);
        });

        signalrClient.on('WhiteboardCleared', (payload) => {
            if (Number(payload?.roomId) !== Number(roomId)) return;
            committedStrokes = [];
            redrawAllStrokes();
            if (payload.clearedByName && Number(payload.clearedByUserId) !== Number(currentUser.id)) {
                toast.info(`${payload.clearedByName} vừa làm sạch Bảng trắng nhóm.`, 2200);
            }
        });

        if (typeof ResizeObserver !== 'undefined' && stageEl) {
            const ro = new ResizeObserver(() => resizeCanvasToDisplaySize());
            ro.observe(stageEl);
        } else {
            window.addEventListener('resize', resizeCanvasToDisplaySize);
        }
    };

    return {
        init() {
            bindCanvasEvents();
        },
        onTabActivated() {
            window.requestAnimationFrame(() => {
                resizeCanvasToDisplaySize();
                signalrClient.requestWhiteboardState(roomId).catch(() => {});
            });
        }
    };
}
