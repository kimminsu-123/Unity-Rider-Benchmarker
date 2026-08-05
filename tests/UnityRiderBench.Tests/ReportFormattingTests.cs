using UnityRiderBench.Models;
using UnityRiderBench.Report;

namespace UnityRiderBench.Tests;

public class ReportFormattingTests
{
    [Theory]
    [InlineData(0L, "0 B")]
    [InlineData(1024L, "1 KB")]
    [InlineData(1024L * 1024, "1 MB")]
    [InlineData(1536L * 1024 * 1024, "1.5 GB")]
    public void FormatBytes_FormatsWithAppropriateUnit(long bytes, string expected)
    {
        var result = ReportFormatting.FormatBytes(bytes);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void FormatBytes_NegativeValue_ReturnsConfirmationNeeded()
    {
        var result = ReportFormatting.FormatBytes(-1);

        Assert.Equal("확인 필요", result);
    }

    [Theory]
    [InlineData(DriveKind.Nvme, "NVMe SSD")]
    [InlineData(DriveKind.SataSsd, "SATA SSD")]
    [InlineData(DriveKind.Hdd, "HDD")]
    [InlineData(DriveKind.Unknown, "알 수 없음")]
    public void DescribeDriveKind_ReturnsExpectedLabel(DriveKind kind, string expected)
    {
        var result = ReportFormatting.DescribeDriveKind(kind);

        Assert.Equal(expected, result);
    }
}
