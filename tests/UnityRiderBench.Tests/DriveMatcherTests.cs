using UnityRiderBench.Models;
using UnityRiderBench.PathAnalysis;

namespace UnityRiderBench.Tests;

public class DriveMatcherTests
{
    private static readonly List<DiskSpec> Disks =
    [
        new("C:", DriveKind.Nvme, 500L * 1024 * 1024 * 1024, 100L * 1024 * 1024 * 1024),
        new("D:", DriveKind.Hdd, 1000L * 1024 * 1024 * 1024, 500L * 1024 * 1024 * 1024),
    ];

    [Theory]
    [InlineData(@"C:\Users\test", DriveKind.Nvme)]
    [InlineData(@"c:\users\test", DriveKind.Nvme)]
    [InlineData(@"D:\Projects\Game", DriveKind.Hdd)]
    public void Match_ReturnsExpectedDriveKind(string path, DriveKind expected)
    {
        var result = DriveMatcher.Match(path, Disks);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void Match_UnmappedDrive_ReturnsUnknown()
    {
        var result = DriveMatcher.Match(@"E:\Somewhere", Disks);

        Assert.Equal(DriveKind.Unknown, result);
    }
}
