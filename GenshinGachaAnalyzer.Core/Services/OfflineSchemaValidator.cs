using System.Text.Json;
using System.Text.RegularExpressions;
using GenshinGachaAnalyzer.Core.Helpers;
using GenshinGachaAnalyzer.Core.Models;

namespace GenshinGachaAnalyzer.Core.Services;

/// <summary>仅实现内置 Schema 使用的关键字，无远程 $ref，不会因导入文件联网。</summary>
public sealed class OfflineSchemaValidator
{
    private readonly JsonElement _schema;
    public OfflineSchemaValidator(string resource = "uigf-v4.schema.json")
    {
        using var stream = typeof(OfflineSchemaValidator).Assembly.GetManifestResourceStream("GenshinGachaAnalyzer.Core.Resources." + resource)!;
        using var document = JsonDocument.Parse(stream);
        _schema = document.RootElement.Clone();
    }
    public void Validate(JsonElement value, IProgress<ImportProgress>? progress = null, CancellationToken token = default)
    {
        int nodes = 0;
        Visit(value, _schema, "$", progress, token, ref nodes);
    }
    private static void Visit(JsonElement value, JsonElement schema, string path, IProgress<ImportProgress>? progress, CancellationToken token, ref int nodes)
    {
        token.ThrowIfCancellationRequested();
        if (++nodes % 10000 == 0) progress?.Report(new(nodes, 0, "离线 Schema 校验"));
        if (schema.TryGetProperty("oneOf", out var choices))
        {
            int matched = choices.EnumerateArray().Count(c => Matches(value, c.GetProperty("type").GetString()!));
            if (matched != 1) Fail(path, "字段类型错误，应为字符串或整数");
        }
        if (schema.TryGetProperty("type", out var type) && !Matches(value, type.GetString()!))
            Fail(path, $"字段类型错误，应为 {type.GetString()}");
        if (schema.TryGetProperty("enum", out var options) && !options.EnumerateArray().Any(o => o.GetRawText() == value.GetRawText() ||
            (o.ValueKind == JsonValueKind.String && value.ValueKind == JsonValueKind.String && o.GetString() == value.GetString())))
            Fail(path, "字段值不在允许范围内");
        if (value.ValueKind == JsonValueKind.Object)
        {
            var keys = new HashSet<string>();
            foreach (var property in value.EnumerateObject())
                if (!keys.Add(property.Name)) Fail(path, $"字段 {property.Name} 重复，请检查导出文件");
            if (schema.TryGetProperty("required", out var required))
                foreach (var key in required.EnumerateArray())
                    if (!value.TryGetProperty(key.GetString()!, out _)) Fail(path, $"缺少必填字段 {key.GetString()}");
            if (schema.TryGetProperty("properties", out var properties))
                foreach (var property in properties.EnumerateObject())
                    if (value.TryGetProperty(property.Name, out var child))
                        Visit(child, property.Value, path + "." + property.Name, progress, token, ref nodes);
        }
        if (value.ValueKind == JsonValueKind.Array && schema.TryGetProperty("items", out var items))
        {
            int i = 0;
            foreach (var child in value.EnumerateArray()) Visit(child, items, $"{path}[{i++}]", progress, token, ref nodes);
        }
        if (value.ValueKind == JsonValueKind.String)
        {
            string text = value.GetString()!;
            if (schema.TryGetProperty("minLength", out var min) && text.Length < min.GetInt32()) Fail(path, "字符串过短");
            if (schema.TryGetProperty("maxLength", out var max) && text.Length > max.GetInt32()) Fail(path, "字符串过长");
            if (schema.TryGetProperty("pattern", out var pattern) && !Regex.IsMatch(text, pattern.GetString()!, RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1)))
                Fail(path, "字段格式错误");
        }
    }
    private static bool Matches(JsonElement value, string type) => type switch
    {
        "object" => value.ValueKind == JsonValueKind.Object,
        "array" => value.ValueKind == JsonValueKind.Array,
        "string" => value.ValueKind == JsonValueKind.String,
        "number" => value.ValueKind == JsonValueKind.Number,
        "integer" => value.ValueKind == JsonValueKind.Number && decimal.TryParse(value.GetRawText(),
            System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var n) && n == decimal.Truncate(n),
        _ => throw new InvalidOperationException("内置 Schema 使用了未实现的关键字。")
    };
    private static void Fail(string path, string message) => throw new UserFacingException($"{path}：{message}。未写入任何记录。");
}
