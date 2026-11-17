using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace QCXR.EditorTools.Art
{
    /// Menú QCXR > Arte > Fase 4 · Montar QCXR_MainScene.
    /// Copia Assets/Scenes/SampleScene.unity (que NO se modifica) a Assets/_Project/Scenes/QCXR_MainScene.unity y,
    /// sobre esa copia, sustituye la parte visual de los placeholders por los prefabs de arte. Los objetos con
    /// scripts (ConveyorBelt, ProductStation, XRButton, RandomEventSystem, UI...) y sus referencias se conservan:
    /// solo se les quitan las mallas primitivas (sus colliders siguen funcionando) y se les añaden visuales hijos.
    /// Reproducible: cada ejecución vuelve a partir de SampleScene.
    public static class QCXRSceneAssembler
    {
        public const string SourceScene = "Assets/Scenes/SampleScene.unity";
        public const string ScenePath = "Assets/_Project/Scenes/QCXR_MainScene.unity";
        const string Env = QCXRStationBuilder.PrefabDir;

        [MenuItem("QCXR/Arte/Fase 4-6 · Montar QCXR_MainScene completa")]
        public static void Assemble()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            QCXRArtBuilder.BuildMaterials();          // paleta ampliada con colores de entorno
            QCXRStationBuilder.BuildPrefabs();
            QCXRFxBuilder.BuildAll();                 // Fase 5: efectos, clips y parches de prefabs
            QCXRUIBuilder.BuildSprites();             // Fase 7: iconos (render) y sprites de UI

            QCXRPrefabBuilder.EnsureFolder("Assets/_Project/Scenes");
            AssetDatabase.DeleteAsset(ScenePath);
            if (!AssetDatabase.CopyAsset(SourceScene, ScenePath)) throw new System.Exception("No se pudo copiar " + SourceScene);
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var all = AllTransforms(scene);

            Transform Find(string name) => all.FirstOrDefault(t => t != null && t.name == name) ?? throw new System.Exception("No existe en la escena: " + name);
            IEnumerable<Transform> FindAll(string name) => all.Where(t => t != null && t.name == name).ToList();

            var envGroup = Find("Environment");
            var factory = Find("Factory");
            var stations = Find("Stations");

            // ── Jugador: XR Origin en el suelo, frente a la banda (estaba a 0.87 m de altura y dentro de la banda) ──
            var origin = all.FirstOrDefault(t => t != null && t.name.StartsWith("XR Origin"));
            if (origin != null) { origin.position = Vector3.zero; origin.rotation = Quaternion.identity; }

            // ── Nave ──
            Strip(Find("Floor"));
            Place("Env_Room", envGroup, Vector3.zero);

            // ── Banda ──
            foreach (var n in new[] { "BeltSurface", "RailFront", "RailBack" }) Strip(Find(n));
            foreach (var t in FindAll("Leg").Concat(FindAll("InspectionMark"))) Object.DestroyImmediate(t.gameObject);
            var belt = Place("Env_ConveyorBelt", factory, new Vector3(0, 0, 0.75f));
            var beltComp = Find("BeltZone").GetComponent<ConveyorBelt>();
            var so = new SerializedObject(beltComp);
            so.FindProperty("beltRenderer").objectReferenceValue = belt.transform.Find("BeltSurface").GetComponent<MeshRenderer>();
            so.FindProperty("textureScroll").floatValue = 1f / QCXRStationBuilder.BeltTile;   // la textura avanza a la misma velocidad que los productos
            so.ApplyModifiedPropertiesWithoutUndo();

            // ── Banco de herramientas (delante de la banda, al alcance) + herramientas en su sitio ──
            var table = Find("ToolTable");
            Strip(table);
            table.position = QCXRStationBuilder.BenchAnchor + new Vector3(0, QCXRStationBuilder.BenchTop / 2, 0);
            table.localScale = new Vector3(1.02f, QCXRStationBuilder.BenchTop, 0.2f);
            var bench = Place("Env_ToolBench", envGroup, QCXRStationBuilder.BenchAnchor);
            PlaceTool(Find("Tool_Hammer"), bench.transform.Find("Spot_Hammer"), Vector3.left, 0.0215f, Vector3.forward);   // de lado: cabeza en horizontal
            PlaceTool(Find("Tool_Screwdriver"), bench.transform.Find("Spot_Screwdriver"), Vector3.left, 0.019f);
            PlaceTool(Find("Tool_Magnifier"), bench.transform.Find("Spot_Magnifier"), Vector3.right, 0.0165f);

            // ── Consola de botones (frente hacia el jugador) ──
            var console = Find("Console");
            Strip(console);
            console.position = QCXRStationBuilder.ConsoleAnchor + new Vector3(0, QCXRStationBuilder.ConsoleSize.y / 2, 0);
            console.localScale = QCXRStationBuilder.ConsoleSize;
            Place("Env_ButtonConsole", stations, QCXRStationBuilder.ConsoleAnchor);
            SetupButton(Find("Btn_StartShift"), "Env_ButtonCap_Green", QCXRStationBuilder.ConsoleAnchor + QCXRStationBuilder.StartButtonLocal);
            SetupButton(Find("Btn_ResetBelt"), "Env_ButtonCap_Red", QCXRStationBuilder.ConsoleAnchor + QCXRStationBuilder.ResetButtonLocal);

            // ── Estantería: visual + punto de colocación por hueco (el producto queda apoyado y mirando al jugador) ──
            foreach (var n in new[] { "RackBase", "RackTopBoard" }) Strip(Find(n));
            foreach (var t in FindAll("RackPost")) Object.DestroyImmediate(t.gameObject);
            var rack = Place("Env_ShelfRack", stations, new Vector3(-0.85f, 0, -0.2f));
            foreach (var (type, local, _) in QCXRStationBuilder.Bays)
            {
                var shelf = Find("Shelf_" + type);
                var marker = shelf.Find("Marker");
                if (marker != null) Object.DestroyImmediate(marker.gameObject);
                var zone = rack.transform.Find($"Bay_{type}/PlacementZone");
                shelf.position = new Vector3(zone.position.x, shelf.position.y, zone.position.z);
                var attach = shelf.Find("AttachPoint") ?? new GameObject("AttachPoint").transform;
                attach.SetParent(shelf, false);
                attach.position = zone.position + Vector3.up * ProductHalfHeight(type);
                attach.rotation = Quaternion.Euler(0, -90, 0);           // frente del producto (-Z) hacia +X (jugador)
                // Etiqueta (texto lo pone ProductStation): en el canto frontal de la balda, mirando al jugador
                var label = shelf.GetComponentInChildren<TMPro.TMP_Text>(true);
                if (label != null)
                {
                    bool upper = local.y > 1f;
                    label.transform.position = new Vector3(-0.85f + 0.214f, local.y - (upper ? 0.02f : 0.03f), zone.position.z);
                    label.transform.rotation = Quaternion.Euler(0, -90, 0);
                    StyleLabel(label, upper ? 0.28f : 0.36f, new Color(0.08f, 0.08f, 0.1f), new Vector2(0.26f, 0.05f));
                }
                var socket = shelf.GetComponent<XRSocketInteractor>();
                var sso = new SerializedObject(socket);
                var at = sso.FindProperty("m_AttachTransform");
                if (at != null) { at.objectReferenceValue = attach; sso.ApplyModifiedPropertiesWithoutUndo(); }
            }

            // ── Cubo de desecho ──
            foreach (var n in new[] { "BinPedestal", "BinFloor", "BinWallL", "BinWallR", "BinWallF", "BinWallB" }) Strip(Find(n));
            var bin = Place("Env_DiscardBin", stations, new Vector3(0.8f, 0, -0.3f));

            // ── Indicador de atasco (se activa con el evento) y baliza de emergencia ──
            var jam = Find("JamIndicator");
            Unscale(jam); Strip(jam);
            jam.position = new Vector3(0, 1.61f, 0.75f + 0.33f);       // sobre el pórtico de inspección
            Place("Env_JamIndicatorLit", jam, jam.position, true);
            var emergency = Find("EmergencyLight");
            var lit = Place("Env_EmergencyBeaconLit", emergency, emergency.position, true);
            lit.transform.localPosition = new Vector3(0, 0.02f, 0);
            lit.transform.localRotation = Quaternion.Euler(180, 0, 0);   // cuelga del techo, cúpula hacia abajo
            var el = emergency.GetComponent<Light>();
            if (el != null) { el.range = 5f; el.intensity = 2.5f; el.shadows = LightShadows.None; }

            // ── Iluminación: una direccional con sombras + ambiente de tres colores (sin luces extra) ──
            var sun = all.FirstOrDefault(t => t != null && t.name == "Directional Light")?.GetComponent<Light>();
            if (sun != null)
            {
                sun.transform.rotation = Quaternion.Euler(55, -25, 0);
                sun.intensity = 1.15f; sun.color = new Color(1f, 0.95f, 0.88f);
                sun.shadows = LightShadows.Soft; sun.shadowStrength = 0.75f;
            }
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.62f, 0.64f, 0.68f);
            RenderSettings.ambientEquatorColor = new Color(0.48f, 0.47f, 0.45f);
            RenderSettings.ambientGroundColor = new Color(0.26f, 0.25f, 0.24f);
            RenderSettings.fog = false;

            // ── Fase 5: efectos en escena, puente de presentación, botones, paneles y sello ──
            QCXRFxBuilder.HookScene(Find, Find("QCXR_Scene"), belt, rack, bin);

            // ── Fase 6: manos en el XR Origin, jerarquía del brief, productos de referencia, Build Settings ──
            QCXRSceneFinalizer.Finalize(scene);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("[QCXR Art] Fase 4: QCXR_MainScene montada en " + ScenePath);
        }

        // ═════════════════════════ UTILIDADES ═════════════════════════

        static List<Transform> AllTransforms(Scene scene)
        {
            var list = new List<Transform>();
            foreach (var r in scene.GetRootGameObjects()) list.AddRange(r.GetComponentsInChildren<Transform>(true));
            return list;
        }

        /// Quita la malla primitiva de un placeholder conservando su collider y sus componentes.
        static void Strip(Transform t)
        {
            var r = t.GetComponent<MeshRenderer>(); if (r) Object.DestroyImmediate(r);
            var f = t.GetComponent<MeshFilter>(); if (f) Object.DestroyImmediate(f);
        }

        /// Deja la escala en 1 sin mover a los hijos (los placeholders "Box" escalaban el objeto entero).
        static void Unscale(Transform t)
        {
            var kids = new List<(Transform, Vector3, Quaternion, Vector3)>();
            foreach (Transform c in t) kids.Add((c, c.position, c.rotation, c.lossyScale));
            var box = t.GetComponent<BoxCollider>();
            Vector3 world = Vector3.Scale(box ? box.size : Vector3.one, t.lossyScale);
            t.localScale = Vector3.one;
            if (box) box.size = world;
            foreach (var (c, p, r, s) in kids)
            {
                c.SetPositionAndRotation(p, r);
                var ps = c.parent.lossyScale;
                c.localScale = new Vector3(s.x / ps.x, s.y / ps.y, s.z / ps.z);
            }
        }

        static GameObject Place(string prefab, Transform parent, Vector3 worldPos, bool keepActiveFromParent = false)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>($"{Env}/{prefab}.prefab");
            var go = (GameObject)PrefabUtility.InstantiatePrefab(asset, parent);
            go.transform.position = worldPos;
            go.transform.rotation = parent.rotation;
            return go;
        }

        /// Herramienta tumbada sobre el banco: mango a lo largo de X, apoyada a "lift" sobre la superficie.
        static void PlaceTool(Transform tool, Transform spot, Vector3 forward, float lift, Vector3? up = null)
        {
            var attach = tool.Find("Attach");
            var rot = Quaternion.LookRotation(forward, up ?? Vector3.up);
            // centro aproximado de la herramienta a lo largo de su +Z (mango corto detrás de Attach, cabeza delante)
            float centerZ = tool.name.Contains("Magnifier") ? 0.073f : tool.name.Contains("Screwdriver") ? 0.0615f : 0.07f;
            Vector3 attachWorld = spot.position + Vector3.up * lift - rot * new Vector3(0, 0, centerZ);
            tool.rotation = rot * Quaternion.Inverse(attach.localRotation);
            tool.position = attachWorld - tool.rotation * attach.localPosition;
        }

        static void SetupButton(Transform btn, string capPrefab, Vector3 worldPos)
        {
            bool wasActive = btn.gameObject.activeSelf;
            btn.gameObject.SetActive(true);
            Unscale(btn);
            Strip(btn);
            btn.position = worldPos;
            var box = btn.GetComponent<BoxCollider>();
            if (box) { box.size = new Vector3(0.1f, 0.04f, 0.1f); box.center = Vector3.zero; }
            var label = btn.GetComponentInChildren<TMPro.TMP_Text>(true);
            if (label != null)
            {
                label.transform.position = worldPos + new Vector3(0, 0.075f, 0);
                label.transform.rotation = Quaternion.Euler(0, 90, 0);    // el texto mira a -X (hacia el jugador)
                StyleLabel(label, 0.32f, Color.white, new Vector2(0.3f, 0.06f));
                label.fontStyle = TMPro.FontStyles.Bold;
            }
            var cap = Place(capPrefab, btn, worldPos + new Vector3(0, -0.013f, 0));
            cap.name = "Cap";
            btn.gameObject.SetActive(wasActive);
        }

        static void StyleLabel(TMPro.TMP_Text t, float size, Color color, Vector2 box)
        {
            t.enableAutoSizing = false;
            t.fontSize = size;
            t.color = color;
            t.alignment = TMPro.TextAlignmentOptions.Center;
            t.textWrappingMode = TMPro.TextWrappingModes.NoWrap;
            t.overflowMode = TMPro.TextOverflowModes.Overflow;
            t.rectTransform.localScale = Vector3.one;
            t.rectTransform.sizeDelta = box;
        }

        static float ProductHalfHeight(ProductType type)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{QCXRArtBuilder.ProductPrefabDir}/Product_{type}.prefab");
            var mf = prefab != null ? prefab.transform.Find("Model")?.GetComponent<MeshFilter>() : null;
            return mf != null ? -mf.sharedMesh.bounds.min.y + 0.002f : 0.1f;
        }

        // ═════════════════════════ VISTAS PREVIAS ═════════════════════════

        public static void RenderScene(string outDir)
        {
            Directory.CreateDirectory(outDir);
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var cam = new GameObject("QA_Cam").AddComponent<Camera>();
            cam.nearClipPlane = 0.03f; cam.farClipPlane = 30f;
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = Color.black;
            const int W = 1280, H = 800;
            var rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 4 };
            cam.targetTexture = rt;
            var shots = new (string name, Vector3 pos, Vector3 look, float fov)[]
            {
                ("POV_Front",   new Vector3(0, 1.6f, -0.05f),   new Vector3(0, 0.9f, 0.75f),   75f),
                ("POV_Left",    new Vector3(0, 1.6f, -0.05f),   new Vector3(-0.85f, 1.05f, -0.2f), 75f),
                ("POV_Right",   new Vector3(0, 1.6f, -0.05f),   new Vector3(0.8f, 0.9f, -0.05f), 75f),
                ("Overview",    new Vector3(-1.9f, 2.6f, -1.45f), new Vector3(0.1f, 0.8f, 0.7f), 60f),
                ("Overview_Back", new Vector3(2.2f, 2.4f, 2.4f), new Vector3(0f, 0.9f, 0.2f), 60f),
                ("Belt_Detail", new Vector3(0.9f, 1.25f, 0.15f), new Vector3(1.4f, 0.95f, 0.75f), 55f),
            };
            foreach (var (name, pos, look, fov) in shots)
            {
                cam.fieldOfView = fov;
                cam.transform.position = pos;
                cam.transform.LookAt(look);
                cam.Render();
                RenderTexture.active = rt;
                var tex = new Texture2D(W, H, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, W, H), 0, 0);
                tex.Apply();
                File.WriteAllBytes(Path.Combine(outDir, $"Scene_{name}.png"), tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                RenderTexture.active = null;
            }
            Object.DestroyImmediate(cam.gameObject);   // la escena no se vuelve a guardar
            Debug.Log("[QCXR Art] Vistas de escena en " + outDir);
        }

        static string Arg(string name)
        {
            var a = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < a.Length - 1; i++) if (a[i] == name) return a[i + 1];
            return null;
        }

        /// Reconstruye TODO el arte desde cero (Fases 2 → 7), en el orden correcto.
        [MenuItem("QCXR/Arte/Reconstruir todo (Fases 2-7)")]
        public static void RebuildEverything()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            QCXRArtBuilder.BuildPhase2();        // productos y defectos
            QCXRToolBuilder.BuildTools();        // herramientas
            QCXRHandBuilder.BuildHands();        // manos
            Assemble();                          // estación, animaciones, VFX, escena final, UI 2D
            QCXRQualityAudit.Run(QCXRQualityAudit.ApplyOptimizations());
        }

        /// Entrada batch: reconstruye todo, renderiza vistas, audita y SALE con EditorApplication.Exit.
        public static void BatchRebuildEverything() => BatchRun(true);

        /// Entrada batch: monta la escena, renderiza vistas y SALE con EditorApplication.Exit (evita cuelgues al cerrar).
        public static void BatchAssembleAndRender() => BatchRun(false);

        static void BatchRun(bool everything)
        {
            int code = 0;
            try
            {
                if (everything) { QCXRArtBuilder.BuildPhase2(); QCXRToolBuilder.BuildTools(); QCXRHandBuilder.BuildHands(); }
                Assemble();
                string outDir = Arg("-qcxrOut") ?? Path.GetFullPath(Path.Combine(Application.dataPath, "../Temp/QCXRPreviews"));
                RenderScene(outDir);
                QCXRFxBuilder.RenderPreviews(outDir);
                var optimizations = QCXRQualityAudit.ApplyOptimizations();   // Fase 7
                QCXRQualityAudit.Run(optimizations);
                AssetDatabase.SaveAssets();
            }
            catch (System.Exception e) { Debug.LogException(e); code = 1; }
            Debug.Log("[QCXR Art] Batch terminado con código " + code);
            if (Application.isBatchMode) EditorApplication.Exit(code);
        }
    }
}
