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

    public static DiskIoBenchmarkResult Run(string targetDirectory)
    {
        Directory.CreateDirectory(targetDirectory);
        var seqPath = Path.Combine(targetDirectory, $".urbench-seq-{Guid.NewGuid():N}.tmp");
        var randPath = Path.Combine(targetDirectory, $".urbench-rand-{Guid.NewGuid():N}.tmp");

        try
        {
            var seqWriteMbPerSec = MeasureSequentialWrite(seqPath);
            var seqReadMbPerSec = MeasureSequentialRead(seqPath);

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
