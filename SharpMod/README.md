# SharpMod
A cross-platform .NET implementation of the Mod95 MOD player

This is a verbatim implementation of the magnificent code developed by Olivier Lapicque for his [Mod95](https://download.openmpt.org/archive/mod95/) player.

For more information, visit https://openmpt.org/legacy_software

## Using SharpMod

Instantiating a new `SoundFile`:

A new `SoundFile` object is instantiated by calling the `SoundFile` ctor and passing the audio backend's parameters, such as the desired sample rate, bit depth (8/16) and channel count (1/2).

```csharp
SharpMod.SoundFile sf = new SharpMod.SoundFile(modFileFullPath, sampleRate, bitDepth == 16, channels == 2, false);
```

<details>
<summary>
A typical audio consumer loop that drives a `SoundFile` looks like this:
</summary>

```text
# Inputs
#   sf            : an initialized SoundFile (sampleRate, bitDepth, channels, loop already set)
#   backend       : the audio device / streaming sink (OpenAL, WASAPI, Web Audio, …)
#   BUFFER_BYTES  : size of one PCM chunk to hand to the backend
#   TARGET_DEPTH  : how many chunks to keep queued ahead of the playback cursor

buffer        := allocate(BUFFER_BYTES)         # reusable PCM scratch buffer
bufferIsClear := false
isPlaying     := true
isPaused      := false

backend.Open(sf.Rate, sf.Is16Bit, sf.IsStereo)
backend.PrimeWithSilence(BUFFER_BYTES)          # one silent chunk so playback can start

while isPlaying:
    if isPaused:
        sleep(short)
        continue

    backend.ReleaseProcessedBuffers()           # recycle chunks the device has consumed

    if backend.QueuedBufferCount() >= TARGET_DEPTH:
        sleep(short)                            # backend is full, don't starve nor flood
        continue

    bytesRead := sf.Read(buffer, BUFFER_BYTES)  # pulls/synthesizes the next PCM chunk

    if bytesRead == 0:
        # End of song reached (Loop=false). Emit silence so the device drains cleanly.
        if not bufferIsClear:
            zero(buffer)
            bufferIsClear := true
        if sf.Position >= sf.PositionCount:
            isPlaying := false
    else:
        bufferIsClear := false

    backend.SubmitBuffer(buffer, BUFFER_BYTES)  # hand the chunk to the device queue

backend.DrainAndClose()
```

Key points:

- `sf.Read(buffer, length)` is the single producer call; it returns the number of PCM bytes written (`0` signals end-of-song when `Loop == false`).
- The buffer's format is whatever was passed to the `SoundFile` ctor (sample rate, 8/16-bit, mono/stereo) — the backend must be opened with the same format.
- Back-pressure is managed by checking the backend's queue depth rather than by sleeping a fixed interval; this prevents both underruns and unbounded memory growth.
- A single silent prime buffer is queued before the loop so the device can start playing immediately without a branch on the first iteration.

Then, whenever the audio backend requests audio data, call the `SoundFile.Read` method to parse the tracker and receive back a raw audio buffer, which can be passed back to the audio renderer.
</details>

## Front-ends

The repository also ships several reference players built on top of the SharpMod library:

- [SharpMod.PlayerGUI](https://github.com/morphx666/SharpMod/blob/master/SharpMod.PlayerGUI/README.md) — cross-platform desktop GUI (Eto.Forms / OpenTK)
- [SharpMod.ConsolePlayer](https://github.com/morphx666/SharpMod/blob/master/SharpMod.ConsolePlayer/README.md) — cross-platform terminal player
- [SharpMod.Wasm](https://github.com/morphx666/SharpMod/blob/master/SharpMod.Wasm/README.md) — browser player running as a WebAssembly module
