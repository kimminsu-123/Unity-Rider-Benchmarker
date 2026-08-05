using System.Diagnostics;
using System.Text.Json;
using UnityRiderBench.Models;

namespace UnityRiderBench.UnityBatchRunner;

public static class BatchProcessRunner
{
    private const string ExecuteMethod = "UnityRiderBench.Probe.DomainReloadProbe.Run";
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(30);

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

            var exited = WaitWithProgress(process, timeout);
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
    // 단일 WaitForExit 대신 짧은 간격으로 폴링하며 경과 시간을 출력한다 — 무응답처럼 보이는 것을 방지.
    private static bool WaitWithProgress(Process process, TimeSpan timeout)
    {
        var elapsed = TimeSpan.Zero;
        while (elapsed < timeout)
        {
            var waitMs = (int)Math.Min(PollInterval.TotalMilliseconds, (timeout - elapsed).TotalMilliseconds);
            if (process.WaitForExit(waitMs))
            {
                return true;
            }

            elapsed += PollInterval;
            Console.WriteLine($"  ...대기 중 (경과 {elapsed.TotalMinutes:0}분 / 제한 {timeout.TotalMinutes:0}분)");
        }

        return false;
    }

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
