using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace QCXR.EditorTools
{
    // Menú QCXR > Herramientas > Añadir reaparición (ToolRespawn) / Probar reaparición.
    // Añade ToolRespawn a los 3 prefabs de herramienta (la escena lo recibe por ser instancias de prefab,
    // sin tocar el archivo de la escena) y prueba el comportamiento con física real en modo editor.
    public static class QCXRToolRespawnSetup
    {
        static readonly string[] Tools = { "Hammer", "Screwdriver", "Magnifier" };
        const string ToolDir = "Assets/_Project/Prefabs/Tools";

        [MenuItem("Tools/QCXR/Herramientas/Añadir reaparición (ToolRespawn)")]
        public static void AddToPrefabs()
        {
            foreach (var t in Tools)
            {
                string path = $"{ToolDir}/Tool_{t}.prefab";
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    if (root.GetComponent<ToolRespawn>() == null) root.AddComponent<ToolRespawn>();
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            AssetDatabase.SaveAssets();
            Debug.Log("[QCXR Respawn] ToolRespawn añadido a Tool_Hammer, Tool_Screwdriver y Tool_Magnifier.");
        }

        [MenuItem("Tools/QCXR/Herramientas/Probar reaparición (física en editor)")]
        public static bool RunTests()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return false;
            var scene = EditorSceneManager.OpenScene("Assets/_Project/Scenes/QCXR_MainScene.unity", OpenSceneMode.Single);
            var tools = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<ToolRespawn>(true)).ToList();
            var prevMode = Physics.simulationMode;
            Physics.simulationMode = SimulationMode.Script;
            int pass = 0, fail = 0;
            void Check(string what, bool ok, string detail)
            {
                if (ok) pass++; else fail++;
                Debug.Log($"[QCXR Respawn] {(ok ? "OK   " : "FALLA")} {what} · {detail}");
            }
            void Simulate(ToolRespawn r, float seconds)
            {
                const float dt = 0.02f;
                for (float t = 0; t < seconds; t += dt) { Physics.Simulate(dt); r.Tick(dt); }
            }
            Check("ToolRespawn en las 3 herramientas de la escena", tools.Count == 3, $"{tools.Count} encontradas");

            foreach (var r in tools)
            {
                var body = r.GetComponent<Rigidbody>();
                r.CaptureHome();
                Vector3 home = r.HomePosition;
                Quaternion homeRot = r.transform.rotation;

                // A) En el suelo, fuera de alcance -> vuelve a su sitio
                r.transform.position = new Vector3(home.x + 0.1f, 0.05f, home.z - 0.4f);
                body.position = r.transform.position;
                Simulate(r, 1.2f);
                float dA = Vector3.Distance(r.transform.position, home);
                Check($"{r.name}: en el suelo → vuelve al banco", dA < 0.03f, $"a {dA * 100:0.0} cm de su sitio");

                // B) Caída física real por el borde delantero del banco -> acaba de nuevo en su sitio
                r.transform.SetPositionAndRotation(new Vector3(home.x, home.y + 0.05f, home.z - 0.22f), homeRot);
                body.position = r.transform.position;
                Simulate(r, 3f);
                float dB = Vector3.Distance(r.transform.position, home);
                Check($"{r.name}: cae del banco por física → reaparece y se queda", dB < 0.04f, $"a {dB * 100:0.0} cm de su sitio tras 3 s");

                // C) Sobre la banda (al alcance del jugador) -> no debe reaparecer
                Vector3 onBelt = new Vector3(Mathf.Clamp(home.x, -0.3f, 0.3f), 0.97f, 0.62f);
                r.transform.SetPositionAndRotation(onBelt, homeRot);
                body.position = onBelt;
                Simulate(r, 2f);
                float dC = Vector3.Distance(r.transform.position, home);
                Check($"{r.name}: sobre la banda (alcanzable) → se queda", dC > 0.15f && !r.IsLost, $"a {dC * 100:0.0} cm de su sitio, IsLost={r.IsLost}");

                r.RespawnNow();
            }

            Physics.simulationMode = prevMode;
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);   // descarta cambios: la escena no se guarda
            Debug.Log($"[QCXR Respawn] Resultado: {pass} OK · {fail} fallas");
            return fail == 0;
        }

        // Entrada batch: añade el componente, prueba y sale con EditorApplication.Exit.
        public static void BatchAddAndTest()
        {
            int code = 0;
            try { AddToPrefabs(); if (!RunTests()) code = 1; }
            catch (System.Exception e) { Debug.LogException(e); code = 1; }
            Debug.Log("[QCXR Art] Batch terminado con código " + code);
            if (Application.isBatchMode) EditorApplication.Exit(code);
        }
    }
}
