# SharpMod.Wasm

SharpMod running in the browser as a WebAssembly module, built on top of the [SharpMod](../SharpMod/README.md) library.

Live demo: [sharpmod.djxavi.com](https://sharpmod.djxavi.com/)

<img width="1588" height="1102" alt="image" src="https://github.com/user-attachments/assets/2fd44e92-6a7c-41f0-ae69-127062245e6f" />

## How it works

`SharpModInterop` is the bridge between the page and the SharpMod library. Each method tagged with `[JSExport]` becomes a regular function on the JavaScript side, so the browser can drive playback without leaving JS:

- hand a module file to `Load` to open it,
- call `Read` whenever the Web Audio backend needs more PCM samples,
- and use the smaller accessors to read back things like the title, the active channels, the current pattern or the waveform of a given instrument — everything the on-screen views need to stay in sync with playback.
