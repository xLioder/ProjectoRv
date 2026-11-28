using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace QCXR.EditorTools
{
    // Menú QCXR > Construir layer, config y prefabs.
    // Crea: capa "Defect", GameConfig, 5 prefabs de producto y 3 de herramienta (con primitivas de Unity).
    // Solo para el editor, no entra en el build.
    public static class QCXRPrefabBuilder
    {
        const string Root = "Assets/_Project";
        const string ProdDir = Root + "/Prefabs/Products";
        const string ToolDir = Root + "/Prefabs/Tools";
        const string MatDir = Root + "/Materials";
        const string DataDir = Root + "/Data";

        class Spec
        {
            public ProductType type; public string name, display;
            public Vector3 size; public Color color;
            public DefectType[] defects;
            public System.Action<Transform, Vector3> decorate;
        }

        [MenuItem("QCXR/Construir layer, config y prefabs")]
        public static void BuildAll()
        {
            int layer = EnsureLayer("Defect");
            if (layer < 0) { EditorUtility.DisplayDialog("QCXR", "No hay capas libres para crear 'Defect'.", "OK"); return; }

            CreateConfig();
            foreach (var s in Specs()) BuildProduct(s, layer);
            BuildHammer(layer); BuildScrewdriver(layer); BuildMagnifier(layer);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog("QCXR",
                $"Listo.\n• Capa 'Defect' = {layer}\n• GameConfig en {DataDir}\n• 5 productos en {ProdDir}\n• 3 herramientas en {ToolDir}", "OK");
        }

        // Definición de los 5 productos
        static IEnumerable<Spec> Specs()
        {
            var screw = DefectType.LooseScrew; var dent = DefectType.Dent;
            var crack = DefectType.HiddenCrack; var burn = DefectType.BurnedCircuit;

            // Robot = producto de práctica del tutorial (3 slots: tornillo, abolladura, grieta)
            yield return new Spec
            {
                type = ProductType.Robot, name = "Robot", display = "Robot",
                size = new Vector3(0.18f, 0.22f, 0.12f), color = new Color(0.25f, 0.5f, 0.9f),
                defects = new[] { screw, dent, crack },
                decorate = (r, s) =>
                {
                    Prim(PrimitiveType.Sphere, "Head", r, new Vector3(0, s.y / 2 + 0.04f, 0), Vector3.one * 0.09f, new Color(0.8f, 0.85f, 0.95f));
                    Prim(PrimitiveType.Cylinder, "Antenna", r, new Vector3(0, s.y / 2 + 0.11f, 0), new Vector3(0.008f, 0.03f, 0.008f), Color.red);
                }
            };
            yield return new Spec
            {
                type = ProductType.Radio, name = "Radio", display = "Radio",
                size = new Vector3(0.30f, 0.16f, 0.10f), color = new Color(0.95f, 0.55f, 0.15f),
                defects = new[] { screw, crack, burn },
                decorate = (r, s) =>
                {
                    foreach (float x in new[] { -0.05f, 0.05f })
                        Prim(PrimitiveType.Cylinder, "Knob", r, new Vector3(x, 0, -s.z / 2 - 0.004f), new Vector3(0.04f, 0.008f, 0.04f), Color.black, false, new Vector3(90, 0, 0));
                    Prim(PrimitiveType.Cylinder, "Antenna", r, new Vector3(-0.12f, s.y / 2 + 0.09f, 0.03f), new Vector3(0.006f, 0.09f, 0.006f), Color.gray, false, new Vector3(0, 0, -15));
                }
            };
            yield return new Spec
            {
                type = ProductType.Lamp, name = "Lamp", display = "Lámpara",
                size = new Vector3(0.14f, 0.10f, 0.14f), color = new Color(0.3f, 0.3f, 0.35f),
                defects = new[] { screw, dent, burn },
                decorate = (r, s) =>
                {
                    Prim(PrimitiveType.Cylinder, "Stem", r, new Vector3(0, s.y / 2 + 0.05f, 0), new Vector3(0.015f, 0.05f, 0.015f), Color.gray);
                    Prim(PrimitiveType.Sphere, "Shade", r, new Vector3(0, s.y / 2 + 0.13f, 0), Vector3.one * 0.12f, new Color(1f, 0.92f, 0.45f));
                }
            };
            yield return new Spec
            {
                type = ProductType.Toaster, name = "Toaster", display = "Tostadora",
                size = new Vector3(0.26f, 0.13f, 0.15f), color = new Color(0.78f, 0.8f, 0.84f),
                defects = new[] { dent, crack, burn },
                decorate = (r, s) =>
                {
                    Prim(PrimitiveType.Cube, "Lever", r, new Vector3(s.x / 2 + 0.01f, 0, 0), new Vector3(0.02f, 0.05f, 0.03f), Color.black);
                    Prim(PrimitiveType.Cube, "Base", r, new Vector3(0, -s.y / 2 - 0.01f, 0), new Vector3(s.x + 0.02f, 0.02f, s.z + 0.02f), new Color(0.2f, 0.2f, 0.2f));
                }
            };
            yield return new Spec
            {
                type = ProductType.Clock, name = "Clock", display = "Reloj",
                size = new Vector3(0.18f, 0.18f, 0.06f), color = new Color(0.3f, 0.75f, 0.4f),
                defects = new[] { screw, dent, crack, burn },
                decorate = (r, s) =>
                {
                    Prim(PrimitiveType.Cylinder, "Face", r, new Vector3(0, 0, -s.z / 2 - 0.003f), new Vector3(0.15f, 0.003f, 0.15f), Color.white, false, new Vector3(90, 0, 0));
                    Prim(PrimitiveType.Cube, "HandH", r, new Vector3(0, 0.02f, -s.z / 2 - 0.008f), new Vector3(0.008f, 0.05f, 0.004f), Color.black);
                    Prim(PrimitiveType.Cube, "HandM", r, new Vector3(0.025f, 0, -s.z / 2 - 0.008f), new Vector3(0.06f, 0.006f, 0.004f), Color.black);
                }
            };
        }

        // Producto
        static void BuildProduct(Spec s, int layer)
        {
            var root = new GameObject("Product_" + s.name);
            var rb = root.AddComponent<Rigidbody>();
            rb.mass = 0.5f; rb.interpolation = RigidbodyInterpolation.Interpolate;

            Prim(PrimitiveType.Cube, "Body", root.transform, Vector3.zero, s.size, s.color, true);
            s.decorate?.Invoke(root.transform, s.size);

            var defects = new List<ProductDefect>();
            foreach (var t in s.defects) defects.Add(AddDefect(root.transform, s.size, t, layer));

            root.AddComponent<XRGrabInteractable>();
            var product = root.AddComponent<Product>();

            var so = new SerializedObject(product);
            so.FindProperty("type").enumValueIndex = (int)s.type;
            so.FindProperty("displayName").stringValue = s.display;
            so.FindProperty("spawnWeight").floatValue = 1f;
            var arr = so.FindProperty("defects");
            arr.arraySize = defects.Count;
            for (int i = 0; i < defects.Count; i++) arr.GetArrayElementAtIndex(i).objectReferenceValue = defects[i];
            so.ApplyModifiedPropertiesWithoutUndo();

            Save(root, $"{ProdDir}/Product_{s.name}.prefab");
        }

        static ProductDefect AddDefect(Transform root, Vector3 size, DefectType t, int layer)
        {
            float top = size.y / 2;
            Vector3 pos; ToolType tool = ToolType.None; float work = 1f; bool repairable = false, hidden = false;
            GameObject visual = null, fixedVis = null;

            var go = new GameObject("Defect_" + t);
            go.transform.SetParent(root, false);

            switch (t)
            {
                case DefectType.LooseScrew:
                    pos = new Vector3(-0.30f * size.x, top, 0.20f * size.z);
                    tool = ToolType.Screwdriver; work = 2f; repairable = true;
                    go.transform.localPosition = pos;
                    visual = Prim(PrimitiveType.Cylinder, "Visual", go.transform, new Vector3(0, 0.012f, 0), new Vector3(0.035f, 0.007f, 0.035f), new Color(0.9f, 0.15f, 0.15f), false, new Vector3(14, 0, 14));
                    fixedVis = Prim(PrimitiveType.Cylinder, "Fixed", go.transform, new Vector3(0, 0.004f, 0), new Vector3(0.035f, 0.003f, 0.035f), new Color(0.2f, 0.8f, 0.3f));
                    break;
                case DefectType.Dent:
                    pos = new Vector3(0.30f * size.x, top, -0.20f * size.z);
                    tool = ToolType.Hammer; work = 3f; repairable = true;
                    go.transform.localPosition = pos;
                    visual = Prim(PrimitiveType.Sphere, "Visual", go.transform, new Vector3(0, 0.002f, 0), new Vector3(0.07f, 0.022f, 0.07f), new Color(0.15f, 0.15f, 0.18f));
                    fixedVis = Prim(PrimitiveType.Sphere, "Fixed", go.transform, new Vector3(0, 0.002f, 0), new Vector3(0.07f, 0.006f, 0.07f), new Color(0.2f, 0.8f, 0.3f));
                    break;
                case DefectType.HiddenCrack:
                    pos = new Vector3(0f, top, -0.30f * size.z);
                    hidden = true;
                    go.transform.localPosition = pos;
                    visual = Prim(PrimitiveType.Cube, "Visual", go.transform, new Vector3(0, 0.003f, 0), new Vector3(0.09f, 0.006f, 0.012f), new Color(0.35f, 0f, 0f), false, new Vector3(0, 30, 0));
                    break;
                default: // BurnedCircuit
                    pos = new Vector3(0.30f * size.x, top, 0.25f * size.z);
                    go.transform.localPosition = pos;
                    visual = Prim(PrimitiveType.Sphere, "Visual", go.transform, new Vector3(0, 0.008f, 0), new Vector3(0.06f, 0.025f, 0.06f), Color.black);
                    break;
            }

            var col = go.AddComponent<SphereCollider>();
            col.isTrigger = true; col.radius = 0.045f;

            var d = go.AddComponent<ProductDefect>();
            SetLayerRecursive(go, layer);

            var so = new SerializedObject(d);
            so.FindProperty("type").enumValueIndex = (int)t;
            so.FindProperty("requiredTool").enumValueIndex = (int)tool;
            so.FindProperty("workRequired").floatValue = work;
            so.FindProperty("repairable").boolValue = repairable;
            so.FindProperty("hiddenUntilInspected").boolValue = hidden;
            so.FindProperty("defectVisual").objectReferenceValue = visual;
            so.FindProperty("fixedVisual").objectReferenceValue = fixedVis;
            so.FindProperty("workCollider").objectReferenceValue = col;
            so.ApplyModifiedPropertiesWithoutUndo();
            return d;
        }

        // Herramientas
        static GameObject NewTool(string name, ToolType type, int layer, out SerializedObject so, out Transform tip)
        {
            var root = new GameObject("Tool_" + name);
            var rb = root.AddComponent<Rigidbody>();
            rb.mass = 0.6f; rb.interpolation = RigidbodyInterpolation.Interpolate;
            tip = new GameObject("Tip").transform;
            tip.SetParent(root.transform, false);
            var attach = new GameObject("Attach").transform;
            attach.SetParent(root.transform, false);
            attach.name = "Attach";

            var grab = root.AddComponent<XRGrabInteractable>();
            var tool = root.AddComponent<Tool>();

            var gso = new SerializedObject(grab);
            var ap = gso.FindProperty("m_AttachTransform");
            if (ap != null) ap.objectReferenceValue = attach;
            gso.ApplyModifiedPropertiesWithoutUndo();

            so = new SerializedObject(tool);
            so.FindProperty("toolType").enumValueIndex = (int)type;
            so.FindProperty("tip").objectReferenceValue = tip;
            so.FindProperty("defectMask").intValue = 1 << layer;
            return root;
        }

        static void BuildHammer(int layer)
        {
            var root = NewTool("Hammer", ToolType.Hammer, layer, out var so, out var tip);
            Prim(PrimitiveType.Cylinder, "Handle", root.transform, Vector3.zero, new Vector3(0.03f, 0.12f, 0.03f), new Color(0.55f, 0.35f, 0.15f), true);
            Prim(PrimitiveType.Cube, "Head", root.transform, new Vector3(0, 0.13f, 0), new Vector3(0.14f, 0.06f, 0.06f), new Color(0.5f, 0.5f, 0.55f), true);
            tip.localPosition = new Vector3(0.07f, 0.13f, 0);
            root.transform.Find("Attach").localPosition = new Vector3(0, -0.04f, 0);
            root.transform.Find("Attach").localEulerAngles = new Vector3(-90, 0, 0);
            so.FindProperty("tipRadius").floatValue = 0.05f;
            so.ApplyModifiedPropertiesWithoutUndo();
            Save(root, $"{ToolDir}/Tool_Hammer.prefab");
        }

        static void BuildScrewdriver(int layer)
        {
            var root = NewTool("Screwdriver", ToolType.Screwdriver, layer, out var so, out var tip);
            Prim(PrimitiveType.Cylinder, "Handle", root.transform, Vector3.zero, new Vector3(0.035f, 0.06f, 0.035f), new Color(0.9f, 0.7f, 0.1f), true);
            var shaft = Prim(PrimitiveType.Cylinder, "Shaft", root.transform, new Vector3(0, 0.12f, 0), new Vector3(0.008f, 0.06f, 0.008f), new Color(0.75f, 0.75f, 0.8f));
            tip.localPosition = new Vector3(0, 0.185f, 0);
            root.transform.Find("Attach").localEulerAngles = new Vector3(-90, 0, 0);
            so.FindProperty("tipRadius").floatValue = 0.03f;
            so.FindProperty("spinPart").objectReferenceValue = shaft.transform;
            so.ApplyModifiedPropertiesWithoutUndo();
            Save(root, $"{ToolDir}/Tool_Screwdriver.prefab");
        }

        static void BuildMagnifier(int layer)
        {
            var root = NewTool("Magnifier", ToolType.Magnifier, layer, out var so, out var tip);
            Prim(PrimitiveType.Cylinder, "Handle", root.transform, new Vector3(0, -0.07f, 0), new Vector3(0.025f, 0.07f, 0.025f), new Color(0.2f, 0.2f, 0.22f), true);
            Prim(PrimitiveType.Cylinder, "Rim", root.transform, new Vector3(0, 0.08f, 0.002f), new Vector3(0.16f, 0.003f, 0.16f), new Color(0.15f, 0.15f, 0.15f), false, new Vector3(90, 0, 0));
            Prim(PrimitiveType.Cylinder, "Glass", root.transform, new Vector3(0, 0.08f, 0), new Vector3(0.14f, 0.004f, 0.14f), new Color(0.6f, 0.85f, 1f, 0.3f), false, new Vector3(90, 0, 0), true);

            var lens = new GameObject("LensPoint").transform;      // su forward (+Z) = hacia donde se mira
            lens.SetParent(root.transform, false);
            lens.localPosition = new Vector3(0, 0.08f, 0);

            var lightGo = new GameObject("LensLight");
            lightGo.transform.SetParent(lens, false);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Spot; light.range = 1.2f; light.spotAngle = 45f; light.intensity = 2f; light.enabled = false;

            tip.localPosition = lens.localPosition;
            root.transform.Find("Attach").localPosition = new Vector3(0, -0.07f, 0);
            so.FindProperty("lens").objectReferenceValue = lens;
            so.FindProperty("lensLight").objectReferenceValue = light;
            so.ApplyModifiedPropertiesWithoutUndo();
            Save(root, $"{ToolDir}/Tool_Magnifier.prefab");
        }

        // Utilidades
        static GameObject Prim(PrimitiveType type, string name, Transform parent, Vector3 pos, Vector3 scale, Color color,
                               bool keepCollider = false, Vector3 euler = default, bool transparent = false)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localEulerAngles = euler;
            go.transform.localScale = scale;
            var col = go.GetComponent<Collider>();
            if (col != null && !keepCollider) Object.DestroyImmediate(col);
            go.GetComponent<MeshRenderer>().sharedMaterial = Mat(color, transparent);
            return go;
        }

        internal static Material Mat(Color c, bool transparent)
        {
            string path = $"{MatDir}/QCXR_{ColorUtility.ToHtmlStringRGBA(c)}{(transparent ? "_t" : "")}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m != null) return m;

            EnsureFolder(MatDir);
            Shader shader = transparent ? Shader.Find("Sprites/Default") : Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            m = new Material(shader) { color = c };
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        static void SetLayerRecursive(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform t in go.transform) SetLayerRecursive(t.gameObject, layer);
        }

        static void Save(GameObject root, string path)
        {
            EnsureFolder(System.IO.Path.GetDirectoryName(path).Replace("\\", "/"));
            PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
        }

        internal static void EnsureFolder(string path)
        {
            var parts = path.Split('/');
            string cur = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = cur + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(cur, parts[i]);
                cur = next;
            }
        }

        static int EnsureLayer(string name)
        {
            int existing = LayerMask.NameToLayer(name);
            if (existing >= 0) return existing;

            var tagManager = new SerializedObject(AssetDatabase.LoadMainAssetAtPath("ProjectSettings/TagManager.asset"));
            var layers = tagManager.FindProperty("layers");
            for (int i = 8; i < 32; i++)
            {
                var sp = layers.GetArrayElementAtIndex(i);
                if (string.IsNullOrEmpty(sp.stringValue))
                {
                    sp.stringValue = name;
                    tagManager.ApplyModifiedProperties();
                    return i;
                }
            }
            return -1;
        }

        static void CreateConfig()
        {
            string path = DataDir + "/GameConfig.asset";
            if (AssetDatabase.LoadAssetAtPath<GameConfigSO>(path) != null) return;
            EnsureFolder(DataDir);
            AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<GameConfigSO>(), path);
        }
    }
}
