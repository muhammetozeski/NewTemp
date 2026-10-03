namespace NewTemp.Localization;

/// <summary>Contains mutable user messages loaded by the localization engine.</summary>
internal static class Strings
{
    const bool IsLogEnabled = true;
    public static string AppTitle = "NewTemp";
    public static string FolderLimit = "{0} klasörü çok büyüdü. {1} aday adı dolu; bazı klasörleri silmeniz gerekiyor.";
    public static string FileLimit = "{0} klasöründe boş bir .txt dosya adı bulunamadı. Bazı dosyaları silmeniz gerekiyor.";
    public static string ExceptionOccurred = "An exception occurred while working in {0}: \"{1}\"";
    public static string InvalidSource = "Kök klasör veya kök klasörü içeren bir klasör kendi içine taşınamaz: {0}";
}
