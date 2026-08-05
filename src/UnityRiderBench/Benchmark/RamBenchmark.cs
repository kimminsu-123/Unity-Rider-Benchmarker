using System.Diagnostics;
using UnityRiderBench.Models;

namespace UnityRiderBench.Benchmark;

// 관리 배열 복사 기반 근사치이며 하드웨어 레벨 메모리 대역폭 측정 도구(AIDA64 등) 수준의
// 정밀도는 아니다 — 상대 비교용 참고 지표.
public static class RamBenchmark
{
    private const int BufferSizeBytes = 64 * 1024 * 1024;
    private const int Iterations = 20;

    public static RamBenchmarkResult Run()
    {
        var source = new byte[BufferSizeBytes];
        var destination = new byte[BufferSizeBytes];
        Random.Shared.NextBytes(source);

        var stopwatch = Stopwatch.StartNew();
        for (var i = 0; i < Iterations; i++)
        {
            Buffer.BlockCopy(source, 0, destination, 0, BufferSizeBytes);
        }
        stopwatch.Stop();

        var totalBytes = (long)BufferSizeBytes * Iterations;
        var mbPerSec = totalBytes / 1024.0 / 1024.0 / stopwatch.Elapsed.TotalSeconds;

        return new RamBenchmarkResult(mbPerSec);
    }
}
