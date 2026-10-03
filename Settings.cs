using System.Reflection;

namespace NewTemp;

/// <summary>
/// Defines all static, globally accessible settings used by the application.
/// This class serves as the central location for defining settings objects.
/// Define whatever setting you want without needing to specify their string keys manually.
/// </summary>
// Do not define any behaviour (functions or properties) here. Define behaviours in SettingsManager instead.
internal static class Settings
{
    internal const bool IsLogEnabled = true;

    public static readonly Setting<string> Language = new("system");
    public static readonly Setting<bool> ExampleEnabled = new(true);
    public static readonly Setting<uint> ExampleCount = new(20);
    public static readonly Setting<string> ExampleText = new("Example value");
    public static readonly Setting<string> ExampleFileName = new("example.txt");
}
/// <summary>
/// A common interface for all setting types, allowing polymorphic management.
/// This interface exposes only the read-only properties required for general usage.
/// </summary>
internal interface ISetting
{
    /// <summary>
    /// Gets the unique string key associated with the setting.
    /// This property is read-only in the public contract to prevent accidental modification.
    /// </summary>
    string Key { get; }

    /// <summary>
    /// Gets the human-readable string key derived from the camelCase/PascalCase key name.
    /// Used primarily for display in the user interface.
    /// </summary>
    string KeyHumanReadable { get; }

    /// <summary>
    /// Gets the current value of the setting as a generic object.
    /// This allows reading the value without knowing the specific generic type <c>T</c>.
    /// </summary>
    object Value { get; }

}

/// <summary>
/// A restricted interface intended ONLY for the <see cref="SettingsManager"/>.
/// It provides the capability to initialize the setting's key during registration.
/// </summary>
internal interface ISettingSetup
{
    /// <summary>
    /// Gets the unique string key associated with the setting.
    /// This property is read-only in the public contract to prevent accidental modification.
    /// </summary>
    string Key { get; }

    /// <summary>
    /// Initializes the setting's key. This method should only be called once by the <see cref="SettingsManager"/>.
    /// </summary>
    /// <param name="key">The unique key name derived from the field name.</param>
    void InitializeKey(string key);

    /// <summary>
    /// Loads and parses the value from a string representation.
    /// </summary>
    /// <param name="value">The string value to parse.</param>
    void LoadFromStr(string value);

    /// <summary>
    /// Serializes the current value to a string.
    /// </summary>
    /// <returns>The string representation of the value.</returns>
    string Serialize();
}

/// <summary>
/// A strongly-typed, generic class to manage a single configuration setting.
/// It implements <see cref="ISetting"/> for general usage and <see cref="ISettingSetup"/> for initialization.
/// </summary>
/// <typeparam name="T">The underlying data type of the setting (e.g., bool, int, string).</typeparam>
/// <remarks>
/// Initializes a new instance of the <see cref="Setting{T}"/> class with a default value.
/// </remarks>
/// <param name="defaultValue">The fallback value to be used if no configuration is found or parsing fails.</param>
internal class Setting<T>(T defaultValue) : ISetting, ISettingSetup
{
    internal const bool IsLogEnabled = true;
    private string _key = string.Empty;

    /// <summary>
    /// Gets the unique string key used for registration and file storage.
    /// The setter is private to ensure immutability from the outside world.
    /// </summary>
    public string Key
    {
        get => _key;
        private set
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                _key = value;
                // Use SplitCamelCase on assignment to set the human-readable key
                KeyHumanReadable = HelperFunctions.SplitCamelCase(value);
            }
        }
    }

    /// <summary>
    /// Gets the human-readable string key derived from the camelCase/PascalCase key name.
    /// Used primarily for display in the user interface.
    /// </summary>
    public string KeyHumanReadable { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the current loaded value of the setting.
    /// </summary>
    public T Value = defaultValue;

    /// <summary>
    /// Gets the initial default value of the setting.
    /// </summary>
    public readonly T DefaultValue = defaultValue;

    /// <summary>
    /// Explicit implementation of <see cref="ISetting.Value"/>.
    /// returns the generic <see cref="Value"/> field boxed as an object.
    /// </summary>
    object ISetting.Value => Value!;

    /// <summary>
    /// Explicit implementation of <see cref="ISettingSetup.InitializeKey"/>.
    /// This method is hidden from the public class interface and can only be accessed
    /// by casting to <see cref="ISettingSetup"/>, preventing accidental usage.
    /// </summary>
    /// <param name="key">The key to assign.</param>
    /// <exception cref="InvalidOperationException">Thrown if the key has already been initialized.</exception>
    void ISettingSetup.InitializeKey(string key)
    {
        if (!string.IsNullOrEmpty(Key))
        {
            // Fail-safe: Prevent re-assignment if called maliciously or accidentally.
            throw new InvalidOperationException($"Key for setting is already initialized to '{Key}'. Cannot re-assign to '{key}'.");
        }
        Key = key;
    }

    /// <summary>
    /// Attempts to parse and load the setting value from a string read from a configuration file.
    /// It uses <see cref="Convert.ChangeType(object, Type)"/> for type conversion.
    /// </summary>
    /// <param name="value">The string representation of the value to load.</param>
    void ISettingSetup.LoadFromStr(string value)
    {
        try
        {
            Value = (T)Convert.ChangeType(value, typeof(T));
        }
        catch
        {
            LogWarning(value, typeof(T));
        }
    }

    /// <summary>
    /// Serializes the current setting value to its string representation for file writing.
    /// </summary>
    /// <returns>The string representation of the current value, or <see cref="string.Empty"/> if null.</returns>
    string ISettingSetup.Serialize() => Value?.ToString() ?? string.Empty;

    /// <summary>
    /// Allows the <see cref="Setting{T}"/> object to be implicitly cast and used as its underlying type <typeparamref name="T"/>.
    /// </summary>
    /// <param name="setting">The setting object.</param>
    /// <returns>The current <see cref="ISetting.Value"/> of the setting.</returns>
    public static implicit operator T(Setting<T> setting) => setting.Value;

    /// <summary>
    /// Logs a warning message when a value fails to parse or convert to the expected type.
    /// </summary>
    /// <param name="invalidVal">The invalid string value that failed parsing.</param>
    /// <param name="expectedType">The target type that was expected.</param>
    private void LogWarning(string invalidVal, Type expectedType)
    {
        NotificationService.Add($"Config Error: Invalid {expectedType.Name} for {Key}. Value: '{invalidVal}'. Using default.", NotificationType.Warning);
    }
}

