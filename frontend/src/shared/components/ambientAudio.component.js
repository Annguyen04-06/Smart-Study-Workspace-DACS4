/**
 * Bộ trộn Âm thanh Tập trung Offline (Web Audio API Procedural Sound Mixer) & Chế độ Zen Focus Mode:
 * - Tổng hợp 100% bằng Web Audio API thuần (không dùng link nhạc ngoài, chạy offline tuyệt đối):
 *   1. Tiếng mưa rơi nhẹ (Light Rain — Filtered Pink/White Noise + LFO)
 *   2. Brown Noise tập trung sâu (Deep Brownian Noise 420Hz Lowpass)
 *   3. Binaural Focus Drone (Sóng não Alpha 10Hz êm dịu 196Hz L / 206Hz R + Sub Pad 98Hz)
 * - Đồng bộ 6 cột sóng nhạc chuyển động (Animated Equalizer Bars) trên Header khi đang phát âm thanh.
 */

import { toast } from './toast.component.js';
import { qs } from '../utils/dom.utils.js';

export function createAmbientAudioController() {
    let audioCtx = null;
    let masterGain = null;
    let channels = null;
    let isZenActive = false;
    let isMixerOpen = false;

    const volumes = {
        master: 0.72,
        rain: 0,
        brown: 0,
        drone: 0
    };

    const ensureAudioEngine = () => {
        if (audioCtx) {
            if (audioCtx.state === 'suspended') {
                audioCtx.resume().catch(() => {});
            }
            return true;
        }

        const AudioContextCls = window.AudioContext || window.webkitAudioContext;
        if (!AudioContextCls) {
            toast.warning('Trình duyệt không hỗ trợ bộ tổng hợp Web Audio API.');
            return false;
        }

        audioCtx = new AudioContextCls();
        masterGain = audioCtx.createGain();
        masterGain.gain.value = volumes.master;
        masterGain.connect(audioCtx.destination);

        const sampleRate = audioCtx.sampleRate;

        // 1. Kênh Tiếng mưa rơi nhẹ (Pink/White Noise qua Bandpass + Lowpass + LFO nhịp mưa)
        const rainLength = sampleRate * 3;
        const rainBuffer = audioCtx.createBuffer(2, rainLength, sampleRate);
        for (let ch = 0; ch < 2; ch++) {
            const data = rainBuffer.getChannelData(ch);
            let b0 = 0;
            let b1 = 0;
            let b2 = 0;
            for (let i = 0; i < rainLength; i++) {
                const white = Math.random() * 2 - 1;
                b0 = 0.99765 * b0 + white * 0.099046;
                b1 = 0.96300 * b1 + white * 0.2965164;
                b2 = 0.57000 * b2 + white * 1.0526913;
                data[i] = (b0 + b1 + b2 + white * 0.1848) * 0.08;
            }
        }
        const rainSource = audioCtx.createBufferSource();
        rainSource.buffer = rainBuffer;
        rainSource.loop = true;

        const rainBand = audioCtx.createBiquadFilter();
        rainBand.type = 'bandpass';
        rainBand.frequency.value = 1150;
        rainBand.Q.value = 0.65;

        const rainLow = audioCtx.createBiquadFilter();
        rainLow.type = 'lowpass';
        rainLow.frequency.value = 2900;

        const rainGain = audioCtx.createGain();
        rainGain.gain.value = 0;

        rainSource.connect(rainBand);
        rainBand.connect(rainLow);
        rainLow.connect(rainGain);
        rainGain.connect(masterGain);
        rainSource.start();

        // 2. Kênh Deep Brown Noise (Tập trung sâu — Tích phân nhiễu trắng mượt ấm)
        const brownLength = sampleRate * 4;
        const brownBuffer = audioCtx.createBuffer(2, brownLength, sampleRate);
        for (let ch = 0; ch < 2; ch++) {
            const data = brownBuffer.getChannelData(ch);
            let lastOut = 0.0;
            for (let i = 0; i < brownLength; i++) {
                const white = Math.random() * 2 - 1;
                data[i] = (lastOut + 0.022 * white) / 1.022;
                lastOut = data[i];
                data[i] *= 2.8;
            }
        }
        const brownSource = audioCtx.createBufferSource();
        brownSource.buffer = brownBuffer;
        brownSource.loop = true;

        const brownFilter = audioCtx.createBiquadFilter();
        brownFilter.type = 'lowpass';
        brownFilter.frequency.value = 410;

        const brownGain = audioCtx.createGain();
        brownGain.gain.value = 0;

        brownSource.connect(brownFilter);
        brownFilter.connect(brownGain);
        brownGain.connect(masterGain);
        brownSource.start();

        // 3. Kênh Binaural Focus Drone (Sóng Alpha 10Hz: 196Hz Trái / 206Hz Phải + Nền 98Hz ấm)
        const droneGain = audioCtx.createGain();
        droneGain.gain.value = 0;

        const droneFilter = audioCtx.createBiquadFilter();
        droneFilter.type = 'lowpass';
        droneFilter.frequency.value = 380;
        droneFilter.connect(droneGain);
        droneGain.connect(masterGain);

        const oscLeft = audioCtx.createOscillator();
        oscLeft.type = 'sine';
        oscLeft.frequency.value = 196;
        const panLeft = audioCtx.createStereoPanner ? audioCtx.createStereoPanner() : null;
        if (panLeft) {
            panLeft.pan.value = -0.65;
            oscLeft.connect(panLeft);
            panLeft.connect(droneFilter);
        } else {
            oscLeft.connect(droneFilter);
        }

        const oscRight = audioCtx.createOscillator();
        oscRight.type = 'sine';
        oscRight.frequency.value = 206;
        const panRight = audioCtx.createStereoPanner ? audioCtx.createStereoPanner() : null;
        if (panRight) {
            panRight.pan.value = 0.65;
            oscRight.connect(panRight);
            panRight.connect(droneFilter);
        } else {
            oscRight.connect(droneFilter);
        }

        const oscSub = audioCtx.createOscillator();
        oscSub.type = 'triangle';
        oscSub.frequency.value = 98;
        const subGain = audioCtx.createGain();
        subGain.gain.value = 0.35;
        oscSub.connect(subGain);
        subGain.connect(droneFilter);

        oscLeft.start();
        oscRight.start();
        oscSub.start();

        channels = {
            rain: rainGain,
            brown: brownGain,
            drone: droneGain
        };

        return true;
    };

    const isAnySoundPlaying = () =>
        volumes.master > 0.01 && (volumes.rain > 0.01 || volumes.brown > 0.01 || volumes.drone > 0.01);

    const syncUiAndGains = () => {
        if (audioCtx && channels) {
            const now = audioCtx.currentTime;
            masterGain.gain.setTargetAtTime(volumes.master, now, 0.06);
            channels.rain.gain.setTargetAtTime(volumes.rain * 0.85, now, 0.08);
            channels.brown.gain.setTargetAtTime(volumes.brown * 0.65, now, 0.08);
            channels.drone.gain.setTargetAtTime(volumes.drone * 0.22, now, 0.08);
        }

        const playing = isAnySoundPlaying();
        const eqGroup = qs('#ws-ambient-eq');
        const soundTriggerBtn = qs('#ws-ambient-mixer-btn');

        if (eqGroup) {
            eqGroup.classList.toggle('is-playing', playing);
        }
        if (soundTriggerBtn) {
            soundTriggerBtn.classList.toggle('border-indigo-400', playing);
            soundTriggerBtn.classList.toggle('bg-indigo-50', playing);
            soundTriggerBtn.classList.toggle('dark:bg-indigo-500/20', playing);
        }

        ['rain', 'brown', 'drone', 'master'].forEach((key) => {
            const slider = qs(`#ambient-slider-${key}`);
            const pctLabel = qs(`#ambient-pct-${key}`);
            const pct = Math.round(volumes[key] * 100);
            if (slider && Number(slider.value) !== pct) {
                slider.value = String(pct);
            }
            if (pctLabel) {
                pctLabel.textContent = `${pct}%`;
            }
        });
    };

    const applyPreset = (presetName) => {
        if (!ensureAudioEngine()) return;
        if (presetName === 'focus-mix') {
            volumes.rain = 0.48;
            volumes.brown = 0.36;
            volumes.drone = 0.28;
        } else if (presetName === 'deep-rain') {
            volumes.rain = 0.72;
            volumes.brown = 0.2;
            volumes.drone = 0;
        } else if (presetName === 'mute-all') {
            volumes.rain = 0;
            volumes.brown = 0;
            volumes.drone = 0;
        }
        syncUiAndGains();
    };

    const setZenMode = (active) => {
        isZenActive = Boolean(active);
        document.body.classList.toggle('zen-focus-active', isZenActive);

        const zenBtn = qs('#ws-zen-mode-btn');
        if (zenBtn) {
            zenBtn.innerHTML = isZenActive
                ? '<span class="text-emerald-600 dark:text-emerald-300 font-bold">🧘 Thoát Zen</span>'
                : '<span>🧘 Zen</span>';
            zenBtn.classList.toggle('border-emerald-400', isZenActive);
            zenBtn.classList.toggle('bg-emerald-50', isZenActive);
            zenBtn.classList.toggle('dark:bg-emerald-500/20', isZenActive);
        }

        if (isZenActive) {
            // Tự động bật nhẹ âm thanh tập trung nếu người dùng chưa mở kênh nào
            if (!isAnySoundPlaying()) {
                applyPreset('focus-mix');
            }
            toast.success('🧘 Đã bật Chế độ Tập trung Zen Mode (Nhấn Esc bất kỳ lúc nào để thoát).', 2600);
        }
    };

    const init = () => {
        const mixerBtn = qs('#ws-ambient-mixer-btn');
        const popoverEl = qs('#ws-ambient-popover');
        const zenBtn = qs('#ws-zen-mode-btn');

        mixerBtn?.addEventListener('click', (e) => {
            e.stopPropagation();
            isMixerOpen = !isMixerOpen;
            popoverEl?.classList.toggle('hidden', !isMixerOpen);
        });

        popoverEl?.addEventListener('click', (e) => {
            e.stopPropagation();
            const presetBtn = e.target.closest('[data-ambient-preset]');
            if (presetBtn) {
                applyPreset(presetBtn.dataset.ambientPreset);
            }
        });

        ['rain', 'brown', 'drone', 'master'].forEach((key) => {
            const slider = qs(`#ambient-slider-${key}`);
            slider?.addEventListener('input', (e) => {
                if (!ensureAudioEngine()) return;
                volumes[key] = Math.max(0, Math.min(1, Number(e.target.value) / 100));
                syncUiAndGains();
            });
        });

        document.addEventListener('click', () => {
            if (isMixerOpen) {
                isMixerOpen = false;
                popoverEl?.classList.add('hidden');
            }
        });

        zenBtn?.addEventListener('click', () => setZenMode(!isZenActive));

        document.addEventListener('keydown', (e) => {
            if (e.key === 'Escape' && isZenActive) {
                setZenMode(false);
            }
        });

        syncUiAndGains();
    };

    return {
        init,
        toggleZenMode: () => setZenMode(!isZenActive),
        applyFocusMix: () => applyPreset('focus-mix')
    };
}
