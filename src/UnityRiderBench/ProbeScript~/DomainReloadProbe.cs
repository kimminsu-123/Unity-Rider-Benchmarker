#if UNITY_EDITOR
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace UnityRiderBench.Probe
{
    // UnityRiderBench CLI가 배치 모드 실행 시 대상 프로젝트의 Assets/Editor/에 임시로 복사하는 프로브 스크립트.
    // "-executeMethod UnityRiderBench.Probe.DomainReloadProbe.Run" 으로 호출된다.
    // -quit 옵션은 사용하지 않는다: 도메인 리로드는 RequestScriptReload() 호출 이후 비동기로 일어나므로,
    // 리로드가 실제로 완료된 뒤 이 스크립트가 직접 EditorApplication.Exit()로 종료해야
    // 리로드 소요 시간을 정확히 측정할 수 있다.
    //
    // [InitializeOnLoad] 필수: RequestScriptReload()가 트리거하는 도메인 리로드는 static 상태를
    // 전부 리셋하므로, Run() 안에서 AssemblyReloadEvents.afterAssemblyReload += 로 건 구독은
    // 리로드 후 새 도메인에서는 사라져 콜백이 다시는 안 불린다(Exit()도 못 불려 배치 프로세스가
    // 영원히 멈춤 — 실사용 중 재현됨). 대신 도메인 리로드를 넘어 값이 살아남는 SessionState에
    // 측정 상태를 저장해두고, 리로드마다 다시 실행되는 static 생성자에서 이어받는다.
    [InitializeOnLoad]
    public static class DomainReloadProbe
    {
        private const string ResultPathKey = "UnityRiderBench.Probe.ResultPath";
        private const string ImportSecondsKey = "UnityRiderBench.Probe.ImportSeconds";
        private const string ReloadStartTicksKey = "UnityRiderBench.Probe.ReloadStartTicks";
        private const string WaitingKey = "UnityRiderBench.Probe.Waiting";

        static DomainReloadProbe()
        {
            if (!SessionState.GetBool(WaitingKey, false))
            {
                return;
            }

            SessionState.SetBool(WaitingKey, false);
            CompleteMeasurement();
        }

        public static void Run()
        {
            var resultPath = GetArg("-urbenchResultPath");
            if (string.IsNullOrEmpty(resultPath))
            {
                EditorApplication.Exit(1);
                return;
            }

            var importStopwatch = Stopwatch.StartNew();
            AssetDatabase.Refresh();
            importStopwatch.Stop();

            SessionState.SetString(ResultPathKey, resultPath);
            SessionState.SetString(ImportSecondsKey, importStopwatch.Elapsed.TotalSeconds.ToString(CultureInfo.InvariantCulture));
            SessionState.SetString(ReloadStartTicksKey, DateTime.UtcNow.Ticks.ToString(CultureInfo.InvariantCulture));
            SessionState.SetBool(WaitingKey, true);

            EditorUtility.RequestScriptReload();
        }

        private static void CompleteMeasurement()
        {
            var resultPath = SessionState.GetString(ResultPathKey, string.Empty);
            if (string.IsNullOrEmpty(resultPath))
            {
                return;
            }

            var startTicks = long.Parse(SessionState.GetString(ReloadStartTicksKey, "0"), CultureInfo.InvariantCulture);
            var importSeconds = SessionState.GetString(ImportSecondsKey, "0");
            var reloadSeconds = TimeSpan.FromTicks(DateTime.UtcNow.Ticks - startTicks).TotalSeconds;

            var json = "{"
                + $"\"UnityVersion\":\"{Application.unityVersion}\","
                + $"\"DomainReloadSeconds\":{reloadSeconds.ToString(CultureInfo.InvariantCulture)},"
                + $"\"AssemblyImportSeconds\":{importSeconds}"
                + "}";

            File.WriteAllText(resultPath, json);
            EditorApplication.Exit(0);
        }

        private static string GetArg(string name)
        {
            var args = Environment.GetCommandLineArgs();
            for (var i = 0; i < args.Length - 1; i++)
            {
                if (string.Equals(args[i], name, StringComparison.Ordinal))
                {
                    return args[i + 1];
                }
            }

            return null;
        }
    }
}
#endif