/// <summary>
/// Manages the registration, loading, and saving of all application settings.
/// It acts as the exclusive initializer for settings keys via <see cref="ISettingSetup"/>.
/// </summary>
internal static class SettingsManager
{
    internal const bool IsLogEnabled = true;
    /// <summary>
    /// Central registry containing all defined settings objects keyed by their field names.
    /// </summary>
    private static readonly Dictionary<string, ISettingSetup> iSettingSetups = [];

    /// <summary>
    /// Central registry containing all defined settings objects keyed by their field names.
    /// </summary>
    private static readonly Dictionary<string, ISetting> iSettings = [];

    /// <summary>
    /// Retrieves all registered setting objects as an array.
    /// </summary>
    /// <returns>An array containing all registered settings.</returns>
    public static ISetting[] GetAllSettings() => [.. iSettings.Values];

    /// <summary>
    /// The static constructor automatically registers all settings defined in <see cref="Settings"/>.
    /// It uses reflection to discover fields and the <see cref="ISettingSetup"/> interface to assign keys securely.
    /// </summary>
    static SettingsManager()
    {
        var fields = typeof(Settings).GetFields(BindingFlags.Public | BindingFlags.Static);

        foreach (var field in fields)
        {
            object? value = field.GetValue(null);

            // Check if the object implements the setup interface.
            // This is the only place in the code that uses this interface.
            if (value is ISettingSetup setupSetting)
            {
                // Securely initialize the key.
                setupSetting.InitializeKey(field.Name);
                iSettingSetups.Add(field.Name, setupSetting);

                // Register via the read-only interface (ISetting).
                if (value is ISetting setting)
                {
                    iSettings[field.Name] = setting;
                }
            }
        }
    }

    /// <summary>
    /// Loads settings from the configuration file (<see cref="AppConstants.ConfigFileName"/>).
    /// If the file does not exist, it saves the current default settings to create it.
    /// </summary>
    public static void LoadSettings()
    {
        if (!File.Exists(AppConstants.ConfigFileName))
        {
            SaveSettings();
            return;
        }

        foreach (var line in File.ReadAllLines(AppConstants.ConfigFileName))
        {
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith(AppConstants.CommentPrefix)) continue;

            var parts = line.Split(AppConstants.KeyValueSeparator, 2);
            if (parts.Length != 2) continue;

            string key = parts[0].Trim();
            string value = parts[1].Trim();

            if (iSettingSetups.TryGetValue(key, out var setting))
            {
                // Polymorphic call via ISetting interface
                setting.LoadFromStr(value);
            }
        }
    }

    /// <summary>
    /// Serializes all currently registered settings and writes them to the configuration file.
    /// </summary>
    public static void SaveSettings()
    {
        StringBuilder sb = new();
        sb.AppendLine($"{AppConstants.CommentPrefix} {AppConstants.AppTitle} Configuration");

        foreach (var setting in iSettingSetups.Values)
        {
            // Polymorphic call via ISetting interface
            sb.AppendLine($"{setting.Key} {AppConstants.KeyValueSeparator} {setting.Serialize()}");
        }
        string configPath = Path.Combine(AppConstants.ThisExeFolder, AppConstants.ConfigFileName);
        File.WriteAllText(configPath, sb.ToString());
    }
}