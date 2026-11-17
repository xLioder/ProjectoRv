using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace QCXR.EditorTools.Art
{
    /// Menú QCXR > Arte > Fase 2. Genera (de forma reproducible) la paleta, los materiales,
    /// los 4 defectos compartidos y los modelos de los 5 productos.
    /// Los prefabs de producto se EDITAN en sitio: se conservan Product, Rigidbody y XRGrabInteractable
    /// (y sus fileID), por lo que las referencias del catálogo de ProductFlow siguen siendo válidas.
    /// Solo editor: no entra en el build y no forma parte de los 17 scripts del juego.
    public static class QCXRArtBuilder
    {
        public const string ArtRoot = "Assets/_Project/Art";
        public const string MatDir = ArtRoot + "/Materials";
        const string TexDir = MatDir + "/Textures";
        public const string DefectPrefabDir = "Assets/_Project/Prefabs/Defects";
        public const string ProductPrefabDir = "Assets/_Project/Prefabs/Products";

        static Material atlas, crackMat;

        public static Material Atlas => atlas != null ? atlas : (atlas = AssetDatabase.LoadAssetAtPath<Material>(MatDir + "/QCXR_Atlas.mat"));

        public struct Slot
        {
            public DefectType type; public Vector3 pos, euler; public float spin, scale;
            public Slot(DefectType t, Vector3 p, Vector3 e, float scale = 1f, float spin = 0f) { type = t; pos = p; euler = e; this.scale = scale; this.spin = spin; }
        }

        public struct Col
        {
            public Vector3 center, size;
            public Col(Vector3 c, Vector3 s) { center = c; size = s; }
        }

        static readonly Vector3 Front = new Vector3(-90, 0, 0);   // +Y local del defecto -> -Z (cara frontal)

        [MenuItem("QCXR/Arte/Fase 2 · Productos y defectos")]
        public static void BuildPhase2()
        {
            int layer = LayerMask.NameToLayer("Defect");
            if (layer < 0) throw new System.Exception("Falta la capa 'Defect'. Ejecuta primero QCXR > Construir layer, config y prefabs.");

            BuildMaterials();
            var defects = new Dictionary<DefectType, GameObject>
            {
                [DefectType.LooseScrew] = BuildLooseScrew(layer),
                [DefectType.Dent] = BuildDent(layer),
                [DefectType.HiddenCrack] = BuildCrack(layer),
                [DefectType.BurnedCircuit] = BuildBurnedCircuit(layer),
            };

            PatchProduct("Robot", Robot(out var s1, out var c1), s1, c1, defects);
            PatchProduct("Radio", Radio(out var s2, out var c2), s2, c2, defects);
            PatchProduct("Lamp", Lamp(out var s3, out var c3), s3, c3, defects);
            PatchProduct("Toaster", Toaster(out var s4, out var c4), s4, c4, defects);
            PatchProduct("Clock", Clock(out var s5, out var c5), s5, c5, defects);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[QCXR Art] Fase 2 completada: paleta, materiales, 4 defectos y 5 productos.");
        }

        // ═════════════════════════ MATERIALES ═════════════════════════

        internal static void BuildMaterials()
        {
            QCXRPrefabBuilder.EnsureFolder(TexDir);
            int cells = Palette.Grid, px = 8, size = cells * px;
            var alb = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var ms = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var em = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var clear = new Color[size * size];
            alb.SetPixels(clear); ms.SetPixels(clear); em.SetPixels(clear);
            for (int i = 0; i < (int)Sw.Count; i++)
            {
                var e = Palette.Get((Sw)i);
                int cx = (i % cells) * px, cy = (i / cells) * px;
                for (int y = 0; y < px; y++)
                    for (int x = 0; x < px; x++)
                    {
                        alb.SetPixel(cx + x, cy + y, new Color(e.albedo.r, e.albedo.g, e.albedo.b, 1f));
                        ms.SetPixel(cx + x, cy + y, new Color(e.metallic, 0f, 0f, e.smoothness));
                        em.SetPixel(cx + x, cy + y, new Color(e.emission.r, e.emission.g, e.emission.b, 1f));
                    }
            }
            var albT = WritePng(alb, TexDir + "/QCXR_Palette_Albedo.png", true);
            var msT = WritePng(ms, TexDir + "/QCXR_Palette_MetalSmooth.png", false);
            var emT = WritePng(em, TexDir + "/QCXR_Palette_Emission.png", true);

            atlas = Mat(MatDir + "/QCXR_Atlas.mat", m =>
            {
                m.SetTexture("_BaseMap", albT);
                m.SetColor("_BaseColor", Color.white);
                m.SetTexture("_MetallicGlossMap", msT);
                m.SetFloat("_Metallic", 1f);
                m.SetFloat("_Smoothness", 1f);
                m.SetFloat("_SmoothnessTextureChannel", 0f);
                m.EnableKeyword("_METALLICSPECGLOSSMAP");
                m.SetTexture("_EmissionMap", emT);
                m.SetColor("_EmissionColor", Color.white);
                m.EnableKeyword("_EMISSION");
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
                m.enableInstancing = false;   // SRP Batcher
            });

            // Grieta: material propio para poder animar su emisión (brillo al revelarse con la lupa).
            crackMat = Mat(MatDir + "/QCXR_Crack.mat", m =>
            {
                // Unlit: el revelado anima _BaseColor (oscuro → naranja intenso → rescoldo) y se ve como brillo propio,
                // sin depender de la palabra clave _EMISSION (que la validación de URP retiraba). Además es más barato.
                m.shader = Shader.Find("Universal Render Pipeline/Unlit");
                m.SetColor("_BaseColor", new Color(0.05f, 0.04f, 0.04f, 1f));
            });
        }

        static Texture2D WritePng(Texture2D tex, string assetPath, bool srgb)
        {
            File.WriteAllBytes(FullPath(assetPath), tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
            var ti = (TextureImporter)AssetImporter.GetAtPath(assetPath);
            ti.textureType = TextureImporterType.Default;
            ti.sRGBTexture = srgb;
            ti.mipmapEnabled = false;
            ti.filterMode = FilterMode.Point;
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.npotScale = TextureImporterNPOTScale.None;
            ti.alphaSource = TextureImporterAlphaSource.FromInput;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
        }

        internal static Material Mat(string path, System.Action<Material> setup)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(shader); AssetDatabase.CreateAsset(m, path); }
            else m.shader = shader;
            setup(m);
            EditorUtility.SetDirty(m);
            return m;
        }

        static string FullPath(string assetPath) => Path.Combine(Application.dataPath, assetPath.Substring("Assets/".Length));

        public static Mesh SaveMesh(MeshKit k, string path, string name)
        {
            QCXRPrefabBuilder.EnsureFolder(Path.GetDirectoryName(path).Replace("\\", "/"));
            var m = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (m == null) { m = k.ToMesh(name); AssetDatabase.CreateAsset(m, path); }
            else { k.Fill(m); m.name = name; }
            var so = new SerializedObject(m);
            var readable = so.FindProperty("m_IsReadable");
            if (readable != null) { readable.boolValue = false; so.ApplyModifiedPropertiesWithoutUndo(); }
            EditorUtility.SetDirty(m);
            return m;
        }

        internal static GameObject MeshObject(string name, Transform parent, Mesh mesh, Material mat, bool shadows)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = shadows ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
            r.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.BlendProbes;
            return go;
        }

        // ═════════════════════════ DEFECTOS COMPARTIDOS ═════════════════════════
        // Convención: origen = punto sobre la superficie del producto, +Y local = normal de la superficie.
        // Jerarquía: Defect_X (ProductDefect) > Visual | Fixed | WorkCollider. Todo en la capa "Defect".

        static GameObject DefectRoot(string name, out Transform visual, out Transform fix, out SphereCollider col, float radius, Vector3 colCenter)
        {
            var root = new GameObject("Defect_" + name);
            visual = new GameObject("Visual").transform; visual.SetParent(root.transform, false);
            fix = new GameObject("Fixed").transform; fix.SetParent(root.transform, false);
            var wc = new GameObject("WorkCollider"); wc.transform.SetParent(root.transform, false);
            col = wc.AddComponent<SphereCollider>();
            col.isTrigger = true; col.radius = radius; col.center = colCenter;
            return root;
        }

        static GameObject FinishDefect(GameObject root, int layer, DefectType type, ToolType tool, float work, bool repairable, bool hidden,
                                       Transform visual, Transform fix, Collider col)
        {
            fix.gameObject.SetActive(false);
            var d = root.AddComponent<ProductDefect>();
            var so = new SerializedObject(d);
            so.FindProperty("type").enumValueIndex = (int)type;
            so.FindProperty("requiredTool").enumValueIndex = (int)tool;
            so.FindProperty("workRequired").floatValue = work;
            so.FindProperty("repairable").boolValue = repairable;
            so.FindProperty("hiddenUntilInspected").boolValue = hidden;
            so.FindProperty("defectVisual").objectReferenceValue = visual.gameObject;
            so.FindProperty("fixedVisual").objectReferenceValue = fix.gameObject;
            so.FindProperty("workCollider").objectReferenceValue = col;
            so.ApplyModifiedPropertiesWithoutUndo();
            SetLayer(root, layer);

            QCXRPrefabBuilder.EnsureFolder(DefectPrefabDir);
            string path = $"{DefectPrefabDir}/{root.name}.prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }

        internal static void SetLayer(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform t in go.transform) SetLayer(t.gameObject, layer);
        }

        // ── Tornillo flojo ──
        static GameObject BuildLooseScrew(int layer)
        {
            const string dir = ArtRoot + "/Defects/LooseScrew";
            var root = DefectRoot("LooseScrew", out var visual, out var fix, out var col, 0.03f, new Vector3(0, 0.008f, 0));

            // Visual: tornillo levantado ~1 cm, inclinado 18°, rosca expuesta y marca roja de inspección.
            var k = new MeshKit();
            k.Cyl(new Vector3(0, 0.0003f, 0), 0.015f, 0.0006f, Sw.PaintRed, default, 18);
            k.Cyl(new Vector3(0, 0.0007f, 0), 0.0062f, 0.0006f, Sw.Black, default, 12);
            var tilt = Quaternion.Euler(0, 0, 24f);
            Vector3 axis = tilt * Vector3.up;
            Vector3 headBase = axis * 0.016f;
            k.Tube(Vector3.zero, headBase, 0.0034f, Sw.SteelDark, 10);
            for (int i = 1; i <= 4; i++) k.Ring(axis * (0.0031f * i), 0.0039f, 0.0008f, Sw.Brass, new Vector3(0, 0, 24), 10, 4);
            k.ScrewHead(Matrix4x4.TRS(headBase, tilt * Quaternion.Euler(0, 25, 0), Vector3.one));
            MeshObject("ScrewLoose", visual, SaveMesh(k, dir + "/LooseScrew_Visual.asset", "LooseScrew_Visual"), atlas, false);

            // Fixed: tornillo asentado. El pivote del hijo ScrewHead está en el eje del tornillo
            // (listo para la animación de giro + descenso de la Fase 5).
            var kf = new MeshKit();
            kf.ScrewHead(Matrix4x4.identity);
            MeshObject("ScrewHead", fix, SaveMesh(kf, dir + "/LooseScrew_Fixed.asset", "LooseScrew_Fixed"), atlas, false);

            return FinishDefect(root, layer, DefectType.LooseScrew, ToolType.Screwdriver, 2f, true, false, visual, fix, col);
        }

        // ── Abolladura ──
        static GameObject BuildDent(int layer)
        {
            const string dir = ArtRoot + "/Defects/Dent";
            var root = DefectRoot("Dent", out var visual, out var fix, out var col, 0.04f, new Vector3(0, 0.006f, 0));
            MeshObject("PlateDented", visual, SaveMesh(DentPlate(true), dir + "/Dent_Visual.asset", "Dent_Visual"), atlas, false);
            MeshObject("PlateSmooth", fix, SaveMesh(DentPlate(false), dir + "/Dent_Fixed.asset", "Dent_Fixed"), atlas, false);
            return FinishDefect(root, layer, DefectType.Dent, ToolType.Hammer, 3f, true, false, visual, fix, col);
        }

        /// Placa de blindaje atornillada 6 x 6 cm (tipo panel industrial). dented = hundimiento facetado.
        static MeshKit DentPlate(bool dented)
        {
            var k = new MeshKit();
            const float S = 0.03f, T0 = 0.001f, T1 = 0.007f, inset = 0.0018f;
            const int N = 10;
            float H(float x, float z)
            {
                if (!dented) return T1;
                float dx = x - 0.003f, dz = z + 0.002f;
                float r = Mathf.Sqrt(dx * dx + dz * dz) / 0.024f;
                if (r >= 1f) return T1;
                float f = Mathf.Pow(1f - r * r, 1.5f);
                float n = Mathf.Sin(x * 913.7f + z * 577.3f) * Mathf.Cos(x * 311.1f - z * 719.9f);
                return T1 - 0.0052f * f + n * 0.0009f * f;
            }
            var p = new Vector3[N + 1, N + 1];
            float a = S - inset;
            for (int i = 0; i <= N; i++)
                for (int j = 0; j <= N; j++)
                {
                    float x = Mathf.Lerp(-a, a, (float)i / N), z = Mathf.Lerp(-a, a, (float)j / N);
                    p[i, j] = new Vector3(x, H(x, z), z);
                }
            var id = Matrix4x4.identity;
            for (int i = 0; i < N; i++)
                for (int j = 0; j < N; j++)
                {
                    k.Poly(new[] { p[i, j], p[i + 1, j], p[i + 1, j + 1] }, Sw.Steel, id, Vector3.up);
                    k.Poly(new[] { p[i, j], p[i + 1, j + 1], p[i, j + 1] }, Sw.Steel, id, Vector3.up);
                }
            Vector3 Bot(Vector3 q) => new Vector3(q.x * S / a, T0, q.z * S / a);
            for (int i = 0; i < N; i++)
            {
                SideQuad(k, p[i, 0], p[i + 1, 0], Bot(p[i, 0]), Bot(p[i + 1, 0]), Vector3.back);
                SideQuad(k, p[i, N], p[i + 1, N], Bot(p[i, N]), Bot(p[i + 1, N]), Vector3.forward);
                SideQuad(k, p[0, i], p[0, i + 1], Bot(p[0, i]), Bot(p[0, i + 1]), Vector3.left);
                SideQuad(k, p[N, i], p[N, i + 1], Bot(p[N, i]), Bot(p[N, i + 1]), Vector3.right);
            }
            // Remaches y pestañas naranjas en esquinas (lenguaje visual de panel de fábrica)
            for (int sx = -1; sx <= 1; sx += 2)
                for (int sz = -1; sz <= 1; sz += 2)
                {
                    k.Rivet(new Vector3(sx * 0.022f, T1, sz * 0.022f), Vector3.zero, 0.0028f);
                    float c = a, e = a - 0.011f;
                    k.Poly(new[] { new Vector3(sx * c, T1 + 0.0002f, sz * c), new Vector3(sx * e, T1 + 0.0002f, sz * c), new Vector3(sx * c, T1 + 0.0002f, sz * e) },
                           Sw.Orange, id, Vector3.up);
                }
            if (dented)
            {
                // Rozadura oscura en el fondo del golpe + grietas de pintura
                k.Blotch(new Vector3(0.003f, H(0.003f, -0.002f) + 0.0003f, -0.002f), 0.0075f, 9, 0.5f, 7, Sw.SteelDark, id);
            }
            return k;
        }

        static void SideQuad(MeshKit k, Vector3 ta, Vector3 tb, Vector3 ba, Vector3 bb, Vector3 outward)
            => k.Poly(new[] { ta, tb, bb, ba }, Sw.SteelDark, Matrix4x4.identity, outward + Vector3.up * 0.2f);

        // ── Grieta oculta ──
        static GameObject BuildCrack(int layer)
        {
            const string dir = ArtRoot + "/Defects/HiddenCrack";
            var root = DefectRoot("HiddenCrack", out var visual, out var fix, out var col, 0.045f, new Vector3(0, 0.005f, 0));
            var k = new MeshKit();
            var id = Matrix4x4.identity;
            const float y = 0.0006f;
            k.Ribbon(new[]
            {
                new Vector2(-0.035f, 0.002f), new Vector2(-0.024f, -0.003f), new Vector2(-0.015f, 0.002f), new Vector2(-0.006f, -0.001f),
                new Vector2(0.004f, 0.004f), new Vector2(0.013f, 0.0f), new Vector2(0.022f, 0.005f), new Vector2(0.035f, 0.001f)
            }, y, 0.0009f, 0.0009f, Sw.Char, id);
            // Ensanche central (la grieta es más ancha en el medio)
            k.Ribbon(new[] { new Vector2(-0.02f, -0.0005f), new Vector2(-0.006f, -0.001f), new Vector2(0.004f, 0.004f), new Vector2(0.016f, 0.0015f) }, y + 0.0001f, 0.0016f, 0.0010f, Sw.Char, id);
            k.Ribbon(new[] { new Vector2(-0.006f, -0.001f), new Vector2(-0.002f, -0.008f), new Vector2(0.003f, -0.012f) }, y + 0.0002f, 0.0012f, 0.0004f, Sw.Char, id);
            k.Ribbon(new[] { new Vector2(0.013f, 0.0f), new Vector2(0.017f, 0.007f), new Vector2(0.020f, 0.011f) }, y + 0.0002f, 0.0010f, 0.0004f, Sw.Char, id);
            k.Ribbon(new[] { new Vector2(-0.024f, -0.003f), new Vector2(-0.027f, -0.009f) }, y + 0.0002f, 0.0008f, 0.0003f, Sw.Char, id);
            MeshObject("Crack", visual, SaveMesh(k, dir + "/HiddenCrack_Visual.asset", "HiddenCrack_Visual"), crackMat, false);
            // Fixed queda vacío y desactivado: la grieta es irreparable.
            return FinishDefect(root, layer, DefectType.HiddenCrack, ToolType.None, 1f, false, true, visual, fix, col);
        }

        // ── Circuito quemado ──
        static GameObject BuildBurnedCircuit(int layer)
        {
            const string dir = ArtRoot + "/Defects/BurnedCircuit";
            var root = DefectRoot("BurnedCircuit", out var visual, out var fix, out var col, 0.045f, new Vector3(0, 0.006f, 0));
            var k = new MeshKit();
            var id = Matrix4x4.identity;
            // Marco de la escotilla de servicio (7 x 5 cm)
            const float W = 0.035f, D = 0.025f, B = 0.004f, FH = 0.004f;
            k.Box(new Vector3(0, FH / 2, -D + B / 2), new Vector3(W * 2, FH, B), 0.001f, Sw.Graphite);
            k.Box(new Vector3(0, FH / 2, D - B / 2), new Vector3(W * 2, FH, B), 0.001f, Sw.Graphite);
            k.Box(new Vector3(-W + B / 2, FH / 2, 0), new Vector3(B, FH, D * 2 - 2 * B), 0.001f, Sw.Graphite);
            k.Box(new Vector3(W - B / 2, FH / 2, 0), new Vector3(B, FH, D * 2 - 2 * B), 0.001f, Sw.Graphite);
            for (int sx = -1; sx <= 1; sx += 2)
                for (int sz = -1; sz <= 1; sz += 2)
                    k.ScrewHead(new Vector3(sx * (W - B / 2), FH, sz * (D - B / 2)), Vector3.zero, 0.0018f);
            // Placa
            k.Box(new Vector3(0, 0.0015f, 0), new Vector3(W * 2 - 2 * B, 0.0015f, D * 2 - 2 * B), 0f, Sw.PcbGreen);
            float py = 0.0023f;
            foreach (float z in new[] { -0.014f, -0.006f, 0.010f, 0.016f })
                k.Ribbon(new[] { new Vector2(-0.028f, z), new Vector2(-0.008f, z), new Vector2(-0.002f, z + 0.004f), new Vector2(0.026f, z + 0.004f) }, py, 0.0011f, 0.0011f, Sw.PcbTrace, id);
            // Chips y componentes
            k.Box(new Vector3(-0.016f, 0.0035f, 0.006f), new Vector3(0.014f, 0.0025f, 0.010f), 0.0006f, Sw.Black);
            k.Box(new Vector3(0.018f, 0.0035f, -0.011f), new Vector3(0.010f, 0.0025f, 0.008f), 0.0006f, Sw.Black);
            k.Cyl(new Vector3(-0.022f, 0.006f, -0.012f), 0.003f, 0.007f, Sw.SteelDark, default, 8, 0.0008f);
            k.Cyl(new Vector3(-0.015f, 0.006f, -0.013f), 0.0028f, 0.007f, Sw.SteelDark, default, 8, 0.0008f);
            // Zona quemada: chip derretido, condensador reventado, mancha negra y brasas
            k.Blotch(new Vector3(0.007f, 0.0024f, 0.002f), 0.0175f, 12, 0.35f, 3, Sw.Scorch, id);
            k.Blotch(new Vector3(0.008f, 0.0026f, 0.003f), 0.012f, 10, 0.45f, 11, Sw.Char, id);
            k.Box(new Vector3(0.006f, 0.0032f, 0.004f), new Vector3(0.013f, 0.0018f, 0.011f), 0.0006f, Sw.Char, new Vector3(4, 12, -3));
            k.Cyl(new Vector3(0.016f, 0.0045f, 0.010f), 0.0032f, 0.0045f, Sw.Char, new Vector3(9, 0, 7), 8);
            k.Blotch(new Vector3(0.016f, 0.0071f, 0.0103f), 0.0024f, 6, 0.5f, 5, Sw.Ember, id);
            k.Blotch(new Vector3(0.003f, 0.0044f, 0.001f), 0.0018f, 6, 0.5f, 9, Sw.Ember, id);
            k.Blotch(new Vector3(0.012f, 0.0028f, -0.006f), 0.0015f, 6, 0.5f, 13, Sw.Ember, id);
            MeshObject("Board", visual, SaveMesh(k, dir + "/BurnedCircuit_Visual.asset", "BurnedCircuit_Visual"), atlas, false);

            // Punto de anclaje del humo (Fase 5). Su +Z apunta hacia afuera de la superficie.
            var smoke = new GameObject("SmokeAnchor").transform;
            smoke.SetParent(visual, false);
            smoke.localPosition = new Vector3(0.010f, 0.006f, 0.005f);
            smoke.localRotation = Quaternion.Euler(-90, 0, 0);
            // Fixed queda vacío y desactivado: el circuito es irreparable.
            return FinishDefect(root, layer, DefectType.BurnedCircuit, ToolType.None, 1f, false, false, visual, fix, col);
        }

        // ═════════════════════════ PRODUCTOS ═════════════════════════
        // Ejes: frente = -Z (mira al jugador en la banda), arriba = +Y, pivote en el centro de la envolvente.

        static void PatchProduct(string name, MeshKit kit, List<Slot> slots, List<Col> cols, Dictionary<DefectType, GameObject> defectPrefabs)
        {
            string path = $"{ProductPrefabDir}/Product_{name}.prefab";
            var mesh = SaveMesh(kit, $"{ArtRoot}/Products/{name}/{name}_Body.asset", $"{name}_Body");
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                for (int i = root.transform.childCount - 1; i >= 0; i--)
                    Object.DestroyImmediate(root.transform.GetChild(i).gameObject);

                MeshObject("Model", root.transform, mesh, atlas, true);

                var colGo = new GameObject("Colliders");
                colGo.transform.SetParent(root.transform, false);
                foreach (var c in cols)
                {
                    var bc = colGo.AddComponent<BoxCollider>();
                    bc.center = c.center; bc.size = c.size;
                }

                var slotRoot = new GameObject("DefectSlots").transform;
                slotRoot.SetParent(root.transform, false);
                var defects = new List<ProductDefect>();
                foreach (var s in slots)
                {
                    var slot = new GameObject("Slot_" + s.type).transform;
                    slot.SetParent(slotRoot, false);
                    slot.localPosition = s.pos;
                    slot.localRotation = Quaternion.Euler(s.euler) * Quaternion.Euler(0, s.spin, 0);
                    var inst = (GameObject)PrefabUtility.InstantiatePrefab(defectPrefabs[s.type], slot);
                    inst.transform.localPosition = Vector3.zero;
                    inst.transform.localRotation = Quaternion.identity;
                    inst.transform.localScale = Vector3.one * s.scale;
                    defects.Add(inst.GetComponent<ProductDefect>());
                }

                var so = new SerializedObject(root.GetComponent<Product>());
                var arr = so.FindProperty("defects");
                arr.arraySize = defects.Count;
                for (int i = 0; i < defects.Count; i++) arr.GetArrayElementAtIndex(i).objectReferenceValue = defects[i];
                so.ApplyModifiedPropertiesWithoutUndo();

                var rb = root.GetComponent<Rigidbody>();
                rb.mass = 0.5f;
                rb.interpolation = RigidbodyInterpolation.Interpolate;
                rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

                PrefabUtility.SaveAsPrefabAsset(root, path);
                Debug.Log($"[QCXR Art] {name}: {kit.TriangleCount} tris, {slots.Count} slots de defecto.");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        // ── Robot (0.18 x 0.22 x 0.12) · producto del tutorial ──
        static MeshKit Robot(out List<Slot> slots, out List<Col> cols)
        {
            var k = new MeshKit();
            // Orugas y chasis
            foreach (int sx in new[] { -1, 1 })
            {
                k.Box(new Vector3(sx * 0.06f, -0.088f, 0), new Vector3(0.045f, 0.044f, 0.12f), 0.012f, Sw.Rubber);
                foreach (float z in new[] { -0.035f, 0f, 0.035f })
                    k.Cyl(new Vector3(sx * 0.0835f, -0.088f, z), 0.013f, 0.004f, Sw.Steel, new Vector3(0, 0, 90), 12, 0.001f);
                for (int i = 0; i < 7; i++)
                    k.Box(new Vector3(sx * 0.06f, -0.066f, -0.045f + i * 0.015f), new Vector3(0.04f, 0.002f, 0.005f), 0f, Sw.Graphite);
            }
            k.Box(new Vector3(0, -0.075f, 0), new Vector3(0.07f, 0.014f, 0.10f), 0.004f, Sw.Graphite);
            // Torso
            k.Box(new Vector3(0, -0.012f, 0), new Vector3(0.15f, 0.11f, 0.11f), 0.012f, Sw.RobotBlue);
            k.Stripes(new Vector3(0, -0.056f, -0.0556f), Vector3.zero, 0.12f, 0.012f, 9, Sw.HazardYellow, Sw.Graphite);
            // Panel de pecho (derecha) con LEDs y ventilación
            k.Box(new Vector3(0.035f, -0.014f, -0.0555f), new Vector3(0.05f, 0.054f, 0.004f), 0.002f, Sw.Cream);
            Sw[] leds = { Sw.LedGreen, Sw.LedAmber, Sw.LedRed };
            for (int i = 0; i < 3; i++)
                k.Cyl(new Vector3(0.022f + i * 0.013f, 0.002f, -0.0578f), 0.0038f, 0.003f, leds[i], new Vector3(90, 0, 0), 10);
            k.Vents(new Vector3(0.035f, -0.024f, -0.0575f), Vector3.zero, 0.036f, 0.018f, 3, Sw.Graphite);
            foreach (int sx in new[] { -1, 1 })
                foreach (int sy in new[] { -1, 1 })
                    k.ScrewHead(new Vector3(0.035f + sx * 0.02f, -0.014f + sy * 0.022f, -0.0575f), Front, 0.0028f);
            // Paneles laterales naranjas
            foreach (int sx in new[] { -1, 1 })
            {
                k.Box(new Vector3(sx * 0.0755f, -0.012f, 0), new Vector3(0.003f, 0.06f, 0.07f), 0.0015f, Sw.Orange);
                foreach (int sz in new[] { -1, 1 })
                    k.Rivet(new Vector3(sx * 0.077f, 0.012f, sz * 0.028f), new Vector3(0, 0, -sx * 90), 0.0025f);
            }
            // Borde trasero de la cubierta
            k.Box(new Vector3(0, 0.0445f, 0.047f), new Vector3(0.13f, 0.004f, 0.01f), 0.0015f, Sw.Orange);
            // Cuello y cabeza
            k.Cyl(new Vector3(0, 0.047f, 0.022f), 0.018f, 0.008f, Sw.GraphiteLight, default, 14);
            k.Box(new Vector3(0, 0.073f, 0.022f), new Vector3(0.085f, 0.046f, 0.06f), 0.01f, Sw.Cream);
            k.Box(new Vector3(0, 0.075f, -0.0085f), new Vector3(0.068f, 0.022f, 0.004f), 0.002f, Sw.Visor);
            foreach (int sx in new[] { -1, 1 })
            {
                k.Box(new Vector3(sx * 0.016f, 0.076f, -0.0108f), new Vector3(0.013f, 0.007f, 0.002f), 0.0008f, Sw.LedCyan);
                k.Cyl(new Vector3(sx * 0.0445f, 0.073f, 0.022f), 0.011f, 0.006f, Sw.Orange, new Vector3(0, 0, 90), 12, 0.0015f);
            }
            k.Tube(new Vector3(0.025f, 0.096f, 0.036f), new Vector3(0.025f, 0.104f, 0.036f), 0.0022f, Sw.SteelDark, 8);
            k.Sphere(new Vector3(0.025f, 0.1055f, 0.036f), 0.0045f, Sw.LedRed, 10, 5);
            // Brazos
            foreach (int sx in new[] { -1, 1 })
            {
                k.Cyl(new Vector3(sx * 0.0795f, 0.02f, 0), 0.016f, 0.009f, Sw.Orange, new Vector3(0, 0, 90), 14, 0.002f);
                k.Box(new Vector3(sx * 0.084f, -0.012f, 0), new Vector3(0.012f, 0.05f, 0.026f), 0.004f, Sw.Graphite);
                k.Box(new Vector3(sx * 0.084f, -0.043f, -0.003f), new Vector3(0.012f, 0.014f, 0.018f), 0.003f, Sw.SteelDark);
            }

            slots = new List<Slot>
            {
                new Slot(DefectType.LooseScrew, new Vector3(-0.045f, 0.043f, -0.032f), Vector3.zero),
                new Slot(DefectType.Dent, new Vector3(-0.036f, -0.012f, -0.055f), Front, 0.85f),
                new Slot(DefectType.HiddenCrack, new Vector3(0.033f, 0.043f, -0.03f), Vector3.zero, 0.85f, 20f),
            };
            cols = new List<Col>
            {
                new Col(new Vector3(0, -0.012f, 0), new Vector3(0.18f, 0.198f, 0.12f)),
                new Col(new Vector3(0, 0.073f, 0.022f), new Vector3(0.09f, 0.05f, 0.064f)),
            };
            return k;
        }

        // ── Radio (0.30 x 0.16 x 0.10) ──
        static MeshKit Radio(out List<Slot> slots, out List<Col> cols)
        {
            var k = new MeshKit();
            k.Box(new Vector3(0, -0.01f, 0), new Vector3(0.28f, 0.13f, 0.10f), 0.015f, Sw.RadioRed);
            k.Box(new Vector3(0, -0.012f, -0.0515f), new Vector3(0.25f, 0.105f, 0.004f), 0.004f, Sw.Cream);
            k.Box(new Vector3(0, -0.068f, -0.052f), new Vector3(0.25f, 0.006f, 0.004f), 0.001f, Sw.Chrome);
            // Altavoz
            k.Cyl(new Vector3(-0.065f, -0.012f, -0.0545f), 0.040f, 0.002f, Sw.Grille, new Vector3(90, 0, 0), 20);
            k.Ring(new Vector3(-0.065f, -0.012f, -0.0545f), 0.041f, 0.0032f, Sw.SteelDark, new Vector3(90, 0, 0), 22, 6);
            for (float dy = -0.028f; dy <= 0.0281f; dy += 0.014f)
            {
                float w = 2f * Mathf.Sqrt(0.037f * 0.037f - dy * dy);
                k.Box(new Vector3(-0.065f, -0.012f + dy, -0.0562f), new Vector3(w, 0.0045f, 0.0016f), 0.0006f, Sw.Chrome);
            }
            // Dial de sintonía iluminado
            k.Box(new Vector3(0.06f, 0.018f, -0.0538f), new Vector3(0.104f, 0.034f, 0.003f), 0.0015f, Sw.Graphite);
            k.Box(new Vector3(0.06f, 0.018f, -0.0548f), new Vector3(0.096f, 0.026f, 0.002f), 0f, Sw.Bulb);
            for (int i = 0; i < 9; i++)
                k.Box(new Vector3(0.022f + i * 0.0095f, 0.024f, -0.0561f), new Vector3(0.0012f, i % 2 == 0 ? 0.009f : 0.005f, 0.0008f), 0f, Sw.Black);
            k.Box(new Vector3(0.048f, 0.018f, -0.0566f), new Vector3(0.0016f, 0.026f, 0.0008f), 0f, Sw.PaintRed);
            // Perillas
            foreach (float x in new[] { 0.035f, 0.085f })
            {
                k.Cyl(new Vector3(x, -0.033f, -0.0585f), 0.013f, 0.01f, Sw.Graphite, new Vector3(90, 0, 0), 14, 0.002f);
                k.Cyl(new Vector3(x, -0.033f, -0.0637f), 0.006f, 0.0012f, Sw.Chrome, new Vector3(90, 0, 0), 12);
                k.Box(new Vector3(x, -0.026f, -0.0637f), new Vector3(0.0018f, 0.006f, 0.0012f), 0f, Sw.Orange);
            }
            // Patas
            foreach (int sx in new[] { -1, 1 })
                foreach (int sz in new[] { -1, 1 })
                    k.Cyl(new Vector3(sx * 0.11f, -0.0775f, sz * 0.033f), 0.01f, 0.005f, Sw.Rubber, default, 10);
            // Asa
            foreach (int sx in new[] { -1, 1 })
                k.Box(new Vector3(sx * 0.105f, 0.0625f, 0.03f), new Vector3(0.014f, 0.016f, 0.018f), 0.003f, Sw.Graphite);
            k.Box(new Vector3(0, 0.073f, 0.03f), new Vector3(0.224f, 0.012f, 0.02f), 0.004f, Sw.Graphite);
            k.Box(new Vector3(0, 0.073f, 0.03f), new Vector3(0.12f, 0.0145f, 0.0225f), 0.005f, Sw.Rubber);
            // Antena plegada
            k.Cyl(new Vector3(-0.128f, 0.05f, 0.044f), 0.006f, 0.01f, Sw.SteelDark, new Vector3(0, 0, 90), 10);
            k.Tube(new Vector3(-0.128f, 0.05f, 0.044f), new Vector3(0.09f, 0.05f, 0.044f), 0.0028f, Sw.Chrome, 8);
            k.Sphere(new Vector3(0.092f, 0.05f, 0.044f), 0.004f, Sw.Chrome, 8, 4);
            // Botones de presintonía
            for (int i = 0; i < 3; i++)
                k.Box(new Vector3(0.08f + i * 0.02f, 0.0575f, -0.025f), new Vector3(0.015f, 0.006f, 0.012f), 0.002f, i == 0 ? Sw.Orange : Sw.Cream);

            slots = new List<Slot>
            {
                new Slot(DefectType.LooseScrew, new Vector3(-0.105f, 0.055f, -0.02f), Vector3.zero),
                new Slot(DefectType.HiddenCrack, new Vector3(0.045f, 0.055f, 0.002f), Vector3.zero, 0.75f, -8f),
                new Slot(DefectType.BurnedCircuit, new Vector3(-0.032f, 0.055f, -0.008f), Vector3.zero, 0.9f),
            };
            cols = new List<Col>
            {
                new Col(new Vector3(0, -0.0125f, 0), new Vector3(0.28f, 0.135f, 0.10f)),
                new Col(new Vector3(0, 0.067f, 0.03f), new Vector3(0.225f, 0.025f, 0.022f)),
            };
            return k;
        }

        // ── Lámpara (base 0.14 x 0.10 x 0.14 + brazo y pantalla) ──
        static MeshKit Lamp(out List<Slot> slots, out List<Col> cols)
        {
            var k = new MeshKit();
            k.Box(new Vector3(0, -0.04f, 0), new Vector3(0.14f, 0.02f, 0.14f), 0.005f, Sw.Graphite);
            k.Box(new Vector3(0, 0.01f, 0), new Vector3(0.13f, 0.08f, 0.13f), 0.012f, Sw.LampYellow);
            k.Stripes(new Vector3(0, -0.021f, -0.0655f), Vector3.zero, 0.104f, 0.009f, 8, Sw.Black, Sw.HazardYellow);
            k.Vents(new Vector3(0.0655f, 0.012f, 0), new Vector3(0, -90, 0), 0.08f, 0.04f, 4, Sw.Graphite);
            // Interruptor y LED en el costado izquierdo
            k.Box(new Vector3(-0.0655f, 0.015f, -0.02f), new Vector3(0.003f, 0.028f, 0.022f), 0.0015f, Sw.Graphite);
            k.Tube(new Vector3(-0.067f, 0.015f, -0.02f), new Vector3(-0.077f, 0.022f, -0.02f), 0.0024f, Sw.Chrome, 8);
            k.Cyl(new Vector3(-0.0668f, -0.003f, -0.02f), 0.003f, 0.003f, Sw.LedGreen, new Vector3(0, 0, 90), 10);
            // Bisagra base del brazo (atrás-izquierda)
            k.Box(new Vector3(-0.035f, 0.058f, 0.04f), new Vector3(0.032f, 0.016f, 0.03f), 0.004f, Sw.Graphite);
            k.Cyl(new Vector3(-0.035f, 0.068f, 0.04f), 0.011f, 0.028f, Sw.Orange, new Vector3(0, 0, 90), 14, 0.002f);
            Vector3 elbow = new Vector3(-0.035f, 0.165f, 0.062f);
            foreach (float dx in new[] { -0.008f, 0.008f })
                k.Tube(new Vector3(-0.035f + dx, 0.068f, 0.04f), elbow + new Vector3(dx, 0, 0), 0.0035f, Sw.Steel, 8);
            k.Cyl(elbow, 0.0105f, 0.028f, Sw.Orange, new Vector3(0, 0, 90), 14, 0.002f);
            // Pantalla: abertura hacia abajo-adelante
            Vector3 shadeAxis = new Vector3(0, 0.8f, 0.6f).normalized;
            Vector3 shadePos = new Vector3(-0.012f, 0.152f, 0.0f);
            Vector3 apex = shadePos + shadeAxis * 0.052f;
            foreach (float dx in new[] { -0.008f, 0.008f })
                k.Tube(elbow + new Vector3(dx, 0, 0), apex + new Vector3(dx * 0.6f, 0, 0), 0.0032f, Sw.Steel, 8);
            k.Sphere(apex, 0.0085f, Sw.Orange, 12, 6);
            var shadeM = Matrix4x4.TRS(shadePos, Quaternion.FromToRotation(Vector3.up, shadeAxis), Vector3.one);
            k.Lathe(new[] { new Vector2(0.045f, 0f), new Vector2(0.045f, 0.006f), new Vector2(0.026f, 0.038f), new Vector2(0.013f, 0.049f), new Vector2(0f, 0.052f) },
                    shadeM, Sw.LampYellow, 18);
            k.Lathe(new[] { new Vector2(0f, 0.046f), new Vector2(0.011f, 0.044f), new Vector2(0.023f, 0.035f), new Vector2(0.042f, 0.005f), new Vector2(0.042f, 0f) },
                    shadeM, Sw.Cream, 18);
            k.Ring(shadePos, 0.0435f, 0.0025f, Sw.Graphite, Quaternion.FromToRotation(Vector3.up, shadeAxis).eulerAngles, 20, 4);
            k.Sphere(shadePos + shadeAxis * 0.02f, 0.013f, Sw.Bulb, 12, 6);

            slots = new List<Slot>
            {
                new Slot(DefectType.LooseScrew, new Vector3(-0.036f, 0.05f, -0.036f), Vector3.zero),
                new Slot(DefectType.Dent, new Vector3(0.022f, 0.05f, -0.02f), Vector3.zero, 0.8f),
                new Slot(DefectType.BurnedCircuit, new Vector3(0, 0.012f, -0.065f), Front, 0.85f),
            };
            cols = new List<Col>
            {
                new Col(Vector3.zero, new Vector3(0.14f, 0.10f, 0.14f)),
                new Col(new Vector3(-0.015f, 0.16f, 0.025f), new Vector3(0.1f, 0.08f, 0.1f)),
            };
            return k;
        }

        // ── Tostadora (0.26 x 0.13 x 0.15) ──
        static MeshKit Toaster(out List<Slot> slots, out List<Col> cols)
        {
            var k = new MeshKit();
            k.Box(new Vector3(0, -0.059f, 0), new Vector3(0.26f, 0.012f, 0.15f), 0.004f, Sw.Graphite);
            k.Box(new Vector3(0, 0.0035f, 0), new Vector3(0.24f, 0.113f, 0.14f), 0.018f, Sw.Chrome);
            // Ranuras
            foreach (int sz in new[] { -1, 1 })
            {
                k.Box(new Vector3(0, 0.0605f, sz * 0.022f), new Vector3(0.18f, 0.003f, 0.03f), 0.0015f, Sw.SteelDark);
                k.Box(new Vector3(0, 0.0616f, sz * 0.022f), new Vector3(0.168f, 0.0015f, 0.012f), 0f, Sw.Black);
            }
            // Banda frontal naranja y franja trasera
            k.Box(new Vector3(0, -0.03f, -0.0705f), new Vector3(0.2f, 0.008f, 0.003f), 0.001f, Sw.Orange);
            k.Stripes(new Vector3(0, -0.03f, 0.0705f), new Vector3(0, 180, 0), 0.2f, 0.01f, 12, Sw.HazardYellow, Sw.Graphite);
            // Lado derecho: palanca, dial y botón de cancelar
            k.Box(new Vector3(0.1205f, 0.012f, 0.02f), new Vector3(0.003f, 0.07f, 0.008f), 0f, Sw.Black);
            k.Box(new Vector3(0.127f, 0.03f, 0.02f), new Vector3(0.012f, 0.012f, 0.022f), 0.003f, Sw.Graphite);
            k.Cyl(new Vector3(0.1235f, -0.012f, -0.028f), 0.012f, 0.007f, Sw.Graphite, new Vector3(0, 0, 90), 14, 0.0015f);
            k.Box(new Vector3(0.1275f, -0.006f, -0.028f), new Vector3(0.0012f, 0.008f, 0.0018f), 0f, Sw.Orange);
            k.Cyl(new Vector3(0.122f, -0.035f, 0.02f), 0.006f, 0.005f, Sw.LedRed, new Vector3(0, 0, 90), 10);
            // Lado izquierdo: placa de identificación
            k.Box(new Vector3(-0.1205f, 0.006f, 0), new Vector3(0.003f, 0.032f, 0.07f), 0.0015f, Sw.Orange);
            foreach (int sz in new[] { -1, 1 })
                k.Rivet(new Vector3(-0.122f, 0.006f, sz * 0.03f), new Vector3(0, 0, 90), 0.0025f);

            slots = new List<Slot>
            {
                new Slot(DefectType.Dent, new Vector3(-0.05f, 0.006f, -0.07f), Front, 0.85f),
                new Slot(DefectType.HiddenCrack, new Vector3(0, 0.06f, -0.045f), Vector3.zero, 0.9f),
                new Slot(DefectType.BurnedCircuit, new Vector3(0.05f, 0.006f, -0.07f), Front, 0.85f),
            };
            cols = new List<Col> { new Col(Vector3.zero, new Vector3(0.26f, 0.13f, 0.15f)) };
            return k;
        }

        // ── Reloj (0.18 x 0.18 x 0.06) · los cuatro defectos ──
        static MeshKit Clock(out List<Slot> slots, out List<Col> cols)
        {
            var k = new MeshKit();
            const float fy = 0.004f;   // centro de la esfera
            k.Box(new Vector3(0, 0.004f, 0), new Vector3(0.18f, 0.172f, 0.06f), 0.008f, Sw.ClockGreen);
            foreach (int sx in new[] { -1, 1 })
            {
                k.Box(new Vector3(sx * 0.06f, -0.086f, 0), new Vector3(0.03f, 0.008f, 0.05f), 0.003f, Sw.Rubber);
                k.Box(new Vector3(sx * 0.0905f, 0.004f, 0), new Vector3(0.003f, 0.12f, 0.036f), 0.0015f, Sw.Orange);
            }
            // Esfera, bisel y marcas
            k.Ring(new Vector3(0, fy, -0.031f), 0.068f, 0.005f, Sw.Chrome, new Vector3(90, 0, 0), 28, 6);
            k.Cyl(new Vector3(0, fy, -0.0306f), 0.066f, 0.0018f, Sw.White, new Vector3(90, 0, 0), 28);
            for (int h = 0; h < 12; h++)
            {
                float a = h * 30f;
                bool major = h % 3 == 0;
                var m = Matrix4x4.TRS(new Vector3(0, fy, -0.0318f), Quaternion.Euler(0, 0, -a), Vector3.one);
                k.Box(m * Matrix4x4.Translate(new Vector3(0, 0.054f, 0)), new Vector3(major ? 0.005f : 0.0028f, major ? 0.011f : 0.007f, 0.0012f), 0f, major ? Sw.Orange : Sw.Black);
            }
            k.Box(Matrix4x4.TRS(new Vector3(0, fy, -0.0322f), Quaternion.Euler(0, 0, -60f), Vector3.one) * Matrix4x4.Translate(new Vector3(0, 0.016f, 0)),
                  new Vector3(0.0055f, 0.034f, 0.0012f), 0.0006f, Sw.Graphite);
            k.Box(Matrix4x4.TRS(new Vector3(0, fy, -0.0328f), Quaternion.Euler(0, 0, -200f), Vector3.one) * Matrix4x4.Translate(new Vector3(0, 0.022f, 0)),
                  new Vector3(0.0035f, 0.048f, 0.0012f), 0.0006f, Sw.Graphite);
            k.Cyl(new Vector3(0, fy, -0.0334f), 0.0045f, 0.0012f, Sw.Orange, new Vector3(90, 0, 0), 12);
            // Tornillos decorativos en tres esquinas (la cuarta es el slot del tornillo flojo)
            k.ScrewHead(new Vector3(0.07f, fy + 0.07f, -0.03f), Front, 0.0045f);
            k.ScrewHead(new Vector3(-0.07f, fy + 0.07f, -0.03f), Front, 0.0045f);
            k.ScrewHead(new Vector3(0.07f, fy - 0.07f, -0.03f), Front, 0.0045f);
            // Llave de cuerda trasera
            k.Cyl(new Vector3(0, 0.03f, 0.032f), 0.012f, 0.004f, Sw.Orange, new Vector3(90, 0, 0), 12, 0.0015f);   // llave de cuerda plana (fondo total ≈ 6.8 cm)

            slots = new List<Slot>
            {
                new Slot(DefectType.LooseScrew, new Vector3(-0.07f, fy - 0.07f, -0.03f), Front, 0.75f),
                new Slot(DefectType.Dent, new Vector3(-0.042f, 0.09f, 0), Vector3.zero, 0.7f),
                new Slot(DefectType.HiddenCrack, new Vector3(0.018f, -0.03f, -0.0316f), Front, 0.75f, -25f),
                new Slot(DefectType.BurnedCircuit, new Vector3(0.038f, 0.09f, 0), Vector3.zero, 0.8f),
            };
            cols = new List<Col> { new Col(new Vector3(0, 0, -0.003f), new Vector3(0.18f, 0.18f, 0.066f)) };
            return k;
        }
    }
}
