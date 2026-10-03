using NewTemp.Localization;

namespace NewTemp;

/// <summary>Loads portable services and prints exactly one successful result path.</summary>
internal static class Program
{
    const bool IsLogEnabled = true;

    /// <summary>Executes one sandbox request and reports contained failures with a nonzero exit code.</summary>
    /// <param name="args">The decoded Windows command-line arguments.</param>
    /// <returns>Zero for success, two for an input or I/O failure, or three for exhaustion.</returns>
    public static int Main(string[] args)
    {
        Console.OutputEncoding = new UTF8Encoding(false);
        var initialization = ApplicationServices.Initialize();
        if (!initialization.Succeeded)
            return ReportFailure(initialization.Error);

        var result = RunSafely(() =>
        {
            InputRequest request = InputParser.Parse(args, Console.IsInputRedirected);
            using Stream? input = request.Kind == InputKind.Stream ? Console.OpenStandardInput() : null;
            return NewTempService.Execute(request, input);
        });
        if (result.Succeeded)
        {
            Console.WriteLine(result.Value);
            return 0;
        }

        return ReportFailure(result.Error, result.Error is RequestException failure ? failure.ExitCode : 2);
    }

    /// <summary>Prints the complete exception without inferring whether partial output exists.</summary>
    /// <param name="exception">The final exception, including its inner exceptions and stack trace.</param>
    /// <param name="exitCode">The failure result returned to the shell.</param>
    /// <returns>The unchanged failure exit code.</returns>
    static int ReportFailure(Exception? exception, int exitCode = 2)
    {
        Console.Error.WriteLine(string.Format(Strings.ExceptionOccurred, AppTitle, exception?.ToString()));
        return exitCode;
    }
}
