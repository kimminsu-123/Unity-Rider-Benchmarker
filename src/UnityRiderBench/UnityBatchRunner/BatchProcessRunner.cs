using System.Diagnostics;
using System.Runtime.InteropServices;
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

    private const int StdOutputHandle = -11;
    private const uint EnableVirtualTerminalProcessing = 0x0004;

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GetStdHandle(int nStdHandle);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetConsoleMode(IntPtr hConsoleHandle, out uint lpMode);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetConsoleMode(IntPtr hConsoleHandle, uint dwMode);

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

        using var process = Process.Start(startInfo);
        if (process is null)
        {
            return null;
        }

        // Ctrl+C로 CLI 자체가 중단되면 using의 Process.Dispose()는 OS 프로세스를 죽이지 않으므로
        // Unity 자식 프로세스가 고아로 남아 프로젝트 락을 계속 쥐고 있게 된다(실사용 중 재현됨,
        // 이후 실행이 즉시 "Exiting without the bug reporter"로 실패하는 원인). Ctrl+C를 가로채
        // 자식을 먼저 정리한 뒤 기본 종료가 이어지도록 한다.
        ConsoleCancelEventHandler onCancel = (_, _) => TryKillProcessTree(process);
        Console.CancelKeyPress += onCancel;

        try
        {
            var exited = WaitWithProgress(process, timeout, logPath);
            if (!exited)
            {
                TryKillProcessTree(process);
                // 실패 원인 진단을 위해 로그는 지우지 않고 남겨둔다(수동 삭제 필요).
                Console.Error.WriteLine($"  Unity 로그 (타임아웃 시점까지의 진행 기록): {logPath}");
                return null;
            }

            if (!File.Exists(resultPath))
            {
                Console.Error.WriteLine($"  Unity 로그 (결과 파일이 생성되지 않음): {logPath}");
                return null;
            }

            var json = File.ReadAllText(resultPath);
            var result = JsonSerializer.Deserialize<DomainReloadResult>(
                json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            TryDelete(resultPath);
            TryDelete(logPath);
            return result;
        }
        finally
        {
            Console.CancelKeyPress -= onCancel;
        }
    }

    // Windows 콘솔은 기본적으로 VT(ANSI 이스케이프) 처리가 꺼져 있을 수 있다. 켜는 데 실패하면
    // (구형 콘솔 호스트 등) 프로그레스 바를 아예 쓰지 않고 텍스트 모드로 폴백해, ANSI 코드가
    // 그대로 글자로 화면에 찍히는 상황을 피한다.
    private static bool TryEnableAnsi()
    {
        try
        {
            var handle = GetStdHandle(StdOutputHandle);
            if (handle == IntPtr.Zero || handle == new IntPtr(-1))
            {
                return false;
            }

            if (!GetConsoleMode(handle, out var mode))
            {
                return false;
            }

            if ((mode & EnableVirtualTerminalProcessing) != 0)
            {
                return true;
            }

            return SetConsoleMode(handle, mode | EnableVirtualTerminalProcessing);
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static void TryKillProcessTree(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (Exception)
        {
            // 이미 종료 중이거나 접근 불가 — 최선을 다한 정리이므로 무시
        }
    }

    // Unity 최초 실행 시 Search 인덱싱 등으로 수 분~수십 분 걸릴 수 있어(Phase 5 검증에서 확인),
    // 단일 WaitForExit 대신 짧은 간격으로 폴링하며 진행 상황을 보여준다 — 무응답처럼 보이는 것을 방지.
    //
    // 프로그레스 바는 Console.SetCursorPosition(Win32 SetConsoleCursorPosition 래핑) 대신
    // ANSI 이스케이프(\x1b[nA 커서 위로, \x1b[2K 줄 지우기)로 직접 그린다. ConPTY 기반 최신
    // 터미널 호스트(Windows Terminal, PowerShell 7, VS Code 통합 터미널)에서 SetCursorPosition이
    // 예외 없이 조용히 no-op되어 매초 새 줄이 계속 쌓이는 문제가 실사용 중 재현됐기 때문이다.
    // VT 처리 활성화(TryEnableAnsi)에 실패하면 애초에 useBar를 켜지 않고,
    // 그래도 렌더링이 실패하면 예외를 폭넓게 잡아 텍스트 폴백으로 전환해 최소한 진행 상황
    // 텍스트는 계속 보이도록 한다 — 화면이 40분간 완전히 비어 보이는 상황을 만들지 않기 위함.
    private static bool WaitWithProgress(Process process, TimeSpan timeout, string logPath)
    {
        var elapsed = TimeSpan.Zero;
        var useBar = !Console.IsOutputRedirected && TryEnableAnsi();
        var barLinesDrawn = 0;
        var nextTextLogAt = TextLogInterval;

        if (useBar)
        {
            TrySetCursorVisible(false);
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
                var status = BuildStatusText(process, logPath);

                if (useBar)
                {
                    try
                    {
                        barLinesDrawn = RenderProgressBar(barLinesDrawn, elapsed, timeout, status);
                        continue;
                    }
                    catch (Exception)
                    {
                        // 이 터미널에서는 프로그레스 바 렌더링이 안 되는 것으로 판단 — 텍스트 폴백으로 전환
                        useBar = false;
                        if (barLinesDrawn > 0)
                        {
                            Console.WriteLine();
                        }

                        Console.WriteLine("  (이 터미널에서는 진행률 표시줄을 그릴 수 없어 텍스트로 전환합니다)");
                        nextTextLogAt = elapsed;
                    }
                }

                if (elapsed >= nextTextLogAt)
                {
                    nextTextLogAt += TextLogInterval;
                    Console.WriteLine($"  ...대기 중 (경과 {elapsed.TotalMinutes:0}분 / 제한 {timeout.TotalMinutes:0}분) — {status}");
                }
            }

            return false;
        }
        finally
        {
            if (useBar)
            {
                TrySetCursorVisible(true);
            }
        }
    }

    private static void TrySetCursorVisible(bool visible)
    {
        try
        {
            Console.CursorVisible = visible;
        }
        catch (Exception)
        {
        }
    }

    // previousLineCount만큼 ANSI로 커서를 위로 올려 직전에 그린 줄을 지우고 새로 그린다.
    // \x1b[2K로 줄 전체를 지우므로 수동 공백 패딩이 필요 없다.
    private static int RenderProgressBar(int previousLineCount, TimeSpan elapsed, TimeSpan timeout, string status)
    {
        if (previousLineCount > 0)
        {
            Console.Write($"\x1b[{previousLineCount}A");
        }

        var ratio = timeout.TotalSeconds <= 0 ? 1.0 : Math.Clamp(elapsed.TotalSeconds / timeout.TotalSeconds, 0, 1);
        var filled = (int)(ProgressBarWidth * ratio);
        var lineWidth = Math.Max(Console.WindowWidth - 1, 1);

        var filledSegment = new string('█', filled);
        var emptySegment = new string('-', ProgressBarWidth - filled);
        var suffix = $"] {ratio * 100,3:0}%  {FormatDuration(elapsed)} / {FormatDuration(timeout)}";

        Console.Write("\x1b[2K");
        Console.Write("  [");
        Console.ForegroundColor = ratio < 0.7 ? ConsoleColor.Green : ratio < 0.9 ? ConsoleColor.Yellow : ConsoleColor.Red;
        Console.Write(filledSegment);
        Console.ResetColor();
        Console.Write(emptySegment);
        Console.Write(suffix);
        Console.WriteLine();

        // 상태 줄이 터미널 폭을 넘어 자동 줄바꿈되면 실제로 그려진 줄 수가 2를 넘게 되어
        // 다음 틱의 "위로 2줄" 이동이 어긋난다 — 반드시 한 줄로 잘라서 그린다.
        var statusLine = "  " + status;
        if (statusLine.Length > lineWidth)
        {
            statusLine = statusLine[..lineWidth];
        }

        Console.Write("\x1b[2K");
        Console.Write(statusLine);
        Console.WriteLine();

        return 2;
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
