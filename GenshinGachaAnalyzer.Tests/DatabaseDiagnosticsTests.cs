using GenshinGachaAnalyzer.Core.Helpers;
using GenshinGachaAnalyzer.Core.Services;
using Microsoft.Data.Sqlite;
using Xunit;

namespace GenshinGachaAnalyzer.Tests;

public sealed class DatabaseDiagnosticsTests
{
    [Theory]
    [InlineData(8, "只读")]
    [InlineData(14, "无法打开原数据库")]
    [InlineData(1, "数据库结构")]
    public void DatabaseErrorsDistinguishAccessFromSchema(int code, string description)
    {
        string message = ErrorMessages.Readable(new SqliteException("authkey=SECRET", code));
        Assert.Contains(description, message);
        Assert.DoesNotContain("SECRET", message);
    }

    [Fact]
    public void FailedLogWriteKeepsSanitizedErrorInMemoryAndFlushesOnRetry()
    {
        using var store = new TestDatabase();
        string blocker = Path.Combine(store.DirectoryPath, "log-blocker");
        File.WriteAllText(blocker, "合成权限故障");
        DiagnosticLog.Configure(blocker);
        try
        {
            DiagnosticLog.Error("数据库打开失败", new SqliteException("authkey=SECRET", 14));
            string diagnostic = DiagnosticLog.ReadLatest();
            Assert.Contains("数据库打开失败", diagnostic);
            Assert.Contains("SqliteException", diagnostic);
            Assert.DoesNotContain("SECRET", diagnostic);
            File.Delete(blocker); Directory.CreateDirectory(blocker);
            DiagnosticLog.Info("写入恢复");
            Assert.Null(DiagnosticLog.LastWriteFailure);
            Assert.Contains("数据库打开失败", DiagnosticLog.ReadLatest());
        }
        finally { DiagnosticLog.Configure(Path.Combine(store.DirectoryPath, "unused")); }
    }
}
