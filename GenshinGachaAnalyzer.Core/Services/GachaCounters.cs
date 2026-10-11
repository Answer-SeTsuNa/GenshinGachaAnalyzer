using System.Numerics;
using GenshinGachaAnalyzer.Core.Models;

namespace GenshinGachaAnalyzer.Core.Services;
public static class GachaCounters
{
    /// <summary>胡桃四星计数：从档案起点累计，只有四星重置；五星照常增加计数。</summary>
    public static int? FourStarProgress(IEnumerable<GachaRecord> records)
    {
        var ordered = records.OrderBy(r => r.Time).ThenBy(r => BigInteger.Parse(r.Id)).ToArray();
        if (ordered.Length == 0) return null;
        int count = 0; bool known = true;
        foreach (var record in ordered)
        {
            count++;
            if (record.RankType is not ("3" or "4" or "5")) known = false;
            if (record.RankType == "4") { count = 0; known = true; }
        }
        return known ? count : null;
    }
}
