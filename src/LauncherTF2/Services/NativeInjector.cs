using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace LauncherTF2.Services;

/// <summary>
/// P/Invoke wrapper for XenosNative.dll — handles DLL injection into a target process.
/// </summary>
internal static class NativeInjector
{
    [DllImport("XenosNative.dll", EntryPoint = "Xenos_InjectByPid",
        CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl)]
    private static extern int Xenos_InjectByPid(uint pid, string dllPath);

    /// <summary>
    /// Injects a DLL into the target process off the UI thread.
    /// Returns 0 on success; negative values indicate specific failures.
    /// </summary>
    public static Task<int> InjectAsync(Process target, string dllPath)
    {
        if (!Environment.Is64BitProcess)
            throw new PlatformNotSupportedException("Launcher must run as x64 to inject into x64 TF2.");

        if (target == null || target.HasExited)
            throw new ArgumentException("Target process is invalid or has already exited.");

        if (!File.Exists(dllPath))
            throw new FileNotFoundException("Injection DLL not found.", dllPath);

        return Task.Run(() =>
        {
            try
            {
                return Xenos_InjectByPid((uint)target.Id, dllPath);
            }
            catch (DllNotFoundException)
            {
                return -1000; // XenosNative.dll missing from output
            }
            catch (Exception)
            {
                return -1001; // Unexpected native call failure
            }
        });
    }

    /// <summary>
    /// Translates a numeric return code from XenosNative into a human-readable message.
    /// </summary>
    public static string TranslateReturnCode(int code) => code switch
    {
        0 => "Injection succeeded",
        -1 => "Invalid arguments (pid or path null)",
        -2 => "OpenProcess failed (access denied or bad PID)",
        -3 => "VirtualAllocEx failed",
        -4 => "WriteProcessMemory failed",
        -5 => "GetModuleHandle(kernel32) failed",
        -6 => "GetProcAddress(LoadLibraryW) failed",
        -7 => "CreateRemoteThread failed",
        -8 => "LoadLibraryW failed inside target process",
        -1000 => "XenosNative.dll not found in launcher directory",
        -1001 => "Unexpected exception during native call",
        _ => $"Unknown error code: 0x{code:X}"
    };
}
