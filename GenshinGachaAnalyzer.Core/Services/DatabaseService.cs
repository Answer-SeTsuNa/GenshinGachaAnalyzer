using System.Globalization;
using GenshinGachaAnalyzer.Core.Helpers;
using GenshinGachaAnalyzer.Core.Models;
using Microsoft.Data.Sqlite;

namespace GenshinGachaAnalyzer.Core.Services;

/// <summary>所有连接按操作释放，所有业务写入使用事务。id 是全库唯一键。</summary>
public sealed class DatabaseService(string path)
{
    public string DatabasePath { get; } = Path.GetFullPath(path);
    private SqliteConnection Open()
    {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = DatabasePath, ForeignKeys = true, Pooling = false, DefaultTimeout = 10
        }.ToString());
        try { connection.Open(); return connection; }
        catch { connection.Dispose(); throw; }
    }

    public void Initialize()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(DatabasePath)!);
        using var connection = Open();
        using var check = connection.CreateCommand();
        check.CommandText = "PRAGMA quick_check;";
        if (!string.Equals(check.ExecuteScalar()?.ToString(), "ok", StringComparison.OrdinalIgnoreCase))
            throw new UserFacingException("数据库完整性检查未通过。请先复制整个数据目录；backups 目录内有可用备份，请勿覆盖原文件。");
        using var version = connection.CreateCommand();
        version.CommandText = "PRAGMA user_version;";
        if (Convert.ToInt32(version.ExecuteScalar()) > 1)
            throw new UserFacingException("数据库由较新版本创建，请使用对应版本打开，避免破坏现有数据。");
        using var stream = typeof(DatabaseService).Assembly.GetManifestResourceStream("GenshinGachaAnalyzer.Core.Resources.initialize.sql")!;
        using var reader = new StreamReader(stream);
        using var command = connection.CreateCommand();
        command.CommandText = reader.ReadToEnd();
        command.ExecuteNonQuery();
    }

    public IReadOnlyList<Account> GetAccounts()
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT uid,nickname,last_updated,lang FROM accounts ORDER BY uid";
        using var reader = command.ExecuteReader();
        var result = new List<Account>();
        while (reader.Read()) result.Add(new(reader.GetString(0), NullableString(reader, 1), Parse(reader.GetString(2)), NullableString(reader, 3)));
        return result;
    }

    public IReadOnlyList<GachaRecord> GetRecords(string uid)
    {
        using var connection = Open();
        return ReadRecords(connection, null, uid);
    }

    private static List<GachaRecord> ReadRecords(SqliteConnection connection, SqliteTransaction? transaction, string uid)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT id,uid,gacha_type,item_id,name,item_type,rank_type,count,time,source FROM records WHERE uid=$uid ORDER BY time,length(id),id";
        command.Parameters.AddWithValue("$uid", uid);
        using var reader = command.ExecuteReader();
        var records = new List<GachaRecord>();
        while (reader.Read()) records.Add(ReadRecord(reader));
        return records;
    }

    private static GachaRecord ReadRecord(SqliteDataReader reader) => new()
    {
        Id = reader.GetString(0), Uid = reader.GetString(1), GachaType = reader.GetString(2), ItemId = reader.GetString(3),
        Name = NullableString(reader, 4), ItemType = NullableString(reader, 5), RankType = NullableString(reader, 6),
        Count = NullableString(reader, 7), Time = Parse(reader.GetString(8)), Source = reader.GetString(9)
    };
    private static string? NullableString(SqliteDataReader reader, int index) => reader.IsDBNull(index) ? null : reader.GetString(index);
    private static DateTime Parse(string value) => DateTime.SpecifyKind(DateTime.ParseExact(value, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture), DateTimeKind.Unspecified);

    public WriteReport Insert(IReadOnlyList<GachaRecord> records, IReadOnlyList<AccountSeed>? seeds = null,
        IProgress<ImportProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        // 预先验证全部记录，避免非法输入导致部分入库。
        foreach (var record in records) RecordValidator.Validate(record);
        var accounts = (seeds ?? records.Select(r => new AccountSeed(r.Uid, "zh-cn")).Distinct().ToArray()).ToArray();
        if (accounts.Any(a => !RecordValidator.IsChineseUid(a.Uid))) throw new UserFacingException("只支持国服 UID。");
        using var connection = Open();
        using var transaction = connection.BeginTransaction();
        var now = DateTime.UtcNow.AddHours(8).ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        foreach (var account in accounts)
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = "INSERT INTO accounts(uid,lang,last_updated) VALUES($uid,$lang,$time) ON CONFLICT(uid) DO UPDATE SET last_updated=excluded.last_updated";
            command.Parameters.AddWithValue("$uid", account.Uid);
            command.Parameters.AddWithValue("$lang", (object?)account.Lang ?? DBNull.Value);
            command.Parameters.AddWithValue("$time", now);
            command.ExecuteNonQuery();
        }
        using var insert = connection.CreateCommand();
        insert.Transaction = transaction;
        insert.CommandText = "INSERT INTO records(id,uid,gacha_type,item_id,name,item_type,rank_type,count,time,source) VALUES($id,$uid,$type,$item,$name,$kind,$rank,$count,$time,$source) ON CONFLICT(id) DO NOTHING";
        foreach (var name in new[] { "$id", "$uid", "$type", "$item", "$name", "$kind", "$rank", "$count", "$time", "$source" }) insert.Parameters.Add(name, SqliteType.Text);
        using var lookup = connection.CreateCommand();
        lookup.Transaction = transaction;
        lookup.CommandText = "SELECT id,uid,gacha_type,item_id,name,item_type,rank_type,count,time,source FROM records WHERE id=$id";
        lookup.Parameters.Add("$id", SqliteType.Text);
        int added = 0, duplicates = 0, processed = 0;
        var conflicts = new HashSet<string>();
        var details = new List<RecordConflict>();
        foreach (var record in records)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var values = new object?[] { record.Id, record.Uid, record.GachaType, record.ItemId, record.Name,
                record.ItemType, record.RankType, record.Count, record.Time.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture), record.Source };
            for (int i = 0; i < values.Length; i++) insert.Parameters[i].Value = values[i] ?? DBNull.Value;
            if (insert.ExecuteNonQuery() == 1) added++;
            else
            {
                lookup.Parameters[0].Value = record.Id;
                using var reader = lookup.ExecuteReader();
                if (!reader.Read()) throw new UserFacingException("重复记录查询失败，本批写入将回滚。");
                var saved = ReadRecord(reader);
                if (saved.SameContent(record)) duplicates++;
                else { conflicts.Add(record.Id); details.Add(ConflictLogService.Compare(saved, record)); }
            }
            if (++processed % 200 == 0) progress?.Report(new(processed, records.Count, "事务写入"));
        }
        cancellationToken.ThrowIfCancellationRequested();
        var report = new WriteReport(added, duplicates, conflicts.ToArray()) { Details = details };
        var log = ConflictLogService.Write(DatabasePath, report, records);
        try { transaction.Commit(); }
        catch { ConflictLogService.Finish(log.Path, false); throw; }
        var warning = ConflictLogService.Finish(log.Path, true) ?? log.Warning;
        progress?.Report(new(records.Count, records.Count, "写入完成"));
        return report with { LogPath = log.Path, LogWarning = warning };
    }

    /// <summary>导出采用单个读事务，避免其他进程写入使多账号快照不一致。</summary>
    public IReadOnlyList<(Account Account, IReadOnlyList<GachaRecord> Records)> Snapshot(IEnumerable<string> selectedUids)
    {
        using var connection = Open();
        using var transaction = connection.BeginTransaction(deferred: true);
        var result = new List<(Account, IReadOnlyList<GachaRecord>)>();
        foreach (var uid in selectedUids.Distinct())
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = "SELECT uid,nickname,last_updated,lang FROM accounts WHERE uid=$uid";
            command.Parameters.AddWithValue("$uid", uid);
            Account account;
            using (var reader = command.ExecuteReader())
            {
                if (!reader.Read()) throw new UserFacingException("所选账号不存在，请刷新账号列表。");
                account = new(reader.GetString(0), NullableString(reader, 1), Parse(reader.GetString(2)), NullableString(reader, 3));
            }
            result.Add((account, ReadRecords(connection, transaction, uid)));
        }
        transaction.Commit();
        return result;
    }

    public string CreateBackup()
    {
        string directory = Path.Combine(Path.GetDirectoryName(DatabasePath)!, "backups");
        Directory.CreateDirectory(directory);
        string file = Path.Combine(directory, $"gacha-{DateTime.Now:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}.db");
        // SQLite 备份 API 包含 WAL 中已提交的数据，不直接复制正在使用的主库文件。
        using var source = Open();
        using var target = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = file, Pooling = false }.ToString());
        target.Open();
        source.BackupDatabase(target);
        return file;
    }

    /// <summary>用户明确确认后的整账号清空。备份成功才删除；DDL 和删除都在同一事务中，失败时一起回滚。</summary>
    public ClearAccountReport ClearAccount(string uid, CancellationToken cancellationToken = default)
    {
        if (!RecordValidator.IsChineseUid(uid)) throw new UserFacingException("账号 UID 无效。");
        cancellationToken.ThrowIfCancellationRequested();
        if (!GetAccounts().Any(a => a.Uid == uid)) throw new UserFacingException("该账号已经不存在，请刷新账号列表。");
        string backup = CreateBackup();
        using var connection = Open();
        using var transaction = connection.BeginTransaction();
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        // 临时解除整账号维护操作的删除保护。SQLite 事务内的 DDL 对其他连接不可见，回滚也恢复触发器。
        command.CommandText = "DROP TRIGGER immutable_records_delete;";
        command.ExecuteNonQuery();
        cancellationToken.ThrowIfCancellationRequested();
        command.CommandText = "DELETE FROM records WHERE uid=$uid;";
        command.Parameters.AddWithValue("$uid", uid);
        int deleted = command.ExecuteNonQuery();
        command.CommandText = "DELETE FROM accounts WHERE uid=$uid;";
        command.ExecuteNonQuery();
        command.CommandText = "CREATE TRIGGER immutable_records_delete BEFORE DELETE ON records BEGIN SELECT RAISE(ABORT, '抽卡记录不可删除'); END;";
        command.ExecuteNonQuery();
        cancellationToken.ThrowIfCancellationRequested();
        transaction.Commit();
        return new(uid, deleted, backup);
    }
}
