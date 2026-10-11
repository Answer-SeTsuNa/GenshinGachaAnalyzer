using Microsoft.Data.Sqlite;

namespace GenshinGachaAnalyzer.Core.Helpers;

public class UserFacingException(string message) : Exception(message);
public sealed class UnsupportedUigfException(string version) : UserFacingException(
    $"文件版本 {version} 不兼容。本工具支持 UIGF v4.2 / v4.1 / v4.0 / v3.0 / v2.4 / v2.3 / v2.2，请使用支持的标准版本重新导出。");
public sealed class AuthKeyExpiredException() : UserFacingException(
    "祈愿链接已失效。请在游戏内重新打开一次祈愿历史页面，然后点击重试。已保存的数据不受影响。");

public static class ErrorMessages
{
    // 不展示原始异常，尤其禁止显示可能含 authkey 的 HttpRequestException.Message。
    public static string Readable(Exception error) => error switch
    {
        UserFacingException => error.Message,
        SqliteException { SqliteErrorCode: 11 or 26 } => "数据库可能损坏。请保留数据目录中的 gacha.db、gacha.db-wal 和 gacha.db-shm，备份文件位于 backups 子目录。不要覆盖原文件。",
        SqliteException { SqliteErrorCode: 13 } => "磁盘空间不足，本次数据库事务已回滚。请释放数据目录所在磁盘空间后重试。",
        SqliteException { SqliteErrorCode: 5 or 6 } => "数据库正在使用中，请关闭另一个本工具窗口后重试。",
        SqliteException { SqliteErrorCode: 8 } => "数据库当前只读，无法写入。请检查程序运行权限与数据目录权限；已有记录不会被清空。",
        SqliteException { SqliteErrorCode: 14 } => "无法打开原数据库。请检查程序运行权限、数据目录权限及文件占用；读取失败不代表记录已删除。",
        SqliteException { SqliteErrorCode: 1 } => "数据库结构或查询与当前程序不匹配。请查看诊断信息，保留原数据库与备份，不要清空或重新导入。",
        SqliteException sqlite => $"本地数据库操作失败（SQLite {sqlite.SqliteErrorCode}/{sqlite.SqliteExtendedErrorCode}）。请查看诊断信息；未完成的事务不会保留，已有记录不会被清空。",
        UnauthorizedAccessException => "没有文件访问权限。请检查路径权限或选择可写入的目录。",
        IOException => "文件读写失败，请检查可用磁盘空间、路径权限，以及文件是否被其他程序占用。未完成的写入不会替换原文件。",
        OperationCanceledException => "操作已取消或网络超时。已保存的数据保留，可重试。",
        HttpRequestException => "无法连接国服祈愿服务。请检查网络后重试；已保存的数据保留。",
        _ => "操作未完成。请重试或检查文件与数据目录；现有抽卡记录不会被覆盖。"
    };
}
