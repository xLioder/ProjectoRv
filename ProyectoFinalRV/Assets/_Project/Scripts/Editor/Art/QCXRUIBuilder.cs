using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace QCXR.EditorTools.Art
{
    /// Fase 7 · Recursos 2D de UI (sección 7 del brief).
    ///  · Iconos de los 5 productos y los 4 defectos: RENDER de los modelos reales con fondo transparente
    ///    (doble pasada negro/blanco → alfa exacto con antialiasing).
    ///  · Sprites procedurales 9-slice teñibles: panel, marco, botón, franja de peligro, indicadores (OK, error, aviso).
    ///  · Sellos S/A/B/C: ya generados en la Fase 5 (QCXRFxBuilder).
    /// ApplyToScene() los coloca en la UI existente sin cambiar la lógica (solo Images y sprites).
    public static class QCXRUIBuilder
    {
        const string UiDir = QCXRArtBuilder.ArtRoot + "/UI";
        const string IconDir = UiDir + "/Icons";
        static readonly string[] Products = { "Robot", "Radio", "Lamp", "Toaster", "Clock" };
        static readonly string[] Defects = { "LooseScrew", "Dent", "HiddenCrack", "BurnedCircuit" };   // mismo orden que DefectType

        public static void BuildSprites()
        {
            QCXRPrefabBuilder.EnsureFolder(IconDir);
            BuildProceduralSprites();
            RenderIcons();
            AssetDatabase.SaveAssets();
            Debug.Log("[QCXR Art] Fase 7: iconos y sprites de UI generados.");
        }

        public static Sprite Sprite(string file) => AssetDatabase.LoadAssetAtPath<Sprite>($"{UiDir}/{file}.png");
        public static Sprite ProductIcon(string p) => AssetDatabase.LoadAssetAtPath<Sprite>($"{IconDir}/Icon_Product_{p}.png");
        public static Sprite DefectIcon(int i) => AssetDatabase.LoadAssetAtPath<Sprite>($"{IconDir}/Icon_Defect_{Defects[i]}.png");

        // ═════════════════════════ IMPORTACIÓN ═════════════════════════

        static void SavePng(Texture2D t, string path, Vector4 border, bool repeat = false)
        {
            File.WriteAllBytes(Path.Combine(Application.dataPath, path.Substring("Assets/".Length)), t.EncodeToPNG());
            Object.DestroyImmediate(t);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var ti = (TextureImporter)AssetImporter.GetAtPath(path);
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            ti.alphaSource = TextureImporterAlphaSource.FromInput;
            ti.alphaIsTransparency = true;
            ti.mipmapEnabled = false;
            ti.wrapMode = repeat ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
            ti.spriteBorder = border;
            ti.SetPlatformTextureSettings(new TextureImporterPlatformSettings
            {
                name = "Android", overridden = true, maxTextureSize = 512,
                format = TextureImporterFormat.ASTC_4x4, compressionQuality = 50
            });
            ti.SaveAndReimport();
        }

        static Texture2D Draw(int w, int h, Func<float, float, Color> f)
        {
            var t = new Texture2D(w, h, TextureFormat.RGBA32, false);
            for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) t.SetPixel(x, y, f(x + 0.5f, y + 0.5f));
            t.Apply();
            return t;
        }

        /// Distancia con signo a un rectángulo redondeado (px). Negativo = dentro.
        static float RoundRect(float x, float y, float w, float h, float r)
        {
            float qx = Mathf.Abs(x - w / 2) - (w / 2 - r), qy = Mathf.Abs(y - h / 2) - (h / 2 - r);
            float ox = Mathf.Max(qx, 0), oy = Mathf.Max(qy, 0);
            return Mathf.Sqrt(ox * ox + oy * oy) + Mathf.Min(Mathf.Max(qx, qy), 0) - r;
        }

        static float Cov(float d) => Mathf.Clamp01(0.5f - d);   // cobertura antialias

        static void BuildProceduralSprites()
        {
            // Panel 9-slice (blanco, se tiñe con Image.color)
            SavePng(Draw(64, 64, (x, y) => new Color(1, 1, 1, Cov(RoundRect(x, y, 64, 64, 14)))), $"{UiDir}/QCXR_UI_Panel.png", new Vector4(18, 18, 18, 18));
            // Marco 9-slice (anillo de 4 px)
            SavePng(Draw(64, 64, (x, y) => { float d = RoundRect(x, y, 64, 64, 14); return new Color(1, 1, 1, Cov(d) * Cov(-(d + 4f))); }),
                    $"{UiDir}/QCXR_UI_Frame.png", new Vector4(18, 18, 18, 18));
            // Botón 9-slice con sombreado inferior (bisel industrial)
            SavePng(Draw(64, 64, (x, y) =>
            {
                float d = RoundRect(x, y, 64, 64, 10);
                float shade = y < 10 ? 0.72f : y > 58 ? 1f : 0.92f;
                float rim = Cov(-(d + 2.5f)) > 0.5f ? 1f : 0.8f;
                return new Color(shade * rim, shade * rim, shade * rim, Cov(d));
            }), $"{UiDir}/QCXR_UI_Button.png", new Vector4(14, 14, 14, 14));
            // Franja de peligro (repetible)
            Color hy = Palette.Get(Sw.HazardYellow).albedo, hb = new Color(0.08f, 0.08f, 0.09f);
            SavePng(Draw(64, 16, (x, y) => ((int)((x + y) / 8f) % 2 == 0) ? hy : hb), $"{UiDir}/QCXR_UI_Hazard.png", Vector4.zero, true);
            // Indicadores
            Color ok = Palette.Get(Sw.ClockGreen).albedo, bad = Palette.Get(Sw.PaintRed).albedo, warn = hy;
            SavePng(Draw(128, 128, (x, y) =>
            {
                float c = Cov(Vector2.Distance(new Vector2(x, y), new Vector2(64, 64)) - 60);
                float a = Seg(x, y, 34, 66, 56, 44, 9f), b = Seg(x, y, 56, 44, 96, 88, 9f);
                return Color.Lerp(new Color(ok.r, ok.g, ok.b, c), new Color(1, 1, 1, c), Mathf.Max(Cov(a), Cov(b)));
            }), $"{UiDir}/QCXR_UI_Indicator_OK.png", Vector4.zero);
            SavePng(Draw(128, 128, (x, y) =>
            {
                float c = Cov(Vector2.Distance(new Vector2(x, y), new Vector2(64, 64)) - 60);
                float a = Seg(x, y, 40, 40, 88, 88, 9f), b = Seg(x, y, 40, 88, 88, 40, 9f);
                return Color.Lerp(new Color(bad.r, bad.g, bad.b, c), new Color(1, 1, 1, c), Mathf.Max(Cov(a), Cov(b)));
            }), $"{UiDir}/QCXR_UI_Indicator_Error.png", Vector4.zero);
            SavePng(Draw(128, 128, (x, y) =>
            {
                // triángulo
                Vector2 p = new Vector2(x, y), A = new Vector2(64, 118), B = new Vector2(8, 14), C = new Vector2(120, 14);
                float tri = Mathf.Max(Edge(p, A, B), Mathf.Max(Edge(p, B, C), Edge(p, C, A)));
                float bar = Seg(x, y, 64, 92, 64, 50, 8f), dot = Vector2.Distance(p, new Vector2(64, 32)) - 7f;
                float cov = Cov(tri), mark = Mathf.Max(Cov(bar), Cov(dot));
                return Color.Lerp(new Color(warn.r, warn.g, warn.b, cov), new Color(0.08f, 0.08f, 0.09f, cov), mark);
            }), $"{UiDir}/QCXR_UI_Indicator_Warning.png", Vector4.zero);
        }

        static float Seg(float x, float y, float ax, float ay, float bx, float by, float r)
        {
            Vector2 p = new Vector2(x, y), a = new Vector2(ax, ay), b = new Vector2(bx, by), ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            return Vector2.Distance(p, a + ab * t) - r;
        }

        static float Edge(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 e = b - a, n = new Vector2(e.y, -e.x).normalized;
            return Vector2.Dot(p - a, n);
        }

        // ═════════════════════════ ICONOS (RENDER) ═════════════════════════

        static void RenderIcons()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var key = new GameObject("Key").AddComponent<Light>(); key.type = LightType.Directional; key.intensity = 1.3f; key.transform.rotation = Quaternion.Euler(45, -35, 0);
            var fill = new GameObject("Fill").AddComponent<Light>(); fill.type = LightType.Directional; fill.intensity = 0.5f; fill.transform.rotation = Quaternion.Euler(20, 150, 0);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat; RenderSettings.ambientLight = new Color(0.55f, 0.56f, 0.6f);
            var cam = new GameObject("Cam").AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor; cam.fieldOfView = 22f; cam.nearClipPlane = 0.005f;
            const int S = 256;
            var rt = new RenderTexture(S, S, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 8 };
            cam.targetTexture = rt;

            Texture2D Grab(Color bg)
            {
                cam.backgroundColor = bg; cam.Render();
                RenderTexture.active = rt;
                var t = new Texture2D(S, S, TextureFormat.RGBA32, false);
                t.ReadPixels(new Rect(0, 0, S, S), 0, 0); t.Apply();
                RenderTexture.active = null;
                return t;
            }

            void Icon(GameObject go, Vector3 dir, string path, float pad)
            {
                var rs = go.GetComponentsInChildren<Renderer>().Where(r => r.enabled && r.gameObject.activeInHierarchy && !(r is ParticleSystemRenderer)).ToList();
                var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
                float dist = b.extents.magnitude / Mathf.Sin(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * pad;
                cam.transform.position = b.center + dir.normalized * dist; cam.transform.LookAt(b.center);
                var black = Grab(Color.black); var white = Grab(Color.white);
                var outT = new Texture2D(S, S, TextureFormat.RGBA32, false);
                for (int y = 0; y < S; y++)
                    for (int x = 0; x < S; x++)
                    {
                        Color k = black.GetPixel(x, y), w = white.GetPixel(x, y);
                        float a = Mathf.Clamp01(1f - ((w.r - k.r) + (w.g - k.g) + (w.b - k.b)) / 3f);
                        outT.SetPixel(x, y, a > 0.003f ? new Color(Mathf.Clamp01(k.r / a), Mathf.Clamp01(k.g / a), Mathf.Clamp01(k.b / a), a) : new Color(0, 0, 0, 0));
                    }
                outT.Apply();
                Object.DestroyImmediate(black); Object.DestroyImmediate(white);
                SavePng(outT, path, Vector4.zero);
            }

            foreach (var p in Products)
            {
                var go = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>($"{QCXRArtBuilder.ProductPrefabDir}/Product_{p}.prefab"));
                go.transform.Find("DefectSlots").gameObject.SetActive(false);   // icono del producto sano
                Icon(go, new Vector3(-0.45f, 0.55f, -1f), $"{IconDir}/Icon_Product_{p}.png", 1.02f);
                Object.DestroyImmediate(go);
            }

            // Defectos sobre un disco crema (legibles sobre cualquier botón)
            var disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            disc.transform.localScale = new Vector3(0.1f, 0.002f, 0.1f); disc.transform.position = new Vector3(0, -0.002f, 0);
            var dm = new Material(Shader.Find("Universal Render Pipeline/Lit")); dm.SetColor("_BaseColor", Palette.Get(Sw.Cream).albedo); dm.SetFloat("_Smoothness", 0.3f);
            disc.GetComponent<Renderer>().sharedMaterial = dm;
            for (int i = 0; i < Defects.Length; i++)
            {
                var go = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>($"{QCXRArtBuilder.DefectPrefabDir}/Defect_{Defects[i]}.prefab"));
                go.transform.Find("Visual").gameObject.SetActive(true);
                go.transform.Find("Fixed").gameObject.SetActive(false);
                if (Defects[i] == "HiddenCrack")
                {
                    go.transform.localScale = Vector3.one * 1.25f;
                    var mpb = new MaterialPropertyBlock(); mpb.SetColor("_BaseColor", new Color(0.55f, 0.18f, 0.04f));   // tono de grieta ya revelada
                    go.GetComponentInChildren<MeshRenderer>().SetPropertyBlock(mpb);
                }
                var both = new GameObject("Both"); go.transform.SetParent(both.transform, true); disc.transform.SetParent(both.transform, true);
                Icon(both, new Vector3(-0.25f, 1f, -0.7f), $"{IconDir}/Icon_Defect_{Defects[i]}.png", 0.95f);
                disc.transform.SetParent(null, true);
                Object.DestroyImmediate(both);
            }
            Object.DestroyImmediate(disc);
        }

        // ═════════════════════════ APLICAR EN LA ESCENA ═════════════════════════

        public static void ApplyToScene(Scene scene)
        {
            Transform Find(string n) => scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).FirstOrDefault(t => t != null && t.name == n);
            Color orange = Palette.Get(Sw.Orange).albedo;

            // Paneles: fondo redondeado + marco naranja + franja de peligro superior
            foreach (var panel in new[] { "HUD", "Instructions", "Report", "Results" })
            {
                var p = Find(panel);
                var bg = p != null ? p.Find("Bg")?.GetComponent<Image>() : null;
                if (bg == null) continue;
                bg.sprite = Sprite("QCXR_UI_Panel"); bg.type = Image.Type.Sliced; bg.pixelsPerUnitMultiplier = 1f;
                var frame = UIChild(bg.transform.parent, "Frame", bg.rectTransform);
                var fi = frame.gameObject.AddComponent<Image>(); fi.sprite = Sprite("QCXR_UI_Frame"); fi.type = Image.Type.Sliced; fi.color = orange; fi.raycastTarget = false;
                frame.SetSiblingIndex(bg.transform.GetSiblingIndex() + 1);
                var deco = UIChild(bg.transform.parent, "Deco", bg.rectTransform);
                deco.anchorMin = new Vector2(0.5f, 0.5f); deco.anchorMax = new Vector2(0.5f, 0.5f);
                deco.sizeDelta = new Vector2(bg.rectTransform.sizeDelta.x - 60f, 12f);
                deco.anchoredPosition = bg.rectTransform.anchoredPosition + new Vector2(0, bg.rectTransform.sizeDelta.y / 2f - 16f);
                var di = deco.gameObject.AddComponent<Image>(); di.sprite = Sprite("QCXR_UI_Hazard"); di.type = Image.Type.Tiled; di.raycastTarget = false;
                deco.SetSiblingIndex(frame.GetSiblingIndex() + 1);
            }

            // Botones del panel de reporte: sprite de botón + icono del defecto
            var report = Find("Report");
            if (report != null)
                foreach (var btn in report.GetComponentsInChildren<Button>(true))
                {
                    var img = btn.GetComponent<Image>();
                    img.sprite = Sprite("QCXR_UI_Button"); img.type = Image.Type.Sliced;
                    int i = Array.FindIndex(new[] { "Tornillo", "Abolladura", "Grieta", "Circuito" }, k => btn.name.Contains(k));
                    if (i < 0) continue;
                    var icon = UIChild(btn.transform, "Icon", null);
                    icon.anchorMin = icon.anchorMax = new Vector2(0f, 0.5f);
                    icon.sizeDelta = new Vector2(58, 58); icon.anchoredPosition = new Vector2(40, 0);
                    var ii = icon.gameObject.AddComponent<Image>(); ii.sprite = DefectIcon(i); ii.preserveAspect = true; ii.raycastTarget = false;
                    var label = btn.transform.Find("Label") as RectTransform;
                    if (label != null) { label.offsetMin += new Vector2(60, 0); }
                }
            var results = Find("Results");
            if (results != null)
                foreach (var btn in results.GetComponentsInChildren<Button>(true))
                {
                    var img = btn.GetComponent<Image>(); img.sprite = Sprite("QCXR_UI_Button"); img.type = Image.Type.Sliced;
                }

            // Icono del producto junto a la etiqueta de cada hueco de la estantería
            foreach (var p in Products)
            {
                var shelf = Find("Shelf_" + p);
                var label = shelf != null ? shelf.GetComponentInChildren<TMPro.TMP_Text>(true) : null;
                if (label == null) continue;
                var go = new GameObject("Icon");
                go.transform.SetParent(shelf, false);
                go.transform.position = label.transform.position + new Vector3(0.002f, 0, -0.085f);
                go.transform.rotation = Quaternion.Euler(0, -90, 0);
                var sr = go.AddComponent<SpriteRenderer>(); sr.sprite = ProductIcon(p);
                float size = shelf.position.y > 1.2f ? 0.04f : 0.05f;
                go.transform.localScale = Vector3.one * size / (sr.sprite != null ? sr.sprite.bounds.size.y : 1f);
            }
        }

        static RectTransform UIChild(Transform parent, string name, RectTransform copy)
        {
            var old = parent.Find(name);
            if (old != null) Object.DestroyImmediate(old.gameObject);
            var rt = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            if (copy != null)
            {
                rt.anchorMin = copy.anchorMin; rt.anchorMax = copy.anchorMax; rt.pivot = copy.pivot;
                rt.anchoredPosition = copy.anchoredPosition; rt.sizeDelta = copy.sizeDelta;
            }
            return rt;
        }
    }
}
