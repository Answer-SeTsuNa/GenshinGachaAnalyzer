namespace GenshinGachaAnalyzer.Core.Models;

/// <summary>只增不改的原始记录。可选元数据缺失时保持 null，绝不猜测星级或物品名。</summary>
public sealed record GachaRecord
{
    public required string Id { get; init; }
    public required string Uid { get; init; }
    public required string GachaType { get; init; }
    public required string ItemId { get; init; }
    public string? Name { get; init; }
    public string? ItemType { get; init; }
    public string? RankType { get; init; }
    public string? Count { get; init; }
    /// <summary>统一保存为国服当地时间 UTC+8；DateTime.Kind 为 Unspecified。</summary>
    public DateTime Time { get; init; }
    public required string Source { get; init; }
    public string PoolType => PoolCatalog.Normalize(GachaType);

    // 来源不影响判重。UIGF 可省略名称、类型、星级、数量；游戏 API 的物品 ID 也可能为空。
    // 缺省不是矛盾：只比较双方已知的字段，重复时仍保留原记录，不补写或猜测缺失值。
    public bool SameContent(GachaRecord other) =>
        Id == other.Id && Uid == other.Uid && GachaType == other.GachaType && Time == other.Time &&
        KnownValuesAgree(ItemId, other.ItemId) && KnownValuesAgree(Name, other.Name) &&
        KnownValuesAgree(ItemType, other.ItemType) && KnownValuesAgree(RankType, other.RankType) &&
        KnownValuesAgree(Count, other.Count);

    internal static bool KnownValuesAgree(string? existing, string? incoming) =>
        string.IsNullOrWhiteSpace(existing) || string.IsNullOrWhiteSpace(incoming) || existing == incoming;
}
