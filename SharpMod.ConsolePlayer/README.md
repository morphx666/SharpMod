# SharpMod.ConsolePlayer

A cross-platform terminal-based player built on top of the [SharpMod](../SharpMod/README.md) library, using [PrettyConsole](https://github.com/dusrdev/PrettyConsole) as the rendering backend

<img width="1259" height="780" alt="image" src="https://github.com/user-attachments/assets/0d1d8a4f-85c4-4ad7-9643-2000350fb603" />

## Usage

    SharpMod.ConsolePlayer <modfile> [options]

`<modfile>` can be a single file, a directory (recursively searched for supported files) or a glob pattern (e.g. `music/*.mod`). Supported formats: `.mod`, `.669`, `.stm`, `.s3m`, `.xm`.

### Options

| Option                       | Description                                                                                       |
| ---------------------------- | ------------------------------------------------------------------------------------------------- |
| `-r`, `--sample-rate <hz>`   | Output sample rate in Hz. Default: `44100`. Valid: `8000, 11025, 16000, 22050, 32000, 44100, 48000, 88200, 96000`. |
| `-b`, `--bit-depth <n>`      | Output bit depth. Default: `16`. Valid: `8, 16`.                                                  |
| `-l`, `--loop`               | Loop the track when it ends.                                                                      |
| `-x`, `--export <path>`      | Render the track to a WAV file at `<path>` (no live playback).                                    |
| `-z`, `--randomize`          | Randomize the order of files in the playlist.                                                     |
| `-H`, `--sample-height <n>`  | Console rows per sample waveform. Default: `0` (`0` hides the waveform). Valid: `0, 1, 2, 3`.     |
| `-m`, `--no-metadata`        | Hide sample metadata columns (Length, Vol, Fmt, LoopStart, LoopEnd).                              |
| `-h`, `--help`               | Show help and exit.                                                                               |

### Examples

    # Play a single file
    SharpMod.ConsolePlayer "mods/Future Crew - Second Reality.S3M"

    # Play every supported file in a directory (recursively)
    SharpMod.ConsolePlayer mods/

    # Play every .XM file matched by a glob pattern
    SharpMod.ConsolePlayer mods/*.XM
