using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.IO;
using LauncherTF2.Core;

namespace LauncherTF2.Services;

public class InjectionService : IDisposable
{
    private static InjectionService? _instance;
    public static InjectionService Instance => _instance ??= new InjectionService();

    private const int PROCESS_ALL_ACCESS = 0x1F0FFF;
    private const uint MEM_COMMIT = 0x00001000;
    private const uint MEM_RESERVE = 0x00002000;
    private const uint PAGE_READWRITE = 0x40;
    private const int STILL_ACTIVE = 259;

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(int dwDesiredAccess, bool bInheritHandle, int dwProcessId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr VirtualAllocEx(IntPtr hProcess, IntPtr lpAddress, uint dwSize, uint flAllocationType, uint flProtect);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool WriteProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, uint nSize, out IntPtr lpNumberOfBytesWritten);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GetProcAddress(IntPtr hModule, string lpProcName);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GetModuleHandle(string lpModuleName);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr CreateRemoteThread(IntPtr hProcess, IntPtr lpThreadAttributes, uint dwStackSize, IntPtr lpStartAddress, IntPtr lpParameter, uint dwCreationFlags, out IntPtr lpThreadId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr hObject);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool VirtualFreeEx(IntPtr hProcess, IntPtr lpAddress, int dwSize, uint dwFreeType);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint GetLastError();

    private CancellationTokenSource? _cancellationTokenSource;
    private Task? _monitoringTask;
    private readonly HashSet<int> _injectedProcessIds = new();
    private readonly string _dllPath;
    private bool _disposed;

    public InjectionService()
    {
        _dllPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "Injections", "casual_fix.dll");
    }

    public void StartMonitoring()
    {
        if (_cancellationTokenSource != null && !_cancellationTokenSource.IsCancellationRequested)
        {
            Logger.LogWarning("Injection monitoring is already running");
            return;
        }

        if (!File.Exists(_dllPath))
        {
            Logger.LogError($"DLL not found at: {_dllPath}");
            return;
        }

        _cancellationTokenSource = new CancellationTokenSource();
        _injectedProcessIds.Clear();

        _monitoringTask = Task.Run(() => MonitorLoop(_cancellationTokenSource.Token), _cancellationTokenSource.Token);
        Logger.LogInfo("Injection monitoring started");
    }

    public void StopMonitoring()
    {
        if (_cancellationTokenSource == null)
            return;

        _cancellationTokenSource.Cancel();
        
        try
        {
            _monitoringTask?.Wait(TimeSpan.FromSeconds(5));
        }
        catch (OperationCanceledException)
        {
            // Expected
        }

        _cancellationTokenSource.Dispose();
        _cancellationTokenSource = null;
        _injectedProcessIds.Clear();

        Logger.LogInfo("Injection monitoring stopped");
    }

    private async Task MonitorLoop(CancellationToken cancellationToken)
    {
        Logger.LogDebug("Starting injection monitoring loop");

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await MonitorAndInject(cancellationToken);
                }
                catch (Exception ex)
                {
                    Logger.LogError("Error in injection monitoring loop", ex);
                }

                await Task.Delay(2000, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when cancellation is requested
            Logger.LogInfo("Injection monitoring loop cancelled");
        }
        catch (Exception ex)
        {
            Logger.LogError("Unexpected error in injection monitoring loop", ex);
        }
    }

    private async Task MonitorAndInject(CancellationToken cancellationToken)
    {
        var processes = Process.GetProcessesByName("hl2");
        
        foreach (var proc in processes)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            try
            {
                // Skip if we've already injected into this process
                if (_injectedProcessIds.Contains(proc.Id))
                {
                    // Check if process is still alive
                    if (!IsProcessAlive(proc))
                    {
                        _injectedProcessIds.Remove(proc.Id);
                    }
                    continue;
                }

                Logger.LogInfo($"Found hl2.exe process (PID: {proc.Id}), attempting injection");
                var success = InjectDll(proc.Id);

                if (success)
                {
                    _injectedProcessIds.Add(proc.Id);
                    Logger.LogInfo($"Successfully injected DLL into PID: {proc.Id}");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error injecting into process {proc.Id}", ex);
            }
            finally
            {
                proc.Dispose();
            }
        }
    }

    private bool IsProcessAlive(Process process)
    {
        try
        {
            return !process.HasExited;
        }
        catch
        {
            return false;
        }
    }

    private bool InjectDll(int processId)
    {
        IntPtr hProcess = IntPtr.Zero;
        IntPtr allocMem = IntPtr.Zero;

        try
        {
            hProcess = OpenProcess(PROCESS_ALL_ACCESS, false, processId);
            if (hProcess == IntPtr.Zero)
            {
                var error = GetLastError();
                Logger.LogWarning($"Failed to open process {processId}. Error code: {error}");
                return false;
            }

            byte[] pathBytes = Encoding.Default.GetBytes(_dllPath);
            allocMem = VirtualAllocEx(hProcess, IntPtr.Zero, (uint)pathBytes.Length + 1, MEM_COMMIT | MEM_RESERVE, PAGE_READWRITE);
            
            if (allocMem == IntPtr.Zero)
            {
                var error = GetLastError();
                Logger.LogWarning($"Failed to allocate memory in process {processId}. Error code: {error}");
                return false;
            }

            if (!WriteProcessMemory(hProcess, allocMem, pathBytes, (uint)pathBytes.Length, out var bytesWritten))
            {
                var error = GetLastError();
                Logger.LogWarning($"Failed to write process memory for {processId}. Error code: {error}");
                return false;
            }

            IntPtr loadLibraryAddr = GetProcAddress(GetModuleHandle("kernel32.dll"), "LoadLibraryA");
            if (loadLibraryAddr == IntPtr.Zero)
            {
                var error = GetLastError();
                Logger.LogWarning($"Failed to get LoadLibraryA address. Error code: {error}");
                return false;
            }

            IntPtr hThread = CreateRemoteThread(hProcess, IntPtr.Zero, 0, loadLibraryAddr, allocMem, 0, out var threadId);
            if (hThread == IntPtr.Zero)
            {
                var error = GetLastError();
                Logger.LogWarning($"Failed to create remote thread in process {processId}. Error code: {error}");
                return false;
            }

            CloseHandle(hThread);
            return true;
        }
        catch (Exception ex)
        {
            Logger.LogError($"Exception during DLL injection into process {processId}", ex);
            return false;
        }
        finally
        {
            if (allocMem != IntPtr.Zero)
            {
                VirtualFreeEx(hProcess, allocMem, 0, 0x8000); // MEM_RELEASE
            }

            if (hProcess != IntPtr.Zero)
            {
                CloseHandle(hProcess);
            }
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        StopMonitoring();
        _disposed = true;

        GC.SuppressFinalize(this);
    }

    ~InjectionService()
    {
        Dispose();
    }
}
