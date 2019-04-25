# SharpMod
A .NET  implementation of the Mod95 mod tracker

This code is a verbatim implementation of the magnificent code developed by Olivier Lapicque for his [Mod95](https://download.openmpt.org/archive/mod95/) player.

For more information, visit https://openmpt.org/legacy_software

![SharpMod](https://xfx.net/stackoverflow/sharpMod/sm01.png)

## Using SharpMod

Instantiating a new `SoundFile`:

A new `SoundFile` object is instantiated by calling the `SoundFile` ctor and passing the audio backend's parameters, such as the desired sample rate, bit bepth (8/16) and channel count (1/2).

    SoundFile sf = new SoundFile(modFileFullPath, sampleRate, bitDepth == 16, channels == 2, false);
  
Once instatieed, call the `SoundFile.Read` method to parse the MOD file and recive back a raw audio buffer, which can the be passed back to the audio renderer.
