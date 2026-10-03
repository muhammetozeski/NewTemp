using System.Runtime.InteropServices;

namespace NewTemp;

/// <summary>Names the mutually exclusive actions selected from one command-line input.</summary>
internal enum InputKind { Empty, File, Directory, NamedContent, Stream, FolderName, Text }

/// <summary>Contains the selected action and its unmodified source or content.</summary>
/// <param name="Kind">The action selected by precedence.</param>
/// <param name="Value">The source path, folder name, or exact text content.</param>
/// <param name="FileName">The optional filename extracted from double quotes.</param>
internal sealed record InputRequest(InputKind Kind, string Value, string? FileName = null);

/// <summary>Distinguishes existing paths, explicit file content, directory names, and plain text.</summary>
internal static class InputParser
{
    const bool IsLogEnabled = true;

    /// <summary>Accepts normal filename arguments and selects binary input when it is redirected.</summary>
    /// <param name="args">The decoded process arguments.</param>
    /// <param name="hasStandardInput">Whether the process has redirected standard input.</param>
    /// <returns>The selected file, directory, text, or stream request.</returns>
    public static InputRequest Parse(string[] args, bool hasStandardInput)
    {
        if (args.Length > 0 && IsValidName(args[0]) && Path.HasExtension(args[0]))
        {
            if (args.Length == 1 && hasStandardInput)
                return new(InputKind.Stream, string.Empty, args[0]);
            if (args.Length > 1)
            {
                string rawInput = Read(args);
                int tokenEnd = rawInput.StartsWith('"')
                    ? rawInput.IndexOf('"', 1) + 1
                    : rawInput.IndexOfAny([' ', '\t']);
                string content = tokenEnd > 0 ? rawInput[tokenEnd..] : string.Join(' ', args.Skip(1));
                // The first whitespace character separates the filename argument from content.
                if (content.Length > 0 && content[0] is ' ' or '\t')
                    content = content[1..];
                return new(InputKind.NamedContent, content, args[0]);
            }
        }
        return Parse(Read(args));
    }

    /// <summary>Reads arguments while retaining literal spacing in the raw multi-argument tail.</summary>
    /// <param name="args">The decoded process arguments.</param>
    /// <returns>The logical user input, without the executable token.</returns>
    public static string Read(string[] args)
    {
        if (args.Length == 0)
            return string.Empty;
        string commandLine = Marshal.PtrToStringUni(GetCommandLineW())
            ?? throw new InvalidOperationException("Windows command line is unavailable.");
        int end = commandLine.StartsWith('"')
            ? commandLine.IndexOf('"', 1) + 1
            : commandLine.IndexOf(' ');
        string rawInput = end > 0 ? commandLine[end..].TrimStart(' ', '\t') : string.Join(' ', args);
        if (args.Length == 1)
        {
            // A shell removes filename quotes from argv. Retain a simple quoted filename,
            // but decode an outer quoting layer around an entire literal-content argument.
            if (rawInput.Length > 2 && rawInput[0] == '"' && rawInput[^1] == '"' &&
                rawInput[1..^1] == args[0] && IsValidName(args[0]) && Path.HasExtension(args[0]))
                return rawInput;
            return args[0];
        }
        return rawInput;
    }

    /// <summary>Returns Windows' original command line before .NET reconstructs argument text.</summary>
    /// <returns>A pointer owned by Windows for the lifetime of the process.</returns>
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    static extern IntPtr GetCommandLineW();

    /// <summary>Selects a request; content following the closing filename quote stays exact.</summary>
    /// <param name="input">The input text after removal of the executable token.</param>
    /// <returns>The action and its source or content.</returns>
    public static InputRequest Parse(string input)
    {
        if (input.Length == 0)
            return new(InputKind.Empty, input);

        string path = input;
        if (path.Length >= 2 && path[0] == '"' && path[^1] == '"')
            path = path[1..^1];
        if (File.Exists(path))
            return new(InputKind.File, Path.GetFullPath(path));
        if (Directory.Exists(path))
            return new(InputKind.Directory, Path.GetFullPath(path));

        if (input[0] == '"')
        {
            int closingQuote = input.IndexOf('"', 1);
            if (closingQuote > 1)
            {
                string fileName = input[1..closingQuote];
                if (IsValidName(fileName) && Path.HasExtension(fileName))
                    return new(InputKind.NamedContent, input[(closingQuote + 1)..], fileName);
            }
        }

        bool isName = IsValidName(path);
        return new(isName ? InputKind.FolderName : InputKind.Text, isName ? path : input);
    }

    /// <summary>Checks a single Windows filename without allowing traversal or device names.</summary>
    /// <param name="name">The proposed file or directory name.</param>
    /// <returns>Whether Windows can use this name as one normal path segment.</returns>
    public static bool IsValidName(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > 255 || name is "." or ".." ||
            name.EndsWith(' ') || name.EndsWith('.') || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            return false;

        string stem = name.Split('.')[0].TrimEnd(' ').ToUpperInvariant();
        if (stem is "CON" or "PRN" or "AUX" or "NUL" or "CONIN$" or "CONOUT$")
            return false;
        return !(stem.Length == 4 && (stem.StartsWith("COM") || stem.StartsWith("LPT")) &&
            "123456789¹²³".Contains(stem[3]));
    }
}
