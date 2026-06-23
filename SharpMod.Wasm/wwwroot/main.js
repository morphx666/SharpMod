import { dotnet } from './_framework/dotnet.js';
import { initPatternsView, renderPatternsView, resetPatternsView } from './view-patterns.js';
import { initSamplesView, renderSamplesView, resetSamplesView } from './view-samples.js';

const $ = (id) => document.getElementById(id);
const status = (msg) => { $('status').textContent = msg; };

status('Loading .NET runtime…');

const { getAssemblyExports, getConfig, runMain } = await dotnet.create();
const config = getConfig();
const exports = await getAssemblyExports(config.mainAssemblyName);
const sm = exports.SharpModInterop;

runMain();

let audioCtx = null;
let workletNode = null;
let pumpTimer = 0;
let pumping = false;
let activeBytesPerFrame = 0;
let activeChannels = 1;
let activeIs16Bit = true;
let queuedFrames = 0;
let queueStartTime = 0;
const TARGET_LEAD_SEC = 0.25;

let view = 'patterns';
let rafId = 0;
let loadedToken = 0; // bumped on every successful Load() so views can drop caches

initPatternsView({ root: $('channels-row'), patterns: $('patterns') });
initSamplesView({ summary: { length: $('s-length'), bpm: $('s-bpm'), filename: $('s-filename') }, list: $('samples-list') });

$('file').disabled = false;
status('Ready. Pick a module to load.');

$('file').addEventListener('change', async (e) => {
    const f = e.target.files && e.target.files[0];
    if (!f) return;
    const buf = new Uint8Array(await f.arrayBuffer());

    stopPlayback();
    const err = sm.Load(buf, 44100, true, true, false);
    if (err) { status('Load failed: ' + err); return; }
    loadedToken++;
    resetPatternsView();
    resetSamplesView();
    $('playPause').disabled = false;
    $('pos').disabled = false;
    $('pos').max = String(sm.GetPositionCount());
    $('s-filename').textContent = f.name;
    status(`Loaded ${f.name} (${buf.length.toLocaleString()} bytes)`);
    ensureRenderLoop();
});

$('playPause').addEventListener('click', () => togglePlayback());
$('pos').addEventListener('input', (e) => sm.SetPosition(parseInt(e.target.value, 10)));

document.querySelectorAll('.tab').forEach(btn => {
    btn.addEventListener('click', () => setView(btn.dataset.view));
});

window.addEventListener('keydown', (e) => {
    if (e.target instanceof HTMLInputElement || e.target instanceof HTMLSelectElement) return;
    if (e.code === 'Space') {
        e.preventDefault();
        togglePlayback();
    } else if (e.code === 'Tab') {
        e.preventDefault();
        setView(view === 'patterns' ? 'samples' : 'patterns');
    } else if (sm.IsLoaded() && /^Digit[1-9]$/.test(e.code)) {
        const n = parseInt(e.code.slice(5), 10) - 1;
        const bank = e.ctrlKey ? 2 : e.shiftKey ? 1 : 0;
        sm.ToggleChannelMute(bank * 9 + n);
        resetPatternsView();
    }
});

function setView(v) {
    view = v;
    $('view-patterns').classList.toggle('hidden', v !== 'patterns');
    $('view-samples').classList.toggle('hidden', v !== 'samples');
    document.querySelectorAll('.tab').forEach(b => b.classList.toggle('active', b.dataset.view === v));
}

function togglePlayback() {
    if (audioCtx) { stopPlayback(); status('Paused'); }
    else if (sm.IsLoaded()) startPlayback();
}

