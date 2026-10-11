using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Principal;

namespace GenshinGachaAnalyzer.WPF.Infrastructure;

/// <summary>只读取进程完整性级别。不会提权，也不会调整数据目录或系统安全设置。</summary>
internal static class ProcessSecurity
{
    public static int? IntegrityLevel()
    {
        using var process = Process.GetCurrentProcess();
        if (!OpenProcessToken(process.Handle, 8, out var token)) return null;
        try
        {
            GetTokenInformation(token, 25, IntPtr.Zero, 0, out int size);
            if (size <= 0) return null;
            var buffer = Marshal.AllocHGlobal(size);
            try
            {
                if (!GetTokenInformation(token, 25, buffer, size, out _)) return null;
                var sid = new SecurityIdentifier(Marshal.ReadIntPtr(buffer)).Value;
                return int.TryParse(sid.Split('-')[^1], out int level) ? level : null;
            }
            finally { Marshal.FreeHGlobal(buffer); }
        }
        finally { CloseHandle(token); }
    }
    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool OpenProcessToken(IntPtr process, uint access, out IntPtr token);
    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetTokenInformation(IntPtr token, int type, IntPtr buffer, int size, out int returned);
    [DllImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr handle);
}
