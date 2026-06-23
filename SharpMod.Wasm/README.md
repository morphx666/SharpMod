# SharpMod.Wasm

SharpMod running in the browser as a WebAssembly module, built on top of the [SharpMod](../SharpMod/README.md) library.

Live demo: [sharpmod.djxavi.com](https://sharpmod.djxavi.com/)

<img width="1588" height="1102" alt="image" src="https://github.com/user-attachments/assets/2fd44e92-6a7c-41f0-ae69-127062245e6f" />

## Interop

The `SharpModInterop` class exposes the SharpMod library to JavaScript through `[JSExport]` entry points. Load a module with `Load(byte[] data, int sampleRate, bool is16Bit, bool stereo, bool loop)` and pull rendered PCM frames with `Read(int byteCount)`; additional accessors expose track metadata, channel state, pattern data and per-instrument waveform envelopes for the on-screen views.
