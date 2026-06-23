// AudioWorkletProcessor that consumes interleaved Float32 PCM posted from the main
// thread (where the .NET SharpMod runtime lives) and feeds it to the audio output.
// The main thread keeps the ring buffer ~half full; we just drain it here.

class ModProcessor extends AudioWorkletProcessor {
    constructor(options) {
        super();
        const p = options.processorOptions || {};
        this.channels = p.channels || 2;
        const capacityFrames = p.capacityFrames || 24000; // ~0.5s @48k
        this.capacity = capacityFrames * this.channels;
        this.ring = new Float32Array(this.capacity);
        this.read = 0;
        this.write = 0;
        this.size = 0;
        this.stopped = false;

        this.port.onmessage = (e) => {
            const msg = e.data;
            if (msg && msg.type === 'samples') {
                this._enqueue(msg.data);
            } else if (msg && msg.type === 'stop') {
                this.stopped = true;
            } else if (msg && msg.type === 'reset') {
                this.read = this.write = this.size = 0;
                this.stopped = false;
            }
        };
    }

    _enqueue(data) {
        const free = this.capacity - this.size;
        const n = Math.min(free, data.length);
        for (let i = 0; i < n; i++) {
            this.ring[this.write] = data[i];
            this.write = (this.write + 1) % this.capacity;
        }
        this.size += n;
    }

    process(_inputs, outputs) {
        const out = outputs[0];
        const frames = out[0].length;
        const ch = out.length;
        const interleavedChannels = this.channels;

        for (let f = 0; f < frames; f++) {
            if (this.size >= interleavedChannels) {
                for (let c = 0; c < ch; c++) {
                    const src = c < interleavedChannels ? c : interleavedChannels - 1;
                    // Read sample for channel src; only advance read pointer once we've
                    // consumed all source channels for this frame.
                    out[c][f] = this.ring[(this.read + src) % this.capacity];
                }
                this.read = (this.read + interleavedChannels) % this.capacity;
                this.size -= interleavedChannels;
            } else {
                for (let c = 0; c < ch; c++) out[c][f] = 0;
            }
        }

        return !this.stopped;
    }
}

registerProcessor('mod-processor', ModProcessor);
