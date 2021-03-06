# SharpMod
A cross-platform .NET implementation of the Mod95 MOD player

This is a verbatim implementation of the magnificent code developed by Olivier Lapicque for his [Mod95](https://download.openmpt.org/archive/mod95/) player.

For more information, visit https://openmpt.org/legacy_software

![SharpMod](https://user-images.githubusercontent.com/12353675/121073541-a0b76380-c7a0-11eb-9cfe-a3ea937fd879.png)

## Using SharpMod

Instantiating a new `SoundFile`:

A new `SoundFile` object is instantiated by calling the `SoundFile` ctor and passing the audio backend's parameters, such as the desired sample rate, bit depth (8/16) and channel count (1/2).

    SharpMod.SoundFile sf = new SharpMod.SoundFile(modFileFullPath, sampleRate, bitDepth == 16, channels == 2, false);
  
Then, whenever the audio backend requests audio data, call the `SoundFile.Read` method to parse the MOD file and receive back a raw audio buffer, which can be passed back to the audio renderer.
