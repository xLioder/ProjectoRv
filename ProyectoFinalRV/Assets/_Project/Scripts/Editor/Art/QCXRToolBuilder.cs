using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using QCXR.Visual;

namespace QCXR.EditorTools.Art
{
    /// Menú QCXR > Arte > Fase 3 · Herramientas. Remodela martillo, destornillador y lupa EDITANDO
    /// los prefabs en sitio: se conservan Tool, XRGrabInteractable, Rigidbody y los objetos de
    /// referencia (Tip, Attach, LensPoint, LensLight) con sus fileID.
    ///
    /// Convención común: el mango va a lo largo de +Z local y Attach (rotación identidad) está en
    /// el centro del puño. XRI alinea Attach con el punto de agarre del controlador, por lo que
    /// +Z de la herramienta = eje del puño de la mano (ver QCXRHandBuilder).
    public static class QCXRToolBuilder
    {
        const string Dir = QCXRArtBuilder.ArtRoot + "/Tools";
        const string PrefabDir = "Assets/_Project/Prefabs/Tools";
        static readonly Vector3 AlongZ = new Vector3(90, 0, 0);   // eje Y de los cilindros -> +Z

        [MenuItem("QCXR/Arte/Fase 3 · Herramientas")]
        public static void BuildTools()
        {
            var glass = QCXRArtBuilder.Mat(QCXRArtBuilder.MatDir + "/QCXR_Glass.mat", m =>
            {
                m.SetFloat("_Surface", 1f);
                m.SetFloat("_Blend", 0f);
                m.SetFloat("_ZWrite", 0f);
                m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                m.SetFloat("_SrcBlendAlpha", (float)UnityEngine.Rendering.BlendMode.One);
                m.SetFloat("_DstBlendAlpha", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                m.SetOverrideTag("RenderType", "Transparent");
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                m.SetShaderPassEnabled("DepthOnly", false);
                m.SetShaderPassEnabled("ShadowCaster", false);
                m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                m.SetColor("_BaseColor", new Color(0.72f, 0.9f, 1f, 0.22f));
                m.SetFloat("_Metallic", 0f);
                m.SetFloat("_Smoothness", 0.95f);
            });

            Patch("Hammer", HandPose.Hammer, 0.6f, BuildHammer);
            Patch("Screwdriver", HandPose.Screwdriver, 0.35f, BuildScrewdriver);
            Patch("Magnifier", HandPose.Magnifier, 0.3f, root => BuildMagnifier(root, glass));
            AssetDatabase.SaveAssets();
            Debug.Log("[QCXR Art] Fase 3: herramientas listas.");
        }

        static readonly HashSet<string> Keep = new HashSet<string> { "Tip", "Attach", "LensPoint" };

        static void Patch(string name, HandPose pose, float mass, System.Action<GameObject> build)
        {
            string path = $"{PrefabDir}/Tool_{name}.prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                for (int i = root.transform.childCount - 1; i >= 0; i--)
                {
                    var c = root.transform.GetChild(i);
                    if (!Keep.Contains(c.name)) Object.DestroyImmediate(c.gameObject);
                }
                build(root);
                var rb = root.GetComponent<Rigidbody>();
                rb.mass = mass;
                rb.interpolation = RigidbodyInterpolation.Interpolate;
                rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
                var hint = root.GetComponent<HandPoseHint>();
                if (hint == null) hint = root.AddComponent<HandPoseHint>();
                if (root.GetComponent<ToolRespawn>() == null) root.AddComponent<ToolRespawn>();   // reaparece en la mesa si se cae
                var so = new SerializedObject(hint);
                so.FindProperty("pose").enumValueIndex = (int)pose;
                so.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        static Transform Ref(GameObject root, string name, Vector3 pos, Vector3 euler)
        {
            var t = root.transform.Find(name);
            if (t == null) { t = new GameObject(name).transform; t.SetParent(root.transform, false); }
            t.localPosition = pos;
            t.localRotation = Quaternion.Euler(euler);
            t.localScale = Vector3.one;
            return t;
        }

        static GameObject Part(GameObject root, string name, MeshKit k, Material mat = null)
        {
            var mesh = QCXRArtBuilder.SaveMesh(k, $"{Dir}/{root.name.Replace("Tool_", "")}_{name}.asset", $"{root.name}_{name}");
            return QCXRArtBuilder.MeshObject(name, root.transform, mesh, mat != null ? mat : QCXRArtBuilder.Atlas, true);
        }

        static void SetTool(GameObject root, System.Action<SerializedObject> set)
        {
            var so = new SerializedObject(root.GetComponent<Tool>());
            set(so);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static Transform Colliders(GameObject root)
        {
            var c = new GameObject("Colliders").transform;
            c.SetParent(root.transform, false);
            return c;
        }

        static void Capsule(Transform parent, Vector3 center, float radius, float height)
        {
            var c = parent.gameObject.AddComponent<CapsuleCollider>();
            c.center = center; c.radius = radius; c.height = height; c.direction = 2;
        }

        static void BoxCol(Transform parent, Vector3 center, Vector3 size)
        {
            var b = parent.gameObject.AddComponent<BoxCollider>();
            b.center = center; b.size = size;
        }

        // ── Martillo (≈ 0.27 m). Cara de golpe hacia -Y, uña hacia +Y ──
        static void BuildHammer(GameObject root)
        {
            var h = new MeshKit();
            h.Cyl(new Vector3(0, 0, -0.066f), 0.0185f, 0.012f, Sw.Orange, AlongZ, 14, 0.004f);
            h.Cyl(new Vector3(0, 0, 0f), 0.0155f, 0.12f, Sw.Rubber, AlongZ, 12, 0.002f);
            foreach (float z in new[] { -0.03f, 0f, 0.03f }) h.Ring(new Vector3(0, 0, z), 0.0158f, 0.0016f, Sw.Graphite, AlongZ, 12, 4);
            h.Cyl(new Vector3(0, 0, 0.064f), 0.0165f, 0.01f, Sw.Orange, AlongZ, 14, 0.002f);
            h.Cyl(new Vector3(0, 0, 0.115f), 0.0105f, 0.1f, Sw.SteelDark, AlongZ, 10, 0.001f);
            h.Cyl(new Vector3(0, 0, 0.162f), 0.0135f, 0.014f, Sw.Orange, AlongZ, 12, 0.002f);
            Part(root, "Handle", h);

            const float hz = 0.19f;
            var k = new MeshKit { Base = Matrix4x4.Translate(new Vector3(0, 0, -hz)) };   // pivote en el centro de la cabeza (animación de impacto)
            k.Box(new Vector3(0, -0.002f, hz), new Vector3(0.038f, 0.07f, 0.042f), 0.006f, Sw.Steel);
            k.Box(new Vector3(0, 0.004f, hz), new Vector3(0.0425f, 0.016f, 0.0465f), 0.003f, Sw.Orange);
            foreach (int sx in new[] { -1, 1 })
                k.Rivet(new Vector3(sx * 0.0213f, 0.004f, hz), new Vector3(0, 0, -sx * 90), 0.003f);
            k.Cyl(new Vector3(0, -0.047f, hz), 0.021f, 0.024f, Sw.Steel, default, 16, 0.003f);
            k.Cyl(new Vector3(0, -0.0595f, hz), 0.0195f, 0.002f, Sw.Chrome, default, 16);
            // Uña partida, unida a la cabeza y ligeramente abierta
            k.Box(new Vector3(0, 0.036f, hz), new Vector3(0.034f, 0.008f, 0.038f), 0.003f, Sw.SteelDark);
            foreach (int sx in new[] { -1, 1 })
                k.Box(new Vector3(sx * 0.0085f, 0.052f, hz), new Vector3(0.013f, 0.03f, 0.034f), 0.003f, Sw.SteelDark, new Vector3(0, 0, -sx * 8));
            Part(root, "Head", k).transform.localPosition = new Vector3(0, 0, hz);

            Ref(root, "Tip", new Vector3(0, -0.061f, hz), Vector3.zero);
            Ref(root, "Attach", Vector3.zero, Vector3.zero);
            var col = Colliders(root);
            Capsule(col, new Vector3(0, 0, 0.045f), 0.017f, 0.24f);
            BoxCol(col, new Vector3(0, 0, hz), new Vector3(0.045f, 0.125f, 0.046f));
            SetTool(root, so => so.FindProperty("tipRadius").floatValue = 0.05f);
        }

        // ── Destornillador (≈ 0.24 m). Shaft separado, pivote en su raíz, gira sobre su +Z ──
        static void BuildScrewdriver(GameObject root)
        {
            var h = new MeshKit();
            h.Cyl(new Vector3(0, 0, -0.052f), 0.0185f, 0.014f, Sw.Orange, AlongZ, 6, 0.004f, false);
            h.Cyl(new Vector3(0, 0, -0.005f), 0.0172f, 0.08f, Sw.Rubber, AlongZ, 6, 0f, false);
            h.Cyl(new Vector3(0, 0, 0.045f), 0.0178f, 0.02f, Sw.Orange, AlongZ, 6, 0.003f, false);
            h.Stripes(new Vector3(0, 0.0153f, -0.005f), new Vector3(90, 0, 90), 0.06f, 0.012f, 6, Sw.HazardYellow, Sw.Graphite);
            h.Lathe(new[] { new Vector2(0, 0.055f), new Vector2(0.016f, 0.055f), new Vector2(0.0075f, 0.07f), new Vector2(0, 0.07f) },
                    Matrix4x4.TRS(Vector3.zero, Quaternion.Euler(AlongZ), Vector3.one), Sw.SteelDark, 12);
            Part(root, "Handle", h);

            // Eje: malla en espacio local del objeto Shaft (raíz en z=0, punta en z=0.112)
            var s = new MeshKit();
            s.Cyl(new Vector3(0, 0, 0.05f), 0.0038f, 0.1f, Sw.Chrome, AlongZ, 8);
            s.Lathe(new[] { new Vector2(0, 0.1f), new Vector2(0.0038f, 0.1f), new Vector2(0.0012f, 0.112f), new Vector2(0, 0.112f) },
                    Matrix4x4.TRS(Vector3.zero, Quaternion.Euler(AlongZ), Vector3.one), Sw.Steel, 8);
            s.Box(new Vector3(0, 0, 0.106f), new Vector3(0.0062f, 0.0013f, 0.011f), 0f, Sw.SteelDark);
            s.Box(new Vector3(0, 0, 0.106f), new Vector3(0.0013f, 0.0062f, 0.011f), 0f, Sw.SteelDark);
            var shaft = Part(root, "Shaft", s);
            shaft.transform.localPosition = new Vector3(0, 0, 0.07f);

            Ref(root, "Tip", new Vector3(0, 0, 0.182f), Vector3.zero);
            Ref(root, "Attach", new Vector3(0, 0, -0.005f), Vector3.zero);
            var col = Colliders(root);
            Capsule(col, new Vector3(0, 0, 0.005f), 0.019f, 0.13f);
            Capsule(col, new Vector3(0, 0, 0.126f), 0.0055f, 0.115f);
            SetTool(root, so =>
            {
                so.FindProperty("tipRadius").floatValue = 0.03f;
                so.FindProperty("spinPart").objectReferenceValue = shaft.transform;
            });
        }

        // ── Lupa (≈ 0.29 m). Cristal en el plano XZ; LensPoint +Z mira a través del cristal (-Y) ──
        static void BuildMagnifier(GameObject root, Material glass)
        {
            const float lz = 0.155f;
            var h = new MeshKit();
            h.Cyl(new Vector3(0, 0, -0.066f), 0.0165f, 0.012f, Sw.Orange, AlongZ, 12, 0.004f);
            h.Cyl(new Vector3(0, 0, 0f), 0.014f, 0.12f, Sw.Graphite, AlongZ, 10, 0.003f);
            foreach (float z in new[] { -0.035f, 0f, 0.035f }) h.Ring(new Vector3(0, 0, z), 0.0145f, 0.0018f, Sw.Orange, AlongZ, 12, 4);
            h.Tube(new Vector3(0, 0, 0.06f), new Vector3(0, 0, 0.093f), 0.0065f, Sw.Steel, 10);
            h.Cyl(new Vector3(0, 0, 0.087f), 0.0095f, 0.01f, Sw.Orange, AlongZ, 12, 0.002f);
            Part(root, "Handle", h);

            var f = new MeshKit { Base = Matrix4x4.Translate(new Vector3(0, 0, -lz)) };   // pivote en el centro del cristal
            f.Ring(new Vector3(0, 0, lz), 0.062f, 0.0075f, Sw.Orange, default, 32, 8);
            f.Ring(new Vector3(0, 0.003f, lz), 0.0565f, 0.0022f, Sw.Chrome, default, 32, 6);
            f.Ring(new Vector3(0, -0.003f, lz), 0.0565f, 0.0022f, Sw.Chrome, default, 32, 6);
            foreach (float a in new[] { 60f, 180f, 300f })
            {
                Vector3 p = new Vector3(Mathf.Sin(a * Mathf.Deg2Rad), 0, -Mathf.Cos(a * Mathf.Deg2Rad)) * 0.062f + new Vector3(0, 0.0072f, lz);
                f.Rivet(p, Vector3.zero, 0.0028f);
            }
            Part(root, "Frame", f).transform.localPosition = new Vector3(0, 0, lz);

            // Aro de enfoque (cian emisivo): aparece cuando la lupa apunta a un defecto oculto (MagnifierFocusFX + Animator)
            var fr = new MeshKit { Base = Matrix4x4.Translate(new Vector3(0, 0, -lz)) };
            fr.Ring(new Vector3(0, 0.0045f, lz), 0.0655f, 0.0028f, Sw.LedCyan, default, 32, 6);
            var ring = Part(root, "FocusRing", fr);
            ring.transform.localPosition = new Vector3(0, 0, lz);
            ring.transform.localScale = Vector3.zero;
            ring.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            var l = new MeshKit();
            l.Cyl(new Vector3(0, 0, lz), 0.0575f, 0.004f, Sw.White, default, 32, 0.0015f);
            var lens = Part(root, "Lens", l, glass);
            lens.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            var lp = Ref(root, "LensPoint", new Vector3(0, 0, lz), new Vector3(90, 0, 0));   // +Z -> -Y
            var light = lp.Find("LensLight");
            if (light != null) { light.localPosition = Vector3.zero; light.localRotation = Quaternion.identity; }
            Ref(root, "Tip", new Vector3(0, 0, lz), Vector3.zero);
            Ref(root, "Attach", Vector3.zero, Vector3.zero);
            var col = Colliders(root);
            Capsule(col, new Vector3(0, 0, 0.012f), 0.016f, 0.16f);
            BoxCol(col, new Vector3(0, 0, lz), new Vector3(0.14f, 0.016f, 0.14f));
            SetTool(root, so => so.FindProperty("lens").objectReferenceValue = lp);
        }
    }
}
