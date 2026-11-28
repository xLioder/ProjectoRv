using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace QCXR.EditorTools.Art
{
    // Menú QCXR > Arte (estación). Genera los prefabs visuales del puesto de trabajo y la nave.
    // No llevan scripts de juego: en la escena se colocan junto a los objetos que ya tienen ConveyorBelt,
    // ProductStation, XRButton, etc. (ver QCXRSceneAssembler).
    //
    // Cada prefab se modela en las coordenadas de su punto de anclaje en la escena (comentado en cada uno).
    public static class QCXRStationBuilder
    {
        public const string PrefabDir = "Assets/_Project/Prefabs/Environment";
        const string Dir = QCXRArtBuilder.ArtRoot + "/Environment";
        public const float BeltTile = 0.48f;   // metros de banda por repetición de textura

        static Material atlas, beltMat;

        [MenuItem("Tools/QCXR/Arte/Fase 4 · Prefabs de estación")]
        public static void BuildPrefabs()
        {
            if (!QCXRSceneAssembler.ConfirmPrefabRegen("los prefabs de estación (Env_*)")) return;
            QCXRPrefabBuilder.EnsureFolder(PrefabDir);
            QCXRPrefabBuilder.EnsureFolder(Dir);
            atlas = QCXRArtBuilder.Atlas;
            beltMat = BuildBeltMaterial();

            ConveyorBelt();
            ToolBench();
            ButtonConsole();
            ButtonCap("Green", Sw.LedGreen);
            ButtonCap("Red", Sw.LedRed);
            ShelfRack();
            DiscardBin();
            Beacon("Env_JamIndicatorLit", Sw.LedAmber, true);
            Beacon("Env_EmergencyBeaconLit", Sw.LedRed, false);
            Room();
            AssetDatabase.SaveAssets();
            Debug.Log("[QCXR Art] Fase 4: prefabs de estación y nave generados.");
        }

        // Utilidades

        static GameObject Root(string name) => new GameObject(name);

        static GameObject Mesh(GameObject root, string name, MeshKit k, bool shadows = true, Transform parent = null, Material mat = null)
        {
            var mesh = QCXRArtBuilder.SaveMesh(k, $"{Dir}/{root.name}_{name}.asset", $"{root.name}_{name}");
            return QCXRArtBuilder.MeshObject(name, parent != null ? parent : root.transform, mesh, mat != null ? mat : atlas, shadows);
        }

        static Transform Empty(GameObject root, string name, Vector3 pos, Vector3 euler = default, Transform parent = null)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent != null ? parent : root.transform, false);
            t.localPosition = pos; t.localRotation = Quaternion.Euler(euler);
            return t;
        }

        static void Save(GameObject root, bool isStatic = true)
        {
            if (isStatic)
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                    GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.BatchingStatic | StaticEditorFlags.ContributeGI | StaticEditorFlags.ReflectionProbeStatic);
            PrefabUtility.SaveAsPrefabAsset(root, $"{PrefabDir}/{root.name}.prefab");
            Object.DestroyImmediate(root);
        }

        static TextMeshPro Sign(Transform parent, string name, string text, Vector3 pos, Vector3 euler, float size, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos; go.transform.localRotation = Quaternion.Euler(euler);
            var tmp = go.AddComponent<TextMeshPro>();
            tmp.text = text; tmp.fontSize = size; tmp.color = color;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontStyle = FontStyles.Bold;
            tmp.rectTransform.sizeDelta = new Vector2(4f, 0.6f);
            return tmp;
        }

        static readonly Vector3 Up = new Vector3(90, 0, 0);       // Stripes/Vents (miran a -Z) -> miran a +Y
        static readonly Vector3 FaceX = new Vector3(0, -90, 0);   // -> miran a +X
        static readonly Vector3 FaceNegX = new Vector3(0, 90, 0); // -> miran a -X
        static readonly Vector3 FaceZ = new Vector3(0, 180, 0);   // -> miran a +Z

        // Banda

        static Material BuildBeltMaterial()
        {
            const int W = 64, H = 64;
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, true);
            Color baseC = new Color32(0x1E, 0x1F, 0x23, 255), rib = new Color32(0x2B, 0x2D, 0x32, 255),
                  chevron = new Color32(0x44, 0x48, 0x50, 255), edge = new Color32(0xF5, 0xC5, 0x18, 255);
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    Color c = baseC;
                    if (y % 16 < 2) c = rib;                                       // costillas transversales
                    int cx = Mathf.Abs(x - W / 2);
                    int band = (y + cx / 2) % 32;                                  // chevrones hacia +V (= sentido de avance)
                    if (cx < 22 && band >= 10 && band < 16) c = chevron;
                    if (x < 3 || x >= W - 3) c = edge;                             // bordes de seguridad
                    tex.SetPixel(x, y, c);
                }
            string path = Dir + "/QCXR_BeltSurface.png";
            File.WriteAllBytes(Path.Combine(Application.dataPath, path.Substring("Assets/".Length)), tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var ti = (TextureImporter)AssetImporter.GetAtPath(path);
            ti.wrapMode = TextureWrapMode.Repeat; ti.filterMode = FilterMode.Bilinear; ti.mipmapEnabled = true;
            ti.anisoLevel = 4; ti.sRGBTexture = true;
            ti.SaveAndReimport();
            var t = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            return QCXRArtBuilder.Mat(QCXRArtBuilder.MatDir + "/QCXR_Belt.mat", m =>
            {
                m.SetTexture("_BaseMap", t); m.SetColor("_BaseColor", Color.white);
                m.SetFloat("_Metallic", 0f); m.SetFloat("_Smoothness", 0.25f);
            });
        }

        // Superficie de la banda con UV propias: U a lo ancho, V a lo largo (V crece hacia -X = sentido de avance).
        static Mesh BeltSurfaceMesh(float length, float width, float y)
        {
            float x0 = -length / 2, x1 = length / 2, z0 = -width / 2, z1 = width / 2;
            var v = new[] { new Vector3(x0, y, z0), new Vector3(x0, y, z1), new Vector3(x1, y, z1), new Vector3(x1, y, z0) };
            var uv = new Vector2[4];
            for (int i = 0; i < 4; i++) uv[i] = new Vector2((v[i].z - z0) / width, -v[i].x / BeltTile);
            var m = new Mesh { name = "Env_ConveyorBelt_Surface", vertices = v, uv = uv, triangles = new[] { 0, 1, 2, 0, 2, 3 } };
            m.normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
            m.RecalculateBounds(); m.RecalculateTangents();
            string path = $"{Dir}/Env_ConveyorBelt_Surface.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null) { AssetDatabase.CreateAsset(m, path); return m; }
            EditorUtility.CopySerialized(m, existing);
            return existing;
        }

        // Anclaje: (0, 0, 0.75) en el mundo. X = largo (3.4 m, avance hacia -X), Z = ancho, Y = altura absoluta.
        static void ConveyorBelt()
        {
            var root = Root("Env_ConveyorBelt");
            const float L = 3.4f, top = 0.9f;
            var k = new MeshKit();
            // Cuerpo bajo la cinta y vigas laterales
            k.Box(new Vector3(0, 0.835f, 0), new Vector3(L + 0.04f, 0.11f, 0.48f), 0.01f, Sw.Graphite);
            foreach (int s in new[] { -1, 1 })
            {
                k.Box(new Vector3(0, 0.845f, s * 0.265f), new Vector3(L + 0.1f, 0.13f, 0.04f), 0.008f, Sw.Orange);
                k.Box(new Vector3(0, 0.935f, s * 0.255f), new Vector3(L, 0.03f, 0.02f), 0.004f, Sw.Steel);
                for (float x = -1.6f; x <= 1.61f; x += 0.4f)
                    k.Rivet(new Vector3(x, 0.875f, s * 0.2855f), new Vector3(s * 90, 0, 0), 0.006f);
                // marcas verdes de la zona de inspección sobre los rieles
                foreach (float x in new[] { -0.17f, 0.17f })
                    k.Box(new Vector3(x, 0.951f, s * 0.255f), new Vector3(0.02f, 0.004f, 0.024f), 0f, Sw.SafetyGreen);
            }
            k.Stripes(new Vector3(0, 0.81f, -0.2855f), Vector3.zero, L, 0.05f, 56, Sw.HazardYellow, Sw.Graphite);
            // Rodillos de extremo
            foreach (int s in new[] { -1, 1 })
            {
                k.Cyl(new Vector3(s * 1.72f, 0.87f, 0), 0.034f, 0.5f, Sw.Steel, new Vector3(90, 0, 0), 16, 0.004f);
                foreach (int z in new[] { -1, 1 })
                    k.Cyl(new Vector3(s * 1.72f, 0.87f, z * 0.288f), 0.024f, 0.012f, Sw.Orange, new Vector3(90, 0, 0), 12, 0.002f);
            }
            // Patas, refuerzos y pies
            foreach (float x in new[] { -1.5f, -0.5f, 0.5f, 1.5f })
            {
                foreach (int s in new[] { -1, 1 })
                {
                    k.Box(new Vector3(x, 0.39f, s * 0.22f), new Vector3(0.06f, 0.78f, 0.06f), 0.008f, Sw.Graphite);
                    k.Box(new Vector3(x, 0.01f, s * 0.22f), new Vector3(0.12f, 0.02f, 0.12f), 0.004f, Sw.SteelDark);
                }
                k.Box(new Vector3(x, 0.32f, 0), new Vector3(0.04f, 0.04f, 0.44f), 0.004f, Sw.Graphite);
            }
            k.Box(new Vector3(0, 0.32f, 0), new Vector3(3.06f, 0.05f, 0.05f), 0.004f, Sw.GraphiteLight);

            // Máquina de entrada (spawn, +X) con capota y cortina de tiras
            k.Box(new Vector3(2.07f, 0.68f, 0), new Vector3(0.5f, 1.36f, 0.64f), 0.03f, Sw.Cream);
            k.Box(new Vector3(1.83f, 0.68f, 0), new Vector3(0.03f, 1.0f, 0.5f), 0.01f, Sw.Orange);
            k.Stripes(new Vector3(2.07f, 0.12f, -0.3215f), Vector3.zero, 0.46f, 0.1f, 8, Sw.HazardYellow, Sw.Graphite);
            k.Vents(new Vector3(2.07f, 0.5f, -0.321f), Vector3.zero, 0.3f, 0.25f, 5, Sw.Graphite);
            for (int i = 0; i < 3; i++) k.Cyl(new Vector3(1.96f + i * 0.06f, 1.2f, -0.321f), 0.012f, 0.008f, i == 0 ? Sw.LedGreen : i == 1 ? Sw.LedAmber : Sw.LedRed, new Vector3(90, 0, 0), 10);
            k.Box(new Vector3(1.55f, 1.33f, 0), new Vector3(0.5f, 0.03f, 0.62f), 0.01f, Sw.Orange);
            foreach (int s in new[] { -1, 1 }) k.Box(new Vector3(1.55f, 1.12f, s * 0.3f), new Vector3(0.5f, 0.42f, 0.02f), 0.006f, Sw.Orange);
            for (float z = -0.24f; z <= 0.241f; z += 0.06f) k.Box(new Vector3(1.31f, 1.21f, z), new Vector3(0.006f, 0.22f, 0.05f), 0.002f, Sw.Rubber);
            k.Stripes(new Vector3(1.55f, 1.3f, -0.311f), Vector3.zero, 0.5f, 0.05f, 9, Sw.HazardYellow, Sw.Graphite);

            // Salida (-X) con tolva de reciclaje
            k.Box(new Vector3(-2.05f, 0.6f, 0), new Vector3(0.46f, 1.2f, 0.62f), 0.03f, Sw.Graphite);
            k.Box(new Vector3(-1.85f, 0.92f, 0), new Vector3(0.06f, 0.18f, 0.5f), 0.01f, Sw.Black);
            k.Box(new Vector3(-1.62f, 1.24f, 0), new Vector3(0.3f, 0.03f, 0.62f), 0.01f, Sw.Graphite);
            foreach (int s in new[] { -1, 1 }) k.Box(new Vector3(-1.62f, 1.07f, s * 0.3f), new Vector3(0.3f, 0.34f, 0.02f), 0.006f, Sw.Graphite);
            k.Stripes(new Vector3(-1.62f, 1.215f, -0.311f), Vector3.zero, 0.3f, 0.04f, 6, Sw.HazardYellow, Sw.Black);
            k.Box(new Vector3(-2.05f, 1.22f, -0.32f), new Vector3(0.2f, 0.08f, 0.01f), 0.002f, Sw.PaintRed);

            // Pórtico de inspección (desde atrás, cabezal sobre el centro de la banda)
            k.Box(new Vector3(0, 1.22f, 0.33f), new Vector3(0.06f, 0.64f, 0.06f), 0.008f, Sw.Orange);
            k.Box(new Vector3(0, 1.56f, 0.15f), new Vector3(0.07f, 0.06f, 0.42f), 0.01f, Sw.Orange);
            k.Box(new Vector3(0, 1.505f, 0), new Vector3(0.22f, 0.05f, 0.13f), 0.012f, Sw.Graphite);
            k.Box(new Vector3(0, 1.478f, 0), new Vector3(0.18f, 0.004f, 0.09f), 0f, Sw.SafetyGreen);
            k.Cyl(new Vector3(0, 1.6f, 0.33f), 0.035f, 0.02f, Sw.Graphite, default, 14, 0.004f);           // base de la baliza de atasco
            k.Lathe(new[] { new Vector2(0, 1.61f), new Vector2(0.03f, 1.61f), new Vector2(0.03f, 1.64f), new Vector2(0.02f, 1.665f), new Vector2(0, 1.67f) },
                    Matrix4x4.Translate(new Vector3(0, 0, 0.33f)), Sw.OrangeDark, 14);                       // cúpula apagada
            Mesh(root, "Frame", k);

            // Cinta con material propio (el desplazamiento lo hace ConveyorBelt.beltRenderer)
            var surf = new GameObject("BeltSurface");
            surf.transform.SetParent(root.transform, false);
            surf.AddComponent<MeshFilter>().sharedMesh = BeltSurfaceMesh(L, 0.48f, top);
            var r = surf.AddComponent<MeshRenderer>(); r.sharedMaterial = beltMat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            Empty(root, "SurfaceRef", new Vector3(0, top, 0), new Vector3(0, -90, 0));     // +Z = sentido de avance (-X mundo)
            Empty(root, "InspectionZone", new Vector3(0, top, 0));
            Empty(root, "SpawnMouth", new Vector3(1.55f, top, 0));
            Empty(root, "ExitMouth", new Vector3(-1.62f, top, 0));
            Sign(root.transform, "Sign_Inspection", "INSPECCIÓN", new Vector3(0, 1.505f, -0.068f), Vector3.zero, 0.35f, new Color(0.2f, 0.95f, 0.45f));
            Save(root);
        }

        // Banco de herramientas

        public static readonly Vector3 BenchAnchor = new Vector3(0, 0, 0.33f);
        public const float BenchTop = 0.8f;

        // Anclaje: (0, 0, 0.33). Banco de 1.0 x 0.18 m, superficie a 0.8 m, delante de la banda.
        static void ToolBench()
        {
            var root = Root("Env_ToolBench");
            var k = new MeshKit();
            k.Box(new Vector3(0, BenchTop - 0.015f, 0), new Vector3(1.02f, 0.03f, 0.2f), 0.006f, Sw.Graphite);
            k.Box(new Vector3(0, BenchTop - 0.001f, 0), new Vector3(0.96f, 0.004f, 0.16f), 0.001f, Sw.Rubber);
            k.Box(new Vector3(0, BenchTop - 0.03f, -0.1f), new Vector3(1.04f, 0.06f, 0.02f), 0.005f, Sw.Orange);
            k.Box(new Vector3(0, BenchTop + 0.012f, 0.094f), new Vector3(1.0f, 0.024f, 0.012f), 0.003f, Sw.Orange);   // tope trasero
            k.Stripes(new Vector3(0, BenchTop - 0.03f, -0.1105f), Vector3.zero, 0.96f, 0.04f, 18, Sw.HazardYellow, Sw.Graphite);
            foreach (int sx in new[] { -1, 1 })
                foreach (int sz in new[] { -1, 1 })
                    k.Box(new Vector3(sx * 0.47f, (BenchTop - 0.03f) / 2, sz * 0.07f), new Vector3(0.04f, BenchTop - 0.03f, 0.04f), 0.006f, Sw.Graphite);
            k.Box(new Vector3(0, 0.22f, 0), new Vector3(0.94f, 0.02f, 0.16f), 0.004f, Sw.SteelDark);              // repisa inferior
            foreach (int sx in new[] { -1, 1 }) k.Box(new Vector3(sx * 0.47f, 0.01f, 0), new Vector3(0.08f, 0.02f, 0.2f), 0.004f, Sw.SteelDark);
            // Siluetas de las herramientas sobre la alfombrilla (dónde devolver cada una)
            var spots = new (string name, float x)[] { ("Hammer", -0.32f), ("Screwdriver", 0f), ("Magnifier", 0.32f) };
            foreach (var (name, x) in spots)
            {
                k.Box(new Vector3(x, BenchTop + 0.0012f, 0), new Vector3(0.27f, 0.0012f, 0.006f), 0f, Sw.CreamDark);
                k.Box(new Vector3(x, BenchTop + 0.0012f, -0.06f), new Vector3(0.04f, 0.0012f, 0.012f), 0f, Sw.Orange);
                Empty(root, "Spot_" + name, new Vector3(x, BenchTop, 0));
            }
            Mesh(root, "Body", k);
            Save(root);
        }

        // Consola de botones

        public static readonly Vector3 ConsoleAnchor = new Vector3(0.8f, 0, 0.25f);
        public static readonly Vector3 ConsoleSize = new Vector3(0.3f, 0.88f, 0.44f);
        public static readonly Vector3 StartButtonLocal = new Vector3(0, 0.925f, -0.1f);   // más cerca del jugador
        public static readonly Vector3 ResetButtonLocal = new Vector3(0, 0.925f, 0.1f);

        // Anclaje: (0.8, 0, 0.25). Pedestal 0.3 (X) x 0.44 (Z), superficie a 0.9 m. Frente hacia -X (el jugador).
        // Carcasas de botón en z = -0.1 ("Iniciar turno") y z = +0.1 ("Reiniciar banda").
        static void ButtonConsole()
        {
            var root = Root("Env_ButtonConsole");
            var k = new MeshKit();
            k.Box(new Vector3(0, 0.44f, 0), ConsoleSize, 0.02f, Sw.Graphite);
            k.Box(new Vector3(0, 0.89f, 0), new Vector3(0.32f, 0.02f, 0.46f), 0.008f, Sw.Orange);
            k.Stripes(new Vector3(-0.1505f, 0.8f, 0), FaceNegX, 0.4f, 0.05f, 11, Sw.HazardYellow, Sw.Black);
            k.Vents(new Vector3(-0.1505f, 0.35f, 0), FaceNegX, 0.26f, 0.2f, 5, Sw.Black);
            k.Box(new Vector3(0, 0.01f, 0), new Vector3(0.34f, 0.02f, 0.48f), 0.004f, Sw.SteelDark);
            Mesh(root, "Pedestal", k);
            foreach (var (name, p, led) in new[] { ("Base_StartShift", StartButtonLocal, Sw.LedGreen), ("Base_ResetBelt", ResetButtonLocal, Sw.LedRed) })
            {
                var h = new MeshKit();
                h.Cyl(new Vector3(0, 0.006f, 0), 0.058f, 0.012f, Sw.Graphite, default, 20, 0.003f);
                h.Ring(new Vector3(0, 0.012f, 0), 0.05f, 0.003f, Sw.Steel, default, 20, 4);
                h.Box(new Vector3(-0.09f, 0.001f, 0), new Vector3(0.03f, 0.002f, 0.1f), 0.001f, Sw.Cream);
                h.Cyl(new Vector3(-0.09f, 0.004f, 0.04f), 0.005f, 0.006f, led, default, 10);
                var go = Mesh(root, name, h);
                go.transform.localPosition = new Vector3(p.x, 0.9f, p.z);
            }
            Save(root);
        }

        // Tapa móvil del botón. Pivote en la base (se coloca 1.3 cm bajo el objeto XRButton). Hijos RestPos/PressedPos.
        static void ButtonCap(string color, Sw sw)
        {
            var root = Root("Env_ButtonCap_" + color);
            var k = new MeshKit();
            k.Cyl(new Vector3(0, 0.003f, 0), 0.031f, 0.006f, Sw.GraphiteLight, default, 16);
            k.Lathe(new[] { new Vector2(0, 0.004f), new Vector2(0.042f, 0.004f), new Vector2(0.044f, 0.01f), new Vector2(0.04f, 0.021f),
                            new Vector2(0.028f, 0.029f), new Vector2(0, 0.032f) }, Matrix4x4.identity, sw, 20);
            var cap = Mesh(root, "Cap", k);
            Empty(root, "RestPos", Vector3.zero);
            Empty(root, "PressedPos", new Vector3(0, -0.012f, 0));
            Save(root, false);
        }

        // Estantería

        public static readonly (ProductType type, Vector3 local, Sw color)[] Bays =
        {
            (ProductType.Robot,   new Vector3(0, 0.9f,  -0.3f),  Sw.RobotBlue),
            (ProductType.Radio,   new Vector3(0, 0.9f,   0f),    Sw.RadioRed),
            (ProductType.Lamp,    new Vector3(0, 0.9f,   0.3f),  Sw.LampYellow),
            (ProductType.Toaster, new Vector3(0, 1.32f, -0.15f), Sw.Chrome),
            (ProductType.Clock,   new Vector3(0, 1.32f,  0.15f), Sw.ClockGreen),
        };

        // Anclaje: (-0.85, 0, -0.2). Frente hacia +X (jugador). Fila baja a 0.9 m, fila alta a 1.32 m.
        static void ShelfRack()
        {
            var root = Root("Env_ShelfRack");
            var k = new MeshKit();
            // Armario inferior
            k.Box(new Vector3(0, 0.44f, 0), new Vector3(0.4f, 0.88f, 1.0f), 0.015f, Sw.Graphite);
            k.Box(new Vector3(0, 0.89f, 0), new Vector3(0.42f, 0.02f, 1.02f), 0.006f, Sw.SteelDark);
            k.Box(new Vector3(0.205f, 0.85f, 0), new Vector3(0.012f, 0.06f, 1.0f), 0.004f, Sw.Orange);
            k.Stripes(new Vector3(0.2015f, 0.12f, 0), FaceX, 0.96f, 0.06f, 18, Sw.HazardYellow, Sw.Graphite);
            foreach (float z in new[] { -0.17f, 0.17f }) k.Box(new Vector3(0.201f, 0.48f, z), new Vector3(0.004f, 0.62f, 0.006f), 0f, Sw.Black);
            foreach (float z in new[] { -0.33f, 0f, 0.33f }) k.Box(new Vector3(0.205f, 0.55f, z + 0.12f), new Vector3(0.012f, 0.1f, 0.016f), 0.004f, Sw.Steel);
            // Cuerpo superior
            foreach (int s in new[] { -1, 1 }) k.Box(new Vector3(-0.17f, 1.27f, s * 0.37f), new Vector3(0.05f, 0.74f, 0.05f), 0.006f, Sw.Orange);
            k.Box(new Vector3(0, 1.3f, 0), new Vector3(0.4f, 0.04f, 0.74f), 0.008f, Sw.SteelDark);
            k.Box(new Vector3(0.198f, 1.3f, 0), new Vector3(0.01f, 0.04f, 0.74f), 0.002f, Sw.Orange);
            k.Box(new Vector3(-0.19f, 1.27f, 0), new Vector3(0.02f, 0.74f, 0.7f), 0.004f, Sw.WallPanel);
            k.Box(new Vector3(-0.1f, 1.655f, 0), new Vector3(0.2f, 0.03f, 0.8f), 0.006f, Sw.Graphite);
            k.Box(new Vector3(0.0f, 1.66f, 0), new Vector3(0.012f, 0.1f, 0.7f), 0.003f, Sw.Orange);
            // Huecos: base con marco y color del producto
            foreach (var (type, p, color) in Bays)
            {
                k.Box(p + new Vector3(0, 0.003f, 0), new Vector3(0.27f, 0.006f, 0.27f), 0.002f, Sw.Graphite);
                k.Box(p + new Vector3(0, 0.0045f, 0), new Vector3(0.23f, 0.006f, 0.23f), 0.002f, color);
            }
            Mesh(root, "Rack", k);
            foreach (var (type, p, color) in Bays)
            {
                var bay = Empty(root, "Bay_" + type, p);
                var l = new MeshKit();
                l.Box(new Vector3(0.19f, 0.012f, 0), new Vector3(0.012f, 0.012f, 0.1f), 0.002f, Sw.LampWhite);
                var light = Mesh(root, "BayLight_" + type, l, false, bay);
                light.name = "BayLight";
                Empty(root, "PlacementZone", new Vector3(0, 0.0075f, 0), default, bay);
            }
            Sign(root.transform, "Sign_Shelves", "ESTANTES · PRODUCTO OK", new Vector3(0.007f, 1.66f, 0), new Vector3(0, -90, 0), 0.5f, Color.black);
            Save(root);
        }

        // Cubo de desecho

        // Anclaje: (0.8, 0, -0.3). Interior 0.34 m, fondo a 0.72 m, borde a 0.98 m.
        static void DiscardBin()
        {
            var root = Root("Env_DiscardBin");
            var k = new MeshKit();
            k.Box(new Vector3(0, 0.35f, 0), new Vector3(0.42f, 0.7f, 0.42f), 0.02f, Sw.Graphite);
            foreach (var e in new[] { Vector3.zero, FaceX, FaceZ, FaceNegX })
            {
                var rot = Quaternion.Euler(e);
                k.Stripes(rot * new Vector3(0, 0, -0.2115f) + new Vector3(0, 0.6f, 0), e, 0.38f, 0.06f, 10, Sw.HazardYellow, Sw.Black);
                k.Box(rot * new Vector3(0, 0, -0.195f) + new Vector3(0, 0.84f, 0), new Vector3(0.42f, 0.28f, 0.03f), 0.006f, Sw.PaintRed, e);
                k.Box(rot * new Vector3(0, 0, -0.1785f) + new Vector3(0, 0.84f, 0), new Vector3(0.355f, 0.26f, 0.003f), 0f, Sw.Black, e);
                k.Box(rot * new Vector3(0, 0, -0.205f) + new Vector3(0, 0.975f, 0), new Vector3(0.47f, 0.025f, 0.05f), 0.006f, Sw.Graphite, e);
            }
            k.Box(new Vector3(0, 0.715f, 0), new Vector3(0.36f, 0.01f, 0.36f), 0f, Sw.Black);
            Mesh(root, "Bin", k);
            var f = new MeshKit();
            foreach (var e in new[] { Vector3.zero, FaceX, FaceZ, FaceNegX })
                f.Box(Quaternion.Euler(e) * new Vector3(0, 0, -0.175f) + new Vector3(0, 0.955f, 0), new Vector3(0.33f, 0.008f, 0.004f), 0f, Sw.LampWhite, e);
            Mesh(root, "FlashRing", f, false);
            Empty(root, "EntryZone", new Vector3(0, 0.98f, 0));
            Sign(root.transform, "Sign_Discard", "DESECHO", new Vector3(-0.2125f, 0.84f, 0), FaceNegX, 0.55f, Color.white);
            Save(root);
        }

        // Balizas

        // Parte encendida de una baliza (se activa/desactiva junto con su objeto de escena).
        // Hijo Rotor: el reflector que la animación hace girar sobre +Y.
        static void Beacon(string name, Sw lit, bool sparksAnchor)
        {
            var root = Root(name);
            var k = new MeshKit();
            k.Lathe(new[] { new Vector2(0, 0f), new Vector2(0.033f, 0f), new Vector2(0.033f, 0.032f), new Vector2(0.022f, 0.058f), new Vector2(0, 0.064f) },
                    Matrix4x4.identity, lit, 16);
            Mesh(root, "LitDome", k, false);
            var rotor = new GameObject("Rotor").transform;
            rotor.SetParent(root.transform, false);
            var r = new MeshKit();
            r.Box(new Vector3(0, 0.025f, 0.01f), new Vector3(0.04f, 0.03f, 0.003f), 0f, Sw.Chrome);
            r.Box(new Vector3(0, 0.025f, -0.006f), new Vector3(0.006f, 0.03f, 0.03f), 0f, Sw.Graphite);
            Mesh(root, "Reflector", r, false, rotor);
            if (sparksAnchor) Empty(root, "SparksAnchor", new Vector3(0.12f, -0.68f, -0.06f), new Vector3(-60, 0, 0));
            Save(root, false);
        }

        // Nave

        // Anclaje: origen. Nave compacta de 5.4 x 4.4 m (x ±2.7, z -1.7..2.7) y 3.2 m de alto.
        static void Room()
        {
            var root = Root("Env_Room");
            const float X = 2.7f, Z0 = -1.7f, Z1 = 2.7f, H = 3.2f;
            float cz = (Z0 + Z1) / 2, dz = Z1 - Z0;

            // Suelo con juntas y señalización
            var fl = new MeshKit();
            fl.Box(new Vector3(0, -0.01f, cz), new Vector3(2 * X, 0.02f, dz), 0f, Sw.Concrete);
            for (float x = -2f; x <= 2.01f; x += 1f) fl.Box(new Vector3(x, 0.0005f, cz), new Vector3(0.01f, 0.001f, dz), 0f, Sw.ConcreteDark);
            for (float z = -1f; z <= 2.01f; z += 1f) fl.Box(new Vector3(0, 0.0005f, z), new Vector3(2 * X, 0.001f, 0.01f), 0f, Sw.ConcreteDark);
            // Zona de trabajo del jugador delimitada con franja de peligro
            float wx = 1.15f, wz0 = -0.85f, wz1 = 0.45f;
            fl.Stripes(new Vector3(0, 0.0012f, wz0), Up, 2 * wx, 0.08f, 24, Sw.HazardYellow, Sw.Black);
            fl.Stripes(new Vector3(-wx, 0.0012f, (wz0 + wz1) / 2), new Vector3(90, 90, 0), wz1 - wz0, 0.08f, 14, Sw.HazardYellow, Sw.Black);
            fl.Stripes(new Vector3(wx, 0.0012f, (wz0 + wz1) / 2), new Vector3(90, 90, 0), wz1 - wz0, 0.08f, 14, Sw.HazardYellow, Sw.Black);
            fl.Box(new Vector3(0, 0.0012f, -0.3f), new Vector3(0.5f, 0.001f, 0.5f), 0f, Sw.ConcreteDark);   // alfombrilla del puesto
            fl.Box(new Vector3(0, 0.0015f, 1.25f), new Vector3(3.6f, 0.001f, 0.06f), 0f, Sw.HazardYellow);   // pasillo tras la banda
            Mesh(root, "Floor", fl, false);

            // Paredes: zócalo grafito, banda naranja, paneles claros y columnas
            var w = new MeshKit();
            void Wall(Vector3 c, Vector3 euler, float width)
            {
                var r = Quaternion.Euler(euler);
                w.Box(c + r * new Vector3(0, 0.5f, 0), new Vector3(width, 1.0f, 0.06f), 0.006f, Sw.Graphite, euler);
                w.Box(c + r * new Vector3(0, 1.06f, -0.005f), new Vector3(width, 0.12f, 0.06f), 0.006f, Sw.Orange, euler);
                w.Box(c + r * new Vector3(0, 2.14f, 0), new Vector3(width, 2.12f, 0.06f), 0f, Sw.WallPanel, euler);
                for (float x = -width / 2 + 1f; x < width / 2; x += 1f)
                    w.Box(c + r * new Vector3(x, 2.14f, -0.031f), new Vector3(0.012f, 2.0f, 0.004f), 0f, Sw.CreamDark, euler);
            }
            Wall(new Vector3(0, 0, Z1), Vector3.zero, 2 * X);   // -Z local = hacia dentro de la nave
            Wall(new Vector3(0, 0, Z0), FaceZ, 2 * X);
            Wall(new Vector3(X, 0, cz), FaceNegX, dz);
            Wall(new Vector3(-X, 0, cz), FaceX, dz);
            foreach (float x in new[] { -X + 0.12f, -0.9f, 0.9f, X - 0.12f })
                foreach (float z in new[] { Z1 - 0.12f, Z0 + 0.12f })
                {
                    w.Box(new Vector3(x, H / 2, z), new Vector3(0.18f, H, 0.18f), 0.01f, Sw.Orange);
                    w.Box(new Vector3(x, 0.15f, z), new Vector3(0.24f, 0.3f, 0.24f), 0.01f, Sw.Graphite);
                }
            Mesh(root, "Walls", w, false);

            // Techo con vigas, luminarias y tuberías
            var c = new MeshKit();
            c.Box(new Vector3(0, H + 0.03f, cz), new Vector3(2 * X, 0.06f, dz), 0f, Sw.GraphiteLight);
            for (float z = Z0 + 0.6f; z < Z1; z += 1.1f) c.Box(new Vector3(0, H - 0.08f, z), new Vector3(2 * X, 0.16f, 0.12f), 0.01f, Sw.Graphite);
            foreach (var p in new[] { new Vector3(-1.1f, 0, -0.2f), new Vector3(1.1f, 0, -0.2f), new Vector3(-1.1f, 0, 1.4f), new Vector3(1.1f, 0, 1.4f), new Vector3(0, 0, 0.6f) })
            {
                c.Tube(new Vector3(p.x, H - 0.16f, p.z), new Vector3(p.x, 2.78f, p.z), 0.008f, Sw.SteelDark, 6);
                c.Box(new Vector3(p.x, 2.75f, p.z), new Vector3(0.9f, 0.06f, 0.22f), 0.012f, Sw.Graphite);
                c.Box(new Vector3(p.x, 2.718f, p.z), new Vector3(0.84f, 0.004f, 0.16f), 0f, Sw.LampWhite);
            }
            foreach (var (y, z, sw) in new[] { (2.6f, Z1 - 0.18f, Sw.Orange), (2.75f, Z1 - 0.2f, Sw.Steel), (2.45f, Z1 - 0.16f, Sw.ClockGreenDark) })
            {
                c.Tube(new Vector3(-X + 0.2f, y, z), new Vector3(X - 0.2f, y, z), 0.045f, sw, 12);
                for (float x = -2f; x <= 2.01f; x += 1f) c.Box(new Vector3(x, y, z + 0.04f), new Vector3(0.03f, 0.12f, 0.06f), 0.004f, Sw.Graphite);
            }
            // Baliza de emergencia (carcasa apagada; la parte encendida es EmergencyLight en la escena)
            c.Tube(new Vector3(0, H, 0.75f), new Vector3(0, 2.24f, 0.75f), 0.012f, Sw.SteelDark, 8);
            c.Cyl(new Vector3(0, 2.235f, 0.75f), 0.045f, 0.03f, Sw.Graphite, default, 16, 0.004f);
            c.Lathe(new[] { new Vector2(0, 2.22f), new Vector2(0.03f, 2.22f), new Vector2(0.03f, 2.19f), new Vector2(0.02f, 2.165f), new Vector2(0, 2.16f) },
                    Matrix4x4.identity, Sw.RadioRedDark, 14);
            Mesh(root, "Ceiling", c, false);

            // Decoración industrial
            var d = new MeshKit();
            void Crate(Vector3 p, float s, float yaw)
            {
                d.Box(p + new Vector3(0, s / 2, 0), Vector3.one * s, 0.015f, Sw.Wood, new Vector3(0, yaw, 0));
                d.Box(p + new Vector3(0, s / 2, 0), new Vector3(s * 1.01f, s * 0.12f, s * 1.01f), 0.004f, Sw.OrangeDark, new Vector3(0, yaw, 0));
            }
            void Pallet(Vector3 p) => d.Box(p + new Vector3(0, 0.06f, 0), new Vector3(1.0f, 0.12f, 0.8f), 0.01f, Sw.Wood);
            Pallet(new Vector3(-2.0f, 0, 2.05f)); Crate(new Vector3(-2.2f, 0.12f, 2.05f), 0.45f, 5); Crate(new Vector3(-1.75f, 0.12f, 2.0f), 0.4f, -8); Crate(new Vector3(-2.0f, 0.57f, 2.05f), 0.38f, 12);
            Pallet(new Vector3(2.05f, 0, -1.2f)); Crate(new Vector3(1.85f, 0.12f, -1.2f), 0.42f, -4); Crate(new Vector3(2.25f, 0.12f, -1.15f), 0.36f, 9);
            foreach (var (p, sw) in new[] { (new Vector3(2.3f, 0, 2.2f), Sw.RobotBlue), (new Vector3(2.0f, 0, 2.3f), Sw.Orange), (new Vector3(-2.35f, 0, -1.3f), Sw.ClockGreenDark) })
            {
                d.Cyl(p + new Vector3(0, 0.42f, 0), 0.2f, 0.84f, sw, default, 16, 0.02f);
                d.Ring(p + new Vector3(0, 0.28f, 0), 0.2f, 0.012f, Sw.Graphite, default, 16, 4);
                d.Ring(p + new Vector3(0, 0.56f, 0), 0.2f, 0.012f, Sw.Graphite, default, 16, 4);
            }
            // Armario eléctrico en la pared derecha y extintor junto a la consola
            d.Box(new Vector3(X - 0.16f, 1.2f, 1.6f), new Vector3(0.2f, 1.2f, 0.7f), 0.02f, Sw.CreamDark);
            d.Stripes(new Vector3(X - 0.259f, 0.75f, 1.6f), FaceNegX, 0.66f, 0.08f, 12, Sw.HazardYellow, Sw.Black);
            for (int i = 0; i < 3; i++) d.Cyl(new Vector3(X - 0.26f, 1.6f, 1.4f + i * 0.08f), 0.015f, 0.01f, i == 2 ? Sw.LedRed : Sw.LedGreen, new Vector3(0, 0, 90), 10);
            d.Cyl(new Vector3(1.25f, 0.3f, 0.35f), 0.07f, 0.5f, Sw.PaintRed, default, 14, 0.02f);
            d.Cyl(new Vector3(1.25f, 0.58f, 0.35f), 0.025f, 0.06f, Sw.Black, default, 10);
            d.Box(new Vector3(1.25f, 0.02f, 0.35f), new Vector3(0.22f, 0.04f, 0.22f), 0.006f, Sw.Graphite);
            // Rejillas de ventilación en la pared del fondo
            foreach (float x in new[] { -1.8f, 1.8f }) d.Vents(new Vector3(x, 1.9f, Z1 - 0.032f), Vector3.zero, 0.6f, 0.4f, 6, Sw.Graphite);
            Mesh(root, "Decor", d);

            // Señalización
            var signs = Empty(root, "Signage", Vector3.zero);
            var s1 = Sign(signs, "Sign_QC", "CONTROL DE CALIDAD", new Vector3(0, 2.25f, Z1 - 0.04f), Vector3.zero, 2.4f, new Color(0.15f, 0.15f, 0.17f));
            s1.rectTransform.sizeDelta = new Vector2(4f, 0.6f);
            Sign(signs, "Sign_Line", "LÍNEA 01 · INSPECCIÓN FINAL", new Vector3(0, 1.95f, Z1 - 0.04f), Vector3.zero, 0.9f, new Color(0.75f, 0.35f, 0.08f));
            Sign(signs, "Sign_Safety", "USA GUANTES · MANTÉN LA ZONA LIMPIA", new Vector3(X - 0.04f, 2.1f, 0.2f), FaceNegX, 0.8f, new Color(0.15f, 0.15f, 0.17f));
            Sign(signs, "Sign_Exit", "SALIDA", new Vector3(-X + 0.04f, 2.3f, -1.0f), FaceX, 1.0f, new Color(0.1f, 0.6f, 0.25f));
            Save(root);
        }
    }
}
