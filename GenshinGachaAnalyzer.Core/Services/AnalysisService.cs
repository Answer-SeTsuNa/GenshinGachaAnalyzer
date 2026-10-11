using System.Numerics;
using GenshinGachaAnalyzer.Core.Models;

namespace GenshinGachaAnalyzer.Core.Services;

public sealed class AnalysisService(IBannerProvider banners, WishCatalogService? catalog = null)
{
    private readonly WishCatalogService _catalog = catalog ?? new();
    public PoolAnalysis Analyze(IEnumerable<GachaRecord> allRecords, PoolDefinition pool)
    {
        var accountRecords = allRecords.Select(_catalog.ForAnalysis).ToArray();
        var records = accountRecords.Where(r => r.PoolType == pool.Type).OrderBy(r => r.Time).ThenBy(r => BigInteger.Parse(r.Id)).ToArray();
        var golds = new List<FiveStarEntry>();
        bool havePreviousGold = false, intervalKnown = true;
        int pulls = 0;
        foreach (var record in records)
        {
            pulls++;
            if (record.RankType is not ("3" or "4" or "5")) intervalKnown = false;
            if (record.RankType != "5") continue;
            // 与胡桃 TypedWishSummaryBuilder 一致：从档案第一条开始累计，首个五星也计入统计。
            int? completePulls = intervalKnown && pulls <= pool.HardPity ? pulls : null;
            bool? up = banners.IsUp(record);
            string upText = pool.Type switch
            {
                "301" => up switch { true => "当期 UP", false => "常驻角色 · 非 UP", null => "UP 信息未覆盖" },
                "302" => up switch { true => "当期 UP", false => "常驻武器 · 非 UP", null => "UP 信息未覆盖" },
                "500" => "当期选择未知",
                _ => _catalog.DescribeStatus(record)
            };
            golds.Add(new(record, completePulls, upText, null) { ObservedPulls = !havePreviousGold && intervalKnown && pulls <= pool.HardPity ? pulls : null });
            havePreviousGold = true; intervalKnown = true; pulls = 0;
        }
        var complete = golds.Where(g => g.Pulls.HasValue).Select(g => g.Pulls!.Value).ToArray();
        // 同值使用平均秩百分位，越低表示抽数越少；仅在至少 10 次可统计出金时显示。
        var personalIntervals = PoolCatalog.MainPools.SelectMany(p => CompleteIntervals(accountRecords, p)).ToArray();
        if (personalIntervals.Length >= 10)
            golds = golds.Select(g => g.Pulls is { } n ? g with
            { Percentile = 100d * (personalIntervals.Count(v => v < n) + 0.5 * personalIntervals.Count(v => v == n)) / personalIntervals.Length } : g).ToList();
        int? pity = records.Length == 0 || !intervalKnown ? null : pulls;
        bool lowerBound = false;
        if (pity > pool.HardPity) pity = null;
        string guarantee = "";
        if (pool.Type == "301")
        {
            bool? lastUp = intervalKnown && golds.Count > 0 ? banners.IsUp(golds[^1].Record) : null;
            guarantee = lastUp switch { true => "小保底", false => "大保底", null => "UP 信息不足，仅显示抽数" };
        }
        else if (pool.Type == "500") guarantee = "当期选择未知，不推导大小保底";
        string note = pool.Type switch
        {
            "302" => "祈愿记录不含定轨选择与命定值，因此无法追踪定轨；不显示大小保底。",
            "500" => "抽数保底跨期继承；命定值与大小保底不跨期继承。原始记录不含当期命定选择，不推导是否歪。",
            "200" => "常驻祈愿没有 UP 概念。抽数按现有记录计算。",
            _ => "两个角色祈愿一起统计抽数。缺少对应卡池信息时，只显示抽数，不判断大小保底。"
        };
        var histogram = Enumerable.Range(0, pool.HardPity / 10).Select(i => new HistogramBin(i * 10 + 1, (i + 1) * 10,
            golds.Where(g => g.Pulls >= i * 10 + 1 && g.Pulls <= (i + 1) * 10).Reverse().ToList().AsReadOnly())).ToArray();
        var cycleMap = CompleteUpCycles(records, pool).ToDictionary(c => c.Key, c => c.Value);
        var observedCycles = new Dictionary<string, int>();
        if (pool.Type is "301" or "302")
        {
            int count = 0, sinceGold = 0; bool seenUp = false, known = true;
            foreach (var r in records)
            {
                count++; sinceGold++; if (r.RankType is not ("3" or "4" or "5")) known = false;
                if (r.RankType != "5") continue;
                if (sinceGold > pool.HardPity) known = false;
                sinceGold = 0;
                bool? up = banners.IsUp(r);
                if (up is null) known = false;
                if (up != true) continue;
                if (!seenUp && known && count <= pool.HardPity * 2) observedCycles[r.Id] = count;
                seenUp = true; count = 0;
            }
        }
        golds = golds.Select(g => g with { UpCyclePulls = cycleMap.GetValueOrDefault(g.Record.Id) is > 0 and var n ? n : null,
            ObservedUpCyclePulls = observedCycles.GetValueOrDefault(g.Record.Id) is > 0 and var observed ? observed : null }).ToList();
        var cycles = cycleMap.Values.ToArray();
        return new(pool, records.Length, records.Any(r => r.RankType is not ("3" or "4" or "5")) ? null : golds.Count,
            pity, lowerBound, complete.Length == 0 ? null : complete.Average(), guarantee, note,
            golds.AsEnumerable().Reverse().ToList().AsReadOnly(), Array.AsReadOnly(histogram), golds.LastOrDefault()?.Percentile)
            { UpCycles = cycles, UpAverage = cycles.Length == 0 ? null : cycles.Average() };
    }
    private IEnumerable<KeyValuePair<string, int>> CompleteUpCycles(IEnumerable<GachaRecord> records, PoolDefinition pool)
    {
        if (pool.Type is not ("301" or "302")) yield break;
        bool haveUp = true, known = true; int pulls = 0, goldPulls = 0;
        foreach (var r in records)
        {
            pulls++; goldPulls++;
            if (r.RankType is not ("3" or "4" or "5")) known = false;
            if (r.RankType != "5") continue;
            // 跨过硬保底的出金区间说明记录不连续，不把它纳入 UP 周期均值。
            if (goldPulls > pool.HardPity) known = false;
            goldPulls = 0;
            bool? up = banners.IsUp(r);
            if (up is null) { haveUp = false; known = true; pulls = 0; }
            else if (up == true)
            {
                if (haveUp && known && pulls <= pool.HardPity * 2) yield return new(r.Id, pulls);
                haveUp = true; known = true; pulls = 0;
            }
        }
    }
    private static IEnumerable<int> CompleteIntervals(IEnumerable<GachaRecord> records, PoolDefinition pool)
    {
        bool known = true; int pulls = 0;
        foreach (var record in records.Where(r => r.PoolType == pool.Type).OrderBy(r => r.Time).ThenBy(r => BigInteger.Parse(r.Id)))
        {
            pulls++;
            if (record.RankType is not ("3" or "4" or "5")) known = false;
            if (record.RankType != "5") continue;
            if (known && pulls <= pool.HardPity) yield return pulls;
            known = true; pulls = 0;
        }
    }
}
