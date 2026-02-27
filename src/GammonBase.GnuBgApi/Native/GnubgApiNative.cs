using System;
using System.Reflection;
using System.Runtime.InteropServices;

namespace GammonBase.Gnubg.Native;

internal static partial class GnubgApiNative
{
    private const string LibraryName = "gnubgapi";

    static GnubgApiNative()
    {
        NativeLibrary.SetDllImportResolver(typeof(GnubgApiNative).Assembly, Resolve);
    }

    private static IntPtr Resolve(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
    {
        if (!string.Equals(libraryName, LibraryName, StringComparison.Ordinal))
        {
            return IntPtr.Zero;
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return TryLoad(new[] { "libgnubgapi.dll", "gnubgapi.dll" }, assembly, searchPath);
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return TryLoad(new[] { "libgnubgapi.dylib" }, assembly, searchPath);
        }

        return TryLoad(new[] { "libgnubgapi.so" }, assembly, searchPath);
    }

    private static IntPtr TryLoad(string[] names, Assembly assembly, DllImportSearchPath? searchPath)
    {
        foreach (var name in names)
        {
            if (NativeLibrary.TryLoad(name, assembly, searchPath, out var handle))
            {
                return handle;
            }
        }

        return IntPtr.Zero;
    }

    [LibraryImport(LibraryName, StringMarshalling = StringMarshalling.Utf8)]
    internal static partial IntPtr gnubgapi_get_last_error();

    [LibraryImport(LibraryName, StringMarshalling = StringMarshalling.Utf8)]
    internal static partial IntPtr gnubgapi_create();

    [LibraryImport(LibraryName, StringMarshalling = StringMarshalling.Utf8)]
    internal static partial void gnubgapi_destroy(IntPtr ctx);

    [LibraryImport(LibraryName, StringMarshalling = StringMarshalling.Utf8)]
    internal static partial GnubgApiStatus gnubgapi_init(
        IntPtr ctx,
        string weightsPath,
        string weightsBinaryPath,
        string? dataDir,
        int noBearoff);

    [LibraryImport(LibraryName, StringMarshalling = StringMarshalling.Utf8)]
    internal static partial void gnubgapi_shutdown(IntPtr ctx);

    [LibraryImport(LibraryName, StringMarshalling = StringMarshalling.Utf8)]
    internal static partial GnubgApiStatus gnubgapi_evaluate_position(
        IntPtr ctx,
        string positionId,
        string? matchId,
        out double equity,
        out double cubefulEquity);
}
