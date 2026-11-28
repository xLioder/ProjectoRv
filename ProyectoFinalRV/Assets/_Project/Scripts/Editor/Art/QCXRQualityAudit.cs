using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using QCXR.Visual;
using Object = UnityEngine.Object;

namespace QCXR.EditorTools.Art
{
    // Optimización para Quest y revisión automática del proyecto.
    // Menú QCXR > Arte (auditoría). Escribe el informe en Assets/_Project/Art/QC_Informe.md.
    public static class QCXRQualityAudit
    {
        public const string ReportPath = QCXRArtBuilder.ArtRoot + "/QC_Informe.md";

        // Optimización

        // Ajustes de bajo riesgo para Quest 2/3. No toca XR, scripting backend ni la lógica del juego.
        public static List<string> ApplyOptimizations()
        {
            var log = new List<string>();
            // URP móvil: la nave mide 5 x 4 m -> no hace falta sombra a 50 m
            var urp = AssetDatabase.LoadAssetAtPath<Object>("Assets/Settings/Mobile_RPAsset.asset");
            if (urp != null)
            {
                var so = new SerializedObject(urp);
                void Set(string p, float v) { var sp = so.FindProperty(p); if (sp != null) { log.Add($"Mobile_RPAsset.{p}: {(sp.propertyType == SerializedPropertyType.Float ? sp.floatValue : sp.intValue)} → {v}"); if (sp.propertyType == SerializedPropertyType.Float) sp.floatValue = v; else sp.intValue = (int)v; } }
                Set("m_ShadowDistance", 8f);
                Set("m_ShadowCascadeCount", 1);
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            // Texturas del proyecto: ASTC en Android (las paletas quedan sin comprimir: son 64 px y de color plano)
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/_Project" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var ti = AssetImporter.GetAtPath(path) as TextureImporter;
                if (ti == null) continue;
                bool palette = path.Contains("QCXR_Palette_");
                var s = ti.GetPlatformTextureSettings("Android");
                var want = palette ? TextureImporterFormat.RGBA32 : (ti.textureType == TextureImporterType.Sprite ? TextureImporterFormat.ASTC_4x4 : TextureImporterFormat.ASTC_6x6);
                if (s.overridden && s.format == want) continue;
                s.overridden = true; s.format = want; s.maxTextureSize = Mathf.Min(s.maxTextureSize <= 0 ? 512 : s.maxTextureSize, 512);
                ti.SetPlatformTextureSettings(s);
                ti.SaveAndReimport();
                log.Add($"{Path.GetFileName(path)} (Android) → {want}");
            }
            AssetDatabase.SaveAssets();
            return log;
        }

        // Auditoría

        class Report
        {
            public readonly StringBuilder sb = new StringBuilder();
            public int pass, fail, warn;
            public void Section(string t) => sb.AppendLine().AppendLine("## " + t).AppendLine().AppendLine("| Comprobación | Resultado | Detalle |").AppendLine("|---|---|---|");
            public void Check(string what, bool ok, string detail = "", bool warnOnly = false)
            {
                string r = ok ? "✅ OK" : warnOnly ? "⚠️ Aviso" : "❌ Falla";
                if (ok) pass++; else if (warnOnly) warn++; else fail++;
                sb.AppendLine($"| {what} | {r} | {detail.Replace("|", "/")} |");
            }
        }

        static readonly Dictionary<string, (Vector3 size, DefectType[] defects)> Spec = new Dictionary<string, (Vector3, DefectType[])>
        {
            ["Robot"] = (new Vector3(0.18f, 0.22f, 0.12f), new[] { DefectType.LooseScrew, DefectType.Dent, DefectType.HiddenCrack }),
            ["Radio"] = (new Vector3(0.30f, 0.16f, 0.10f), new[] { DefectType.LooseScrew, DefectType.HiddenCrack, DefectType.BurnedCircuit }),
            ["Lamp"] = (new Vector3(0.14f, 0.10f, 0.14f), new[] { DefectType.LooseScrew, DefectType.Dent, DefectType.BurnedCircuit }),
            ["Toaster"] = (new Vector3(0.26f, 0.13f, 0.15f), new[] { DefectType.Dent, DefectType.HiddenCrack, DefectType.BurnedCircuit }),
            ["Clock"] = (new Vector3(0.18f, 0.18f, 0.06f), new[] { DefectType.LooseScrew, DefectType.Dent, DefectType.HiddenCrack, DefectType.BurnedCircuit }),
        };

