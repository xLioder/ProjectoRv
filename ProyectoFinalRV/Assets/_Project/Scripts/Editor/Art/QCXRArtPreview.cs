using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace QCXR.EditorTools.Art
{
    /// Menú QCXR > Arte > Renderizar vistas previas. Genera hojas PNG de control de calidad
    /// (vista del jugador, cenital y trasera de cada producto + defectos dañado/reparado).
    /// Salida: Temp/QCXRPreviews (o la ruta pasada con -qcxrOut en modo batch).
    public static class QCXRArtPreview
    {
        const int Tile = 640;
        static readonly string[] Products = { "Robot", "Radio", "Lamp", "Toaster", "Clock" };
        static readonly string[] Defects = { "LooseScrew", "Dent", "HiddenCrack", "BurnedCircuit" };

        [MenuItem("QCXR/Arte/Renderizar vistas previas")]
        public static void RenderAll()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            string outDir = Arg("-qcxrOut") ?? Path.GetFullPath(Path.Combine(Application.dataPath, "../Temp/QCXRPreviews"));
            Directory.CreateDirectory(outDir);

            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional; sun.intensity = 1.25f; sun.color = new Color(1f, 0.96f, 0.9f);
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(52, -35, 0);
            var fill = new GameObject("Fill").AddComponent<Light>();
            fill.type = LightType.Directional; fill.intensity = 0.35f; fill.color = new Color(0.75f, 0.85f, 1f);
            fill.shadows = LightShadows.None;
            fill.transform.rotation = Quaternion.Euler(25, 150, 0);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.42f, 0.44f, 0.48f);

            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.transform.localScale = Vector3.one * 0.2f;
            var gm = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            gm.SetColor("_BaseColor", new Color(0.62f, 0.64f, 0.66f));
            gm.SetFloat("_Smoothness", 0.2f);
            ground.GetComponent<Renderer>().sharedMaterial = gm;

            var cam = new GameObject("Cam").AddComponent<Camera>();
            cam.fieldOfView = 28f; cam.nearClipPlane = 0.01f; cam.farClipPlane = 10f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.16f, 0.18f, 0.21f);
            var rt = new RenderTexture(Tile, Tile, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 4 };
            cam.targetTexture = rt;

            // Productos: jugador (frente-arriba), cenital, trasera 3/4
            var views = new[] { new Vector3(-0.3f, 0.85f, -1f), new Vector3(0.02f, 1f, -0.12f), new Vector3(0.9f, 0.55f, 0.85f) };
            foreach (var p in Products)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{QCXRArtBuilder.ProductPrefabDir}/Product_{p}.prefab");
                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                var b = Bounds(go);
                go.transform.position = new Vector3(0, -b.min.y + 0.0005f, 0);
                b = Bounds(go);
                ground.transform.position = Vector3.zero;
                var sheet = new Texture2D(Tile * views.Length, Tile, TextureFormat.RGB24, false);
                for (int i = 0; i < views.Length; i++) Shot(cam, rt, b, views[i], sheet, i, 1.0f);
                Save(sheet, Path.Combine(outDir, $"Product_{p}.png"));
                Object.DestroyImmediate(go);
            }

            // Defectos: fila superior = Visual (dañado), inferior = Fixed (reparado)
            var dsheet = new Texture2D(Tile * Defects.Length, Tile * 2, TextureFormat.RGB24, false);
            var plate = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var pm = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            pm.SetColor("_BaseColor", new Color(0.92f, 0.88f, 0.8f));
            plate.GetComponent<Renderer>().sharedMaterial = pm;
            plate.transform.localScale = new Vector3(0.12f, 0.01f, 0.12f);
            plate.transform.position = new Vector3(0, -0.005f, 0);
            for (int i = 0; i < Defects.Length; i++)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{QCXRArtBuilder.DefectPrefabDir}/Defect_{Defects[i]}.prefab");
                for (int row = 0; row < 2; row++)
                {
                    var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                    go.transform.Find("Visual").gameObject.SetActive(row == 0);
                    go.transform.Find("Fixed").gameObject.SetActive(row == 1);
                    var b = new Bounds(new Vector3(0, 0.006f, 0), new Vector3(0.075f, 0.012f, 0.06f));
                    Shot(cam, rt, b, new Vector3(-0.25f, 1f, -0.8f), dsheet, i, 1f, row == 0 ? Tile : 0);
                    Object.DestroyImmediate(go);
                }
            }
            Save(dsheet, Path.Combine(outDir, "Defects.png"));
            Object.DestroyImmediate(plate);

            // Alineación a escala real de los 5 productos
            var all = new List<GameObject>();
            float x = -0.45f;
            foreach (var p in Products)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{QCXRArtBuilder.ProductPrefabDir}/Product_{p}.prefab");
                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                var b = Bounds(go);
                go.transform.position = new Vector3(x - b.min.x, -b.min.y + 0.0005f, 0);
                x += b.size.x + 0.05f;
                all.Add(go);
            }
            ground.transform.localScale = Vector3.one * 0.3f;
            var lineupRt = new RenderTexture(Tile * 3, Tile, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 4 };
            cam.targetTexture = lineupRt;
            cam.fieldOfView = 22f;
            cam.transform.position = new Vector3(0.05f, 0.75f, -1.1f);
            cam.transform.LookAt(new Vector3(0.05f, 0.06f, 0));
            cam.Render();
            RenderTexture.active = lineupRt;
            var lineup = new Texture2D(Tile * 3, Tile, TextureFormat.RGB24, false);
            lineup.ReadPixels(new Rect(0, 0, Tile * 3, Tile), 0, 0);
            Save(lineup, Path.Combine(outDir, "Lineup.png"));
            RenderTexture.active = null;
            foreach (var go in all) Object.DestroyImmediate(go);
            Debug.Log("[QCXR Art] Vistas previas en " + outDir);
        }

        static void Shot(Camera cam, RenderTexture rt, Bounds b, Vector3 dir, Texture2D sheet, int tileX, float pad, int y = 0)
        {
            cam.targetTexture = rt;
            dir.Normalize();
            float r = b.extents.magnitude;
            float dist = r / Mathf.Sin(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * pad;
            cam.transform.position = b.center + dir * dist;
            cam.transform.LookAt(b.center, Mathf.Abs(dir.y) > 0.98f ? Vector3.forward : Vector3.up);
            cam.Render();
            RenderTexture.active = rt;
            sheet.ReadPixels(new Rect(0, 0, Tile, Tile), tileX * Tile, y);
            RenderTexture.active = null;
        }

        static Bounds Bounds(GameObject go)
        {
            var rs = go.GetComponentsInChildren<Renderer>();
            var b = rs[0].bounds;
            foreach (var r in rs) b.Encapsulate(r.bounds);
            return b;
        }

        static void Save(Texture2D t, string path)
        {
            t.Apply();
            File.WriteAllBytes(path, t.EncodeToPNG());
            Object.DestroyImmediate(t);
        }

        static string Arg(string name)
        {
            var a = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < a.Length - 1; i++) if (a[i] == name) return a[i + 1];
            return null;
        }

        /// Punto de entrada batch: construye la Fase 2 y renderiza las vistas previas.
        public static void BuildAndRender()
        {
            QCXRArtBuilder.BuildPhase2();
            RenderAll();
        }
    }
}
