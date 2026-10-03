# Input and allocation contract

Read this before changing command-line parsing, name selection, or moving user data.

## Precedence and result

`InputParser.Parse` selects one action in this order:

| Input | Action | Printed result |
| --- | --- | --- |
| No arguments or an empty argument | Allocate a Base36 directory | Created directory |
| Existing file path | Move the file, retaining its filename and bytes | New file path |
| Existing directory path | Move the directory into a new sandbox | New directory path |
| Double-quoted valid filename with an extension, followed by any content | Write the exact tail under that filename | New file path |
| Valid Windows directory name | Create or return that named directory under the root | Named directory |
| Any other text | Allocate a Base36 directory and its first unused Base36 `.txt` name | New text file path |

Existing paths win even when quoted. A nonexistent path containing separators is text. Windows device names, trailing spaces or periods, and traversal segments cannot become directory names; they become text instead. Quoted names containing a path cannot escape the sandbox and fall through to text.

Only a quoted filename with an extension selects named content. The tail begins immediately after the closing double quote. For `"test.mp3"  content "kept"`, the file contains exactly two leading spaces, `content`, one space, and `"kept"`. No extension-specific decoding is performed. Text output uses UTF-8 without a byte-order marker; moved file bytes are unchanged.

## Windows quoting boundary

`InputParser.Read` consumes a single decoded argument directly when the shell supplied the complete content as one string. It retains a simple quoted filename even when it is the only argument, allowing an empty file.

For multiple arguments, `GetCommandLineW` provides the original Windows command line, and the executable token is removed. `Environment.CommandLine` reconstructs the argument text: the actual integration test lost repeated spaces and content quotes with that property. The native input fixed the same test. Preserve this distinction when refactoring.

The shell must pass the characters to NewTemp. In PowerShell, a single-quoted outer string such as `'"test.mp3"literal text'` retains the filename quotes. The Windows raw-argument form `"test.mp3"  raw   content "quoted"` was also exercised directly through `ProcessStartInfo.Arguments`.

## Directory and filename selection

`NewTempService.Execute` creates the configured root when necessary, snapshots its immediate directory names, and passes the names to `Allocate`. A `for` loop tries `Base36.ToBase36(i)` for `0 <= i < MaximumIndex`. Names are compared without case sensitivity; a file occupying a candidate path also causes that name to be skipped. Invalid Windows device names are skipped.

The supplied Base36 implementation uses `abcdefghijklmnopqrstuvwxyz0123456789`. Do not substitute a digits-first Base36 library. The loop deliberately finds the first gap instead of incrementing the greatest existing name.

The `.txt` fallback uses the same sequence and exclusive limit for filenames inside the allocated folder. `FileMode.CreateNew` prevents an existing file from being overwritten. Explicit directory names bypass the numeric allocation limit. Named directories are idempotent when they already exist under the configured root.

A Windows named mutex serializes NewTemp requests using the same normalized root. An abandoned owner is logged and the next process resumes selection. The root and its ancestors cannot be moved into that root.

## Moving and failure containment

Files use `File.Move`; directories use `Directory.Move`. The move service handles Windows error `17` for cross-volume directory moves with a recursive copy, then deletes the source only after copying completes. Links are recreated without recursively following them. This alternative is present in source; the same-volume directory move was the exercised path in the integration suite.

Only sharing and lock violations, Windows errors `32` and `33`, are retried through Polly. Each file operation is synchronous and finishes before another attempt begins. Permanent failures propagate to the one outer `RunSafely` boundary, which records the error and returns failure. No success path is printed on failure.

## Verification scope

The v1.0.0 self-contained Windows x64 executable passed 100 assertions across 18 isolated integration scenarios in [Verify.ps1](../tests/Verify.ps1). The suite exercised first-gap selection, case-insensitive collisions, files occupying candidate names, bounded exhaustion, named directories, missing and malformed limits, byte-preserving file moves, nested directory moves, exact named content, empty files, raw command-line quotes, plain text, reserved names, Turkish/English/Turkish switching, root move rejection, and portable relocation.

The framework-dependent executable separately created a named file and preserved the literal tail. Test data, portable copies, logs, and output are kept under ignored `work/` relative to the project root. The reusable test sets its bundle-extraction cache there as well.
