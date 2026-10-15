using System.Globalization;
using System.Text.Json;
using GenshinGachaAnalyzer.Core.Models;

namespace GenshinGachaAnalyzer.Core.Services;

public sealed record WishItem(string Id, string Name, string Kind, string Rank, string Icon);
public sealed record WishEvent(string Name, string Version, int Order, string Type, DateTimeOffset From,
    DateTimeOffset To, string[] UpGold, string[] UpPurple, string? BannerFile = null, string? BannerUrl = null, string? BannerAlternate = null)
{
    public string Key => $"{Type}:{From:O}";
    public string PeriodText => $"{Version} · 第 {Order} 期";
}
public sealed record ItemSummary(WishItem Item, int Count, DateTime FirstTime, DateTime LastTime,
    IReadOnlyList<GachaRecord> Records);
public sealed record WishHistoryGroup(string Key, string Name, string Pool, string Period, DateTime From,
    DateTime To, WishEvent? Event, IReadOnlyList<GachaRecord> Records);
public sealed record WishCountdown(WishItem Item, IReadOnlyList<WishEvent> Histories, int Days, bool IsActive);

/// <summary>内置快照离线兜底；经验证的联网数据只更新分析资料，不修改原始祈愿记录。</summary>
public sealed class WishCatalogService
{
    public const string SnapshotCommit = "b3aef3dbe0299e512654bb296930b3b6571fc87f";
    public const string SourceUrl = "https://github.com/SnapHutaoRemasteringProject/Snap.Metadata";
    private Dictionary<string, WishItem> _items;
    public IReadOnlyList<WishItem> Items => _items.Values.ToArray();
    public IReadOnlyList<WishEvent> Events { get; private set; }
    public DateTimeOffset CoveredThrough => Events.Count == 0 ? DateTimeOffset.MinValue : Events.Max(e => e.To);
    public DateTimeOffset? UpdatedAt { get; private set; }
    public string CoverageText => $"排期截至 {CoveredThrough:yyyy-MM-dd} · {(UpdatedAt is null ? "内置资料" : "已缓存在线资料")} · 不预测复刻";
    public WishCatalogService() : this(Read<WishItem>("items.json"), Read<WishEvent>("wish-events.json")) { }
    public WishCatalogService(IEnumerable<WishItem> items, IEnumerable<WishEvent> events)
    {
        _items = items.ToDictionary(i => i.Id);
        Events = events.Where(e => e.Type is "301" or "400" or "302" or "500").OrderBy(e => e.From).ToArray();
    }
    public void Apply(CatalogSnapshot snapshot)
    {
        WishCatalogUpdateService.Validate(snapshot);
        if (snapshot.Events.Max(e => e.To) < CoveredThrough) throw new InvalidDataException("卡池资料覆盖范围倒退。");
        _items = snapshot.Items.ToDictionary(i => i.Id);
        Events = snapshot.Events.OrderBy(e => e.From).ToArray(); UpdatedAt = snapshot.CheckedAt;
    }
    private static T[] Read<T>(string name)
    {
        using var stream = typeof(WishCatalogService).Assembly.GetManifestResourceStream("GenshinGachaAnalyzer.Core.Resources." + name)!;
        return JsonSerializer.Deserialize<T[]>(stream, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? [];
    }
    public WishItem Resolve(GachaRecord record)
    {
        var item = FindItem(record);
        return new(item?.Id ?? record.ItemId, item?.Name ?? (string.IsNullOrWhiteSpace(record.Name) ? $"物品 {record.ItemId}" : record.Name), item?.Kind ?? record.ItemType ?? "未知类型",
            record.RankType is "3" or "4" or "5" ? record.RankType : item?.Rank ?? "未知", item?.Icon ?? "");
    }
    private WishItem? FindItem(GachaRecord record) => _items.GetValueOrDefault(record.ItemId) ??
        (string.IsNullOrEmpty(record.ItemId) && record.Name is not null ? _items.Values.FirstOrDefault(i => i.Name == record.Name) : null);
    public WishItem? FindByName(string name) => _items.Values.FirstOrDefault(i => i.Name == name);
    public WishItem? FindById(string id) => _items.GetValueOrDefault(id);
    public WishEvent? FindEvent(GachaRecord record) => Events.FirstOrDefault(e => e.Type == record.GachaType && e.From.DateTime <= record.Time && record.Time <= e.To.DateTime);
    // 名单来源：胡桃 AvatarIds / WeaponIds。是否当期 UP 必须先查排期，不能把常驻身份当作非 UP。
    public static readonly HashSet<string> StandardAvatars = ["10000003", "10000016", "10000035", "10000041", "10000042", "10000069", "10000079", "10000109"];
    public static readonly HashSet<string> StandardGoldWeapons = ["11501", "11502", "12501", "12502", "13502", "13505", "14501", "14502", "15501", "15502"];
    public bool? IsCurrentUp(GachaRecord record)
    {
        var item = Resolve(record); if (item.Rank != "5" || record.PoolType is not ("301" or "302")) return null;
        var period = FindEvent(record); if (period is null) return null;
        if (period.UpGold.Contains(item.Id)) return true;
        if (record.PoolType == "301" && StandardAvatars.Contains(item.Id) || record.PoolType == "302" && StandardGoldWeapons.Contains(item.Id)) return false;
        return null; // 非当期名单且非已知常驻候选，不能擅自判为歪。
    }
    public string DescribeStatus(GachaRecord record)
    {
        var item = Resolve(record);
        string kind = StandardAvatars.Contains(item.Id) ? "常驻角色" : StandardGoldWeapons.Contains(item.Id) ? "常驻武器" : "";
        if (record.PoolType == "500") return "集录候选 · " + (kind.Length > 0 ? kind + " · " : "") + "命定未知";
        if (record.PoolType is "200" or "100") return kind.Length > 0 ? kind : "常驻祈愿";
        return IsCurrentUp(record) switch { true => kind.Length > 0 ? "当期 UP · " + kind : "当期 UP", false => kind + " · 非 UP", _ => kind.Length > 0 ? kind + " · UP 未知" : "UP 排期未覆盖" };
    }
    // 仅生成分析副本：按物品 ID 读取胡桃元数据，数据库与标准导出仍保留原始字段。
    public GachaRecord ForAnalysis(GachaRecord record)
    {
        var item = Resolve(record);
        return record with { RankType = item.Rank == "未知" ? record.RankType : item.Rank,
            Name = item.Name, ItemType = item.Kind };
    }
    public WishItem Resolve(string id, string? rank = null) => _items.GetValueOrDefault(id) ?? new(id, $"物品 {id}", "未知类型", rank ?? "未知", "");
    public IReadOnlyList<ItemSummary> Summarize(IEnumerable<GachaRecord> records, string? kind = null) => records
        .GroupBy(r => (Resolve(r).Id, Resolve(r).Name, Resolve(r).Kind, Resolve(r).Rank))
        .Select(g => new ItemSummary(Resolve(g.First()), g.Count(), g.Min(r => r.Time), g.Max(r => r.Time), g.OrderByDescending(r => r.Time).ToArray()))
        .Where(s => kind is null || s.Item.Kind == kind).OrderByDescending(s => s.Count).ThenByDescending(s => s.LastTime).ThenBy(s => s.Item.Id).ToArray();
    public IReadOnlyList<WishHistoryGroup> BuildHistory(IEnumerable<GachaRecord> records)
    {
        // 原始池 301 / 400 分开匹配排期；未覆盖的期次不猜测，按月份归档并明确说明。
        var matched = records.Where(r => r.PoolType is "301" or "302" or "500").Select(r => (Record: r, Event: FindEvent(r))).ToArray();
        var populated = matched.GroupBy(x => x.Event?.Key ?? $"unknown:{x.Record.GachaType}:{x.Record.Time:yyyy-MM}").Select(g =>
        {
            var first = g.First(); var period = first.Event;
            var saved = g.Select(x => x.Record).OrderByDescending(r => r.Time).ThenByDescending(r => r.Id.Length).ThenByDescending(r => r.Id, StringComparer.Ordinal).ToArray();
            string pool = PoolCatalog.NameOf(first.Record.PoolType);
            return new WishHistoryGroup(g.Key, period?.Name ?? pool, pool,
                period?.PeriodText ?? first.Record.Time.ToString("yyyy 年 MM 月", CultureInfo.InvariantCulture) + (first.Record.PoolType is "200" or "100" ? " · 按月归档" : " · 排期未覆盖"),
                period?.From.DateTime ?? saved.Min(r => r.Time), period?.To.DateTime ?? saved.Max(r => r.Time), period, saved);
        }).ToArray();
        var occupied = populated.Select(g => g.Key).ToHashSet();
        return populated.Concat(Events.Where(e => !occupied.Contains(e.Key)).Select(e =>
            new WishHistoryGroup(e.Key, e.Name, PoolCatalog.NameOf(e.Type), e.PeriodText, e.From.DateTime, e.To.DateTime, e, [])))
            .OrderByDescending(g => g.From).ThenBy(g => g.Key, StringComparer.Ordinal).ToArray();
    }
    public IReadOnlyList<WishCountdown> BuildCountdown(DateTimeOffset now)
    {
        var excludedAvatars = StandardAvatars.Concat(new[] { "10000006", "10000015", "10000021", "10000062" }).ToHashSet();
        // 同一期双角色池共享四星 UP，按物品和起止时间合并，不将它计为两次复刻。
        return Events.Where(e => e.From <= now)
            .SelectMany(e => e.UpGold.Select(id => (Id: id, Rank: "5", Event: e)).Concat(e.UpPurple.Select(id => (Id: id, Rank: "4", Event: e))))
            .Where(x => !excludedAvatars.Contains(x.Id) && !(x.Rank == "5" && StandardGoldWeapons.Contains(x.Id)))
            .GroupBy(x => x.Id).Select(g =>
            {
                var periods = g.Select(x => x.Event).DistinctBy(e => (e.From, e.To)).OrderByDescending(e => e.From).ToArray();
                var last = periods[0]; bool active = last.From <= now && now <= last.To;
                // 过期快照只计至覆盖边界，不能宣称未覆盖时间内没有复刻。
                var observed = now < CoveredThrough ? now : CoveredThrough;
                int days = (int)Math.Floor((active ? last.To - now : observed - last.To).TotalDays);
                return new WishCountdown(Resolve(g.Key, g.First().Rank), periods, Math.Max(0, days), active);
            }).OrderByDescending(c => !c.IsActive).ThenByDescending(c => c.Days).ThenBy(c => c.Item.Name).ToArray();
    }
}
