using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace QCXR.EditorTools.Art
{
    /// Build de PRUEBA para Quest (Android · IL2CPP · ARM64) de QCXR_MainScene. No cambia ajustes del proyecto:
    /// usa los de Player Settings tal como están. El APK va a la ruta de -qcxrOut (o Temp/QCXRBuild).
    public static class QCXRBuildCheck
    {
        [MenuItem("QCXR/Arte/Build de prueba Quest (APK)")]
        public static void Build() => Run(false);

        public static void BatchBuild() => Run(true);

        static void Run(bool batch)
        {
            int code = 0;
            try
            {
                string outDir = Arg("-qcxrOut") ?? Path.GetFullPath(Path.Combine(Application.dataPath, "../Temp/QCXRBuild"));
                Directory.CreateDirectory(outDir);
                var opts = new BuildPlayerOptions
                {
                    scenes = new[] { QCXRSceneAssembler.ScenePath },
                    target = BuildTarget.Android,
                    targetGroup = BuildTargetGroup.Android,
                    locationPathName = Path.Combine(outDir, "QCXR_Test.apk"),
                    options = BuildOptions.None,
                };
                Debug.Log($"[QCXR Build] Inicio · backend {PlayerSettings.GetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Android)} · arquitecturas {PlayerSettings.Android.targetArchitectures}");
                var report = BuildPipeline.BuildPlayer(opts);
                var s = report.summary;
                Debug.Log($"[QCXR Build] Resultado: {s.result} · errores {s.totalErrors} · avisos {s.totalWarnings} · tamaño {s.totalSize / (1024f * 1024f):0.0} MB · {s.totalTime.TotalMinutes:0.0} min");
                foreach (var step in report.steps)
                    foreach (var m in step.messages.Where(m => m.type == LogType.Error || m.type == LogType.Exception))
                        Debug.Log($"[QCXR Build] ERROR en '{step.name}': {m.content}");
                if (s.result != BuildResult.Succeeded) code = 1;
            }
            catch (System.Exception e) { Debug.LogException(e); code = 1; }
            Debug.Log("[QCXR Art] Batch terminado con código " + code);
            if (batch && Application.isBatchMode) EditorApplication.Exit(code);
        }

        static string Arg(string name)
        {
            var a = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < a.Length - 1; i++) if (a[i] == name) return a[i + 1];
            return null;
        }
    }
}
