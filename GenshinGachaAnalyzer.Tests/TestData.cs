using System.Text.Json;
using GenshinGachaAnalyzer.Core.Models;

namespace GenshinGachaAnalyzer.Tests;

internal static class TestData
{
    public static GachaRecord Record(string id = "1", string type = "301", string? rank = "3", string uid = "100000001") => new()
    {
        Id = id, Uid = uid, GachaType = type, ItemId = "10001", Name = "测试物品", ItemType = "角色", RankType = rank,
        Count = "1", Time = new DateTime(2025, 1, 1, 0, 0, 0), Source = "UIGF导入"
    };
    public static JsonElement Json(string version = "v4.1", string type = "301", string shared = "301", string uid = "100000001", int timezone = 8, bool optional = true)
    {
        var record = new Dictionary<string, object?>
        {
            ["id"] = "1", ["gacha_type"] = type, ["uigf_gacha_type"] = shared, ["item_id"] = "10001", ["time"] = "2025-01-01 00:00:00"
        };
        if (optional) { record["rank_type"] = "5"; record["name"] = "测试物品"; record["item_type"] = "角色"; record["count"] = "1"; }
        return JsonSerializer.SerializeToElement(new
        {
            info = new { version, export_app = "Tests", export_app_version = "1", export_timestamp = 1735689600 },
            hk4e = new[] { new { uid, timezone, lang = "zh-cn", list = new[] { record } } }
        });
    }
    public static List<GachaRecord> Sequence(string type, params int[] intervals)
    {
        var result = new List<GachaRecord>(); int id = 1;
        foreach (int interval in intervals)
            for (int pull = 1; pull <= interval; pull++, id++)
                result.Add(Record(id.ToString(), type, pull == interval ? "5" : "3") with { Time = new DateTime(2025, 1, 1).AddSeconds(id) });
        return result;
    }
}
internal sealed class TestDatabase : IDisposable
{
    public string DirectoryPath { get; } = Path.Combine(Path.GetTempPath(), "GenshinGachaAnalyzer.Tests", Guid.NewGuid().ToString("N"));
    public Core.Services.DatabaseService Database { get; }
    public TestDatabase() { Database = new(Path.Combine(DirectoryPath, "gacha.db")); Database.Initialize(); }
    public void Dispose() { if (Directory.Exists(DirectoryPath)) Directory.Delete(DirectoryPath, true); }
}
