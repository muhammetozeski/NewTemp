using System.Reflection;

// The test consumes internal settings through reflection, without adding production test hooks.
var assembly = typeof(NewTemp.AppPaths).Assembly;
var settings = assembly.GetType("NewTemp.Settings", true)!;
var manager = assembly.GetType("NewTemp.SettingsManager", true)!;
var configuration = assembly.GetType("NewTemp.Configuration", true)!;
var constants = assembly.GetType("NewTemp.AppConstants", true)!;
string configPath = (string)constants.GetField("ConfigFileName")!.GetValue(null)!;
string defaultsRoot = (string)constants.GetField("DefaultRootDirectory")!.GetRawConstantValue()!;
int assertions = 0;

void Assert(bool condition, string message)
{
    if (!condition) throw new Exception(message);
    assertions++;
}

void Load(string? text)
{
    foreach (var field in settings.GetFields(BindingFlags.Public | BindingFlags.Static))
    {
        var setting = field.GetValue(null)!;
        var value = setting.GetType().GetField("Value")!;
        value.SetValue(setting, setting.GetType().GetField("DefaultValue")!.GetValue(setting));
    }
    if (text is null) File.Delete(configPath);
    else File.WriteAllText(configPath, text);
    manager.GetMethod("LoadSettings")!.Invoke(null, null);
    configuration.GetMethod("Validate")!.Invoke(null, null);
}

T Value<T>(string key)
{
    var setting = settings.GetField(key)!.GetValue(null)!;
    return (T)setting.GetType().GetField("Value")!.GetValue(setting)!;
}

Load(null);
Assert(File.Exists(configPath), "Missing configuration was not created.");
Assert(Value<string>("RootDirectory") == defaultsRoot, "Missing configuration root fallback failed.");
Assert(Value<int>("MaximumIndex") == 10000, "Missing configuration limit fallback failed.");
Assert(File.ReadAllText(configPath).Contains("Language = system"), "Registered settings were not saved.");

foreach (string text in new[] { "", "RootDirectory =", "RootDirectory = relative", "RootDirectory = C:\\bad*path", "RootDirectory = C:\\CON", "RootDirectory = C:\\folder:invalid", "RootDirectory = C:\\bad\0path" })
{
    Load(text);
    Assert(Value<string>("RootDirectory") == defaultsRoot, $"Root fallback failed: {text}");
}

foreach (string text in new[] { "", "MaximumIndex =", "MaximumIndex = broken", "MaximumIndex = 2147483648", "MaximumIndex = 1", "MaximumIndex = 0", "MaximumIndex = -20" })
{
    Load(text);
    Assert(Value<int>("MaximumIndex") == 10000, $"Limit fallback failed: {text}");
}

string customRoot = Path.Combine(NewTemp.AppPaths.Root, "custom sandbox");
foreach (string language in new[] { "", "../invalid", "unknown-language", "de" })
{
    Load($"Language = {language}");
    Assert(Value<string>("Language") == "system", "Invalid language did not fall back to system.");
}
Load($"RootDirectory = {customRoot}\nMaximumIndex = 25\nLanguage = en\nFutureSetting = untouched");
Assert(Value<string>("RootDirectory") == customRoot, "Custom root was not preserved.");
Assert(Value<int>("MaximumIndex") == 25, "Custom limit was not preserved.");
Assert(Value<string>("Language") == "en", "Language preference was not loaded.");
Assert(!Directory.Exists(defaultsRoot) || !Directory.Exists(Path.Combine(defaultsRoot, "custom sandbox")), "Probe wrote into the default root.");

Console.WriteLine($"PASS: {assertions} configuration assertions; the storage root was never created by this probe.");
