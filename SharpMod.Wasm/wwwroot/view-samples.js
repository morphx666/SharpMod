// Samples view: one row per instrument with metadata columns plus a waveform
// canvas. The waveform envelope is cached per (instrument, pixel-width) and only
// re-rendered when the loaded module changes or the canvas is resized. The
// per-frame work is just clearing the cursor overlay and redrawing the channel
// play-heads as thin yellow lines, mirroring the console renderer.

const NAME_WIDTH = 28;

let summaryEls = null;
let listRoot = null;
let rows = [];       // per-instrument: { row, nameEl, overlayEl, canvas, ctx, cachedW, envelope }
let lastToken = -1;
let lastCount = -1;
let lengthShown = -1, bpmShown = -1;

export function initSamplesView(opts) {
    summaryEls = opts.summary;
    listRoot = opts.list;
}

export function resetSamplesView() {
    rows = [];
    lastCount = -1;
    lengthShown = bpmShown = -1;
    listRoot.innerHTML = '';
}

export function renderSamplesView(sm, token) {
    if (token !== lastToken) { resetSamplesView(); lastToken = token; }
    const total = sm.GetInstrumentCount();
    const count = Math.max(0, total - 1); // entry 0 is the engine's "no instrument" slot
    if (count <= 0) return;
    if (count !== lastCount) buildRows(sm, count);

    updateSummary(sm);

    for (let i = 0; i < count; i++) {
        const r = rows[i];
        ensureWaveform(sm, i + 1, r);
        drawCursors(sm, i + 1, r);
        updateNameProgress(sm, i + 1, r);
    }
}

function buildRows(sm, count) {
    listRoot.innerHTML = '';
    rows = new Array(count);
    for (let i = 0; i < count; i++) {
        const idx = i + 1;
        const meta = sm.GetInstrumentMeta(idx);
        const name = sanitize(sm.GetInstrumentName(idx) || '');
        const empty = !meta || meta.length === 0 || meta[0] === 0;

        const row = document.createElement('div');
        row.className = 'samples-row' + (empty ? ' empty' : '');

        const colI = span('col-i', String(idx).padStart(2, '0'));
        const colN = document.createElement('span');
        colN.className = 'col-n';
        const overlay = document.createElement('span');
        overlay.className = 'progress-overlay';
        overlay.style.width = '0%';
        const nameText = document.createElement('span');
        nameText.className = 'name-text';
        nameText.textContent = name || ' ';
        colN.appendChild(overlay);
        colN.appendChild(nameText);

        const length = meta ? meta[0] : 0;
        const vol = meta ? meta[1] : 0;
        const fmt = empty ? '    ' : `${meta[2] ? '16' : ' 8'}/${meta[3] ? 'S' : 'M'}`;
        const ls = meta ? meta[4] : 0;
        const le = meta ? meta[5] : 0;

        const colL = span('col-l',  String(length));
        const colV = span('col-v',  String(vol));
        const colF = span('col-f',  fmt);
        const colLs = span('col-ls', String(ls));
        const colLe = span('col-le', String(le));

        const colW = document.createElement('span');
        colW.className = 'col-w';
        const canvas = document.createElement('canvas');
        colW.appendChild(canvas);

        row.append(colI, colN, colL, colV, colF, colLs, colLe, colW);
        listRoot.appendChild(row);

        rows[i] = {
            row, nameEl: nameText, overlayEl: overlay,
            canvas, ctx: canvas.getContext('2d'),
            cachedW: 0, envelope: null, empty,
            length, vol, ls, le, fmt, name
        };
    }
    lastCount = count;
}

function span(cls, text) {
    const el = document.createElement('span');
    el.className = cls;
    el.textContent = text;
    return el;
}

function updateSummary(sm) {
    const len = sm.GetLengthSeconds();
    if (len !== lengthShown) {
        const h = Math.floor(len / 3600), m = Math.floor((len % 3600) / 60), s = len % 60;
        summaryEls.length.textContent = h > 0
            ? `${h}:${String(m).padStart(2, '0')}:${String(s).padStart(2, '0')}`
            : `${m}:${String(s).padStart(2, '0')}`;
        lengthShown = len;
    }
    const bpm = sm.GetAverageTempo();
    if (bpm !== bpmShown) { summaryEls.bpm.textContent = String(bpm); bpmShown = bpm; }
}

function ensureWaveform(sm, idx, r) {
    const dpr = window.devicePixelRatio || 1;
    const wCss = r.canvas.clientWidth;
    const hCss = r.canvas.clientHeight;
    if (wCss <= 0 || hCss <= 0) return;
    const w = Math.floor(wCss * dpr);
    const h = Math.floor(hCss * dpr);
    if (r.canvas.width !== w || r.canvas.height !== h) {
        r.canvas.width = w; r.canvas.height = h;
        r.cachedW = 0;
    }
    if (r.empty) { r.ctx.clearRect(0, 0, w, h); return; }
    if (r.cachedW === w && r.envelope) return;
    const env = sm.GetWaveformEnvelope(idx, w);
    r.envelope = env;
    r.cachedW = w;
    const ctx = r.ctx;
    ctx.clearRect(0, 0, w, h);
    ctx.fillStyle = '#56b6c2'; // cyan
    if (env.length === 0) return;
    for (let x = 0; x < w; x++) {
        const vmin = env[x * 2], vmax = env[x * 2 + 1];
        const y0 = Math.round((1 - vmax) * 0.5 * (h - 1));
        const y1 = Math.round((1 - vmin) * 0.5 * (h - 1));
        ctx.fillRect(x, Math.min(y0, y1), 1, Math.max(1, Math.abs(y1 - y0) + 1));
    }
    // Cache the rendered waveform as an ImageBitmap-equivalent: stash the
    // ImageData so each frame's cursor overlay can blit it back cheaply.
    r.bg = ctx.getImageData(0, 0, w, h);
}

function drawCursors(sm, idx, r) {
    if (r.empty || !r.bg) return;
    const ctx = r.ctx;
    const w = r.canvas.width, h = r.canvas.height;
    ctx.putImageData(r.bg, 0, 0);
    const cursors = sm.GetInstrumentCursors(idx);
    if (!cursors || cursors.length === 0) return;
    ctx.fillStyle = '#e5c07b'; // yellow
    for (let i = 0; i < cursors.length; i++) {
        const x = Math.max(0, Math.min(w - 1, Math.floor(cursors[i] * w)));
        ctx.fillRect(x, 0, Math.max(1, Math.round(w / 200)), h);
    }
}

function updateNameProgress(sm, idx, r) {
    if (r.empty) return;
    const cursors = sm.GetInstrumentCursors(idx);
    let maxR = 0;
    for (let i = 0; i < cursors.length; i++) if (cursors[i] > maxR) maxR = cursors[i];
    r.overlayEl.style.width = `${(maxR * 100).toFixed(2)}%`;
}

function sanitize(s) {
    let out = '';
    for (let i = 0; i < s.length; i++) {
        const c = s.charCodeAt(i);
        out += (c < 0x20 || c === 0x7F) ? ' ' : s[i];
    }
    return out.slice(0, NAME_WIDTH);
}
