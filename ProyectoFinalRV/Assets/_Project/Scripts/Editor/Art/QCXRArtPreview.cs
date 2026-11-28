using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace QCXR.EditorTools.Art
{
    // Menú QCXR > Arte > Renderizar vistas previas. Genera hojas PNG de control de calidad
    // (vista del jugador, cenital y trasera de cada producto + defectos dañado/reparado).
    // Salida: Temp/QCXRPreviews (o la ruta pasada con -qcxrOut en modo batch).
    public static class QCXRArtPreview
    {
        const int Tile = 640;
        static readonly string[] Products = { "Robot", "Radio", "Lamp", "Toaster", "Clock" };
        static readonly string[] Defects = { "LooseScrew", "Dent", "HiddenCrack", "BurnedCircuit" };

        [MenuItem("Tools/QCXR/Arte/Renderizar vistas previas")]
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

        // Para modo batch: genera productos/defectos y renderiza las vistas previas.
        public static void BuildAndRender() => RunBatch(() =>
        {
            QCXRArtBuilder.BuildPhase2();
            RenderAll();
        });

        // En batchmode, salir explícitamente: con solo -quit Unity se ha quedado colgado
        // horas en el cierre (petición de red de Curl). Exit(0/1) termina el proceso al acabar.
        static void RunBatch(System.Action work)
        {
            int code = 0;
            try { work(); AssetDatabase.SaveAssets(); }
            catch (System.Exception e) { Debug.LogException(e); code = 1; }
            if (Application.isBatchMode) EditorApplication.Exit(code);
        }

        // Herramientas y manos

        static readonly string[] Tools = { "Hammer", "Screwdriver", "Magnifier" };
        static readonly string[] PoseNames = { "Open", "Fist", "Grab", "HoldHammer", "HoldScrewdriver", "HoldMagnifier", "Point", "Press", "Inspect", "IndexCurl" };

        static Camera Stage(out GameObject ground)
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional; sun.intensity = 1.25f; sun.color = new Color(1f, 0.96f, 0.9f);
            sun.shadows = LightShadows.Soft; sun.transform.rotation = Quaternion.Euler(52, -35, 0);
            var fill = new GameObject("Fill").AddComponent<Light>();
            fill.type = LightType.Directional; fill.intensity = 0.4f; fill.color = new Color(0.75f, 0.85f, 1f);
            fill.transform.rotation = Quaternion.Euler(25, 150, 0);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.42f, 0.44f, 0.48f);
            ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.transform.localScale = Vector3.one * 0.2f;
            var gm = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            gm.SetColor("_BaseColor", new Color(0.62f, 0.64f, 0.66f)); gm.SetFloat("_Smoothness", 0.2f);
            ground.GetComponent<Renderer>().sharedMaterial = gm;
            var cam = new GameObject("Cam").AddComponent<Camera>();
            cam.fieldOfView = 28f; cam.nearClipPlane = 0.01f; cam.farClipPlane = 10f;
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.16f, 0.18f, 0.21f);
            return cam;
        }

        // Envolvente real de una mano posada (las bounds del SkinnedMeshRenderer son fijas).
        static Bounds BoneBounds(GameObject hand)
        {
            var ts = hand.transform.Find("HandOffset/Wrist").GetComponentsInChildren<Transform>();
            var b = new Bounds(ts[0].position, Vector3.zero);
            foreach (var t in ts) b.Encapsulate(t.position);
            b.Expand(0.04f);
            return b;
        }

        static GameObject Load(string path) => (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path));

        static void Ground(GameObject ground, Bounds b) => ground.transform.position = new Vector3(b.center.x, b.min.y - 0.002f, b.center.z);

        static GameObject HoldTool(string tool)
        {
            var t = Load($"Assets/_Project/Prefabs/Tools/Tool_{tool}.prefab");
            var attach = t.transform.Find("Attach");
            t.transform.rotation = Quaternion.Inverse(attach.localRotation);
            t.transform.position = -(t.transform.rotation * attach.localPosition);
            return t;
        }

        static GameObject Hand(bool left, string pose)
        {
            string s = left ? "L" : "R";
            var h = Load($"Assets/_Project/Prefabs/Hands/Hand_{s}.prefab");
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>($"Assets/_Project/Animations/Hands/{(left ? "Left" : "Right")}/Hand_{s}_{pose}.anim");
            clip.SampleAnimation(h, 0f);
            return h;
        }

        [MenuItem("Tools/QCXR/Arte/Renderizar vistas previas · Fase 3")]
        public static void RenderPhase3()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            string outDir = Arg("-qcxrOut") ?? Path.GetFullPath(Path.Combine(Application.dataPath, "../Temp/QCXRPreviews"));
            Directory.CreateDirectory(outDir);
            var cam = Stage(out var ground);
            var rt = new RenderTexture(Tile, Tile, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 4 };

            // Herramientas: 3/4, lateral y detalle del extremo de trabajo
            var tsheet = new Texture2D(Tile * 3, Tile * Tools.Length, TextureFormat.RGB24, false);
            for (int i = 0; i < Tools.Length; i++)
            {
                var go = Load($"Assets/_Project/Prefabs/Tools/Tool_{Tools[i]}.prefab");
                go.transform.rotation = Quaternion.Euler(0, 90, 0);
                var b = Bounds(go);
                Ground(ground, b);
                int y = (Tools.Length - 1 - i) * Tile;
                Shot(cam, rt, b, new Vector3(-0.5f, 0.6f, -1f), tsheet, 0, 1f, y);
                Shot(cam, rt, b, new Vector3(0f, 0.15f, -1f), tsheet, 1, 1f, y);
                var tip = go.transform.Find("Tip");
                Shot(cam, rt, new Bounds(tip.position, Vector3.one * 0.06f), new Vector3(-0.4f, 0.5f, -1f), tsheet, 2, 1f, y);
                Object.DestroyImmediate(go);
            }
            Save(tsheet, Path.Combine(outDir, "Tools.png"));

            // Poses de la mano derecha (marco de modelado: palma abajo, dedos al fondo)
            var psheet = new Texture2D(Tile * 5, Tile * 2, TextureFormat.RGB24, false);
            for (int i = 0; i < PoseNames.Length; i++)
            {
                var h = Hand(false, PoseNames[i]);
                var off = h.transform.Find("HandOffset");
                h.transform.rotation = Quaternion.Inverse(off.localRotation);
                h.transform.position = Vector3.zero;
                var b = BoneBounds(h);
                Ground(ground, b);
                Shot(cam, rt, b, new Vector3(0.45f, 0.8f, 0.7f), psheet, i % 5, 0.85f, i < 5 ? Tile : 0);
                Object.DestroyImmediate(h);
            }
            Save(psheet, Path.Combine(outDir, "HandPoses.png"));

            // Cada mano sujetando cada herramienta por su Attach
            var gsheet = new Texture2D(Tile * 4, Tile * Tools.Length, TextureFormat.RGB24, false);
            string[] holds = { "HoldHammer", "HoldScrewdriver", "HoldMagnifier" };
            for (int i = 0; i < Tools.Length; i++)
            {
                int y = (Tools.Length - 1 - i) * Tile;
                for (int side = 0; side < 2; side++)
                {
                    bool left = side == 1;
                    var h = Hand(left, holds[i]);
                    var t = HoldTool(Tools[i]);
                    var b = Bounds(h); b.Encapsulate(Bounds(t));
                    Ground(ground, b);
                    float sx = left ? -1f : 1f;
                    Shot(cam, rt, b, new Vector3(sx * 0.9f, 0.45f, -0.35f), gsheet, side * 2, 1f, y);
                    Shot(cam, rt, b, new Vector3(-sx * 0.7f, 0.6f, -0.6f), gsheet, side * 2 + 1, 1f, y);
                    Object.DestroyImmediate(h); Object.DestroyImmediate(t);
                }
            }
            Save(gsheet, Path.Combine(outDir, "HandsHoldingTools.png"));
            Object.DestroyImmediate(ground);
            Debug.Log("[QCXR Art] Vistas previas Fase 3 en " + outDir);
        }

        public static void BuildAndRenderPhase3() => RunBatch(() =>
        {
            QCXRToolBuilder.BuildTools();
            QCXRHandBuilder.BuildHands();
            RenderPhase3();
        });
    }
}
