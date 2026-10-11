using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using GenshinGachaAnalyzer.Core.Helpers;
using GenshinGachaAnalyzer.Core.Models;

namespace GenshinGachaAnalyzer.Core.Services;

public sealed class UigfService
{
    private readonly OfflineSchemaValidator _validator = new();
    private readonly OfflineSchemaValidator _legacyValidator = new("uigf-legacy.schema.json");
    private readonly OfflineSchemaValidator _v22Validator = new("uigf-v2.2.schema.json");
    private readonly WishCatalogService _catalog = new();
    public static IReadOnlyList<string> SupportedVersions { get; } = ["v4.2", "v4.1", "v4.0", "v3.0", "v2.4", "v2.3", "v2.2"];
    public async Task<PreparedImport> PreflightAsync(string path, IProgress<ImportProgress>? progress = null, CancellationToken token = default)
    {
        if (!Path.GetExtension(path).Equals(".json", StringComparison.OrdinalIgnoreCase)) throw new UserFacingException("请选择 .json 格式的 UIGF 文件。");
        if (new FileInfo(path).Length > 512L * 1024 * 1024) throw new UserFacingException("文件超过 512 MB，请使用原工具按账号分别导出后导入。");
        progress?.Report(new(0, 0, "读取 JSON"));
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 65536, true);
        try
        {
            using var document = await JsonDocument.ParseAsync(stream, new JsonDocumentOptions { MaxDepth = 64 }, token);
            return Parse(document.RootElement, progress, token);
        }
        catch (JsonException) { throw new UserFacingException("文件不是有效的 JSON，或嵌套过深。请从原工具重新导出；未写入任何记录。"); }
    }

    public PreparedImport Parse(JsonElement root, IProgress<ImportProgress>? progress = null, CancellationToken token = default)
    {
        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("info", out var info) || info.ValueKind != JsonValueKind.Object)
            throw new UserFacingException("缺少 info 对象，无法识别 UIGF 文件。");
        // 新旧标准结构不同：v4 是 hk4e 多账号，v2/v3 是 info.uid + list 单账号。
        string? version = info.TryGetProperty("version", out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() :
            info.TryGetProperty("uigf_version", out v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
        if (version is null) throw new UserFacingException("缺少文件版本 info.version。");
        if (!SupportedVersions.Contains(version)) throw new UnsupportedUigfException(version);
        bool modern = version.StartsWith("v4.", StringComparison.Ordinal);
        if (modern && !info.TryGetProperty("version", out _)) throw new UserFacingException("这不是标准 UIGF v4 文件：需要 info.version、info.export_timestamp 与 hk4e，请用兼容工具重新导出。");
        progress?.Report(new(0, 0, "离线 Schema 校验"));
        (modern ? _validator : version == "v2.2" ? _v22Validator : _legacyValidator).Validate(root, progress, token);
        JsonElement hk4e = default;
        if (modern && (!root.TryGetProperty("hk4e", out hk4e) || hk4e.GetArrayLength() == 0)) throw new UserFacingException("文件中没有 hk4e 原神账号。其他游戏的数据不会导入。");
        var inputAccounts = modern ? hk4e.EnumerateArray().ToArray() : new[] { info };
        var accounts = new List<AccountSeed>();
        var records = new List<GachaRecord>();
        var warnings = new List<string>();
        int missingRanks = 0, missingNames = 0, processed = 0;
        int total = modern ? inputAccounts.Sum(a => a.GetProperty("list").GetArrayLength()) : root.GetProperty("list").GetArrayLength();
        foreach (var account in inputAccounts)
        {
            var uidElement = account.GetProperty("uid");
            string uid = uidElement.ValueKind == JsonValueKind.String ? uidElement.GetString()! : uidElement.GetRawText();
            if (!RecordValidator.IsChineseUid(uid)) throw new UserFacingException("文件包含不支持的 UID。本工具只支持九位国服 UID（1–5 开头），请按国服账号分别导出。");
            double timezone = modern ? account.GetProperty("timezone").GetDouble() :
                account.TryGetProperty("region_time_zone", out var offset) ? offset.GetDouble() : 8;
            if (!double.IsFinite(timezone) || timezone is < -12 or > 14)
                throw new UserFacingException("账号 timezone 超出合法时区范围。");
            accounts.Add(new(uid, Optional(account, "lang")));
            foreach (var element in (modern ? account.GetProperty("list") : root.GetProperty("list")).EnumerateArray())
            {
                token.ThrowIfCancellationRequested();
                string type = element.GetProperty("gacha_type").GetString()!;
                if (version.StartsWith("v2.", StringComparison.Ordinal) && type == "500")
                    throw new UserFacingException("UIGF v2.x 不包含集录祈愿类型 500，请使用 v3.0 或 v4.x 标准文件。");
                if (element.GetProperty("uigf_gacha_type").GetString() != PoolCatalog.Normalize(type))
                    throw new UserFacingException($"记录 {element.GetProperty("id").GetString()} 的卡池编号与 UIGF 共享保底编号不一致。");
                if (!DateTime.TryParseExact(element.GetProperty("time").GetString(), "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out var time))
                    throw new UserFacingException("记录 time 含有无效日期，请检查原始导出文件。");
                DateTime normalized;
                try { normalized = DateTime.SpecifyKind(time.AddHours(8 - timezone), DateTimeKind.Unspecified); }
                catch (ArgumentOutOfRangeException) { throw new UserFacingException("记录时间无法转换为国服 UTC+8。"); }
                var record = new GachaRecord
                {
                    Id = element.GetProperty("id").GetString()!, Uid = uid, GachaType = type,
                    ItemId = Optional(element, "item_id") ?? "", Name = Optional(element, "name"),
                    ItemType = Optional(element, "item_type"), RankType = Optional(element, "rank_type"), Count = Optional(element, "count"),
                    Time = normalized, Source = "UIGF导入"
                };
                if (!modern && string.IsNullOrEmpty(record.ItemId) && record.Name is { } oldName && _catalog.FindByName(oldName) is { } knownItem)
                {
                    record = record with { ItemId = knownItem.Id };
                    warnings.Add("旧版空 item_id 已按胡桃物品表精确名称映射；未匹配的物品仍保留原始名称，不猜测 ID。");
                }
                RecordValidator.Validate(record);
                if (record.RankType is not ("3" or "4" or "5")) missingRanks++;
                if (string.IsNullOrEmpty(record.Name)) missingNames++;
                records.Add(record);
                if (++processed % 200 == 0) progress?.Report(new(processed, total, "完整性预检"));
            }
            if (timezone != 8) warnings.Add("已将文件当地时间按 timezone 转换为 UTC+8；导出统一使用 timezone=8。");
        }
        var repeated = records.GroupBy(r => r.Id).Count(g => g.Count() > 1);
        if (repeated > 0) warnings.Add($"文件内有 {repeated} 个重复 ID，写入时将去重并保留先存在的内容。");
        if (missingRanks > 0) warnings.Add($"{missingRanks} 条记录未提供有效星级：展示与分析按胡桃物品 ID 表补全，无法匹配的仍显示未知；原始星级字段不改写。");
        if (missingNames > 0) warnings.Add($"{missingNames} 条记录未提供名称：展示使用胡桃物品表，未匹配时显示物品 ID。");
        if (root.TryGetProperty("hkrpg", out _) || root.TryGetProperty("nap", out _) || root.TryGetProperty("hk4e_ugc", out _)) warnings.Add("文件中的其他游戏数据与千星奇域数据将忽略，仅导入原神普通祈愿 hk4e。");
        warnings.Add("结构与字段已校验；UIGF 无法证明记录是否来自游戏，也无法证明历史记录完整。首个可见出金区间不计入平均值。");
        var counts = records.GroupBy(r => (r.Uid, r.PoolType)).Select(g => new PoolCount(g.Key.Uid, PoolCatalog.NameOf(g.Key.PoolType), g.Count())).ToArray();
        progress?.Report(new(total, total, "预检完成，等待确认"));
        return new(version, accounts.DistinctBy(a => a.Uid).ToList().AsReadOnly(), records.AsReadOnly(), Array.AsReadOnly(counts), warnings.Distinct().ToList().AsReadOnly());
    }
    private static string? Optional(JsonElement element, string key) => element.TryGetProperty(key, out var value) ? value.GetString() : null;

    public async Task ExportAsync(string path, IReadOnlyList<(Account Account, IReadOnlyList<GachaRecord> Records)> snapshot, CancellationToken token = default, string version = "v4.1")
    {
        if (snapshot.Count == 0) throw new UserFacingException("请至少选择一个账号。");
        if (!SupportedVersions.Contains(version)) throw new UnsupportedUigfException(version);
        bool modern = version.StartsWith("v4.", StringComparison.Ordinal);
        if (!modern && snapshot.Count != 1) throw new UserFacingException("UIGF v2/v3 是单账号格式，请只选择一个账号，或改用 v4.x 多账号格式。");
        if (version.StartsWith("v2.", StringComparison.Ordinal) && snapshot.Any(a => a.Records.Any(r => r.GachaType == "500")))
            throw new UserFacingException("所选账号包含集录祈愿，UIGF v2.x 无法表示。请改用 v3.0 或 v4.x；不会丢弃这些记录。");
        if (version == "v2.2" && snapshot.Any(a => a.Records.Any(r => _catalog.Resolve(r).Kind == "未知类型" || string.IsNullOrWhiteSpace(r.Name) && _catalog.FindById(r.ItemId) is null)))
            throw new UserFacingException("UIGF v2.2 要求物品名称与类型，部分物品未被元数据覆盖。请改用 v2.3 或更新格式。");
        string target = Path.GetFullPath(path);
        string temporary = target + "." + Guid.NewGuid().ToString("N") + ".partial";
        try
        {
            await using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 65536, true))
            {
                using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
                writer.WriteStartObject(); writer.WriteStartObject("info");
                writer.WriteNumber("export_timestamp", DateTimeOffset.UtcNow.ToUnixTimeSeconds());
                writer.WriteString("export_app", "GenshinGachaAnalyzer"); writer.WriteString("export_app_version", typeof(UigfService).Assembly.GetName().Version!.ToString(3)); writer.WriteString(modern ? "version" : "uigf_version", version);
                if (!modern)
                {
                    writer.WriteString("uid", snapshot[0].Account.Uid);
                    if (snapshot[0].Account.Lang is { } lang) writer.WriteString("lang", lang);
                    if (version is "v3.0" or "v2.4") writer.WriteNumber("region_time_zone", 8);
                }
                writer.WriteEndObject(); if (modern) writer.WriteStartArray("hk4e");
                foreach (var (account, records) in snapshot)
                {
                    if (modern)
                    {
                        writer.WriteStartObject(); writer.WriteString("uid", account.Uid); writer.WriteNumber("timezone", 8);
                        if (account.Lang is not null) writer.WriteString("lang", account.Lang);
                    }
                    writer.WriteStartArray("list");
                    foreach (var record in records)
                    {
                        token.ThrowIfCancellationRequested();
                        RecordValidator.Validate(record);
                        writer.WriteStartObject(); writer.WriteString("uigf_gacha_type", record.PoolType);
                        writer.WriteString("gacha_type", record.GachaType); writer.WriteString("item_id", record.ItemId);
                        writer.WriteString("time", record.Time.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
                        if (record.Name is not null || version == "v2.2") writer.WriteString("name", record.Name ?? _catalog.Resolve(record).Name);
                        if (record.ItemType is not null || version == "v2.2") writer.WriteString("item_type", record.ItemType ?? _catalog.Resolve(record).Kind);
                        if (record.RankType is not null) writer.WriteString("rank_type", record.RankType);
                        if (record.Count is not null) writer.WriteString("count", record.Count);
                        writer.WriteString("id", record.Id); writer.WriteEndObject();
                    }
                    writer.WriteEndArray(); if (modern) writer.WriteEndObject();
                }
                if (modern) writer.WriteEndArray(); writer.WriteEndObject();
                await writer.FlushAsync(token);
                await stream.FlushAsync(token);
                stream.Flush(flushToDisk: true);
            }
            token.ThrowIfCancellationRequested();
            // 同目录完整临时文件原子替换；磁盘满、取消等失败不会留下半截目标文件。
            File.Move(temporary, target, overwrite: true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}



