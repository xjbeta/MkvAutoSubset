using System;
using System.Runtime.InteropServices;
using System.Text.Json;

public static class mkvlib
{
#if OSX
    const string so = "mkvlib_osx.so";
#endif
#if Linux
    const string so = "mkvlib_linux.so";
#endif
#if Windows
    const string so = "mkvlib_windows.so";
#endif

    #region imports

    [DllImport(so)]
    static extern bool InitInstance(logCallback lcb);

    [DllImport(so)]
    static extern IntPtr GetMKVInfo(IntPtr ptr);

    [DllImport(so)]
    static extern bool DumpMKV(IntPtr file, IntPtr output, bool subset, logCallback lcb);

    [DllImport(so)]
    static extern IntPtr CheckSubset(IntPtr file, logCallback lcb);

    [DllImport(so)]
    static extern bool CreateMKV(IntPtr file, IntPtr tracks, IntPtr attachments, IntPtr output, IntPtr slang,
        IntPtr stitle, bool clean);

    [DllImport(so)]
    static extern bool ASSFontSubset(IntPtr files, IntPtr fonts, IntPtr output, bool dirSafe, logCallback lcb);

    [DllImport(so)]
    static extern IntPtr QueryFolder(IntPtr dir, logCallback lcb);

    [DllImport(so)]
    static extern bool DumpMKVs(IntPtr dir, IntPtr output, bool subset, logCallback lcb);

    [DllImport(so)]
    static extern bool CreateMKVs(IntPtr vDir, IntPtr sDir, IntPtr fDir, IntPtr tDir, IntPtr oDir, IntPtr slang,
        IntPtr stitle, bool clean, logCallback lcb);

    [DllImport(so)]
    static extern bool MakeMKVs(IntPtr dir, IntPtr data, IntPtr output, IntPtr slang, IntPtr stitle, logCallback lcb);

    #endregion

    public static bool InitInstance(Action<string> lcb)
    {
        return InitInstance(_lcb(lcb));
    }

    public static string GetMKVInfo(string file)
    {
        return css(GetMKVInfo(cs(file)));
    }

    public static bool DumpMKV(string file, string output, bool subset, Action<string> lcb)
    {
        return DumpMKV(cs(file), cs(output), subset, _lcb(lcb));
    }

    public static bool[] CheckSubset(string file, Action<string> lcb)
    {
        string json = css(CheckSubset(cs(file), _lcb(lcb)));
        JsonDocument doc = JsonDocument.Parse(json);
        bool[] result = new bool[2];
        result[0] = doc.RootElement.GetProperty("subsetted").GetBoolean();
        result[1] = doc.RootElement.GetProperty("error").GetBoolean();
        return result;
    }

    public static bool CreateMKV(string file, string[] tracks, string[] attachments, string output, string slang,
        string stitle, bool clean)
    {
        string _tracks = JsonSerializer.Serialize<string[]>(tracks);
        string _attachments = JsonSerializer.Serialize<string[]>(attachments);
        return CreateMKV(cs(file), cs(_tracks), cs(_attachments), cs(output), cs(slang), cs(stitle), clean);
    }

    public static bool ASSFontSubset(string[] files, string fonts, string output, bool dirSafe, Action<string> lcb)
    {
        string _files = JsonSerializer.Serialize<string[]>(files);
        return ASSFontSubset(cs(_files), cs(fonts), cs(output), dirSafe, _lcb(lcb));
    }

    public static string[] QueryFolder(string dir, Action<string> lcb)
    {
        string result = css(QueryFolder(cs(dir), _lcb(lcb)));
        return JsonSerializer.Deserialize<string[]>(result);
    }

    public static bool DumpMKVs(string dir, string output, bool subset, Action<string> lcb)
    {
        return DumpMKVs(cs(dir), cs(output), subset, _lcb(lcb));
    }

    public static bool CreateMKVs(string vDir, string sDir, string fDir, string tDir, string oDir, string slang,
        string stitle, bool clean, Action<string> lcb)
    {
        return CreateMKVs(cs(vDir), cs(sDir), cs(fDir), cs(tDir), cs(oDir), cs(slang), cs(stitle), clean, _lcb(lcb));
    }

    public static bool MakeMKVs(string dir, string data, string output, string slang, string stitle, Action<string> lcb)
    {
        return MakeMKVs(cs(dir), cs(data), cs(output), cs(slang), cs(stitle), _lcb(lcb));
    }


    delegate void logCallback(IntPtr ptr);

    static logCallback _lcb(Action<string> lcb)
    {
        return (ptr) =>
        {
            if (lcb != null)
                lcb(css(ptr));
        };
    }

    static IntPtr cs(string str)
    {
        return Marshal.StringToCoTaskMemUTF8(str);
    }

    static string css(IntPtr ptr)
    {
        return Marshal.PtrToStringUTF8(ptr);
    }
}