using System.Numerics;
using GenshinGachaAnalyzer.Core.Models;

namespace GenshinGachaAnalyzer.Core.Services;

public sealed record MonthlyDraws(string Month, int Count);
public sealed record PersonalReport(int Total, int Gold, int ActiveDays, double? CharacterUpAverage,
    double? WeaponUpAverage, int? ConsecutiveLosses, int? LongestGap, int? CurrentGap,
    DateTime? First, DateTime? Last, IReadOnlyList<MonthlyDraws> Months);

/// <summary>仅统计单个 UID 的已有档案；空档不能证明游戏中没有抽卡。</summary>
public sealed class PersonalAnalysisService(WishCatalogService catalog)
{
    public PersonalReport Analyze(IReadOnlyList<GachaRecord> records, DateTime today)
    {
        var analysis = new AnalysisService(new CatalogBannerProvider(catalog), catalog);
        var characters = analysis.Analyze(records, PoolCatalog.MainPools[0]);
        var weapons = analysis.Analyze(records, PoolCatalog.MainPools[1]);
        var activity = records.Where(r => r.PoolType is "301" or "302" or "500").ToArray();
        var dates = activity.Select(r => r.Time.Date).Distinct().Order().ToArray();
        int? gap = dates.Length < 2 ? null : dates.Zip(dates.Skip(1), (a, b) => Math.Max(0, (b - a).Days - 1)).Max();
        int? streak = null; int losses = 0; bool? previousUp = null;
        foreach (var gold in characters.Golds.Reverse())
        {
            bool? up = catalog.IsCurrentUp(gold.Record);
            if (up is null) { losses = 0; previousUp = null; continue; }
            // 角色大保底后的 UP 不打断小保底连歪；未知排期打断可观察序列。
            if (up == false) { losses++; streak = Math.Max(streak ?? 0, losses); }
            else if (previousUp == true) losses = 0;
            streak ??= 0; previousUp = up;
        }
        var ordered = records.OrderBy(r => r.Time).ThenBy(r => BigInteger.Parse(r.Id)).ToArray();
        return new(records.Count, records.Count(r => catalog.Resolve(r).Rank == "5"), dates.Length,
            characters.UpAverage, weapons.UpAverage, streak, gap,
            dates.Length == 0 ? null : Math.Max(0, (today.Date - dates[^1]).Days),
            ordered.FirstOrDefault()?.Time, ordered.LastOrDefault()?.Time,
            activity.GroupBy(r => r.Time.ToString("yyyy-MM")).OrderBy(g => g.Key).Select(g => new MonthlyDraws(g.Key, g.Count())).ToArray());
    }
}
