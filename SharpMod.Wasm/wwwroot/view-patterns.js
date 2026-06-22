// Patterns view: channel headers with VU meters + a horizontally-scrolling
// stack of channel columns showing the previous / current / next patterns.
//
// The console renderer keeps the play-head centered vertically and scrolls
// rows past it. We do the same here by translating the columns vertically so
// the currently-playing row sits on `--play-line`.

const ROWS_PER_PATTERN = 64;
const ROW_HEIGHT_PX = 18;     // must match .pat-row height in styles.css
const VU_MAX = 256;
const VU_DECAY_PER_FRAME = 16;

let channelsRoot = null;
let patternsRoot = null;
let liveSm = null;            // refreshed each renderPatternsView() call for click handlers
let channelCells = [];        // per channel: { wrap, name, canvas, ctx, levelL, levelR }
let columnEls = [];           // per channel: { col, rows: HTMLElement[64*3] }
let lastChannelCount = -1;
let lastRow = -1, lastPattern = -1, lastNextPattern = -1, lastDisplayPat = -1;
let lastToken = -1;
let lastMuted = [];
let cachedPatternRows = new Map(); // patternIndex -> string[64] (raw 14*nch char rows)

export function initPatternsView(opts) {
    channelsRoot = opts.root;
    patternsRoot = opts.patterns;
}

export function resetPatternsView() {
    channelCells = [];
    columnEls = [];
    lastChannelCount = -1;
    lastRow = lastPattern = lastNextPattern = lastDisplayPat = -1;
    lastMuted = [];
    cachedPatternRows.clear();
    channelsRoot.innerHTML = '';
    patternsRoot.innerHTML = '';
}

export function renderPatternsView(sm, token) {
    liveSm = sm;
    if (token !== lastToken) { resetPatternsView(); lastToken = token; }
    const nch = sm.GetActiveChannels();
    if (nch <= 0) return;
    if (nch !== lastChannelCount) buildLayout(nch);

    const states = sm.GetChannelStates();
    paintVuMeters(states, nch);

    const muteChanged = updateMuteHeaders(states, nch);

    const row = sm.GetRow();
    const cur = sm.GetCurrentPattern();
    const display = sm.GetDisplayPattern();
    const next = sm.GetNextPattern();
    const prevDisplay = cur > 0 ? sm.GetOrderAt(cur - 1) : -1;
    const nextDisplay = next !== 0xFF ? sm.GetOrderAt(next) : -1;

    const patternsChanged = display !== lastDisplayPat || prevDisplay !== lastPattern || nextDisplay !== lastNextPattern;
    if (patternsChanged || muteChanged) {
        repaintPatternBlocks(sm, nch, [prevDisplay, display, nextDisplay]);
        lastDisplayPat = display;
        lastPattern = prevDisplay;
        lastNextPattern = nextDisplay;
    }
    if (row !== lastRow || patternsChanged) {
        scrollAndHighlight(row);
        lastRow = row;
    }
}

function buildLayout(nch) {
    channelsRoot.innerHTML = '';
    patternsRoot.innerHTML = '';
    channelCells = new Array(nch);
    columnEls = new Array(nch);
    for (let c = 0; c < nch; c++) {
        const wrap = document.createElement('div');
        wrap.className = 'channel';
        wrap.title = `Click to toggle mute (channel ${c + 1})`;
        const name = document.createElement('div');
        name.className = 'ch-name';
        name.textContent = `Channel ${c + 1}`;
        const canvas = document.createElement('canvas');
        canvas.width = 130; canvas.height = 10;
        wrap.appendChild(name); wrap.appendChild(canvas);
        wrap.addEventListener('click', () => { liveSm?.ToggleChannelMute(c); });
        channelsRoot.appendChild(wrap);
        channelCells[c] = { wrap, name, canvas, ctx: canvas.getContext('2d'), levelL: 0, levelR: 0 };

        const col = document.createElement('div');
        col.className = 'pat-col';
        col.style.transform = 'translateY(0px)';
        // Pre-create 3 patterns worth of rows for fast vertical scroll.
        const rows = [];
        for (let i = 0; i < ROWS_PER_PATTERN * 3; i++) {
            const r = document.createElement('div');
            r.className = 'pat-row dim';
            col.appendChild(r);
            rows.push(r);
        }
        patternsRoot.appendChild(col);
        columnEls[c] = { col, rows };
    }
    lastChannelCount = nch;
    lastMuted = new Array(nch).fill(0);
}

function paintVuMeters(states, nch) {
    for (let c = 0; c < nch; c++) {
        const k = c * 6;
        const muted = states[k] === 1;
        const cv = states[k + 2];
        const pan = Math.max(0, Math.min(VU_MAX, states[k + 3]));
        const stereo = states[k + 4] === 1;
        const active = states[k + 5] === 1;
        let tL = 0, tR = 0;
        if (active && !muted) {
            if (stereo) { tL = cv; tR = cv; }
            else { tL = cv * (VU_MAX - pan) / VU_MAX; tR = cv * pan / VU_MAX; }
        }
        const cell = channelCells[c];
        cell.levelL = Math.max(tL, cell.levelL - VU_DECAY_PER_FRAME);
        cell.levelR = Math.max(tR, cell.levelR - VU_DECAY_PER_FRAME);
        drawVu(cell);
    }
}

