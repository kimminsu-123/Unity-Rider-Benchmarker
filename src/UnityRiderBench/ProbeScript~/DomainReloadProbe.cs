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
    // afterAssemblyReload 콜백이 실제로 완료된 뒤 이 스크립트가 직접 EditorApplication.Exit()로 종료해야
    // 리로드 소요 시간을 정확히 측정할 수 있다.
    public static class DomainReloadProbe
    {
        private static string resultPath;
        private static Stopwatch importStopwatch;
        private static Stopwatch reloadStopwatch;

        public static void Run()
        {
            resultPath = GetArg("-urbenchResultPath");
            if (string.IsNullOrEmpty(resultPath))
            {
                EditorApplication.Exit(1);
                return;
            }

            importStopwatch = Stopwatch.StartNew();
            AssetDatabase.Refresh();
            importStopwatch.Stop();

            AssemblyReloadEvents.afterAssemblyReload += OnAfterAssemblyReload;

            reloadStopwatch = Stopwatch.StartNew();
            EditorUtility.RequestScriptReload();
        }

        private static void OnAfterAssemblyReload()
        {
            AssemblyReloadEvents.afterAssemblyReload -= OnAfterAssemblyReload;
            reloadStopwatch.Stop();

            var json = "{"
                + $"\"UnityVersion\":\"{Application.unityVersion}\","
                + $"\"DomainReloadSeconds\":{reloadStopwatch.Elapsed.TotalSeconds.ToString(CultureInfo.InvariantCulture)},"
                + $"\"AssemblyImportSeconds\":{importStopwatch.Elapsed.TotalSeconds.ToString(CultureInfo.InvariantCulture)}"
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
