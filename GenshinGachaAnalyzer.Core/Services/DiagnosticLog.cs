using System.Text;
using System.Text.RegularExpressions;

namespace GenshinGachaAnalyzer.Core.Services;

/// <summary>本地诊断日志。不记录祈愿 URL；错误文本中的令牌在落盘前清洗。</summary>
public static partial class DiagnosticLog
{
    private static readonly object Gate = new();
    private static readonly StringBuilder Pending = new();
    public static string? DirectoryPath { get; private set; }
    public static string? CurrentPath => DirectoryPath is { } path ? Path.Combine(path, $"runtime-{DateTime.Today:yyyy-MM-dd}.log") : null;
    public static string? LastWriteFailure { get; private set; }
    public static void Configure(string directory)
    {
        lock (Gate)
        {
            if (!string.Equals(DirectoryPath, directory, StringComparison.OrdinalIgnoreCase))
            { Pending.Clear(); LastWriteFailure = null; }
            DirectoryPath = directory;
        }
    }
    [GeneratedRegex(@"https?://[^\s<>""']+", RegexOptions.IgnoreCase)]
    private static partial Regex Urls();
    [GeneratedRegex(@"(?i)(authkey|stoken|ltoken|cookie|authorization|access_token)(?:%3[dD]|[\\\s""']*[:=][\\\s""']*)[^\s&""'<>\\]+")]
    private static partial Regex Secrets();
    [GeneratedRegex(@"(?im)\b(cookie|authorization)\s*:\s*[^\r\n]+")]
    private static partial Regex Headers();
    public static string Sanitize(string text)
    {
        // 删除整个查询串，也覆盖 URL 编码后的 authkey，避免异常消息泄露凭据。
        text = Urls().Replace(text, match => match.Value.Split('?')[0].Split('#')[0] + " [查询凭据已隐藏]");
        text = Headers().Replace(text, match => match.Groups[1].Value + ":[已隐藏]");
        return Secrets().Replace(text, match => match.Groups[1].Value + "=[已隐藏]");
    }
    public static void Info(string message) => Write("INFO", message);
    public static void Error(string operation, Exception error) => Write("ERROR", operation + Environment.NewLine + error);
    private static void Write(string level, string message)
    {
        lock (Gate)
        {
            string entry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{level}] {Sanitize(message)}\n";
            try
            {
                if (CurrentPath is not { } path) return;
                Directory.CreateDirectory(DirectoryPath!);
                // 日文件超过 8MB 时另存一段，避免开发日志无限增长。
                if (File.Exists(path) && new FileInfo(path).Length > 8 * 1024 * 1024)
                    File.Move(path, path + "." + DateTime.Now.ToString("HHmmssfff"), false);
                // 权限恢复后把此前未能落盘的脱敏诊断一起写入。
                File.AppendAllText(path, Pending.ToString() + entry, Encoding.UTF8);
                Pending.Clear();
                LastWriteFailure = null;
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                LastWriteFailure = "诊断日志无法写入：" + Sanitize(error.Message);
                Pending.Append(entry);
                if (Pending.Length > 64 * 1024) Pending.Remove(0, Pending.Length - 64 * 1024);
            }
        }
    }
    public static string ReadLatest()
    {
        lock (Gate)
        {
            // 无写入权限时仍可在开发者窗口看到本次错误，而非只有一句日志写入失败。
            if (LastWriteFailure is not null) return LastWriteFailure + "\n\n本次未落盘的诊断（仅内存）：\n" + Pending;
            if (CurrentPath is not { } path || !File.Exists(path)) return LastWriteFailure ?? "尚无运行日志。";
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            stream.Seek(Math.Max(0, stream.Length - 256 * 1024), SeekOrigin.Begin);
            using var reader = new StreamReader(stream, Encoding.UTF8);
            return reader.ReadToEnd();
        }
    }
}
