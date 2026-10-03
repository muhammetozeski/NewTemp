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
        {
            Console.Error.WriteLine(string.Format(Strings.OperationFailed, Logger.LogFileName));
            return 2;
        }

        var result = RunSafely(() => NewTempService.Execute(InputParser.Read(args)));
        if (result.Succeeded)
        {
            Console.WriteLine(result.Value);
            return 0;
        }

        Console.Error.WriteLine(result.Error is RequestException request
            ? request.Message
            : string.Format(Strings.OperationFailed, Logger.LogFileName));
        return result.Error is RequestException failure ? failure.ExitCode : 2;
    }
}
