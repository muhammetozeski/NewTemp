<p align="center"><img src="Assets/icon.png" width="96" alt="NewTemp folder and console icon"></p>

# NewTemp

![Windows](https://img.shields.io/badge/Windows-x64-0078D4)
![.NET](https://img.shields.io/badge/.NET-10-512BD4)
![CSharp](https://img.shields.io/badge/C%23-14-512BD4)
[![Release](https://img.shields.io/github/v/release/muhammetozeski/NewTemp)](https://github.com/muhammetozeski/NewTemp/releases/latest)

A small console utility that creates short sandbox paths for temporary files, tests, and disposable work. With no arguments, it creates the first available Base36 directory under `C:\C` and prints its full path.

## Install

Download [the latest release](https://github.com/muhammetozeski/NewTemp/releases/latest):

- `NewTemp.exe` includes its .NET runtime and runs on Windows x64.
- `NewTemp-FrameworkDependent-RequiresNET10.exe` is smaller and requires the .NET 10 x64 runtime.
- Place `NewTemp.settings.txt` beside the executable. If absent, NewTemp creates it with defaults.

Keep the program in its own portable folder. Settings are beside the executable; language resources and logs are created under that folder when used.

The executables are digitally signed. To let Windows verify the signature, run `Install-Certificate.cmd` from `SignatureTrust.zip` once. The programs run without it; only the signature stays unverified.

## Use

These PowerShell examples use the local installation path:

```powershell
# Create the first available short directory, such as C:\C\a.
& 'C:\E\kp\aaBenimProgramlarim\NewTemp\NewTemp.exe'

# Create a fresh named directory, adding a Base36 suffix if the name is occupied.
& 'C:\E\kp\aaBenimProgramlarim\NewTemp\NewTemp.exe' 'test sandbox'

# Move an existing file into a new short directory and print its new path.
& 'C:\E\kp\aaBenimProgramlarim\NewTemp\NewTemp.exe' 'C:\Examples\sample.mp3'

# Move an existing directory, including its contents.
& 'C:\E\kp\aaBenimProgramlarim\NewTemp\NewTemp.exe' 'C:\Examples\test files'

# Store literal content under a quoted filename. The extension is not interpreted.
& 'C:\E\kp\aaBenimProgramlarim\NewTemp\NewTemp.exe' "sample.mp3" literal content

# Pipe a native program's binary output directly into a named file.
cmd.exe /d /c type "C:\Examples\sample.mp3" | & 'C:\E\kp\aaBenimProgramlarim\NewTemp\NewTemp.exe' "streamed.mp3"

# A string containing characters invalid in directory names becomes a.txt.
& 'C:\E\kp\aaBenimProgramlarim\NewTemp\NewTemp.exe' 'text: disposable content'
```

Existing path arguments are **moved**, so their original paths disappear after success. Other valid directory names create a new empty directory: `test sandbox`, then `test sandboxa`, `test sandboxb`, and the next free Base36 suffix. No separator is inserted; characters already in the requested name, including hyphens, are preserved. Existing directories and files are skipped. For named text, one space or tab separates the filename from its content; the remaining text is preserved, including additional whitespace and quotes, as UTF-8 without a byte-order marker. Outer single quotes around the filename and content together are unnecessary. A filename extension does not trigger conversion or decoding.

With redirected standard input and one filename argument, NewTemp writes the incoming bytes directly into that file and prints its full path after the input ends. This preserves binary data, including zero bytes. A matching filename in the current directory is left in place. Native-to-native binary piping was verified in PowerShell; piping text objects instead supplies the shell's text representation.

The supplied Base36 alphabet is `abcdefghijklmnopqrstuvwxyz0123456789`: indices start at zero, so the first names are `a`, `b`, and `c`. Holes are reused. The default exclusive limit is `10000`; exhaustion reports that the root has grown too large and some directories should be deleted.

Success writes one full path to standard output and exits with code `0`. Failure writes the prefix `An exception occurred while working in NewTemp:` followed by the quoted, complete exception's `ToString()` output, including its type, inner exceptions, and stack trace, to standard error: code `3` for name exhaustion and code `2` for other failures. A failure does not assert whether a directory or partial file was created. The utility does not wait for a keypress.

## Configure

Edit `NewTemp.settings.txt` next to the executable:

```text
RootDirectory = C:\C
MaximumIndex = 10000
Language = system
```

`RootDirectory` uses `C:\C` when missing, empty, relative, or syntactically invalid. `MaximumIndex` uses `10000` when missing, not parseable as an integer, or less than or equal to `1`. `Language` accepts `system`, `tr`, and `en`; invalid or unavailable selections fall back to `system`. Valid settings are not overwritten during startup.

See [configuration and portable paths](Docs/Configuration-and-Portability.md) for extension and verification details.

## Project knowledge

- [Documentation index](Docs/İçindekiler.md)
- [Input and allocation contract](Docs/Input-and-Allocation.md)
- [Walkthrough handover guide](Walkthrough/Walkthrough-Rehberi.md)