async function startPlayback() {
    stopPlayback();
    if (!sm.IsLoaded()) return;
    setPlayPauseUi(true);
    const rate = sm.GetSampleRate();
    activeIs16Bit = sm.GetIs16Bit();
    activeChannels = sm.GetIsStereo() ? 2 : 1;
    activeBytesPerFrame = activeChannels * (activeIs16Bit ? 2 : 1);

    audioCtx = new AudioContext({ sampleRate: rate });
    await audioCtx.audioWorklet.addModule('mod-processor.js');
    workletNode = new AudioWorkletNode(audioCtx, 'mod-processor', {
        numberOfInputs: 0,
        numberOfOutputs: 1,
        outputChannelCount: [activeChannels],
        processorOptions: { channels: activeChannels, capacityFrames: Math.round(rate * 0.75) }
    });
    workletNode.connect(audioCtx.destination);

    queuedFrames = 0;
    queueStartTime = audioCtx.currentTime;
    pumpChunk(Math.round(rate * TARGET_LEAD_SEC));
    pumpTimer = setInterval(pump, 20);
    status('Playing');
    ensureRenderLoop();
}

function stopPlayback() {
    if (pumpTimer) { clearInterval(pumpTimer); pumpTimer = 0; }
    if (workletNode) { try { workletNode.port.postMessage({ type: 'stop' }); workletNode.disconnect(); } catch {} workletNode = null; }
    if (audioCtx) { audioCtx.close().catch(() => {}); audioCtx = null; }
    setPlayPauseUi(false);
}

function setPlayPauseUi(playing) {
    const btn = $('playPause');
    btn.querySelector('.icon').className = `icon fa-solid ${playing ? 'fa-pause' : 'fa-play'}`;
    btn.querySelector('.label').textContent = playing ? 'Pause' : 'Play';
}

function pump() {
    if (pumping || !workletNode || !audioCtx) return;
    const elapsedFrames = (audioCtx.currentTime - queueStartTime) * audioCtx.sampleRate;
    const pendingFrames = queuedFrames - elapsedFrames;
    const targetFrames = audioCtx.sampleRate * TARGET_LEAD_SEC;
    if (pendingFrames >= targetFrames) return;
    pumping = true;
    try { pumpChunk(Math.ceil(targetFrames - pendingFrames)); }
    finally { pumping = false; }
}

function pumpChunk(frames) {
    if (!workletNode || frames <= 0) return;
    const bytes = frames * activeBytesPerFrame;
    const raw = sm.Read(bytes);
    if (!raw || raw.length === 0) return;
    const float = convertToFloat32(raw, activeIs16Bit);
    queuedFrames += float.length / activeChannels;
    workletNode.port.postMessage({ type: 'samples', data: float }, [float.buffer]);
}

function convertToFloat32(bytes, is16Bit) {
    if (is16Bit) {
        const dv = new DataView(bytes.buffer, bytes.byteOffset, bytes.byteLength);
        const n = bytes.byteLength >> 1;
        const out = new Float32Array(n);
        for (let i = 0; i < n; i++) out[i] = dv.getInt16(i * 2, true) / 32768;
        return out;
    }
    const n = bytes.byteLength;
    const out = new Float32Array(n);
    for (let i = 0; i < n; i++) out[i] = (bytes[i] - 128) / 128;
    return out;
}

function ensureRenderLoop() {
    if (rafId) return;
    const tick = () => {
        rafId = requestAnimationFrame(tick);
        if (!sm.IsLoaded()) return;
        const pos = sm.GetPosition();
        const total = sm.GetPositionCount();
        $('m-title').textContent = sm.GetTitle() || '(untitled)';
        $('m-type').textContent = sm.GetTypeName();
        $('m-channels').textContent = sm.GetActiveChannels();
        $('m-pattern').textContent = `${sm.GetCurrentPattern()} / ${total > 0 ? Math.max(0, Math.floor(total / 64) - 1) : 0}`;
        $('m-row').textContent = sm.GetRow();
        $('m-tempo').textContent = `${sm.GetMusicTempo()} / ${sm.GetMusicSpeed()}`;
        $('pos').value = String(pos);
        if (view === 'patterns') renderPatternsView(sm, loadedToken);
        else renderSamplesView(sm, loadedToken);
    };
    rafId = requestAnimationFrame(tick);
}

