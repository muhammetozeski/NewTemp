using System.Security.Cryptography;
using NewTemp.Localization;

namespace NewTemp;

/// <summary>Indicates an expected allocation or input error suitable for direct console display.</summary>
/// <param name="message">The localized explanation.</param>
/// <param name="exitCode">The process result for callers.</param>
internal sealed class RequestException(string message, int exitCode = 2) : Exception(message)
{
    public readonly int ExitCode = exitCode;
}

/// <summary>Allocates short sandbox directories and stores the selected input within them.</summary>
internal static class NewTempService
{
    const bool IsLogEnabled = true;

    /// <summary>Completes one request and returns the exact resulting file or directory path.</summary>
    /// <param name="request">The parsed command-line request.</param>
    /// <param name="standardInput">The byte stream for a redirected-input request.</param>
    /// <returns>The created directory or stored item path.</returns>
    public static string Execute(InputRequest request, Stream? standardInput = null)
    {
        string root = Settings.RootDirectory.Value;
        if (request.Kind == InputKind.Directory &&
            (string.Equals(Path.TrimEndingDirectorySeparator(request.Value), root, StringComparison.OrdinalIgnoreCase) ||
             root.StartsWith(Path.TrimEndingDirectorySeparator(request.Value) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)))
            throw new RequestException(string.Format(Strings.InvalidSource, request.Value));

        string mutexName = @"Local\NewTemp-" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(root.ToUpperInvariant())));
        using var mutex = new Mutex(false, mutexName);
        bool ownsMutex = false;
        try
        {
            try { ownsMutex = mutex.WaitOne(); }
            catch (AbandonedMutexException) { ownsMutex = true; Log("Recovered abandoned allocation mutex.", isRun: IsLogEnabled); }

            Directory.CreateDirectory(root);
            var folders = Directory.GetDirectories(root).Select(Path.GetFileName).ToHashSet(StringComparer.OrdinalIgnoreCase);
            Log($"Request selected; kind={request.Kind}; inputLength={request.Value.Length}; fileName={request.FileName}; root={root}", isRun: IsLogEnabled);
            Log(folders, isRun: IsLogEnabled);
            string folder = request.Kind == InputKind.FolderName
                ? Allocate(root, folders, Settings.MaximumIndex.Value, request.Value)
                : Allocate(root, folders, Settings.MaximumIndex.Value);
            Directory.CreateDirectory(folder);

            string result = request.Kind switch
            {
                InputKind.File => MoveFile(request.Value, folder),
                InputKind.Directory => MoveDirectory(request.Value, folder),
                InputKind.NamedContent => WriteContent(folder, request.FileName!, request.Value),
                InputKind.Stream => WriteStream(folder, request.FileName!, standardInput ?? throw new ArgumentNullException(nameof(standardInput))),
                InputKind.Text => WriteContent(folder, AllocateTextName(folder), request.Value),
                _ => folder
            };
            Log($"Request completed; result={result}", isRun: IsLogEnabled);
            return result;
        }
        finally
        {
            if (ownsMutex)
                mutex.ReleaseMutex();
        }
    }

    /// <summary>Finds a free Base36 name or adds the first free Base36 suffix to a requested name.</summary>
    /// <param name="root">The destination parent.</param>
    /// <param name="folders">The initial directory-name snapshot.</param>
    /// <param name="limit">The exclusive integer bound.</param>
    /// <param name="requestedName">An optional preferred directory name, used unchanged when free.</param>
    /// <returns>The first available full directory path.</returns>
    static string Allocate(string root, HashSet<string?> folders, int limit, string? requestedName = null)
    {
        if (requestedName is not null)
        {
            string requestedPath = Path.Combine(root, requestedName);
            if (!folders.Contains(requestedName) && !File.Exists(requestedPath) && !Directory.Exists(requestedPath))
                return requestedPath;
        }
        for (int i = 0; i < limit; i++)
        {
            string name = Base36.ToBase36(i);
            if (requestedName is not null)
            {
                string suffix = name;
                name = requestedName[..Math.Min(requestedName.Length, 255 - suffix.Length)] + suffix;
            }
            string candidate = Path.Combine(root, name);
            if (InputParser.IsValidName(name) && !folders.Contains(name) && !File.Exists(candidate) && !Directory.Exists(candidate))
                return candidate;
        }
        throw new RequestException(string.Format(Strings.FolderLimit, root, limit), 3);
    }

    /// <summary>Finds the first unused Base36 filename with a .txt extension.</summary>
    /// <param name="folder">The allocated sandbox folder.</param>
    /// <returns>The available filename without a parent path.</returns>
    static string AllocateTextName(string folder)
    {
        for (int i = 0; i < Settings.MaximumIndex.Value; i++)
        {
            string name = Base36.ToBase36(i) + ".txt";
            string path = Path.Combine(folder, name);
            if (InputParser.IsValidName(name) && !File.Exists(path) && !Directory.Exists(path))
                return name;
        }
        throw new RequestException(string.Format(Strings.FileLimit, folder), 3);
    }

    /// <summary>Writes literal text as UTF-8 without interpreting its filename extension.</summary>
    /// <param name="folder">The allocated sandbox folder.</param>
    /// <param name="fileName">A valid filename.</param>
    /// <param name="content">The exact content after the closing quote or the full plain input.</param>
    /// <returns>The created file's full path.</returns>
    static string WriteContent(string folder, string fileName, string content)
    {
        string destination = Path.Combine(folder, fileName);
        using var file = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        using var writer = new StreamWriter(file, new UTF8Encoding(false));
        writer.Write(content);
        Log($"Content written; destination={destination}; characters={content.Length}; encoding=UTF-8", isRun: IsLogEnabled);
        return destination;
    }

    /// <summary>Copies redirected input directly into the destination without decoding or buffering the entire file.</summary>
    /// <param name="folder">The allocated sandbox folder.</param>
    /// <param name="fileName">The valid destination filename.</param>
    /// <param name="input">The stream owned by the caller, read until end of input.</param>
    /// <returns>The completed file's full path.</returns>
    static string WriteStream(string folder, string fileName, Stream input)
    {
        string destination = Path.Combine(folder, fileName);
        using var file = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        input.CopyTo(file);
        Log($"Stream written; destination={destination}; bytes={file.Position}", isRun: IsLogEnabled);
        return destination;
    }

    /// <summary>Moves an existing file, retaining its original filename and bytes.</summary>
    /// <param name="source">The existing file path.</param>
    /// <param name="folder">The new sandbox folder.</param>
    /// <returns>The file's destination path.</returns>
    static string MoveFile(string source, string folder)
    {
        string destination = Path.Combine(folder, Path.GetFileName(source));
        ExecuteFileOperation(() => File.Move(source, destination), "MoveFile");
        Log($"File moved; source={source}; destination={destination}", isRun: IsLogEnabled);
        return destination;
    }

    /// <summary>Moves a directory, copying across volumes before removing the complete source tree.</summary>
    /// <param name="source">The existing directory path.</param>
    /// <param name="folder">The new sandbox folder.</param>
    /// <returns>The directory's destination path.</returns>
    static string MoveDirectory(string source, string folder)
    {
        string destination = Path.Combine(folder, Path.GetFileName(Path.TrimEndingDirectorySeparator(source)));
        try
        {
            ExecuteFileOperation(() => Directory.Move(source, destination), "MoveDirectory");
        }
        catch (IOException exception) when ((exception.HResult & 0xffff) == 17)
        {
            Log($"Directory move crosses volumes; using copy then delete; source={source}; destination={destination}; error={exception}", isRun: IsLogEnabled);
            try
            {
                CopyDirectory(source, destination);
                Directory.Delete(source, true);
            }
            catch (Exception fallbackException)
            {
                throw new AggregateException("Both directory move methods failed.", exception, fallbackException);
            }
        }
        Log($"Directory moved; source={source}; destination={destination}", isRun: IsLogEnabled);
        return destination;
    }

    /// <summary>Copies a complete directory, preserving links without following them recursively.</summary>
    /// <param name="source">The source directory.</param>
    /// <param name="destination">The not-yet-existing destination directory.</param>
    static void CopyDirectory(string source, string destination)
    {
        var sourceDirectory = new DirectoryInfo(source);
        if (sourceDirectory.LinkTarget is not null)
        {
            Directory.CreateSymbolicLink(destination, sourceDirectory.ResolveLinkTarget(false)!.FullName);
            return;
        }
        Directory.CreateDirectory(destination);
        foreach (FileInfo file in sourceDirectory.EnumerateFiles())
        {
            string target = Path.Combine(destination, file.Name);
            if (file.LinkTarget is not null)
                File.CreateSymbolicLink(target, file.ResolveLinkTarget(false)!.FullName);
            else
                ExecuteFileOperation(() => File.Copy(file.FullName, target, true), "CopyFileAcrossVolumes");
        }
        foreach (DirectoryInfo child in sourceDirectory.EnumerateDirectories())
            CopyDirectory(child.FullName, Path.Combine(destination, child.Name));
    }

    /// <summary>Retries only sharing and lock violations; each synchronous attempt ends before another begins.</summary>
    /// <param name="action">A synchronous move or repeatable copy.</param>
    /// <param name="name">The operation label in logs.</param>
    static void ExecuteFileOperation(Action action, string name) => Resilience.ExecuteResilientlyAsync(
        token => { token.ThrowIfCancellationRequested(); action(); return Task.CompletedTask; },
        exception => exception is IOException && (exception.HResult & 0xffff) is 32 or 33,
        operationName: name).GetAwaiter().GetResult();
}
