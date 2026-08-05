using System.Diagnostics;
using System.Text.Json;
using UnityRiderBench.Models;

namespace UnityRiderBench.UnityBatchRunner;

public static class BatchProcessRunner
{
    private const string ExecuteMethod = "UnityRiderBench.Probe.DomainReloadProbe.Run";
    private static readonly TimeSpan TickInterval = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan TextLogInterval = TimeSpan.FromSeconds(30);
    private const int ProgressBarWidth = 40;
    private const int LogTailReadBytes = 4096;

    // -quit을 쓰지 않는 이유는 ProbeScript~/DomainReloadProbe.cs 상단 주석 참고 —
    // 도메인 리로드 완료를 콜백으로 확인한 뒤 프로브 스크립트가 스스로 EditorApplication.Exit()를 호출한다.
    public static DomainReloadResult? Run(string unityExePath, string projectPath, TimeSpan timeout)
    {
        var resultPath = Path.Combine(Path.GetTempPath(), $"urbench-domainreload-{Guid.NewGuid():N}.json");
        var logPath = Path.Combine(Path.GetTempPath(), $"urbench-unity-{Guid.NewGuid():N}.log");

        var startInfo = new ProcessStartInfo
        {
            FileName = unityExePath,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        startInfo.ArgumentList.Add("-batchmode");
        startInfo.ArgumentList.Add("-nographics");
        startInfo.ArgumentList.Add("-projectPath");
        startInfo.ArgumentList.Add(projectPath);
        startInfo.ArgumentList.Add("-executeMethod");
        startInfo.ArgumentList.Add(ExecuteMethod);
        startInfo.ArgumentList.Add("-urbenchResultPath");
        startInfo.ArgumentList.Add(resultPath);
        startInfo.ArgumentList.Add("-logFile");
        startInfo.ArgumentList.Add(logPath);

        try
        {
            using var process = Process.Start(startInfo);
            if (process is null)
            {
                return null;
            }

            var exited = WaitWithProgress(process, timeout, logPath);
            if (!exited)
            {
                process.Kill(entireProcessTree: true);
                return null;
            }

            if (!File.Exists(resultPath))
            {
                return null;
            }

            var json = File.ReadAllText(resultPath);
            return JsonSerializer.Deserialize<DomainReloadResult>(
                json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        finally
        {
            TryDelete(resultPath);
            TryDelete(logPath);
        }
    }

    // Unity 최초 실행 시 Search 인덱싱 등으로 수 분~수십 분 걸릴 수 있어(Phase 5 검증에서 확인),
    // 단일 WaitForExit 대신 짧은 간격으로 폴링하며 진행 상황을 보여준다 — 무응답처럼 보이는 것을 방지.
    private static bool WaitWithProgress(Process process, TimeSpan timeout, string logPath)
    {
        return Console.IsOutputRedirected
            ? WaitWithPlainText(process, timeout, logPath)
            : WaitWithProgressBar(process, timeout, logPath);
    }

    // 출력이 파일/파이프로 리다이렉트된 경우: 커서 제어가 의미 없으므로 기존처럼
    // 30초 간격 텍스트 라인만 출력(수백~수천 줄의 스팸 방지).
    private static bool WaitWithPlainText(Process process, TimeSpan timeout, string logPath)
    {
        var elapsed = TimeSpan.Zero;
        var nextLogAt = TextLogInterval;

        while (elapsed < timeout)
        {
            var waitMs = (int)Math.Min(TickInterval.TotalMilliseconds, (timeout - elapsed).TotalMilliseconds);
            if (process.WaitForExit(waitMs))
            {
                return true;
            }

            elapsed += TickInterval;
            if (elapsed >= nextLogAt)
            {
                nextLogAt += TextLogInterval;
                Console.WriteLine($"  ...대기 중 (경과 {elapsed.TotalMinutes:0}분 / 제한 {timeout.TotalMinutes:0}분) — {BuildStatusText(process, logPath)}");
            }
        }

        return false;
    }

    // 실제 터미널일 때: 프로그레스 바 + 상태 줄 두 칸을 매초 같은 자리에 덮어써서 갱신한다.
    private static bool WaitWithProgressBar(Process process, TimeSpan timeout, string logPath)
    {
        var elapsed = TimeSpan.Zero;
        var barRow = Console.CursorTop;
        Console.WriteLine();
        Console.WriteLine();
        var statusRow = barRow + 1;
        var renderingEnabled = true;

        try
        {
            Console.CursorVisible = false;
        }
        catch (IOException)
        {
        }

        try
        {
            while (elapsed < timeout)
            {
                var waitMs = (int)Math.Min(TickInterval.TotalMilliseconds, (timeout - elapsed).TotalMilliseconds);
                if (process.WaitForExit(waitMs))
                {
                    return true;
                }

                elapsed += TickInterval;

                if (renderingEnabled)
                {
                    try
                    {
                        var status = BuildStatusText(process, logPath);
                        RenderProgressBar(barRow, statusRow, elapsed, timeout, status);
                    }
                    catch (Exception ex) when (ex is IOException or ArgumentOutOfRangeException)
                    {
                        // 콘솔 크기 조회 실패 등 렌더링 문제 — 이후로는 조용히 렌더링만 끄고 측정은 계속 진행
                        renderingEnabled = false;
                    }
                }
            }

            return false;
        }
        finally
        {
            try
            {
                Console.CursorVisible = true;
                Console.SetCursorPosition(0, statusRow + 1);
            }
            catch (IOException)
            {
            }
        }
    }

    private static void RenderProgressBar(int barRow, int statusRow, TimeSpan elapsed, TimeSpan timeout, string status)
    {
        var ratio = timeout.TotalSeconds <= 0 ? 1.0 : Math.Clamp(elapsed.TotalSeconds / timeout.TotalSeconds, 0, 1);
        var filled = (int)(ProgressBarWidth * ratio);
        var lineWidth = Math.Max(Console.WindowWidth - 1, 60);

        var prefix = "  [";
        var filledSegment = new string('█', filled);
        var emptySegment = new string('-', ProgressBarWidth - filled);
        var suffix = $"] {ratio * 100,3:0}%  {FormatDuration(elapsed)} / {FormatDuration(timeout)}";
        var plainLength = prefix.Length + filledSegment.Length + emptySegment.Length + suffix.Length;
        var padding = new string(' ', Math.Max(0, lineWidth - plainLength));

        Console.SetCursorPosition(0, barRow);
        Console.Write(prefix);
        Console.ForegroundColor = ratio < 0.7 ? ConsoleColor.Green : ratio < 0.9 ? ConsoleColor.Yellow : ConsoleColor.Red;
        Console.Write(filledSegment);
        Console.ResetColor();
        Console.Write(emptySegment);
        Console.Write(suffix);
        Console.Write(padding);

        Console.SetCursorPosition(0, statusRow);
        var statusLine = "  " + status;
        if (statusLine.Length > lineWidth)
        {
            statusLine = statusLine[..lineWidth];
        }

        Console.Write(statusLine.PadRight(lineWidth));
    }

    private static string FormatDuration(TimeSpan value) => value.ToString(@"mm\:ss");

    // Unity 프로세스가 실제로 일하고 있는지(누적 CPU 시간)와 현재 어떤 단계인지(로그 마지막 줄)를 보여준다.
    private static string BuildStatusText(Process process, string logPath)
    {
        string aliveText;
        double cpuSeconds;
        try
        {
            process.Refresh();
            aliveText = process.HasExited ? "종료됨" : "실행 중";
            cpuSeconds = process.TotalProcessorTime.TotalSeconds;
        }
        catch (InvalidOperationException)
        {
            aliveText = "확인 불가";
            cpuSeconds = 0;
        }

        var lastLogLine = TryReadLastLogLine(logPath) ?? "(로그 대기 중)";
        return $"Unity {aliveText} (CPU {cpuSeconds:0.#}s 사용) | {lastLogLine}";
    }

    // Unity가 -logFile로 계속 쓰고 있는 파일을 공유 읽기로 열어 끝부분만 읽는다.
    private static string? TryReadLastLogLine(string logPath)
    {
        try
        {
            using var fs = new FileStream(logPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            if (fs.Length == 0)
            {
                return null;
            }

            var readSize = (int)Math.Min(LogTailReadBytes, fs.Length);
            fs.Seek(-readSize, SeekOrigin.End);
            using var reader = new StreamReader(fs);
            var tail = reader.ReadToEnd();
            var lines = tail.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            return lines.Length > 0 ? Truncate(lines[^1], 100) : null;
        }
        catch (IOException)
        {
            return null;
        }
    }

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength] + "…";

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
        }
    }
}
