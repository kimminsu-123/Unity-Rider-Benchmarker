using System.Diagnostics;
using UnityRiderBench.Models;

namespace UnityRiderBench.Benchmark;

// Library/ 캐시 갱신처럼 다수의 소용량 파일이 오가는 시나리오를 근사하기 위해
// 순차 처리량(대용량 파일)과 랜덤 IOPS(4KB 블록)를 함께 측정한다.
public static class DiskIoBenchmark
{
    private const int SequentialFileSizeBytes = 64 * 1024 * 1024;
    private const int SequentialChunkSizeBytes = 1 * 1024 * 1024;
    private const int RandomFileSizeBytes = 16 * 1024 * 1024;
    private const int RandomBlockSizeBytes = 4 * 1024;
    private const int RandomOpCount = 500;

    // 순차 측정은 64회 청크뿐인 단일 샘플이라 표본이 작아, 같은 물리 드라이브에서도 실행마다
    // 값이 수 배씩 흔들리는 것이 실사용 중 확인됐다(예: 같은 NVMe에서 447 / 346 / 106 MB/s로 관측,
    // 2026-08-06). JIT 워밍업이나 일시적 백그라운드 I/O 경합으로 추정되나 정확한 원인은 확인 필요.
    // 여러 번 측정해 중앙값을 취해 단발성 노이즈가 등급 판정을 흔드는 것을 줄인다.
    private const int SequentialTrialCount = 3;

    public static DiskIoBenchmarkResult Run(string targetDirectory)
    {
        Directory.CreateDirectory(targetDirectory);
        var seqPath = Path.Combine(targetDirectory, $".urbench-seq-{Guid.NewGuid():N}.tmp");
        var randPath = Path.Combine(targetDirectory, $".urbench-rand-{Guid.NewGuid():N}.tmp");

        try
        {
            var seqWriteMbPerSec = Median(SequentialTrialCount, () => MeasureSequentialWrite(seqPath));
            var seqReadMbPerSec = Median(SequentialTrialCount, () => MeasureSequentialRead(seqPath));

            PrepareRandomFile(randPath);
            var randWriteIops = MeasureRandomIops(randPath, isWrite: true);
            var randReadIops = MeasureRandomIops(randPath, isWrite: false);

            return new DiskIoBenchmarkResult(targetDirectory, seqWriteMbPerSec, seqReadMbPerSec, randWriteIops, randReadIops);
        }
        finally
        {
            TryDelete(seqPath);
            TryDelete(randPath);
        }
    }

    private static double Median(int trialCount, Func<double> measure)
    {
        var values = new double[trialCount];
        for (var i = 0; i < trialCount; i++)
        {
            values[i] = measure();
        }

        Array.Sort(values);
        return values[trialCount / 2];
    }

    private static double MeasureSequentialWrite(string path)
    {
        var buffer = new byte[SequentialChunkSizeBytes];
        Random.Shared.NextBytes(buffer);
        var chunkCount = SequentialFileSizeBytes / SequentialChunkSizeBytes;

        var stopwatch = Stopwatch.StartNew();
        // WriteThrough: OS 쓰기 캐시를 우회해 실제 디스크 쓰기 속도에 가깝게 측정
        using (var fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, SequentialChunkSizeBytes, FileOptions.WriteThrough))
        {
            for (var i = 0; i < chunkCount; i++)
            {
                fs.Write(buffer, 0, buffer.Length);
            }
        }
        stopwatch.Stop();

        return SequentialFileSizeBytes / 1024.0 / 1024.0 / stopwatch.Elapsed.TotalSeconds;
    }

    private static double MeasureSequentialRead(string path)
    {
        var buffer = new byte[SequentialChunkSizeBytes];

        var stopwatch = Stopwatch.StartNew();
        using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, SequentialChunkSizeBytes, FileOptions.SequentialScan))
        {
            while (fs.Read(buffer, 0, buffer.Length) > 0)
            {
            }
        }
        stopwatch.Stop();

        // 직전에 쓴 파일을 곧바로 읽으므로 OS 읽기 캐시의 영향을 완전히 배제하지는 못함(확인 필요).
        return SequentialFileSizeBytes / 1024.0 / 1024.0 / stopwatch.Elapsed.TotalSeconds;
    }

    private static void PrepareRandomFile(string path)
    {
        using var fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
        fs.SetLength(RandomFileSizeBytes);
    }

    private static double MeasureRandomIops(string path, bool isWrite)
    {
        var buffer = new byte[RandomBlockSizeBytes];
        if (isWrite)
        {
            Random.Shared.NextBytes(buffer);
        }

        var maxOffset = RandomFileSizeBytes - RandomBlockSizeBytes;
        var access = isWrite ? FileAccess.Write : FileAccess.Read;
        var options = isWrite ? FileOptions.WriteThrough : FileOptions.RandomAccess;

        using var fs = new FileStream(path, FileMode.Open, access, FileShare.ReadWrite, RandomBlockSizeBytes, options);

        var stopwatch = Stopwatch.StartNew();
        for (var i = 0; i < RandomOpCount; i++)
        {
            var offset = Random.Shared.Next(0, maxOffset);
            fs.Seek(offset, SeekOrigin.Begin);
            if (isWrite)
            {
                fs.Write(buffer, 0, buffer.Length);
            }
            else
            {
                fs.ReadExactly(buffer, 0, buffer.Length);
            }
        }
        stopwatch.Stop();

        return RandomOpCount / stopwatch.Elapsed.TotalSeconds;
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