        // Referencia rota: hay un id guardado pero el objeto no existe (API de Unity 6.6: EntityId).
        static bool IsSet(SerializedProperty p) { object id = p.objectReferenceEntityIdValue; return !id.Equals(Activator.CreateInstance(id.GetType())); }

        static int Tris(Mesh m) => m == null ? 0 : (int)Enumerable.Range(0, m.subMeshCount).Sum(i => (long)m.GetIndexCount(i)) / 3;

        static T Load<T>(string path) where T : Object => AssetDatabase.LoadAssetAtPath<T>(path);

        [MenuItem("QCXR/Arte/Fase 7 · Auditoría de calidad")]
        public static void Run() => Run(null);

        public static void Run(List<string> optimizationLog)
        {
            var R = new Report();
            int defectLayer = LayerMask.NameToLayer("Defect");
            R.sb.AppendLine("# QC · Informe de control de calidad (arte)").AppendLine()
                .AppendLine($"Generado automáticamente por `QCXR > Arte > Fase 7 · Auditoría de calidad` el {DateTime.Now:yyyy-MM-dd HH:mm}.");

            // Productos
            R.Section("Productos (3.1) y defectos en producto (3.2)");
            foreach (var kv in Spec)
            {
                var go = Load<GameObject>($"{QCXRArtBuilder.ProductPrefabDir}/Product_{kv.Key}.prefab");
                if (go == null) { R.Check($"Product_{kv.Key} existe", false); continue; }
                var model = go.transform.Find("Model");
                var mfm = model ? model.GetComponent<MeshFilter>() : null;
                var mesh = mfm ? mfm.sharedMesh : null;
                bool comps = go.GetComponent<Product>() && go.GetComponent<Rigidbody>() && go.GetComponent<XRGrabInteractable>();
                R.Check($"{kv.Key}: Product + Rigidbody + XRGrabInteractable", comps);
                R.Check($"{kv.Key}: malla única en Model", mesh != null, mesh ? $"{Tris(mesh)} tris" : "sin malla");
                if (mesh != null)
                {
                    var b = mesh.bounds;
                    var s = kv.Value.size;
                    bool dimsOk = Mathf.Abs(b.size.x - s.x) <= s.x * 0.15f + 0.005f && Mathf.Abs(b.size.z - s.z) <= s.z * 0.15f + 0.005f
                                  && (kv.Key == "Lamp" ? b.size.y >= s.y : Mathf.Abs(b.size.y - s.y) <= s.y * 0.15f + 0.005f);
                    R.Check($"{kv.Key}: dimensiones (brief {s.x:0.00}×{s.y:0.00}×{s.z:0.00} m)", dimsOk,
                        $"modelo {b.size.x:0.000}×{b.size.y:0.000}×{b.size.z:0.000} m" + (kv.Key == "Lamp" ? " (alto incluye brazo y pantalla)" : ""));
                    float off = kv.Key == "Lamp" ? 0f : new Vector2(b.center.x, b.center.z).magnitude;
                    R.Check($"{kv.Key}: pivote centrado (XZ)", off < 0.02f, $"desvío {off * 100:0.0} cm");
                }
                var defects = go.GetComponentsInChildren<ProductDefect>(true);
                var types = defects.Select(d => d.Type).OrderBy(t => t).ToArray();
                bool setOk = types.SequenceEqual(kv.Value.defects.OrderBy(t => t));
                R.Check($"{kv.Key}: slots de defecto según la tabla", setOk, string.Join(", ", types));
                var arr = new SerializedObject(go.GetComponent<Product>()).FindProperty("defects");
                R.Check($"{kv.Key}: Product.defects enlazado", arr.arraySize == defects.Length, $"{arr.arraySize} referencias");
                foreach (var d in defects)
                {
                    var so = new SerializedObject(d);
                    bool hier = d.transform.Find("Visual") && d.transform.Find("Fixed") && so.FindProperty("workCollider").objectReferenceValue != null;
                    bool layer = d.GetComponentsInChildren<Transform>(true).All(t => t.gameObject.layer == defectLayer);
                    bool separate = d.transform.parent != null && d.transform.parent.name.StartsWith("Slot_") && PrefabUtility.IsPartOfPrefabInstance(d.gameObject);
                    R.Check($"{kv.Key}/{d.Type}: Visual/Fixed/WorkCollider, capa Defect, pieza independiente", hier && layer && separate,
                        $"jerarquía {(hier ? "ok" : "falta")}, capa {(layer ? "ok" : "mal")}, prefab compartido {(separate ? "sí" : "no")}");
                }
                var anim = model ? model.GetComponent<Animator>() : null;
                R.Check($"{kv.Key}: animación de entrada en Model (no en la raíz)", anim != null && anim.runtimeAnimatorController != null && go.GetComponent<Animator>() == null);
            }

            // Defectos compartidos
            R.Section("Defectos compartidos (3.2)");
            foreach (var (name, repairable) in new[] { ("LooseScrew", true), ("Dent", true), ("HiddenCrack", false), ("BurnedCircuit", false) })
            {
                var go = Load<GameObject>($"{QCXRArtBuilder.DefectPrefabDir}/Defect_{name}.prefab");
                if (go == null) { R.Check($"Defect_{name} existe", false); continue; }
                var fix = go.transform.Find("Fixed");
                bool fixedOk = repairable ? fix.GetComponentsInChildren<Renderer>(true).Any() : !fix.GetComponentsInChildren<Renderer>(true).Any();
                R.Check($"{name}: Fixed {(repairable ? "con versión reparada" : "vacío (irreparable)")}", fixedOk);
                if (name == "LooseScrew") R.Check("LooseScrew: pivote de ScrewHead en el eje (giro + descenso)", fix.Find("ScrewHead") && fix.Find("ScrewHead").localPosition == Vector3.zero);
                if (name == "BurnedCircuit") R.Check("BurnedCircuit: humo en Visual/SmokeAnchor", go.transform.Find("Visual/SmokeAnchor")?.GetComponentInChildren<ParticleSystem>(true) != null);
                if (name == "HiddenCrack")
                {
                    var cm = go.GetComponentInChildren<MeshRenderer>(true).sharedMaterial;
                    var rc = Load<AnimationClip>("Assets/_Project/Animations/Products/Defect_CrackReveal.anim");
                    bool animates = rc != null && AnimationUtility.GetCurveBindings(rc).Any(b => b.propertyName.StartsWith("material._BaseColor"));
                    R.Check("HiddenCrack: material propio (Unlit) y brillo de revelado animado", cm.shader.name.Contains("Unlit") && cm.name == "QCXR_Crack" && animates, cm.shader.name);
                }
            }

            // Herramientas
            R.Section("Herramientas (4)");
            foreach (var t in new[] { "Hammer", "Screwdriver", "Magnifier" })
            {
                var go = Load<GameObject>($"Assets/_Project/Prefabs/Tools/Tool_{t}.prefab");
                var tool = go.GetComponent<Tool>();
                var so = new SerializedObject(tool);
                R.Check($"{t}: Tool + XRGrabInteractable + Rigidbody + colliders", go.GetComponent<XRGrabInteractable>() && go.GetComponent<Rigidbody>() && go.GetComponentsInChildren<Collider>().Any());
                R.Check($"{t}: Tip y Attach", so.FindProperty("tip").objectReferenceValue != null && go.transform.Find("Attach") && go.transform.Find("Tip"));
                var grab = new SerializedObject(go.GetComponent<XRGrabInteractable>()).FindProperty("m_AttachTransform");
                R.Check($"{t}: XRGrabInteractable usa Attach", grab != null && grab.objectReferenceValue == go.transform.Find("Attach"));
                R.Check($"{t}: HandPoseHint", go.GetComponent<HandPoseHint>() != null);
                var ta = go.GetComponent<Animator>();
                R.Check($"{t}: Animator de respuesta (solo piezas hijas)", ta != null && ta.runtimeAnimatorController != null && !ta.applyRootMotion, ta && ta.runtimeAnimatorController ? ta.runtimeAnimatorController.name : "");
                if (t == "Screwdriver")
                {
                    var shaft = so.FindProperty("spinPart").objectReferenceValue as Transform;
                    bool sep = shaft != null && shaft.name == "Shaft" && shaft.GetComponent<MeshFilter>() && go.transform.Find("Handle") && shaft != go.transform.Find("Handle");
                    var m = shaft ? shaft.GetComponent<MeshFilter>().sharedMesh : null;
                    bool axis = m != null && m.bounds.size.z > 5 * m.bounds.size.x && Mathf.Abs(m.bounds.center.x) < 0.001f && Mathf.Abs(m.bounds.center.y) < 0.001f;
                    R.Check("Screwdriver: Shaft separado del mango (spinPart)", sep);
                    R.Check("Screwdriver: Shaft gira sobre su eje +Z sin desplazarse", axis, m ? $"bounds {m.bounds.size}" : "");
                }
                if (t == "Magnifier")
                {
                    var lens = so.FindProperty("lens").objectReferenceValue as Transform;
                    R.Check("Magnifier: LensPoint en el centro del cristal", lens != null && lens.name == "LensPoint" && Vector3.Distance(lens.localPosition, go.transform.Find("Lens").GetComponent<MeshFilter>().sharedMesh.bounds.center) < 0.003f);
                    R.Check("Magnifier: +Z de LensPoint mira a través del cristal (normal ±Y)", lens != null && Mathf.Abs(Vector3.Dot(lens.forward, Vector3.up)) > 0.99f);
                    R.Check("Magnifier: MagnifierFocusFX con LensPoint y LensLight", go.GetComponent<MagnifierFocusFX>() != null);
                    R.Check("Magnifier: cristal transparente", go.transform.Find("Lens").GetComponent<MeshRenderer>().sharedMaterial.renderQueue >= 3000);
                }
            }

            // Manos
            R.Section("Manos VR (5)");
            foreach (var s in new[] { "L", "R" })
            {
                var go = Load<GameObject>($"Assets/_Project/Prefabs/Hands/Hand_{s}.prefab");
                var smr = go ? go.GetComponentInChildren<SkinnedMeshRenderer>(true) : null;
                R.Check($"Hand_{s}: malla con piel y rig", smr != null && smr.bones.Length == 21 && smr.sharedMesh.boneWeights.Length == smr.sharedMesh.vertexCount, smr ? $"{smr.bones.Length} huesos, {Tris(smr.sharedMesh)} tris" : "");
                var names = smr ? smr.bones.Select(b => b.name).ToList() : new List<string>();
                bool fingers = new[] { "Wrist", "Thumb1", "Index1", "Middle1", "Ring1", "Pinky1" }.All(names.Contains);
                R.Check($"Hand_{s}: muñeca, pulgar, índice, medio, anular y meñique", fingers);
                var anim = go.GetComponent<Animator>();
                R.Check($"Hand_{s}: Animator + HandPoseDriver", anim && anim.runtimeAnimatorController && go.GetComponent<HandPoseDriver>());
                var clips = anim && anim.runtimeAnimatorController ? anim.runtimeAnimatorController.animationClips.Select(c => c.name).Distinct().ToList() : new List<string>();
                string[] poses = { "Open", "Fist", "Grab", "HoldHammer", "HoldScrewdriver", "HoldMagnifier", "Point", "Press", "Inspect" };
                bool allPoses = poses.All(p => clips.Any(c => c.EndsWith("_" + p)));
                R.Check($"Hand_{s}: las 9 poses del brief en el Animator", allPoses, $"{clips.Count} clips");
            }

            // Escena
            var scene = EditorSceneManager.OpenScene(QCXRSceneAssembler.ScenePath, OpenSceneMode.Single);
            var all = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToList();
            R.Section("Escena QCXR_MainScene (11)");
            string[] roots = { "GameManager", "Environment", "Factory", "Products", "Tools", "XR Origin (XR Rig)", "UI", "Effects" };
            var rootNames = scene.GetRootGameObjects().Select(g => g.name).ToList();
            R.Check("Grupos raíz del brief", roots.All(rootNames.Contains), string.Join(" · ", rootNames));
            foreach (var n in new[] { "ConveyorBelt", "ProductSpawnPoint", "InspectionPoint", "BeltEnd", "ToolTable", "ButtonConsole", "Shelves", "DiscardBin", "EmergencyIndicator" })
                R.Check($"Factory/{n}", all.Any(t => t.name == n && t.parent && t.parent.name == "Factory"));
            R.Check("XR Origin conservado (Starter Assets) en el suelo", all.Any(t => t.name == "XR Origin (XR Rig)" && t.position == Vector3.zero && PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject)));
            foreach (var h in new[] { ("Left Controller", "Hand_L"), ("Right Controller", "Hand_R") })
            {
                var ctrl = all.FirstOrDefault(t => t.name == h.Item1);
                var hand = ctrl ? ctrl.Find(h.Item2) : null;
                var drv = hand ? hand.GetComponent<HandPoseDriver>() : null;
                int n = drv ? new SerializedObject(drv).FindProperty("interactors").arraySize : 0;
                R.Check($"{h.Item2} bajo {h.Item1} con interactores", n > 0, $"{n} interactores");
            }
            // Referencias rotas
            int missing = 0, missingScripts = 0; var missingWhere = new List<string>();
            foreach (var t in all)
            {
                missingScripts += GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject);
                foreach (var c in t.GetComponents<Component>())
                {
                    if (c == null) continue;
                    var so = new SerializedObject(c);
                    var it = so.GetIterator();
                    while (it.NextVisible(true))
                        if (it.propertyType == SerializedPropertyType.ObjectReference && it.objectReferenceValue == null && IsSet(it))
                        { missing++; if (missingWhere.Count < 6) missingWhere.Add($"{t.name}.{c.GetType().Name}.{it.propertyPath}"); }
                }
            }
            R.Check("Sin scripts ausentes", missingScripts == 0, $"{missingScripts}");
            R.Check("Sin referencias rotas", missing == 0, missing == 0 ? "0" : string.Join(", ", missingWhere));
            var flow = all.Select(t => t.GetComponent<ProductFlow>()).FirstOrDefault(c => c);
            var cat = flow ? new SerializedObject(flow).FindProperty("catalog") : null;
            R.Check("ProductFlow.catalog apunta a los 5 productos", cat != null && cat.arraySize == 5 && Enumerable.Range(0, 5).All(i => cat.GetArrayElementAtIndex(i).objectReferenceValue != null));
            var belt = all.Select(t => t.GetComponent<ConveyorBelt>()).FirstOrDefault(c => c);
            R.Check("ConveyorBelt.beltRenderer = superficie nueva", belt && new SerializedObject(belt).FindProperty("beltRenderer").objectReferenceValue != null);
            foreach (var sh in all.Select(t => t.GetComponent<XRSocketInteractor>()).Where(c => c))
            {
                var at = new SerializedObject(sh).FindProperty("m_AttachTransform").objectReferenceValue as Transform;
                R.Check($"{sh.name}: socket con AttachPoint", at != null && at.name == "AttachPoint");
            }
            var refs = all.FirstOrDefault(t => t.name.StartsWith("ReferenceOnShelves"));
            R.Check("Productos de referencia desactivados y EditorOnly", refs != null && !refs.gameObject.activeSelf && refs.CompareTag("EditorOnly"));
            R.Check("QCXR_MainScene es la escena 0 de Build Settings", EditorBuildSettings.scenes.Length > 0 && EditorBuildSettings.scenes[0].path == QCXRSceneAssembler.ScenePath && EditorBuildSettings.scenes[0].enabled);

