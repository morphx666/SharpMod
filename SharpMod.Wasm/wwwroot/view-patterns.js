// Patterns view: channel headers with VU meters + a horizontally-scrolling
// stack of channel columns showing the previous / current / next patterns.
//
// The console renderer keeps the play-head centered vertically and scrolls
// rows past it. We do the same here by translating the columns vertically so
// the currently-playing row sits on `--play-line`.

const ROWS_PER_PATTERN = 64;
// Pattern row height is defined in styles.css as --row-h (single source of truth).
const ROW_HEIGHT_PX = parseFloat(
    getComputedStyle(document.documentElement).getPropertyValue('--row-h')
) || 18;
const VU_MAX = 256;
const VU_DECAY_PER_FRAME = 16;

const BLOCK_HEIGHT_PX = ROWS_PER_PATTERN * ROW_HEIGHT_PX;

let channelsRoot = null;
let patternsRoot = null;
let liveSm = null;            // refreshed each renderPatternsView() call for click handlers
let channelCells = [];        // per channel: { wrap, name, canvas, ctx, levelL, levelR }
let columnEls = [];           // per channel: { col, blocks: HTMLElement[3], rowsByBlock: HTMLElement[3][64] }
let lastChannelCount = -1;
let lastRow = -1, lastPattern = -1, lastNextPattern = -1, lastDisplayPat = -1;
let lastToken = -1;
let lastMuted = [];
let cachedPatternRows = new Map(); // patternIndex -> string[64] (raw 14*nch char rows)
// Maps physical block index → logical position (0=prev, 1=cur, 2=next). The
// rotation fast path mutates this array in place; full paints reset it.
let blockLogicalPos = [0, 1, 2];
let lastActiveBlockIdx = -1, lastActiveRowInBlock = -1;

export function initPatternsView(opts) {
    channelsRoot = opts.root;
    patternsRoot = opts.patterns;
}

export function resetPatternsView() {
    channelCells = [];
    columnEls = [];
    lastChannelCount = -1;
    lastRow = lastPattern = lastNextPattern = lastDisplayPat = -1;
    lastActiveBlockIdx = lastActiveRowInBlock = -1;
    blockLogicalPos = [0, 1, 2];
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

    const cur = sm.GetCurrentPattern();
    const display = sm.GetDisplayPattern();
    const next = sm.GetNextPattern();
    // Engine "loaded but never played" sentinel: Start() leaves Row=0x3F and
    // NextPattern==CurrentPattern. Pretend row 0 of the current pattern is on
    // the play-line so what the user sees matches what Play will start with.
    const fresh = cur === next;
    const row = fresh ? 0 : sm.GetRow();
    const prevDisplay = cur > 0 ? sm.GetOrderAt(cur - 1) : -1;
    const effectiveNext = fresh ? cur + 1 : next;
    const nextDisplay = effectiveNext !== 0xFF ? sm.GetOrderAt(effectiveNext) : -1;

    const patternsChanged = display !== lastDisplayPat || prevDisplay !== lastPattern || nextDisplay !== lastNextPattern;
    if (patternsChanged) {
        // Forward-by-one is the common playback case (newPrev was old cur,
        // newCur was old next); we can keep two blocks untouched and only
        // repaint the obsolete one with the new "next" content.
        const canRotate = lastDisplayPat >= 0
            && prevDisplay === lastDisplayPat
            && display === lastNextPattern;
        if (canRotate) rotateForwardAndPaint(sm, nch, nextDisplay);
        else paintAllBlocks(sm, nch, [prevDisplay, display, nextDisplay]);
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
        // Three absolute-positioned blocks (prev/cur/next). The rotation fast
        // path reassigns each block's top + dim class to shift logical roles
        // without moving DOM nodes or re-painting their cell spans.
        const blocks = new Array(3);
        const rowsByBlock = new Array(3);
        for (let p = 0; p < 3; p++) {
            const blk = document.createElement('div');
            blk.className = p === 1 ? 'pat-block' : 'pat-block dim';
            blk.style.top = (p * BLOCK_HEIGHT_PX) + 'px';
            const rows = new Array(ROWS_PER_PATTERN);
            for (let r = 0; r < ROWS_PER_PATTERN; r++) {
                const rowEl = document.createElement('div');
                rowEl.className = 'pat-row';
                rowEl._spans = buildRowSpans(rowEl);
                blk.appendChild(rowEl);
                rows[r] = rowEl;
            }
            col.appendChild(blk);
            blocks[p] = blk;
            rowsByBlock[p] = rows;
        }
        patternsRoot.appendChild(col);
        columnEls[c] = { col, blocks, rowsByBlock };
    }
    blockLogicalPos = [0, 1, 2];
    lastActiveBlockIdx = lastActiveRowInBlock = -1;
    lastChannelCount = nch;
    lastMuted = new Array(nch).fill(0);
}

