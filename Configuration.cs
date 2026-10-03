namespace NewTemp;

/// <summary>Applies semantic fallbacks after the shared settings parser loads values.</summary>
internal static class Configuration
{
    const bool IsLogEnabled = true;

    /// <summary>Normalizes an absolute Windows storage path and a positive allocation bound.</summary>
    public static void Validate()
    {
        if (Settings.MaximumIndex.Value <= 1)
        {
            Log($"Invalid MaximumIndex={Settings.MaximumIndex.Value}; fallback={DefaultMaximumIndex}", isRun: IsLogEnabled);
            Settings.MaximumIndex.Value = DefaultMaximumIndex;
        }

        try
        {
            string root = Settings.RootDirectory.Value;
            if (string.IsNullOrWhiteSpace(root) || !Path.IsPathFullyQualified(root) ||
                root.IndexOfAny(Path.GetInvalidPathChars()) >= 0 || root.Contains('*') || root.Contains('?'))
                throw new ArgumentException("RootDirectory must be an absolute Windows directory path.");

            string fullPath = Path.GetFullPath(root);
            string remainder = fullPath[Path.GetPathRoot(fullPath)!.Length..];
            if (remainder.Split(['\\', '/'], StringSplitOptions.RemoveEmptyEntries).Any(part => !InputParser.IsValidName(part)))
                throw new ArgumentException("RootDirectory contains an invalid directory name.");
            Settings.RootDirectory.Value = Path.TrimEndingDirectorySeparator(fullPath);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            Log($"Invalid RootDirectory; fallback={DefaultRootDirectory}; error={exception}", isRun: IsLogEnabled);
            Settings.RootDirectory.Value = DefaultRootDirectory;
        }

        Log($"Configuration ready; root={Settings.RootDirectory.Value}; maximumIndex={Settings.MaximumIndex.Value}", isRun: IsLogEnabled);
    }
}
