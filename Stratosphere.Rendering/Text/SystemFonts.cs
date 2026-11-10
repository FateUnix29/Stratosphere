namespace Stratosphere.Rendering.Text;

public static class SystemFonts
{
    private static readonly Lazy<List<string>> Files = new(ScanFiles);
    public static readonly string[] CommonSans =
    {
        "DejaVuSans.ttf", "NotoSans-Regular.ttf", "LiberationSans-Regular.ttf", "Cantarell-Regular.otf", "segoeui.ttf",
        "arial.ttf", "Arial.ttf", "Helvetica.ttc", "Verdana.ttf"
    };

    public static IReadOnlyList<string> Directories => Candidates().Where(Directory.Exists).ToList();

    public static string? Find(params string[] fileNames)
    {
        var files = Files.Value;
        foreach (var wanted in fileNames)
            foreach (var path in files)
                if (string.Equals(Path.GetFileName(path), wanted, StringComparison.OrdinalIgnoreCase))
                    return path;
        return null;
    }

    public static string Require(params string[] fileNames)
    { return Find(fileNames) ?? throw new FileNotFoundException("SystemFonts couldn't find any basic fonts."); }

    private static List<string> ScanFiles()
    {
        var options = new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true, MatchCasing = MatchCasing.CaseInsensitive };
        var found = new List<string>();
        foreach (var directory in Directories)
            foreach (var pattern in new[] { "*.ttf", "*.otf", "*.ttc" })
                found.AddRange(Directory.EnumerateFiles(directory, pattern, options));
        return found;
    }

    private static IEnumerable<string> Candidates()
    {
        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (OperatingSystem.IsWindows())
        {
            /* windows doesn't function as of me writing this comment due to the freetype issue but i guess we should put the functionality in anyway. */
            yield return Environment.GetFolderPath(Environment.SpecialFolder.Fonts);
            yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "Windows", "Fonts");
            yield break;
        }
        else
        {
            string dataHome = Environment.GetEnvironmentVariable("XDG_DATA_HOME") is { Length: > 0 } xdg ? xdg : Path.Combine(home, ".local", "share");
            yield return Path.Combine(dataHome, "fonts");
            yield return Path.Combine(home, ".fonts");
            string dataDirs = Environment.GetEnvironmentVariable("XDG_DATA_DIRS") is { Length: > 0 } dirs ? dirs : "/usr/local/share:/usr/share";
            foreach (var dir in dataDirs.Split(':', StringSplitOptions.RemoveEmptyEntries)) yield return Path.Combine(dir, "fonts");
        }
        /* macos can kiss my ass, followed by using proton on the windows build of stratosphere-based games */
    }
}
