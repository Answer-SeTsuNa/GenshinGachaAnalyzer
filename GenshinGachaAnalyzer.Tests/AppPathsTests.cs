using GenshinGachaAnalyzer.Core.Helpers;
using Xunit;

namespace GenshinGachaAnalyzer.Tests;

public sealed class AppPathsTests
{
    [Fact]
    public void DesktopKeepsLegacyDataLocation()
    {
        Assert.Equal(Path.Combine("synthetic-roaming", "GenshinGachaAnalyzer"), AppPaths.ResolveDataDirectory(false, "synthetic-roaming", "synthetic-user"));
    }
    [Fact]
    public void PackagedArchiveIsOutsideRemovablePackageData()
    {
        var result = AppPaths.ResolveDataDirectory(true, "synthetic-roaming", "synthetic-user");
        Assert.Equal(Path.Combine("synthetic-user", "木偶祈愿档案"), result);
        Assert.DoesNotContain("synthetic-roaming", result);
    }
}
