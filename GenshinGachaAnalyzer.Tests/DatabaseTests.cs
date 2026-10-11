using GenshinGachaAnalyzer.Core.Helpers;
using GenshinGachaAnalyzer.Core.Models;
using GenshinGachaAnalyzer.Core.Services;
using Microsoft.Data.Sqlite;
using Xunit;

namespace GenshinGachaAnalyzer.Tests;

public sealed class DatabaseTests
{
    [Fact]
    public void DuplicateImportDoesNotOverwriteSource()
    {
        using var store = new TestDatabase();
        var original = TestData.Record() with { Source = "游戏获取" };
        Assert.Equal(1, store.Database.Insert([original]).Added);
        var result = store.Database.Insert([original with { Source = "UIGF导入" }]);
        Assert.Equal(1, result.Duplicates); Assert.Equal(0, result.Conflicts);
        Assert.Equal("游戏获取", Assert.Single(store.Database.GetRecords(original.Uid)).Source);
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MissingFieldsAreCompatibleWithoutChangingEitherSource(bool missingFirst)
    {
        using var store = new TestDatabase();
        var full = TestData.Record() with { Source = "游戏获取" };
        var missing = full with { ItemId = "", Name = null, ItemType = null, RankType = null, Count = null, Source = "UIGF导入" };
        var saved = missingFirst ? missing : full;
        store.Database.Insert([saved]);
        var result = store.Database.Insert([missingFirst ? full : missing]);
        Assert.Equal(0, result.Added); Assert.Equal(1, result.Duplicates); Assert.Equal(0, result.Conflicts);
        Assert.Empty(result.Details);
        Assert.Equal(saved, Assert.Single(store.Database.GetRecords(saved.Uid)));
    }
    [Theory]
    [InlineData("item")]
    [InlineData("time")]
    [InlineData("pool")]
    [InlineData("rank")]
    public void KnownContradictionsRemainConflictsWhenOtherFieldsAreMissing(string field)
    {
        using var store = new TestDatabase(); var saved = TestData.Record(); store.Database.Insert([saved]);
        var incoming = saved with { Name = null, Count = null };
        incoming = field switch
        {
            "item" => incoming with { ItemId = "10002" },
            "time" => incoming with { Time = incoming.Time.AddSeconds(1) },
            "pool" => incoming with { GachaType = "200" },
            _ => incoming with { RankType = "5" }
        };
        var result = store.Database.Insert([incoming]);
        Assert.Equal(0, result.Duplicates); Assert.Equal(1, result.Conflicts);
        Assert.Single(Assert.Single(result.Details).Differences);
        Assert.Equal(saved, Assert.Single(store.Database.GetRecords(saved.Uid)));
    }
    [Fact]
    public void ConflictLogListsContradictionsAndExcludesMissingFields()
    {
        using var store = new TestDatabase(); var saved = TestData.Record(); store.Database.Insert([saved]);
        var result = store.Database.Insert([saved with { ItemId = "10002", Name = null, Count = null }]);
        var log = File.ReadAllText(result.LogPath!);
        Assert.Contains("物品 ID | 已有=", log);
        Assert.DoesNotContain("名称 | 已有=", log); Assert.DoesNotContain("数量 | 已有=", log);
        Assert.Equal("物品 ID", Assert.Single(Assert.Single(result.Details).Differences).Field);
    }
    [Fact]
    public void ConflictingIdKeepsExistingDataEvenAcrossAccounts()
    {
        using var store = new TestDatabase();
        var original = TestData.Record(); store.Database.Insert([original]);
        var result = store.Database.Insert([original with { Uid = "200000001", Name = "不同内容" }]);
        Assert.Equal("1", Assert.Single(result.ConflictIds));
        Assert.Equal(original, Assert.Single(store.Database.GetRecords(original.Uid)));
        Assert.Empty(store.Database.GetRecords("200000001"));
    }
    [Fact]
    public void InvalidBatchAddsNothing()
    {
        using var store = new TestDatabase();
        Assert.Throws<UserFacingException>(() => store.Database.Insert([TestData.Record(), TestData.Record("invalid")]));
        Assert.Empty(store.Database.GetAccounts());
    }
    [Fact]
    public void CancellationRollsBackAccountsAndRecords()
    {
        using var store = new TestDatabase(); using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        Assert.Throws<OperationCanceledException>(() => store.Database.Insert([TestData.Record()], cancellationToken: cancellation.Token));
        Assert.Empty(store.Database.GetAccounts()); Assert.Empty(store.Database.GetRecords("100000001"));
    }
    [Fact]
    public void ForeignKeyFailureRollsBackWholeBatch()
    {
        using var store = new TestDatabase();
        Assert.Throws<SqliteException>(() => store.Database.Insert([TestData.Record(), TestData.Record("2", uid: "200000001")], [new AccountSeed("100000001", "zh-cn")]));
        Assert.Empty(store.Database.GetRecords("100000001")); Assert.Empty(store.Database.GetAccounts());
    }
    [Theory]
    [InlineData("UPDATE records SET name='篡改'")]
    [InlineData("DELETE FROM records")]
    public void DatabaseTriggersRejectMutation(string sql)
    {
        using var store = new TestDatabase(); store.Database.Insert([TestData.Record()]);
        using var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = store.Database.DatabasePath, Pooling = false }.ToString()); connection.Open();
        using var command = connection.CreateCommand(); command.CommandText = sql;
        Assert.Throws<SqliteException>(() => command.ExecuteNonQuery());
        Assert.Equal("测试物品", Assert.Single(store.Database.GetRecords("100000001")).Name);
    }
    [Fact]
    public void BackupIncludesCommittedWalRecords()
    {
        using var store = new TestDatabase(); store.Database.Insert([TestData.Record()]);
        var backup = new DatabaseService(store.Database.CreateBackup()); backup.Initialize();
        Assert.Equal(TestData.Record(), Assert.Single(backup.GetRecords("100000001")));
    }
    [Fact]
    public void ClearAccountBacksUpAndAllowsFreshImportWithoutTouchingOtherAccounts()
    {
        using var store = new TestDatabase();
        var first = TestData.Record(); var other = TestData.Record("2", uid: "200000001");
        store.Database.Insert([first, other]);
        var result = store.Database.ClearAccount(first.Uid);
        Assert.Equal(1, result.Deleted); Assert.True(File.Exists(result.BackupPath));
        Assert.Empty(store.Database.GetRecords(first.Uid));
        Assert.Equal(other, Assert.Single(store.Database.GetRecords(other.Uid)));
        Assert.Equal(other.Uid, Assert.Single(store.Database.GetAccounts()).Uid);
        var backup = new DatabaseService(result.BackupPath); backup.Initialize();
        Assert.Equal(first, Assert.Single(backup.GetRecords(first.Uid)));
        Assert.Equal(1, store.Database.Insert([first]).Added);
        Assert.Equal(1, store.Database.Insert([first]).Duplicates);
        using var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = store.Database.DatabasePath, Pooling = false }.ToString()); connection.Open();
        using var command = connection.CreateCommand(); command.CommandText = "DELETE FROM records";
        Assert.Throws<SqliteException>(() => command.ExecuteNonQuery());
        Assert.Equal(first, Assert.Single(store.Database.GetRecords(first.Uid)));
    }
    [Fact]
    public void CancelledClearDoesNotDeleteAnyRecords()
    {
        using var store = new TestDatabase(); store.Database.Insert([TestData.Record()]);
        using var cancel = new CancellationTokenSource(); cancel.Cancel();
        Assert.Throws<OperationCanceledException>(() => store.Database.ClearAccount("100000001", cancel.Token));
        Assert.Single(store.Database.GetAccounts()); Assert.Single(store.Database.GetRecords("100000001"));
    }
    [Fact]
    public void BackupFailurePreventsClear()
    {
        using var store = new TestDatabase(); store.Database.Insert([TestData.Record()]);
        File.WriteAllText(Path.Combine(store.DirectoryPath, "backups"), "阻止备份目录创建");
        Assert.Throws<IOException>(() => store.Database.ClearAccount("100000001"));
        Assert.Single(store.Database.GetRecords("100000001"));
    }
    [Fact]
    public void ClearingAbsentAccountFailsWithoutCreatingAccount()
    {
        using var store = new TestDatabase();
        Assert.Throws<UserFacingException>(() => store.Database.ClearAccount("100000001"));
        Assert.Empty(store.Database.GetAccounts());
    }
    [Fact]
    public void AccountsRemainIndependentAndSnapshotIncludesOnlySelected()
    {
        using var store = new TestDatabase(); store.Database.Insert([TestData.Record(), TestData.Record("2", uid: "200000001")]);
        Assert.Equal(2, store.Database.GetAccounts().Count);
        var snapshot = store.Database.Snapshot(["200000001"]);
        Assert.Equal("200000001", Assert.Single(snapshot).Account.Uid); Assert.Equal("2", Assert.Single(snapshot[0].Records).Id);
    }
    [Fact]
    public void CorruptDatabaseIsNotRecreated()
    {
        string directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")); Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, "broken.db"); File.WriteAllText(path, "not a database");
        try
        {
            Assert.Throws<SqliteException>(() => new DatabaseService(path).Initialize());
            Assert.Equal("not a database", File.ReadAllText(path));
        }
        finally { Directory.Delete(directory, true); }
    }
}
