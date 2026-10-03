# Input and allocation contract

Read this before changing command-line parsing, name selection, or moving user data.

## Precedence and result

`InputParser.Parse` recognizes named command-line content and redirected input before applying the original path/name/text selection:

| Input | Action | Printed result |
| --- | --- | --- |
| No arguments or an empty argument | Allocate a Base36 directory | Created directory |
| Redirected standard input and one valid filename with an extension | Copy incoming bytes into a newly allocated sandbox | New file path |
| A valid filename with an extension followed by content arguments | Write the original content tail after one separator | New file path |
| Existing file path | Move the file, retaining its filename and bytes | New file path |
| Existing directory path | Move the directory into a new sandbox | New directory path |
| Double-quoted valid filename with an extension, followed by any content | Write the exact tail under that filename | New file path |
| Valid Windows directory name | Create a fresh directory, adding a Base36 suffix if needed | New named directory |
| Any other text | Allocate a Base36 directory and its first unused Base36 `.txt` name | New text file path |

Existing paths win in the original path/name/text selection even when quoted. A stream request treats its filename as the destination name, leaving any matching source file untouched. A nonexistent path containing separators is text. Windows device names, trailing spaces or periods, and traversal segments cannot become directory names; they become text instead. Quoted names containing a path cannot escape the sandbox and fall through to text.

A normal command is `NewTemp "test.mp3" content`. The first space or tab after the filename is syntax; everything after it is content. For `"test.mp3"  content "kept"`, the file contains one leading space, `content`, one space, and `"kept"`. No extension-specific decoding is performed. Text output uses UTF-8 without a byte-order marker; moved and streamed file bytes are unchanged. The older single-argument literal form remains accepted.

## Windows quoting boundary

`InputParser.Read` consumes a single decoded argument directly when the shell supplied the complete content as one string. It retains a simple quoted filename even when it is the only argument, allowing an empty file.

For multiple arguments, `GetCommandLineW` provides the original Windows command line, and the executable token is removed. `Environment.CommandLine` reconstructs the argument text: the actual integration test lost repeated spaces and content quotes with that property. The native input fixed the same test. Preserve this distinction when refactoring.

The shell must pass the characters to NewTemp. Ordinary PowerShell invocation with `"test.mp3" content` is supported even when PowerShell omits unnecessary filename quotes from the native command line. Filename identity comes from the decoded first argument; the original native tail preserves content spacing and quotes. The raw argument form was also exercised through `ProcessStartInfo.Arguments`.

## Stream input

`producer.exe | NewTemp "file.mp3"` redirects the producer's output into NewTemp. `Console.OpenStandardInput` and `Stream.CopyTo` transfer those bytes directly into a new `FileStream`; no text reader, encoding conversion, or full-file buffer is used. A successful result is printed only after end of input and file disposal. An empty stream creates an empty file. With no arguments, directory allocation remains unchanged even if standard input is closed or redirected.

The v1.1.0 input suite in [Verify-Input.ps1](../tests/Verify-Input.ps1) verified normal filename/content invocation without outer single quotes, repeated spaces and quotes, the previous literal-argument form, binary and empty input, an existing filename collision, no-argument allocation, the actual PowerShell native pipeline, and a destination failure. It used ten scenarios, with all temporary data in a NewTemp scratch folder outside the project.

## Directory and filename selection

`NewTempService.Execute` creates the configured root when necessary, snapshots its immediate directory names, and passes the names to `Allocate`. A `for` loop tries `Base36.ToBase36(i)` for `0 <= i < MaximumIndex`. Names are compared without case sensitivity; a file occupying a candidate path also causes that name to be skipped. Invalid Windows device names are skipped.

The supplied Base36 implementation uses `abcdefghijklmnopqrstuvwxyz0123456789`. Do not substitute a digits-first Base36 library. The loop deliberately finds the first gap instead of incrementing the greatest existing name.

The `.txt` fallback uses the same sequence and exclusive limit for filenames inside the allocated folder. `FileMode.CreateNew` prevents an existing file from being overwritten. A requested directory name is used unchanged if free; otherwise `Allocate` tries `name-a`, `name-b`, and the first unused suffix from the supplied Base36 alphabet, bounded by `MaximumIndex`. Directory comparisons ignore case, and files occupying candidate paths are skipped. A name near Windows' 255-character segment limit is shortened only enough to fit its suffix. Existing named directories are not reused.

A Windows named mutex serializes NewTemp requests using the same normalized root. An abandoned owner is logged and the next process resumes selection. The root and its ancestors cannot be moved into that root.

## Moving and failure containment

Files use `File.Move`; directories use `Directory.Move`. The move service handles Windows error `17` for cross-volume directory moves with a recursive copy, then deletes the source only after copying completes. Links are recreated without recursively following them. This alternative is present in source; the same-volume directory move was the exercised path in the integration suite.

Only sharing and lock violations, Windows errors `32` and `33`, are retried through Polly. Each file operation is synchronous and finishes before another attempt begins. Permanent failures propagate to the one outer `RunSafely` boundary, which records the error and returns failure. `Program.ReportFailure` prints the complete exception's `ToString()` under the requested English prefix, including initialization failures. It does not infer whether a directory or partial output exists. No success path is printed on failure.

## Verification scope

The v1.0.0 self-contained Windows x64 executable passed 100 assertions across 18 isolated integration scenarios in [Verify.ps1](../tests/Verify.ps1). The suite exercised first-gap selection, case-insensitive collisions, files occupying candidate names, bounded exhaustion, named directories, missing and malformed limits, byte-preserving file moves, nested directory moves, exact named content, empty files, raw command-line quotes, plain text, reserved names, Turkish/English/Turkish switching, root move rejection, and portable relocation.

The framework-dependent executable separately created a named file and preserved the literal tail. New runs keep test data, portable copies, logs, and bundle-extraction caches in a NewTemp scratch directory outside the source project; pass `-ScratchDirectory` to reuse a task's folder.
