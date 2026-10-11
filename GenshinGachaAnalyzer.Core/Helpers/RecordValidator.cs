using System.Text.RegularExpressions;
using GenshinGachaAnalyzer.Core.Models;

namespace GenshinGachaAnalyzer.Core.Helpers;

public static partial class RecordValidator
{
    [GeneratedRegex("^[0-9]{1,19}$")] private static partial Regex IdPattern();
    [GeneratedRegex("^[1-5][0-9]{8}$")] private static partial Regex UidPattern();
    public static bool IsChineseUid(string uid) => UidPattern().IsMatch(uid);
    public static void Validate(GachaRecord record)
    {
        if (!IdPattern().IsMatch(record.Id) || !IsChineseUid(record.Uid) || !PoolCatalog.IsKnown(record.GachaType) ||
            record.Time == default || record.Time.Kind != DateTimeKind.Unspecified ||
            record.Source is not ("游戏获取" or "UIGF导入"))
            throw new UserFacingException("记录的 ID、国服 UID、卡池、时间或来源不合法，未写入数据库。");
    }
}
