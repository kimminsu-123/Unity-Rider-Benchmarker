using System.Diagnostics;
using System.Management;
using System.Runtime.InteropServices;
using UnityRiderBench.Models;

namespace UnityRiderBench.SpecCollector;

public static class OsInfoCollector
{
    public static OsSpec Collect()
    {
        var osVersion = TryGetWindowsCaption() ?? RuntimeInformation.OSDescription;
        var dotNetVersion = RuntimeInformation.FrameworkDescription;
        var hasJdk = HasJdk();

        return new OsSpec(osVersion, dotNetVersion, hasJdk);
    }

    private static string? TryGetWindowsCaption()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT Caption, Version, BuildNumber FROM Win32_OperatingSystem");

            foreach (var obj in searcher.Get())
            {
                using (obj)
                {
                    var caption = (obj["Caption"] as string)?.Trim();
                    var build = (obj["BuildNumber"] as string)?.Trim();
                    if (caption is null)
                    {
                        return null;
                    }

                    return build is null ? caption : $"{caption} (Build {build})";
                }
            }
        }
        catch (ManagementException)
        {
            // WMI 조회 실패 시 RuntimeInformation.OSDescription으로 폴백
        }

        return null;
    }

    private static bool HasJdk()
    {
        var javaHome = Environment.GetEnvironmentVariable("JAVA_HOME");
        if (!string.IsNullOrEmpty(javaHome) &&
            File.Exists(Path.Combine(javaHome, "bin", "java.exe")))
        {
            return true;
        }

        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = "java",
                Arguments = "-version",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            });

            process?.WaitForExit(2000);
            return process is { ExitCode: 0 };
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            return false;
        }
    }
}