function buildRowSpans(r) {
    const mkSpan = (cls) => { const s = document.createElement('span'); s.className = cls; s.textContent = '...'; return s; };
    const nt = mkSpan('ph');
    const ins = mkSpan('ph');
    const vo = mkSpan('ph');
    const fx = mkSpan('ph');
    ins.textContent = '..';
    r.appendChild(document.createTextNode(' '));
    r.appendChild(nt);
    r.appendChild(document.createTextNode(' '));
    r.appendChild(ins);
    r.appendChild(document.createTextNode(' '));
    r.appendChild(vo);
    r.appendChild(document.createTextNode(' '));
    r.appendChild(fx);
    r.appendChild(document.createTextNode(' '));
    return [nt, ins, vo, fx];
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

function paintAllBlocks(sm, nch, patternIndices) {
    // Reset to canonical layout (block 0 = prev, 1 = cur, 2 = next) and paint
    // every block. Used on first render and after non-rotation pattern jumps
    // (position breaks, Bxx position-jump, manual scrubbing).
    blockLogicalPos[0] = 0; blockLogicalPos[1] = 1; blockLogicalPos[2] = 2;
    applyBlockPositions(nch);
    for (let p = 0; p < 3; p++) paintBlock(sm, nch, p, patternIndices[p]);
}

function rotateForwardAndPaint(sm, nch, newNextIdx) {
    // The physical block currently at logicalPos 0 (the obsolete "prev")
    // becomes the new "next". All other blocks shift one role down: cur→prev,
    // next→cur. We only need to repaint that one obsolete block.
    const obsBlock = blockLogicalPos.indexOf(0);
    for (let p = 0; p < 3; p++) blockLogicalPos[p] = (blockLogicalPos[p] + 2) % 3;
    applyBlockPositions(nch);
    paintBlock(sm, nch, obsBlock, newNextIdx);
}

function applyBlockPositions(nch) {
    // Push each physical block to the pixel slot matching its new logical role
    // and toggle .dim so cur (logicalPos == 1) renders at full intensity.
    for (let c = 0; c < nch; c++) {
        const blocks = columnEls[c].blocks;
        for (let p = 0; p < 3; p++) {
            const logPos = blockLogicalPos[p];
            const top = (logPos * BLOCK_HEIGHT_PX) + 'px';
            if (blocks[p].style.top !== top) blocks[p].style.top = top;
            blocks[p].classList.toggle('dim', logPos !== 1);
        }
    }
}

function paintBlock(sm, nch, blockIdx, patternIdx) {
    const data = getPatternRows(sm, patternIdx);
    for (let c = 0; c < nch; c++) {
        const rows = columnEls[c].rowsByBlock[blockIdx];
        const segOffset = c * 14;
        for (let r = 0; r < ROWS_PER_PATTERN; r++) {
            paintRowCell(rows[r]._spans, data ? data[r] : null, segOffset);
        }
    }
}

// Each 14-char command segment is: NNN II VVV FFF (with spaces at 3,6,10).
function paintRowCell(spans, rowText, off) {
    if (!rowText || rowText.length < off + 14) {
        setSpan(spans[0], '...', 'nt');
        setSpan(spans[1], '..',  'in');
        setSpan(spans[2], '...', 'vo');
        setSpan(spans[3], '...', 'fx');
        return;
    }
    setSpan(spans[0], rowText.substr(off,      3), 'nt');
    setSpan(spans[1], rowText.substr(off + 4,  2), 'in');
    setSpan(spans[2], rowText.substr(off + 7,  3), 'vo');
    setSpan(spans[3], rowText.substr(off + 11, 3), 'fx');
}

function setSpan(span, text, cls) {
    const target = isPh(text) ? 'ph' : cls;
    if (span.textContent !== text) span.textContent = text;
    if (span.className !== target) span.className = target;
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

function isPh(s) {
    for (let i = 0; i < s.length; i++) { const c = s[i]; if (c !== '.' && c !== ' ') return false; }
    return true;
}

function scrollAndHighlight(row) {
    // Translate each column vertically so the active row's center lands on the
    // play-line band at the viewport's vertical midpoint. The "cur" pattern
    // always occupies logicalPos 1 → fixed pixel slot [BLOCK_HEIGHT_PX,
    // 2·BLOCK_HEIGHT_PX], regardless of which physical block currently holds
    // it, so the translate math doesn't change after a rotation.
    if (columnEls.length === 0) return;
    const containerH = patternsRoot.parentElement.getBoundingClientRect().height;
    const playLine = containerH / 2;
    const activePixelY = BLOCK_HEIGHT_PX + row * ROW_HEIGHT_PX;
    const offset = Math.round(playLine - activePixelY - ROW_HEIGHT_PX / 2);
    const curBlock = blockLogicalPos.indexOf(1);
    const prevBlock = lastActiveBlockIdx, prevRow = lastActiveRowInBlock;
    const moved = prevBlock !== curBlock || prevRow !== row;
    for (let c = 0; c < columnEls.length; c++) {
        const { col, rowsByBlock } = columnEls[c];
        col.style.transform = `translateY(${offset}px)`;
        if (moved && prevBlock >= 0) rowsByBlock[prevBlock][prevRow]?.classList.remove('active');
        rowsByBlock[curBlock][row]?.classList.add('active');
    }
    lastActiveBlockIdx = curBlock;
    lastActiveRowInBlock = row;
}
