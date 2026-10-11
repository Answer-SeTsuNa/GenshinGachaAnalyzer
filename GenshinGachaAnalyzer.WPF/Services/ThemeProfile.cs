namespace GenshinGachaAnalyzer.WPF.Services;
public sealed record ThemeProfile(string Key, string Name, string Accent, string NightAccent, string LightBackground, string DarkBackground, string Description, string Artwork)
{
    public override string ToString() => Name;
    // 色板沿用首版 0.1.0；牛牛沿用初次加入的 0.1.1。替换主题图案时保持配色。
    public static IReadOnlyList<ThemeProfile> All { get; } = [
        new("Gear", "木偶", "#866643", "#D6B180", "#F4EFE6", "#27231F", "", ""),
        new("Ayaka", "神里绫华", "#4D87A7", "#A4DCEB", "#EDF7FA", "#172D39", "", ""),
        new("Klee", "可莉", "#BB514C", "#FFAC94", "#FCF0EB", "#322222", "", ""),
        new("Zhongli", "钟离", "#A17A31", "#ECC575", "#F8F3E8", "#29251E", "", ""),
        new("Furina", "芙芙", "#4167B1", "#A5C4FF", "#EFF2FC", "#1A223B", "", ""),
        new("Varesa", "牛牛", "#BB5B91", "#F1A9D0", "#FCEFF6", "#302232", "", "")];
}