function drawVu(cell) {
    const ctx = cell.ctx;
    const w = cell.canvas.width, h = cell.canvas.height;
    ctx.clearRect(0, 0, w, h);
    const half = w / 2;
    const fillL = Math.min(1, cell.levelL / VU_MAX) * half;
    const fillR = Math.min(1, cell.levelR / VU_MAX) * half;
    // Same color regions as the console (outward red → yellow → green from center).
    // Render the right-half gradient with x going from center outward, then mirror for left.
    const drawHalf = (x0, dir, fill) => {
        for (let i = 0; i < fill; i++) {
            const t = i / half;
            const color = t < 0.30 ? '#98c379' : t < 0.75 ? '#e5c07b' : '#e06c75';
            ctx.fillStyle = color;
            ctx.fillRect(x0 + dir * i, h * 0.25, 1, h * 0.5);
        }
    };
    drawHalf(half, 1, fillR);
    drawHalf(half - 1, -1, fillL);
}

function updateMuteHeaders(states, nch) {
    let changed = false;
    for (let c = 0; c < nch; c++) {
        const m = states[c * 6];
        if (m !== lastMuted[c]) {
            lastMuted[c] = m;
            const cell = channelCells[c];
            cell.wrap.classList.toggle('muted', m === 1);
            cell.name.textContent = m === 1 ? `Channel ${c + 1} [M]` : `Channel ${c + 1}`;
            changed = true;
        }
    }
    return changed;
}

function repaintPatternBlocks(sm, nch, patternIndices) {
    for (let c = 0; c < nch; c++) {
        const rows = columnEls[c].rows;
        for (let p = 0; p < 3; p++) {
            const pi = patternIndices[p];
            const data = getPatternRows(sm, pi);
            const dim = p !== 1; // only middle block (current pattern) is active
            for (let r = 0; r < ROWS_PER_PATTERN; r++) {
                const el = rows[p * ROWS_PER_PATTERN + r];
                el.className = dim ? 'pat-row dim' : 'pat-row';
                el.innerHTML = data ? cellHtml(data[r].substr(c * 14, 14), dim) : placeholderHtml(dim);
            }
        }
    }
}

function getPatternRows(sm, patternIndex) {
    if (patternIndex < 0) return null;
    let cached = cachedPatternRows.get(patternIndex);
    if (cached) return cached;
    const text = sm.GetPatternData(patternIndex);
    cached = text.split('\n');
    cachedPatternRows.set(patternIndex, cached);
    return cached;
}

function placeholderHtml(dim) {
    return `<span class="ph"> ... .. ... ... </span>`;
}

// Each 14-char command segment is: NNN II VVV FFF (with spaces at 3,6,10).
function cellHtml(seg, dim) {
    if (!seg || seg.length < 14) return placeholderHtml(dim);
    const note = seg.substr(0, 3);
    const inst = seg.substr(4, 2);
    const vol  = seg.substr(7, 3);
    const efx  = seg.substr(11, 3);
    const cls = (s, c) => isPh(s) ? `<span class="ph">${s}</span>` : `<span class="${c}">${s}</span>`;
    return ` ${cls(note, 'nt')} ${cls(inst, 'in')} ${cls(vol, 'vo')} ${cls(efx, 'fx')} `;
}

function isPh(s) {
    for (let i = 0; i < s.length; i++) { const c = s[i]; if (c !== '.' && c !== ' ') return false; }
    return true;
}

function scrollAndHighlight(row) {
    // Translate each column vertically so the active row's center lands exactly on
    // the play-line band at the viewport's vertical midpoint. Row heights are pinned
    // by CSS to ROW_HEIGHT_PX, so we don't measure (avoids sub-pixel drift).
    if (columnEls.length === 0) return;
    const containerH = patternsRoot.parentElement.getBoundingClientRect().height;
    const playLine = containerH / 2;
    const activeIdx = ROWS_PER_PATTERN + row;
    const offset = Math.round(playLine - activeIdx * ROW_HEIGHT_PX - ROW_HEIGHT_PX / 2);
    const lastIdx = lastRow >= 0 ? ROWS_PER_PATTERN + lastRow : -1;
    for (let c = 0; c < columnEls.length; c++) {
        const { col, rows } = columnEls[c];
        col.style.transform = `translateY(${offset}px)`;
        if (lastIdx >= 0 && lastIdx !== activeIdx) rows[lastIdx]?.classList.remove('active');
        rows[activeIdx]?.classList.add('active');
    }
}
