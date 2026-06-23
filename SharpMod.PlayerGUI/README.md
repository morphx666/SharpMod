# SharpMod.PlayerGUI

A cross-platform desktop player built on top of the [SharpMod](../SharpMod/README.md) library, using [Eto.Forms](https://github.com/picoe/Eto) for the UI and [OpenTK](https://opentk.net/) (OpenAL) for audio playback.

![SharpMod](https://user-images.githubusercontent.com/12353675/121073541-a0b76380-c7a0-11eb-9cfe-a3ea937fd879.png)

## Projects

The GUI is split into a shared form and one launcher per platform:

- `SharpMod.PlayerGUI` — shared `MainForm`, rendering and playback logic
- `SharpMod.PlayerGUI.Mac` — macOS version
- `SharpMod.PlayerGUI.Gtk` — Linux version (Gtk)
- `SharpMod.PlayerGUI.Wpf` — Windows version (WPF)

## Usage

Drop a supported tracker file onto the window to start playback
