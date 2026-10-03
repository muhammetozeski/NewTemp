namespace NewTemp;

/// <summary>Names the mutually exclusive actions selected from one command-line input.</summary>
internal enum InputKind { Empty, File, Directory, NamedContent, FolderName, Text }

/// <summary>Contains the selected action and its unmodified source or content.</summary>
/// <param name="Kind">The action selected by precedence.</param>
/// <param name="Value">The source path, folder name, or exact text content.</param>
/// <param name="FileName">The optional filename extracted from double quotes.</param>
internal sealed record InputRequest(InputKind Kind, string Value, string? FileName = null);

/// <summary>Distinguishes existing paths, explicit file content, directory names, and plain text.</summary>
internal static class InputParser
{
    const bool IsLogEnabled = true;

    /// <summary>Reads arguments while retaining literal spacing in the raw multi-argument tail.</summary>
    /// <param name="args">The decoded process arguments.</param>
    /// <returns>The logical user input, without the executable token.</returns>
    public static string Read(string[] args)
    {
        if (args.Length == 0)
            return string.Empty;
        if (args.Length == 1)
            return args[0];

        string commandLine = Environment.CommandLine;
        int end = commandLine.StartsWith('"')
            ? commandLine.IndexOf('"', 1) + 1
            : commandLine.IndexOf(' ');
        return end > 0 ? commandLine[end..].TrimStart(' ', '\t') : string.Join(' ', args);
    }

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
