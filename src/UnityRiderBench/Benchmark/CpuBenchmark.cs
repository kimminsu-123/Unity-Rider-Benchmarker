using System.Diagnostics;
using System.Security.Cryptography;
using UnityRiderBench.Models;

namespace UnityRiderBench.Benchmark;

public static class CpuBenchmark
{
    private static readonly TimeSpan Duration = TimeSpan.FromSeconds(2);
    private const int BlockSizeBytes = 64 * 1024;

    // Unity 스크립트 컴파일/Burst 컴파일과 동일한 워크로드는 아니며,
    // 멀티스레드 CPU 처리량을 가늠하기 위한 상대 비교용 지표(SHA-256 해시 처리량)다.
    public static CpuBenchmarkResult Run()
    {
        var threadCount = Environment.ProcessorCount;
        long totalBytesHashed = 0;
        var overallStopwatch = Stopwatch.StartNew();

        Parallel.For(0, threadCount, _ =>
        {
            var buffer = new byte[BlockSizeBytes];
            Random.Shared.NextBytes(buffer);

            long localBytes = 0;
            var localStopwatch = Stopwatch.StartNew();
            while (localStopwatch.Elapsed < Duration)
            {
                SHA256.HashData(buffer);
                localBytes += BlockSizeBytes;
            }

            Interlocked.Add(ref totalBytesHashed, localBytes);
        });

        overallStopwatch.Stop();

        var mbPerSec = totalBytesHashed / 1024.0 / 1024.0 / overallStopwatch.Elapsed.TotalSeconds;
        return new CpuBenchmarkResult(mbPerSec, overallStopwatch.Elapsed, threadCount);
    }
}
