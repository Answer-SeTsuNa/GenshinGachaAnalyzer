using System.Text.Json;
using GenshinGachaAnalyzer.Core.Models;

namespace GenshinGachaAnalyzer.Core.Services;

public sealed record BannerPeriod(string Type, DateTime Start, DateTime End, string[] UpItemIds, string[] EligibleItemIds, string SourceUrl);
public interface IBannerProvider
{
    /// <summary>只有经审核、覆盖完整五星候选集合的排期才能判定；null 表示未知。</summary>
    bool? IsUp(GachaRecord record);
}
/// <summary>从参考项目的离线排期判定角色/武器 UP。集录选择和武器定轨不参与推导。</summary>
public sealed class CatalogBannerProvider(WishCatalogService? catalog = null) : IBannerProvider
{
    private readonly WishCatalogService _catalog = catalog ?? new();
    public bool? IsUp(GachaRecord record) => _catalog.IsCurrentUp(record);
}
public sealed class EmbeddedBannerProvider : IBannerProvider
{
    private readonly IReadOnlyList<BannerPeriod> _periods;
    public EmbeddedBannerProvider()
    {
        using var stream = typeof(EmbeddedBannerProvider).Assembly.GetManifestResourceStream("GenshinGachaAnalyzer.Core.Resources.banners.json")!;
        _periods = JsonSerializer.Deserialize<List<BannerPeriod>>(stream, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? [];
    }
    public EmbeddedBannerProvider(IReadOnlyList<BannerPeriod> periods) => _periods = periods;
    public bool? IsUp(GachaRecord record)
    {
        // 集录命定选择不在原始祈愿记录中，不能把卡池候选名单当作玩家选择。
        if (record.PoolType != "301" || record.RankType != "5" || string.IsNullOrWhiteSpace(record.ItemId)) return null;
        var period = _periods.SingleOrDefault(p => p.Type == record.GachaType && p.Start <= record.Time && record.Time < p.End);
        if (period is null || string.IsNullOrWhiteSpace(period.SourceUrl) || !period.EligibleItemIds.Contains(record.ItemId)) return null;
        return period.UpItemIds.Contains(record.ItemId);
    }
}
