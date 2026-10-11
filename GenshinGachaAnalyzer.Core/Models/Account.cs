namespace GenshinGachaAnalyzer.Core.Models;

public sealed record Account(string Uid, string? Nickname, DateTime LastUpdated, string? Lang = "zh-cn");
public sealed record AccountSeed(string Uid, string? Lang);

public sealed record PoolDefinition(string Type, string Name, int HardPity, string Glyph, string Accent);

public static class PoolCatalog
{
    // 官方编号：301 / 400 共享角色祈愿保底，100 是新手池而非常驻池。
    public static IReadOnlyList<PoolDefinition> MainPools { get; } = Array.AsReadOnly(new[]
    {
        new PoolDefinition("301", "角色祈愿", 90, "✦", "#8064A8"),
        new PoolDefinition("302", "武器祈愿", 80, "◇", "#A77636"),
        new PoolDefinition("500", "集录祈愿", 90, "✧", "#508D89"),
        new PoolDefinition("200", "常驻祈愿", 90, "○", "#5C7C9B")
    });
    public static string Normalize(string type) => type == "400" ? "301" : type;
    public static bool IsKnown(string type) => type is "100" or "200" or "301" or "302" or "400" or "500";
    public static string NameOf(string type) => Normalize(type) == "100" ? "新手祈愿（仅存档）" :
        MainPools.FirstOrDefault(p => p.Type == Normalize(type))?.Name ?? "未知卡池";
}