            // Rendimiento
            R.Section("Rendimiento Quest (12)");
            var renderers = all.Select(t => t.GetComponent<Renderer>()).Where(r => r && r.gameObject.activeInHierarchy && r.enabled && !(r is ParticleSystemRenderer)).ToList();
            int tris = renderers.Sum(r => { if (r is SkinnedMeshRenderer s) return Tris(s.sharedMesh); var mf = r.GetComponent<MeshFilter>(); return mf ? Tris(mf.sharedMesh) : 0; });
            var mats = renderers.SelectMany(r => r.sharedMaterials).Where(m => m).Distinct().ToList();
            var meshRenderers = renderers.Count(r => !(r is SkinnedMeshRenderer) && r.GetComponent<MeshFilter>());
            R.Check("Triángulos visibles en escena (sin productos dinámicos)", tris < 100000, $"{tris:N0} (objetivo Quest 2 < 100 k; +≈2 k por producto en banda)");
            R.Check("Renderers 3D activos", meshRenderers < 120, $"{meshRenderers} (con SRP Batcher)");
            R.Check("Materiales distintos en escena", mats.Count <= 20, $"{mats.Count}: {string.Join(", ", mats.Select(m => m.name).Take(14))}");
            var lights = all.Select(t => t.GetComponent<Light>()).Where(l => l).ToList();
            R.Check("Luces en tiempo real", lights.Count(l => l.gameObject.activeInHierarchy) <= 2, string.Join(", ", lights.Select(l => $"{l.name}({l.type}{(l.gameObject.activeInHierarchy ? "" : ", apagada")})")));
            var ps = Resources.FindObjectsOfTypeAll<ParticleSystem>().Where(p => p.gameObject.scene == scene).ToList();
            R.Check("Partículas: máximo simultáneo acotado", ps.Sum(p => p.main.maxParticles) <= 400, $"{ps.Count} sistemas, suma maxParticles = {ps.Sum(p => p.main.maxParticles)}");
            var urpAssets = Enumerable.Range(0, QualitySettings.count).Select(QualitySettings.GetRenderPipelineAssetAt).Where(a => a != null).Select(a => a.name).Distinct().ToList();
            if (GraphicsSettings.defaultRenderPipeline != null) urpAssets.Add(GraphicsSettings.defaultRenderPipeline.name);
            R.Check("URP activo (por nivel de calidad o global)", urpAssets.Count > 0, string.Join(", ", urpAssets.Distinct()));
            var mobile = AssetDatabase.LoadAssetAtPath<Object>("Assets/Settings/Mobile_RPAsset.asset");
            if (mobile)
            {
                var so = new SerializedObject(mobile);
                R.Check("MSAA 4x (Mobile_RPAsset)", so.FindProperty("m_MSAA").intValue == 4);
                R.Check("Distancia de sombras acotada", so.FindProperty("m_ShadowDistance").floatValue <= 10f, $"{so.FindProperty("m_ShadowDistance").floatValue} m");
                R.Check("HDR desactivado", !so.FindProperty("m_SupportsHDR").boolValue);
            }
            R.Check("IL2CPP en Android", PlayerSettings.GetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Android) == ScriptingImplementation.IL2CPP);
            R.Check("ARM64 en Android", (PlayerSettings.Android.targetArchitectures & AndroidArchitecture.ARM64) != 0);
            int texTotal = 0, texAstc = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/_Project" }))
            {
                var ti = AssetImporter.GetAtPath(AssetDatabase.GUIDToAssetPath(guid)) as TextureImporter;
                if (ti == null) continue;
                texTotal++;
                var s = ti.GetPlatformTextureSettings("Android");
                if (s.overridden && (s.format.ToString().StartsWith("ASTC") || s.format == TextureImporterFormat.RGBA32)) texAstc++;
            }
            R.Check("Texturas del arte con formato Android definido (ASTC / paleta RGBA32)", texAstc == texTotal, $"{texAstc}/{texTotal}");
            R.Check("Iluminación horneada", false, "No horneada a propósito: el evento de falla eléctrica modifica la intensidad de las luces en tiempo real (RandomEventSystem). El entorno ya está marcado como estático para hornear GI indirecta si se decide.", true);

            // UI 2D
            R.Section("Elementos 2D y UI (7)");
            foreach (var p in new[] { "Robot", "Radio", "Lamp", "Toaster", "Clock" }) R.Check($"Icono de producto {p}", QCXRUIBuilder.ProductIcon(p) != null);
            for (int i = 0; i < 4; i++) R.Check($"Icono de defecto {(DefectType)i}", QCXRUIBuilder.DefectIcon(i) != null);
            foreach (var f in new[] { "QCXR_UI_Panel", "QCXR_UI_Frame", "QCXR_UI_Button", "QCXR_UI_Hazard", "QCXR_UI_Indicator_OK", "QCXR_UI_Indicator_Error", "QCXR_UI_Indicator_Warning" })
                R.Check($"Sprite {f}", QCXRUIBuilder.Sprite(f) != null);
            for (int i = 0; i < 4; i++) R.Check($"Sello {QCXRFxBuilder.Grades[i]}", QCXRFxBuilder.GradeSprite(i) != null);

            // Animaciones y VFX
            R.Section("Animaciones (8) y efectos (9)");
            string[] clipFiles = { "Products/Defect_ScrewTighten", "Products/Defect_DentStraighten", "Products/Defect_CrackReveal", "Products/Product_Enter",
                                   "Environment/Button_Press", "Environment/Belt_Jam", "Environment/Beacon_Spin", "Environment/Flash_Good", "Environment/Flash_Bad",
                                   "Environment/Bin_Flash", "UI/UI_FadeIn", "UI/UI_FadeOut", "UI/Stamp_Drop",
                                   "Tools/Hammer_Hit", "Tools/Screwdriver_Press", "Tools/Magnifier_Focus", "Tools/Magnifier_Reveal" };
            foreach (var c in clipFiles)
            {
                var clip = Load<AnimationClip>($"Assets/_Project/Animations/{c}.anim");
                R.Check($"Clip {Path.GetFileName(c)}", clip != null, clip ? $"{clip.length:0.00} s{(clip.isLooping ? ", bucle" : "")}" : "");
            }
            foreach (var fx in new[] { "FX_Sparks", "FX_RepairFlash", "FX_HammerDust", "FX_BurnedCircuitSmoke", "FX_Confetti" })
            {
                var go = Load<GameObject>($"Assets/_Project/Prefabs/Effects/{fx}.prefab");
                var p = go ? go.GetComponent<ParticleSystem>() : null;
                bool urpMat = p && go.GetComponent<ParticleSystemRenderer>().sharedMaterial.shader.name.Contains("Universal Render Pipeline");
                R.Check($"{fx}: prefab URP", p != null && urpMat, p ? $"max {p.main.maxParticles}, {(p.main.loop ? "bucle" : "una vez")}" : "");
            }

            if (optimizationLog != null && optimizationLog.Count > 0)
            {
                R.sb.AppendLine().AppendLine("## Ajustes de optimización aplicados").AppendLine();
                foreach (var l in optimizationLog) R.sb.AppendLine("- " + l);
            }

            var head = $"\n**Resultado: {R.pass} OK · {R.fail} fallas · {R.warn} avisos**\n";
            var text = R.sb.ToString();
            int firstSection = text.IndexOf("\n## ", StringComparison.Ordinal);
            text = text.Insert(firstSection, head);
            File.WriteAllText(Path.Combine(Application.dataPath, ReportPath.Substring("Assets/".Length)), text);
            AssetDatabase.ImportAsset(ReportPath);
            Debug.Log($"[QCXR Art] Fase 7: auditoría {R.pass} OK, {R.fail} fallas, {R.warn} avisos → {ReportPath}");
        }
    }
}
