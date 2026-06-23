# SharpMod
A cross-platform .NET implementation of the Mod95 MOD player

This is a verbatim implementation of the magnificent code developed by Olivier Lapicque for his [Mod95](https://download.openmpt.org/archive/mod95/) player.

For more information, visit https://openmpt.org/legacy_software

## Using SharpMod

Instantiating a new `SoundFile`:

A new `SoundFile` object is instantiated by calling the `SoundFile` ctor and passing the audio backend's parameters, such as the desired sample rate, bit depth (8/16) and channel count (1/2).

    SharpMod.SoundFile sf = new SharpMod.SoundFile(modFileFullPath, sampleRate, bitDepth == 16, channels == 2, false);

Then, whenever the audio backend requests audio data, call the `SoundFile.Read` method to parse the tracker and receive back a raw audio buffer, which can be passed back to the audio renderer.

## Front-ends

The repository also ships several reference players built on top of the SharpMod library:

- [SharpMod.PlayerGUI](https://github.com/morphx666/SharpMod/blob/master/SharpMod.PlayerGUI/README.md) — cross-platform desktop GUI (Eto.Forms / OpenTK)
- [SharpMod.ConsolePlayer](https://github.com/morphx666/SharpMod/blob/master/SharpMod.ConsolePlayer/README.md) — cross-platform terminal player
- [SharpMod.Wasm](https://github.com/morphx666/SharpMod/blob/master/SharpMod.Wasm/README.md) — browser player running as a WebAssembly module
