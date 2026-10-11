using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using GenshinGachaAnalyzer.Core.Models;

namespace GenshinGachaAnalyzer.Core.Services;

/// <summary>纯本地写入诊断。只记录抽卡字段，不接受网络请求、凭据或 authkey。</summary>
public static class ConflictLogService
{
    public static RecordConflict Compare(GachaRecord saved, GachaRecord incoming)
    {
        var differences = new List<FieldDifference>();
        // 与判重使用同一规则，仅将双方已知且不同的值列为冲突；字段缺省不混入冲突原因。
        void Add(string field, string? oldValue, string? newValue) { if (!GachaRecord.KnownValuesAgree(oldValue, newValue)) differences.Add(new(field, oldValue, newValue)); }
        Add("UID", saved.Uid, incoming.Uid); Add("卡池类型", saved.GachaType, incoming.GachaType);
        Add("物品 ID", saved.ItemId, incoming.ItemId); Add("抽取时间（UTC+8）", saved.Time.ToString("yyyy-MM-dd HH:mm:ss"), incoming.Time.ToString("yyyy-MM-dd HH:mm:ss"));
        Add("名称", saved.Name, incoming.Name); Add("物品类型", saved.ItemType, incoming.ItemType);
        Add("星级", saved.RankType, incoming.RankType); Add("数量", saved.Count, incoming.Count);
        bool core = differences.Any(d => d.Field is "UID" or "卡池类型" or "物品 ID" or "抽取时间（UTC+8）");
        string reason = saved.Uid != incoming.Uid ? "相同记录 ID 属于不同 UID" : core ? "相同 ID 的核心抽卡字段不同，请核对原文件与时区" :
            "双方已知的可选元数据不同，请核对内容或导出语言；保留已有记录";
        return new(incoming.Id, reason, saved, incoming, differences);
    }
    public static (string? Path, string? Warning) Write(string databasePath, WriteReport report, IReadOnlyList<GachaRecord> records)
    {
        try
        {
            string directory = Path.Combine(Path.GetDirectoryName(databasePath)!, "logs"); Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, $"write-{DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(8)):yyyyMMdd-HHmmssfff}-{Guid.NewGuid():N}.log");
            using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
            using var writer = new StreamWriter(stream, new UTF8Encoding(false), leaveOpen: true);
            writer.WriteLine("木偶祈愿档案 · 本地写入诊断（不上传）");
            writer.WriteLine($"时间：{DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(8)):yyyy-MM-dd HH:mm:ss} UTC+8");
            writer.WriteLine("此文件记录字段差异；事务是否提交请查看文件末尾状态。");
            writer.WriteLine($"输入：{records.Count} 条；新增：{report.Added}；重复：{report.Duplicates}；冲突 ID：{report.Conflicts}");
            writer.WriteLine("涉及 UID：" + string.Join("、", records.Select(r => r.Uid).Distinct()));
            var options = new JsonSerializerOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
            foreach (var conflict in report.Details)
            {
                writer.WriteLine($"\n冲突 ID：{conflict.Id}\n原因：{conflict.Reason}");
                foreach (var difference in conflict.Differences)
                    writer.WriteLine($"{difference.Field} | 已有={JsonSerializer.Serialize(difference.Existing, options)} | 本次={JsonSerializer.Serialize(difference.Incoming, options)}");
                writer.WriteLine("已有记录=" + JsonSerializer.Serialize(conflict.Existing, options));
                writer.WriteLine("输入记录=" + JsonSerializer.Serialize(conflict.Incoming, options));
            }
            writer.Flush(); stream.Flush(true);
            return (path, null);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException) { return (null, "本地诊断日志无法保存，请检查数据目录权限与剩余空间。"); }
    }
    public static string? Finish(string? path, bool committed)
    {
        if (path is null) return null;
        try { File.AppendAllText(path, "\n事务状态：" + (committed ? "已提交；冲突记录保留已有内容，不覆盖。" : "未提交；本批写入已回滚。") + "\n", new UTF8Encoding(false)); return null; }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException) { return "诊断日志末尾状态无法保存；请以界面结果为准。"; }
    }
}
