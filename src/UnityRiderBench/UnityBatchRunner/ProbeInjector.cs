namespace UnityRiderBench.UnityBatchRunner;

// CLI 실행마다 probe 스크립트를 대상 프로젝트에 임시로 복사했다가, 배치 실행이 끝나면
// (예외가 나더라도) 자동으로 삭제한다 — 프로젝트에 흔적을 남기지 않기 위한 방식.
public sealed class ProbeInjector : IDisposable
{
    private const string ProbeFileName = "DomainReloadProbe.cs";

    private readonly string _targetDirPath;
    private readonly string _targetFilePath;
    private bool _injected;

    public ProbeInjector(string projectPath)
    {
        _targetDirPath = Path.Combine(projectPath, "Assets", "Editor", "UnityRiderBenchProbe");
        _targetFilePath = Path.Combine(_targetDirPath, ProbeFileName);
    }

    public void Inject()
    {
        var templatePath = Path.Combine(AppContext.BaseDirectory, "ProbeScript~", ProbeFileName);
        if (!File.Exists(templatePath))
        {
            throw new FileNotFoundException("Probe 스크립트 템플릿을 찾을 수 없습니다.", templatePath);
        }

        Directory.CreateDirectory(_targetDirPath);
        File.Copy(templatePath, _targetFilePath, overwrite: true);
        _injected = true;
    }

    public void Dispose()
    {
        if (!_injected)
        {
            return;
        }

        try
        {
            if (File.Exists(_targetFilePath))
            {
                File.Delete(_targetFilePath);
            }

            var metaPath = _targetFilePath + ".meta";
            if (File.Exists(metaPath))
            {
                File.Delete(metaPath);
            }

            if (Directory.Exists(_targetDirPath) && !Directory.EnumerateFileSystemEntries(_targetDirPath).Any())
            {
                Directory.Delete(_targetDirPath);
            }
        }
        catch (IOException)
        {
            // 임시 스크립트 삭제 실패는 배치 실행 결과 자체에 영향을 주지 않으므로 예외를 전파하지 않는다.
        }
    }
}
