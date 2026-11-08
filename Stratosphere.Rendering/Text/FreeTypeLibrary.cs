using SharpFont;

/*
 * !+!+!+!+!+!+!+!+!+!+!+!+!+!+!+!+!+!+!+!+!+!+!+!+!+!+!+!+!+!+!+!+!+!+!+!+!+!+!+!+!+!+!+!+!
 * ++ **                           URGENT TODO, DO NOT FORGET                          ** ++
 * !+!+!+!+!+!+!+!+!+!+!+!+!+!+!+!+!+!+!+!+!+!+!+!+!+!+!+!+!+!+!+!+!+!+!+!+!+!+!+!+!+!+!+!+!
 *
 * Text currently only works on Linux. On Windows systems, every single attempt to load a font dies RIGHT HERE.
 * Windows players get no text.
 *
 * Missing:
 * `freetype6.dll` as native binary next to executable.
 * On Linux systems, the system provides FreeType.
 * On Windows, not so much.
 * It can't be any FreeType. Stock will not work. SharpFont assumes FreeType integer types (FT_Pos, FT_Long, FT_ULong, F26Dot6, FT_Fixed) are 64 bits on 64-bit platforms.
 * Unfortunately, Microsoft is a fucking psychopath, they are very volatile and very hard to work with.
 * On 64-bit Windows systems, a `long` is still 32-bit. Every single struct would be read at the wrong offsets. Amazing, isn't it? Nature is beautiful.
 * RobustToolbox solves this with a FreeType fork that just makes a two-line header patch that makes things __int64 on Windows. Use that one instead.
 *
 * For the time being, though, Windows players forgot how to read. Please give them lessons on how to read.
 */

internal static class FreeTypeLibrary
{
    /* freetype isn't thread safe, keep it on one thread only */
    private static readonly Lazy<Library> Shared = new(Create, LazyThreadSafetyMode.ExecutionAndPublication);

    public static Library Instance => Shared.Value;

    private static Library Create()
    {
        try { return new Library(); }
        catch (Exception e) when (e is DllNotFoundException or EntryPointNotFoundException or BadImageFormatException)
        { throw new InvalidOperationException(MissingAdvice(), e); }
    }

    internal static string MissingAdvice()
    {
        if (OperatingSystem.IsWindows())
            return "FreeType could not be loaded due to a missing `freetype6.dll`. It must also be the Space Wizards build of FreeType, not stock. "
            + "Refer to the TODO in FreeTypeLibrary.cs. "
            + "Also, this is entirely Microsoft's fault. Blame them.";

        if (OperatingSystem.IsLinux() || OperatingSystem.IsFreeBSD())
        {
            string returnString = "FreeType could not be loaded because libfreetype.so.6 wasn't found. Install it from your package manager "
            + "(Arch: `freetype2`, Debian-based: `libfreetype6`, Fedora: `freetype`).";

            if (OperatingSystem.IsFreeBSD())
                returnString += "\nLook. I have no idea how Stratosphere ended up running on a FreeBSD system, but your operating system reports itself being FreeBSD. "
                + "Congratulations. That is... Incredibly impressive. I didn't think it would reach systems like this. "
                + "However, this also means I have absolutely no clue if this thing works on FreeBSD, so good luck. Try installing FreeType.";

            return returnString;
        }

        return "FreeType could not be loaded, and nothing has been set up for your platform. Sincerely, how did you make it here? Unless you're a mac user. That might do it. "
        + "See the TODO in FreeTypeLibrary.cs, and create an issue on our repository stating your platform.";
    }
}
