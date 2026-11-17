using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using QCXR.Visual;
using Object = UnityEngine.Object;

namespace QCXR.EditorTools.Art
{
    /// Fase 5 · Animaciones y VFX. Lo llama QCXRSceneAssembler (menú QCXR > Arte > Fase 4/5 · Montar QCXR_MainScene).
    ///  1) Texturas/materiales de partículas y 5 prefabs de efecto (Prefabs/Effects).
    ///  2) Clips y AnimatorControllers (Animations/Products|Environment|UI).
    ///  3) Parches a prefabs existentes (productos, defectos, estación) — idempotentes.
    ///  4) Ganchos de escena: efectos, PresentationBridge, botones, paneles UI y sello de nota.
    /// No duplica animaciones del código: encogimiento al desaparecer, giro del eje del destornillador,
    /// parpadeo de luces y mensajes de pantalla siguen siendo de los scripts.
    public static class QCXRFxBuilder
    {
        const string VfxDir = "Assets/_Project/VFX";
        const string FxPrefabDir = "Assets/_Project/Prefabs/Effects";
        const string AnimRoot = "Assets/_Project/Animations";
        const string UiArtDir = QCXRArtBuilder.ArtRoot + "/UI";

        static Material additive, alpha, confettiMat;

        public static void BuildAll()
        {
            foreach (var d in new[] { VfxDir, FxPrefabDir, AnimRoot + "/Products", AnimRoot + "/Environment", AnimRoot + "/UI", UiArtDir })
                QCXRPrefabBuilder.EnsureFolder(d);
            BuildFxMaterials();
            BuildEffects();
            BuildGradeSprites();
            BuildAnimations();
            PatchPrefabs();
            AssetDatabase.SaveAssets();
            Debug.Log("[QCXR Art] Fase 5: efectos, clips, controladores y parches de prefabs listos.");
        }

        // ═════════════════════════ MATERIALES DE PARTÍCULAS ═════════════════════════

        static Texture2D Tex(string name, int size, Func<float, float, Color> f, bool sprite = false)
        {
            var t = new Texture2D(size, size, TextureFormat.RGBA32, false);
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    t.SetPixel(x, y, f((x + 0.5f) / size * 2f - 1f, (y + 0.5f) / size * 2f - 1f));
            string dir = sprite ? UiArtDir : VfxDir;
            string path = $"{dir}/{name}.png";
            File.WriteAllBytes(Path.Combine(Application.dataPath, path.Substring("Assets/".Length)), t.EncodeToPNG());
            Object.DestroyImmediate(t);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var ti = (TextureImporter)AssetImporter.GetAtPath(path);
            ti.alphaSource = TextureImporterAlphaSource.FromInput;
            ti.alphaIsTransparency = true;
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.mipmapEnabled = !sprite;
            if (sprite) { ti.textureType = TextureImporterType.Sprite; ti.spriteImportMode = SpriteImportMode.Single; }
            ti.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        static float Hash(float x, float y) { float h = Mathf.Sin(x * 127.1f + y * 311.7f) * 43758.5453f; return h - Mathf.Floor(h); }

        static Material ParticleMat(string name, Texture2D tex, bool add)
        {
            return QCXRArtBuilder.Mat($"{VfxDir}/{name}.mat", m =>
            {
                m.shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
                m.SetFloat("_Surface", 1f);
                m.SetFloat("_Blend", add ? 2f : 0f);
                m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                m.SetFloat("_DstBlend", (float)(add ? UnityEngine.Rendering.BlendMode.One : UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha));
                m.SetFloat("_SrcBlendAlpha", (float)UnityEngine.Rendering.BlendMode.One);
                m.SetFloat("_DstBlendAlpha", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                m.SetFloat("_ZWrite", 0f);
                m.SetOverrideTag("RenderType", "Transparent");
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                if (tex != null) m.SetTexture("_BaseMap", tex);
                m.SetColor("_BaseColor", Color.white);
            });
        }

        static void BuildFxMaterials()
        {
            var soft = Tex("QCXR_FX_Soft", 64, (x, y) => { float r = Mathf.Clamp01(1f - Mathf.Sqrt(x * x + y * y)); return new Color(1, 1, 1, r * r); });
            var puff = Tex("QCXR_FX_Puff", 64, (x, y) =>
            {
                float r = Mathf.Sqrt(x * x + y * y);
                float n = 0.65f + 0.35f * Hash(Mathf.Floor(x * 6), Mathf.Floor(y * 6));
                return new Color(1, 1, 1, Mathf.Clamp01(1f - r) * Mathf.Clamp01(1f - r) * n);
            });
            additive = ParticleMat("QCXR_FX_Additive", soft, true);
            alpha = ParticleMat("QCXR_FX_Alpha", puff, false);
            confettiMat = ParticleMat("QCXR_FX_Confetti", null, false);
        }

        // ═════════════════════════ EFECTOS ═════════════════════════

        static ParticleSystem NewPs(GameObject go, Material mat, bool loop, float duration, int max, bool playOnAwake)
        {
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.loop = loop; main.duration = duration; main.maxParticles = max; main.playOnAwake = playOnAwake;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var em = ps.emission; em.rateOverTime = 0f;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            r.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            return ps;
        }

        static Gradient Fade(Color a, Color b, float a0 = 1f, float a1 = 0f, float hold = 0.2f)
        {
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(a, 0f), new GradientColorKey(b, 1f) },
                      new[] { new GradientAlphaKey(a0, 0f), new GradientAlphaKey(a0, hold), new GradientAlphaKey(a1, 1f) });
            return g;
        }

        static void SaveFx(GameObject go)
        {
            PrefabUtility.SaveAsPrefabAsset(go, $"{FxPrefabDir}/{go.name}.prefab");
            Object.DestroyImmediate(go);
        }

        static void BuildEffects()
        {
            // Chispas del atasco (bucle, se encienden con JamIndicator)
            {
                var go = new GameObject("FX_Sparks");
                var ps = NewPs(go, additive, true, 0.6f, 60, true);
                var main = ps.main;
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.5f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(1.0f, 2.4f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.008f, 0.016f);
                main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.85f, 0.35f), new Color(1f, 0.45f, 0.1f));
                main.gravityModifier = 1.4f;
                var em = ps.emission; em.rateOverTime = 18f; em.SetBursts(new[] { new ParticleSystem.Burst(0f, 10, 14, 1, 0.01f) });
                var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 35f; sh.radius = 0.01f;
                var col = ps.colorOverLifetime; col.enabled = true; col.color = Fade(Color.white, new Color(1f, 0.3f, 0.05f), 1f, 0f, 0.5f);
                var r = go.GetComponent<ParticleSystemRenderer>();
                r.renderMode = ParticleSystemRenderMode.Stretch; r.velocityScale = 0.035f; r.lengthScale = 2f;
                SaveFx(go);
            }
            // Destello de reparación (una vez): flash + chispitas que suben
            {
                var go = new GameObject("FX_RepairFlash");
                var ps = NewPs(go, additive, false, 0.6f, 4, false);
                var main = ps.main;
                main.startLifetime = 0.28f; main.startSpeed = 0f; main.startSize = 0.14f;
                main.startColor = new Color(0.55f, 1f, 0.7f);
                var em = ps.emission; em.SetBursts(new[] { new ParticleSystem.Burst(0f, 1) });
                var sh = ps.shape; sh.enabled = false;
                var sol = ps.sizeOverLifetime; sol.enabled = true; sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0, 0.4f, 1, 1.2f));
                var col = ps.colorOverLifetime; col.enabled = true; col.color = Fade(Color.white, Color.white, 1f, 0f, 0.1f);
                var child = new GameObject("Sparkles");
                child.transform.SetParent(go.transform, false);
                var ps2 = NewPs(child, additive, false, 0.6f, 24, false);
                var m2 = ps2.main;
                m2.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.6f);
                m2.startSpeed = new ParticleSystem.MinMaxCurve(0.25f, 0.7f);
                m2.startSize = new ParticleSystem.MinMaxCurve(0.008f, 0.018f);
                m2.startColor = new ParticleSystem.MinMaxGradient(new Color(0.4f, 1f, 0.6f), new Color(0.5f, 0.95f, 1f));
                m2.gravityModifier = -0.25f;
                var em2 = ps2.emission; em2.SetBursts(new[] { new ParticleSystem.Burst(0f, 16) });
                var sh2 = ps2.shape; sh2.shapeType = ParticleSystemShapeType.Sphere; sh2.radius = 0.02f;
                var col2 = ps2.colorOverLifetime; col2.enabled = true; col2.color = Fade(Color.white, Color.white, 1f, 0f, 0.4f);
                SaveFx(go);
            }
            // Polvo al martillar (una vez)
            {
                var go = new GameObject("FX_HammerDust");
                var ps = NewPs(go, alpha, false, 0.7f, 16, false);
                var main = ps.main;
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.7f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(0.15f, 0.45f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.045f);
                main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.78f, 0.74f, 0.66f, 0.7f), new Color(0.6f, 0.58f, 0.55f, 0.6f));
                main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
                main.gravityModifier = -0.05f;
                var em = ps.emission; em.SetBursts(new[] { new ParticleSystem.Burst(0f, 10) });
                var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Hemisphere; sh.radius = 0.02f;
                var sol = ps.sizeOverLifetime; sol.enabled = true; sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, 0.6f, 1, 1.6f));
                var col = ps.colorOverLifetime; col.enabled = true; col.color = Fade(Color.white, Color.white, 1f, 0f, 0.2f);
                SaveFx(go);
            }
            // Humo del circuito quemado (bucle continuo)
            {
                var go = new GameObject("FX_BurnedCircuitSmoke");
                var ps = NewPs(go, alpha, true, 2f, 20, true);
                var main = ps.main;
                main.startLifetime = new ParticleSystem.MinMaxCurve(1.4f, 2.2f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(0.03f, 0.07f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.032f);
                main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.16f, 0.15f, 0.15f, 0.85f), new Color(0.3f, 0.29f, 0.28f, 0.75f));
                main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
                main.gravityModifier = -0.02f;
                var em = ps.emission; em.rateOverTime = 9f;
                var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 12f; sh.radius = 0.006f;
                var sol = ps.sizeOverLifetime; sol.enabled = true; sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, 1f, 1, 3.2f));
                var rot = ps.rotationOverLifetime; rot.enabled = true; rot.z = new ParticleSystem.MinMaxCurve(-0.8f, 0.8f);
                var col = ps.colorOverLifetime; col.enabled = true; col.color = Fade(Color.white, Color.white, 0f, 0f, 0.15f);
                var g = new Gradient();
                g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                          new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(0f, 1f) });
                col.color = g;
                SaveFx(go);
            }
            // Confeti de la nota final (una vez)
            {
                var go = new GameObject("FX_Confetti");
                var ps = NewPs(go, confettiMat, false, 3f, 120, false);
                var main = ps.main;
                main.startLifetime = new ParticleSystem.MinMaxCurve(2f, 3f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(1.6f, 3f);
                main.startSize3D = true;
                main.startSizeX = new ParticleSystem.MinMaxCurve(0.012f, 0.02f);
                main.startSizeY = new ParticleSystem.MinMaxCurve(0.02f, 0.032f);
                main.startSizeZ = 1f;
                main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
                var g = new Gradient { mode = GradientMode.Fixed };
                g.SetKeys(new[]
                {
                    new GradientColorKey(Palette.Get(Sw.Orange).albedo, 0.2f), new GradientColorKey(Palette.Get(Sw.HazardYellow).albedo, 0.4f),
                    new GradientColorKey(Palette.Get(Sw.RobotBlue).albedo, 0.6f), new GradientColorKey(Palette.Get(Sw.ClockGreen).albedo, 0.8f),
                    new GradientColorKey(Palette.Get(Sw.RadioRed).albedo, 1f)
                }, new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
                main.startColor = new ParticleSystem.MinMaxGradient(g) { mode = ParticleSystemGradientMode.RandomColor };
                main.gravityModifier = 0.55f;
                var em = ps.emission; em.SetBursts(new[] { new ParticleSystem.Burst(0f, 50), new ParticleSystem.Burst(0.15f, 40) });
                var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 40f; sh.radius = 0.05f;
                var rot = ps.rotationOverLifetime; rot.enabled = true; rot.z = new ParticleSystem.MinMaxCurve(-6f, 6f);
                var vel = ps.limitVelocityOverLifetime; vel.enabled = true; vel.limit = 1.2f; vel.dampen = 0.25f;
                var col = ps.colorOverLifetime; col.enabled = true;
                var fade = new Gradient();
                fade.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                             new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.8f), new GradientAlphaKey(0f, 1f) });
                col.color = fade;
                go.transform.rotation = Quaternion.Euler(-90, 0, 0);   // dispara hacia arriba
                SaveFx(go);
            }
        }

        // ═════════════════════════ SELLOS DE NOTA (S, A, B, C) ═════════════════════════

        public static readonly string[] Grades = { "S", "A", "B", "C" };

        static void BuildGradeSprites()
        {
            Color[] cols = { Palette.Get(Sw.HazardYellow).albedo, Palette.Get(Sw.ClockGreen).albedo, Palette.Get(Sw.RobotBlue).albedo, Palette.Get(Sw.RadioRed).albedo };
            for (int i = 0; i < 4; i++)
            {
                Color c = cols[i], dark = Color.Lerp(c, Color.black, 0.35f);
                int shape = i;
                Tex("QCXR_Stamp_" + Grades[i], 256, (x, y) =>
                {
                    float r = Mathf.Sqrt(x * x + y * y), a = Mathf.Atan2(y, x);
                    float edge;
                    switch (shape)
                    {
                        case 0: edge = 0.82f + 0.12f * Mathf.Pow(Mathf.Abs(Mathf.Cos(a * 8f)), 6f); break;                      // S: estrella de 16 puntas
                        case 1: edge = 0.88f + 0.05f * Mathf.Abs(Mathf.Cos(a * 12f)); break;                                  // A: festoneado
                        case 2: { float s = Mathf.PI / 3f; float k = Mathf.Repeat(a, s) - s / 2f; edge = 0.86f * Mathf.Cos(s / 2f) / Mathf.Cos(k); break; } // B: hexágono
                        default: edge = 0.9f; break;                                                                           // C: círculo liso
                    }
                    if (r > edge) return new Color(0, 0, 0, 0);
                    float aa = Mathf.Clamp01((edge - r) * 80f);
                    Color col = r > edge - 0.07f ? dark : (r > 0.62f && r < 0.68f) ? Color.white : c;
                    if (r < 0.62f) col = Color.Lerp(c, dark, 0.25f);
                    col.a = aa;
                    return col;
                }, true);
            }
        }

        public static Sprite GradeSprite(int i) => AssetDatabase.LoadAssetAtPath<Sprite>($"{UiArtDir}/QCXR_Stamp_{Grades[i]}.png");

        // ═════════════════════════ CLIPS Y CONTROLADORES ═════════════════════════

        struct Curve
        {
            public string path, prop; public Type type; public float[] t, v;
            public Curve(string path, Type type, string prop, float[] t, float[] v) { this.path = path; this.type = type; this.prop = prop; this.t = t; this.v = v; }
        }

        static AnimationClip MakeClip(string path, bool loop, params Curve[] curves)
        {
            var clip = new AnimationClip { name = Path.GetFileNameWithoutExtension(path), frameRate = 60 };
            foreach (var c in curves)
            {
                var keys = new Keyframe[c.t.Length];
                for (int i = 0; i < keys.Length; i++) keys[i] = new Keyframe(c.t[i], c.v[i]);
                var curve = new AnimationCurve(keys);
                for (int i = 0; i < keys.Length; i++)
                {
                    AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.ClampedAuto);
                    AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.ClampedAuto);
                }
                AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(c.path, c.type, c.prop), curve);
            }
            var s = AnimationUtility.GetAnimationClipSettings(clip);
            s.loopTime = loop;
            AnimationUtility.SetAnimationClipSettings(clip, s);
            var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (existing == null) { AssetDatabase.CreateAsset(clip, path); return clip; }
            EditorUtility.CopySerialized(clip, existing);
            existing.name = clip.name;
            return existing;
        }

        static Curve[] Vec(string path, Type type, string prop, float[] t, Vector3[] v) => new[]
        {
            new Curve(path, type, prop + ".x", t, v.Select(q => q.x).ToArray()),
            new Curve(path, type, prop + ".y", t, v.Select(q => q.y).ToArray()),
            new Curve(path, type, prop + ".z", t, v.Select(q => q.z).ToArray()),
        };

        static Curve[] Emission(string path, float[] t, Color[] c) => new[]
        {
            new Curve(path, typeof(MeshRenderer), "material._EmissionColor.r", t, c.Select(q => q.r).ToArray()),
            new Curve(path, typeof(MeshRenderer), "material._EmissionColor.g", t, c.Select(q => q.g).ToArray()),
            new Curve(path, typeof(MeshRenderer), "material._EmissionColor.b", t, c.Select(q => q.b).ToArray()),
        };

        static Curve[] Join(params Curve[][] parts) => parts.SelectMany(p => p).ToArray();

        static AnimatorController NewController(string path)
        {
            AssetDatabase.DeleteAsset(path);
            return AnimatorController.CreateAnimatorControllerAtPath(path);
        }

        static AnimatorState State(AnimatorController c, string name, Motion m, bool isDefault = false)
        {
            var sm = c.layers[0].stateMachine;
            var st = sm.AddState(name);
            st.motion = m;
            if (isDefault) sm.defaultState = st;
            return st;
        }

        static void Back(AnimatorState from, AnimatorState to, float dur = 0.05f)
        {
            var t = from.AddTransition(to); t.hasExitTime = true; t.exitTime = 1f; t.duration = dur;
        }

        static void Trigger(AnimatorController c, AnimatorState to, string param, float dur = 0.02f)
        {
            var t = c.layers[0].stateMachine.AddAnyStateTransition(to);
            t.AddCondition(AnimatorConditionMode.If, 0, param); t.duration = dur; t.hasExitTime = false; t.canTransitionToSelf = true;
        }

        public static readonly Dictionary<string, RuntimeAnimatorController> Ctrl = new Dictionary<string, RuntimeAnimatorController>();

        static void BuildAnimations()
        {
            string P = AnimRoot + "/Products", E = AnimRoot + "/Environment", U = AnimRoot + "/UI";
            var T = typeof(Transform);

            // ── Producto entrando a la banda (0.3 s, rebote) — en el hijo Model ──
            var enter = MakeClip($"{P}/Product_Enter.anim", false, Join(
                Vec("", T, "m_LocalScale", new[] { 0f, 0.14f, 0.22f, 0.3f }, new[] { new Vector3(0.6f, 0.6f, 0.6f), new Vector3(1.1f, 0.9f, 1.1f), new Vector3(0.96f, 1.05f, 0.96f), Vector3.one }),
                new[] { new Curve("", T, "m_LocalPosition.y", new[] { 0f, 0.14f, 0.22f, 0.3f }, new[] { 0.06f, 0f, 0.012f, 0f }) }));
            var c = NewController($"{P}/Product_Model.controller"); State(c, "Enter", enter, true); Ctrl["Product"] = c;

            // ── Tornillo apretándose (0.4 s): de levantado/inclinado a plano con dos vueltas — en Fixed ──
            Vector3 raised = Quaternion.Euler(0, 0, 24f) * Vector3.up * 0.016f;
            var screw = MakeClip($"{P}/Defect_ScrewTighten.anim", false, Join(
                Vec("ScrewHead", T, "m_LocalPosition", new[] { 0f, 0.3f, 0.4f }, new[] { raised, new Vector3(0, 0.0015f, 0), Vector3.zero }),
                Vec("ScrewHead", T, "localEulerAnglesRaw", new[] { 0f, 0.3f, 0.4f }, new[] { new Vector3(0, 745f, 24f), new Vector3(0, 30f, 2f), Vector3.zero })));
            c = NewController($"{P}/Defect_Screw.controller"); State(c, "Tighten", screw, true); Ctrl["Screw"] = c;

            // ── Abolladura enderezándose (0.3 s): la chapa cede y recupera su forma — en Fixed ──
            var dent = MakeClip($"{P}/Defect_DentStraighten.anim", false,
                Vec("PlateSmooth", T, "m_LocalScale", new[] { 0f, 0.1f, 0.2f, 0.3f }, new[] { new Vector3(1.04f, 0.3f, 1.04f), new Vector3(0.97f, 1.35f, 0.97f), new Vector3(1.01f, 0.9f, 1.01f), Vector3.one }));
            c = NewController($"{P}/Defect_Dent.controller"); State(c, "Straighten", dent, true); Ctrl["Dent"] = c;

            // ── Revelar grieta (0.4 s): brillo naranja que se apaga dejando un rescoldo; la grieta "se propaga" — en Visual ──
            var crack = MakeClip($"{P}/Defect_CrackReveal.anim", false, Join(
                Emission("Crack", new[] { 0f, 0.12f, 0.4f }, new[] { new Color(0, 0, 0), new Color(4f, 2.2f, 0.6f), new Color(0.55f, 0.22f, 0.05f) }),
                new[] { new Curve("Crack", T, "m_LocalScale.x", new[] { 0f, 0.25f }, new[] { 0.3f, 1f }) }));
            c = NewController($"{P}/Defect_Crack.controller"); State(c, "Reveal", crack, true); Ctrl["Crack"] = c;

            // ── Botón presionándose (0.15 s) — raíz de Env_ButtonCap ──
            var bIdle = MakeClip($"{E}/Button_Idle.anim", true, new Curve("Cap", T, "m_LocalPosition.y", new[] { 0f, 1f / 30f }, new[] { 0f, 0f }));
            var bPress = MakeClip($"{E}/Button_Press.anim", false, new Curve("Cap", T, "m_LocalPosition.y", new[] { 0f, 0.05f, 0.15f }, new[] { 0f, -0.012f, 0f }));
            c = NewController($"{E}/Button.controller");
            c.AddParameter("Press", AnimatorControllerParameterType.Trigger);
            var si = State(c, "Idle", bIdle, true); var sp = State(c, "Press", bPress); Trigger(c, sp, "Press"); Back(sp, si, 0.01f);
            Ctrl["Button"] = c;

            // ── Banda atascada (bucle): vibración de la cinta — raíz de Env_ConveyorBelt ──
            var beltIdle = MakeClip($"{E}/Belt_Idle.anim", true, Vec("BeltSurface", T, "m_LocalPosition", new[] { 0f, 1f / 30f }, new[] { Vector3.zero, Vector3.zero }));
            float[] jt = { 0f, 0.04f, 0.08f, 0.12f, 0.16f, 0.2f };
            var beltJam = MakeClip($"{E}/Belt_Jam.anim", true, Vec("BeltSurface", T, "m_LocalPosition", jt, new[]
            {
                Vector3.zero, new Vector3(0.004f, 0.0025f, 0), new Vector3(-0.003f, -0.001f, 0.0015f), new Vector3(0.002f, 0.003f, -0.001f), new Vector3(-0.004f, 0.0005f, 0), Vector3.zero
            }));
            c = NewController($"{E}/Belt.controller");
            c.AddParameter("Jammed", AnimatorControllerParameterType.Bool);
            var bi = State(c, "Idle", beltIdle, true); var bj = State(c, "Jam", beltJam);
            var t1 = bi.AddTransition(bj); t1.AddCondition(AnimatorConditionMode.If, 0, "Jammed"); t1.duration = 0.05f; t1.hasExitTime = false;
            var t2 = bj.AddTransition(bi); t2.AddCondition(AnimatorConditionMode.IfNot, 0, "Jammed"); t2.duration = 0.1f; t2.hasExitTime = false;
            Ctrl["Belt"] = c;

            // ── Balizas (bucle): reflector girando + pulso de la cúpula — raíz de Env_*Lit ──
            var spin = MakeClip($"{E}/Beacon_Spin.anim", true, Join(
                new[] { new Curve("Rotor", T, "localEulerAnglesRaw.y", new[] { 0f, 0.8f }, new[] { 0f, 360f }) },
                Vec("LitDome", T, "m_LocalScale", new[] { 0f, 0.2f, 0.4f, 0.6f, 0.8f }, new[] { Vector3.one, Vector3.one * 1.06f, Vector3.one, Vector3.one * 1.06f, Vector3.one })));
            var curveSpin = AnimationUtility.GetEditorCurve(spin, EditorCurveBinding.FloatCurve("Rotor", T, "localEulerAnglesRaw.y"));
            for (int i = 0; i < curveSpin.length; i++) { AnimationUtility.SetKeyLeftTangentMode(curveSpin, i, AnimationUtility.TangentMode.Linear); AnimationUtility.SetKeyRightTangentMode(curveSpin, i, AnimationUtility.TangentMode.Linear); }
            AnimationUtility.SetEditorCurve(spin, EditorCurveBinding.FloatCurve("Rotor", T, "localEulerAnglesRaw.y"), curveSpin);
            c = NewController($"{E}/Beacon.controller"); State(c, "Spin", spin, true); Ctrl["Beacon"] = c;

            // ── Destellos (estante 0.4 s verde/rojo · cubo 0.3 s) — en BayLight / FlashRing ──
            Color idle = new Color(0.35f, 0.35f, 0.35f), green = new Color(0.4f, 3.2f, 0.9f), red = new Color(3.2f, 0.35f, 0.25f);
            var fIdle = MakeClip($"{E}/Flash_Idle.anim", true, Emission("", new[] { 0f, 1f / 30f }, new[] { idle, idle }));
            var fGood = MakeClip($"{E}/Flash_Good.anim", false, Emission("", new[] { 0f, 0.06f, 0.4f }, new[] { idle, green, idle }));
            var fBad = MakeClip($"{E}/Flash_Bad.anim", false, Emission("", new[] { 0f, 0.06f, 0.4f }, new[] { idle, red, idle }));
            var binGood = MakeClip($"{E}/Bin_Flash.anim", false, Emission("", new[] { 0f, 0.05f, 0.3f }, new[] { idle, new Color(3f, 3f, 3f), idle }));
            c = NewController($"{E}/Flash.controller");
            c.AddParameter("Good", AnimatorControllerParameterType.Trigger); c.AddParameter("Bad", AnimatorControllerParameterType.Trigger);
            var fi = State(c, "Idle", fIdle, true); var fg = State(c, "Good", fGood); var fb = State(c, "Bad", fBad);
            Trigger(c, fg, "Good"); Trigger(c, fb, "Bad"); Back(fg, fi); Back(fb, fi);
            Ctrl["Flash"] = c;
            var bin = new AnimatorOverrideController(c) { name = "BinFlash" };
            bin.ApplyOverrides(new List<KeyValuePair<AnimationClip, AnimationClip>> { new KeyValuePair<AnimationClip, AnimationClip>(fGood, binGood) });
            string binPath = $"{E}/BinFlash.overrideController";
            AssetDatabase.DeleteAsset(binPath); AssetDatabase.CreateAsset(bin, binPath);
            Ctrl["BinFlash"] = bin;

            // ── Panel de UI: fundido de entrada al activarse (0.2 s) y de salida con trigger "Hide" ──
            var uiIn = MakeClip($"{U}/UI_FadeIn.anim", false, Join(
                new[] { new Curve("", typeof(CanvasGroup), "m_Alpha", new[] { 0f, 0.2f }, new[] { 0f, 1f }) }));
            var uiShown = MakeClip($"{U}/UI_Shown.anim", true, new Curve("", typeof(CanvasGroup), "m_Alpha", new[] { 0f, 1f / 30f }, new[] { 1f, 1f }));
            var uiOut = MakeClip($"{U}/UI_FadeOut.anim", false, new Curve("", typeof(CanvasGroup), "m_Alpha", new[] { 0f, 0.2f }, new[] { 1f, 0f }));
            c = NewController($"{U}/UI_Panel.controller");
            c.AddParameter("Hide", AnimatorControllerParameterType.Trigger);
            var ui1 = State(c, "FadeIn", uiIn, true); var ui2 = State(c, "Shown", uiShown); var ui3 = State(c, "FadeOut", uiOut);
            Back(ui1, ui2, 0f); Trigger(c, ui3, "Hide");
            Ctrl["UIPanel"] = c;

            // ── Sello de nota final (0.5 s): cae, golpea y rebota ──
            var stamp = MakeClip($"{U}/Stamp_Drop.anim", false, Join(
                Vec("", T, "m_LocalScale", new[] { 0f, 0.22f, 0.32f, 0.42f, 0.5f }, new[] { Vector3.one * 2.4f, Vector3.one * 0.86f, Vector3.one * 1.08f, Vector3.one * 0.97f, Vector3.one }),
                new[]
                {
                    new Curve("", T, "localEulerAnglesRaw.z", new[] { 0f, 0.22f, 0.5f }, new[] { -28f, -10f, -12f }),
                    new Curve("", typeof(CanvasGroup), "m_Alpha", new[] { 0f, 0.12f }, new[] { 0f, 1f }),
                }));
            c = NewController($"{U}/Stamp.controller"); State(c, "Drop", stamp, true); Ctrl["Stamp"] = c;
        }

        // ═════════════════════════ PARCHES A PREFABS ═════════════════════════

        static void EditPrefab(string path, Action<GameObject> edit)
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            try { edit(root); PrefabUtility.SaveAsPrefabAsset(root, path); }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        static Animator SetAnimator(GameObject go, RuntimeAnimatorController ctrl, AnimatorCullingMode cull = AnimatorCullingMode.AlwaysAnimate)
        {
            var a = go.GetComponent<Animator>();
            if (a == null) a = go.AddComponent<Animator>();
            a.runtimeAnimatorController = ctrl;
            a.applyRootMotion = false;
            a.cullingMode = cull;
            return a;
        }

        static void AddFx(Transform parent, string fx)
        {
            var old = parent.Find(fx);
            if (old != null) Object.DestroyImmediate(old.gameObject);
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>($"{FxPrefabDir}/{fx}.prefab"), parent);
            inst.transform.localPosition = Vector3.zero;
            inst.transform.localRotation = Quaternion.identity;
        }

        static void PatchPrefabs()
        {
            string D = QCXRArtBuilder.DefectPrefabDir, Pr = QCXRArtBuilder.ProductPrefabDir, En = QCXRStationBuilder.PrefabDir;
            EditPrefab($"{D}/Defect_LooseScrew.prefab", r => SetAnimator(r.transform.Find("Fixed").gameObject, Ctrl["Screw"]));
            EditPrefab($"{D}/Defect_Dent.prefab", r => SetAnimator(r.transform.Find("Fixed").gameObject, Ctrl["Dent"]));
            EditPrefab($"{D}/Defect_HiddenCrack.prefab", r => SetAnimator(r.transform.Find("Visual").gameObject, Ctrl["Crack"]));
            EditPrefab($"{D}/Defect_BurnedCircuit.prefab", r => AddFx(r.transform.Find("Visual/SmokeAnchor"), "FX_BurnedCircuitSmoke"));
            foreach (var p in new[] { "Robot", "Radio", "Lamp", "Toaster", "Clock" })
                EditPrefab($"{Pr}/Product_{p}.prefab", r => SetAnimator(r.transform.Find("Model").gameObject, Ctrl["Product"], AnimatorCullingMode.CullUpdateTransforms));

            foreach (var cap in new[] { "Env_ButtonCap_Green", "Env_ButtonCap_Red" }) EditPrefab($"{En}/{cap}.prefab", r => SetAnimator(r, Ctrl["Button"]));
            EditPrefab($"{En}/Env_JamIndicatorLit.prefab", r => { SetAnimator(r, Ctrl["Beacon"]); AddFx(r.transform.Find("SparksAnchor"), "FX_Sparks"); });
            EditPrefab($"{En}/Env_EmergencyBeaconLit.prefab", r => SetAnimator(r, Ctrl["Beacon"]));
            EditPrefab($"{En}/Env_ShelfRack.prefab", r =>
            {
                foreach (var bay in r.GetComponentsInChildren<Transform>(true).Where(t => t.name == "BayLight"))
                    SetAnimator(bay.gameObject, Ctrl["Flash"]);
            });
            EditPrefab($"{En}/Env_DiscardBin.prefab", r => SetAnimator(r.transform.Find("FlashRing").gameObject, Ctrl["BinFlash"]));
            EditPrefab($"{En}/Env_ConveyorBelt.prefab", r =>
            {
                SetAnimator(r, Ctrl["Belt"]);
                GameObjectUtility.SetStaticEditorFlags(r.transform.Find("BeltSurface").gameObject, 0);   // se mueve al atascarse
            });
        }

        // ═════════════════════════ VISTAS DE VERIFICACIÓN ═════════════════════════

        /// Hoja de efectos (simulados a media vida) + hoja de animaciones de defectos muestreadas en el tiempo.
        public static void RenderPreviews(string outDir)
        {
            Directory.CreateDirectory(outDir);
            UnityEditor.SceneManagement.EditorSceneManager.NewScene(UnityEditor.SceneManagement.NewSceneSetup.EmptyScene, UnityEditor.SceneManagement.NewSceneMode.Single);
            var sun = new GameObject("Sun").AddComponent<Light>(); sun.type = LightType.Directional; sun.intensity = 1.2f; sun.transform.rotation = Quaternion.Euler(50, -30, 0);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat; RenderSettings.ambientLight = new Color(0.45f, 0.46f, 0.5f);
            var cam = new GameObject("Cam").AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.14f, 0.15f, 0.18f); cam.nearClipPlane = 0.005f; cam.fieldOfView = 35f;
            const int T = 480;
            var rt = new RenderTexture(T, T, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 4 };
            cam.targetTexture = rt;
            void Shot(Texture2D sheet, int col, int row, int rows, Vector3 target, Vector3 dir, float dist)
            {
                cam.transform.position = target + dir.normalized * dist; cam.transform.LookAt(target);
                cam.Render(); RenderTexture.active = rt;
                sheet.ReadPixels(new Rect(0, 0, T, T), col * T, (rows - 1 - row) * T); RenderTexture.active = null;
            }

            // Efectos
            var fxNames = new[] { "FX_Sparks", "FX_RepairFlash", "FX_HammerDust", "FX_BurnedCircuitSmoke", "FX_Confetti" };
            var fxTimes = new[] { 0.3f, 0.12f, 0.25f, 2.5f, 0.6f };
            var fxDist = new[] { 0.6f, 0.35f, 0.35f, 0.35f, 2.2f };
            var fxSheet = new Texture2D(T * fxNames.Length, T, TextureFormat.RGB24, false);
            for (int i = 0; i < fxNames.Length; i++)
            {
                var go = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>($"{FxPrefabDir}/{fxNames[i]}.prefab"));
                go.transform.position = Vector3.zero;
                if (fxNames[i] == "FX_Sparks" || fxNames[i] == "FX_BurnedCircuitSmoke") go.transform.rotation = Quaternion.Euler(-70, 0, 0);
                var ps = go.GetComponent<ParticleSystem>();
                ps.Simulate(fxTimes[i], true, true, true);
                Vector3 target = fxNames[i] == "FX_Confetti" ? new Vector3(0, 0.9f, 0) : fxNames[i] == "FX_BurnedCircuitSmoke" ? new Vector3(0, 0.07f, 0) : new Vector3(0, 0.05f, 0);
                Shot(fxSheet, i, 0, 1, target, new Vector3(0.3f, 0.25f, -1f), fxDist[i]);
                Object.DestroyImmediate(go);
            }
            fxSheet.Apply(); File.WriteAllBytes(Path.Combine(outDir, "FX_Effects.png"), fxSheet.EncodeToPNG()); Object.DestroyImmediate(fxSheet);

            // Animaciones de defectos: tornillo (0/0.2/0.4 s), abolladura (0/0.1/0.3 s), grieta (0.05/0.12/0.4 s)
            var rows = new (string prefab, string animated, string clip, float[] times)[]
            {
                ("Defect_LooseScrew", "Fixed", "Products/Defect_ScrewTighten.anim", new[] { 0f, 0.2f, 0.4f }),
                ("Defect_Dent", "Fixed", "Products/Defect_DentStraighten.anim", new[] { 0f, 0.1f, 0.3f }),
                ("Defect_HiddenCrack", "Visual", "Products/Defect_CrackReveal.anim", new[] { 0.05f, 0.12f, 0.4f }),
            };
            var plate = GameObject.CreatePrimitive(PrimitiveType.Cube);
            plate.transform.localScale = new Vector3(0.12f, 0.01f, 0.12f); plate.transform.position = new Vector3(0, -0.005f, 0);
            var pm = new Material(Shader.Find("Universal Render Pipeline/Lit")); pm.SetColor("_BaseColor", new Color(0.25f, 0.5f, 0.9f));
            plate.GetComponent<Renderer>().sharedMaterial = pm;
            var animSheet = new Texture2D(T * 3, T * rows.Length, TextureFormat.RGB24, false);
            for (int r = 0; r < rows.Length; r++)
            {
                var go = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>($"{QCXRArtBuilder.DefectPrefabDir}/{rows[r].prefab}.prefab"));
                var animated = go.transform.Find(rows[r].animated).gameObject;
                go.transform.Find("Visual").gameObject.SetActive(rows[r].animated == "Visual");
                animated.SetActive(true);
                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>($"{AnimRoot}/{rows[r].clip}");
                for (int c = 0; c < 3; c++)
                {
                    AnimationMode.StartAnimationMode();
                    AnimationMode.BeginSampling();
                    AnimationMode.SampleAnimationClip(animated, clip, rows[r].times[c]);
                    AnimationMode.EndSampling();
                    Shot(animSheet, c, r, rows.Length, new Vector3(0, 0.006f, 0), new Vector3(-0.35f, 0.9f, -0.9f), 0.17f);
                    AnimationMode.StopAnimationMode();
                }
                Object.DestroyImmediate(go);
            }
            animSheet.Apply(); File.WriteAllBytes(Path.Combine(outDir, "Anim_Defects.png"), animSheet.EncodeToPNG()); Object.DestroyImmediate(animSheet);
            Debug.Log("[QCXR Art] Vistas de la Fase 5 en " + outDir);
        }

        // ═════════════════════════ GANCHOS DE ESCENA ═════════════════════════

        public static void HookScene(Func<string, Transform> find, Transform sceneRoot, GameObject belt, GameObject rack, GameObject bin)
        {
            // Efectos reutilizables
            var fx = new GameObject("Effects").transform;
            fx.SetParent(sceneRoot, false);
            ParticleSystem Spawn(string name, Vector3 pos)
            {
                var go = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>($"{FxPrefabDir}/{name}.prefab"), fx);
                go.transform.position = pos;
                return go.GetComponent<ParticleSystem>();
            }
            var repair = Spawn("FX_RepairFlash", new Vector3(0, 0.95f, 0.75f));
            var dust = Spawn("FX_HammerDust", new Vector3(0, 0.95f, 0.75f));
            var results = find("Results");
            var confetti = Spawn("FX_Confetti", results.position + Vector3.down * 0.45f);

            // Botones: XRButton.onPressed → Animator.SetTrigger("Press") de su tapa
            foreach (var btn in new[] { "Btn_StartShift", "Btn_ResetBelt" })
            {
                var b = find(btn);
                var xr = b.GetComponent<XRButton>();
                var cap = b.Find("Cap")?.GetComponent<Animator>();
                if (xr == null || cap == null) continue;
                var field = typeof(XRButton).GetField("onPressed", BindingFlags.NonPublic | BindingFlags.Instance);
                var evt = (UnityEvent)field.GetValue(xr);
                if (evt == null) { evt = new UnityEvent(); field.SetValue(xr, evt); }
                for (int i = evt.GetPersistentEventCount() - 1; i >= 0; i--) if (evt.GetPersistentTarget(i) == cap) UnityEventTools.RemovePersistentListener(evt, i);
                UnityEventTools.AddStringPersistentListener(evt, new UnityAction<string>(cap.SetTrigger), "Press");
                EditorUtility.SetDirty(xr);
            }

            // Paneles de UI: CanvasGroup + fundido de entrada
            foreach (var panel in new[] { "HUD", "Instructions", "Report", "Results" })
            {
                var t = find(panel);
                if (t.GetComponent<CanvasGroup>() == null) t.gameObject.AddComponent<CanvasGroup>();
                SetAnimator(t.gameObject, Ctrl["UIPanel"]);
            }

            // Sello de nota: el texto Grade pasa a ser hijo del sello (GameUI sigue escribiéndolo igual)
            var grade = results.Find("Grade") as RectTransform;
            Image stampImg = null;
            if (grade != null)
            {
                var stamp = new GameObject("Stamp", typeof(RectTransform)).GetComponent<RectTransform>();
                stamp.SetParent(results, false);
                stamp.anchorMin = grade.anchorMin; stamp.anchorMax = grade.anchorMax; stamp.pivot = new Vector2(0.5f, 0.5f);
                stamp.anchoredPosition = grade.anchoredPosition;
                stamp.sizeDelta = new Vector2(190, 190);
                stamp.localRotation = Quaternion.Euler(0, 0, -12f);
                stampImg = stamp.gameObject.AddComponent<Image>();
                stampImg.sprite = GradeSprite(0); stampImg.raycastTarget = false; stampImg.preserveAspect = true;
                stamp.gameObject.AddComponent<CanvasGroup>();
                SetAnimator(stamp.gameObject, Ctrl["Stamp"]);
                grade.SetParent(stamp, false);
                grade.anchorMin = grade.anchorMax = new Vector2(0.5f, 0.5f);
                grade.anchoredPosition = Vector2.zero;
                grade.localRotation = Quaternion.identity;
                var tmp = grade.GetComponent<TMPro.TMP_Text>();
                if (tmp != null) { tmp.color = Color.white; tmp.fontSize = 110; tmp.fontStyle = TMPro.FontStyles.Bold; tmp.outlineWidth = 0.15f; tmp.outlineColor = new Color32(30, 30, 30, 255); }
            }

            // Puente de presentación
            var managers = find("Managers");
            var bridgeGo = new GameObject("Presentation");
            bridgeGo.transform.SetParent(managers, false);
            var bridge = bridgeGo.AddComponent<PresentationBridge>();
            var so = new SerializedObject(bridge);
            so.FindProperty("beltAnimator").objectReferenceValue = belt.GetComponent<Animator>();
            so.FindProperty("repairFlash").objectReferenceValue = repair;
            so.FindProperty("hammerDust").objectReferenceValue = dust;
            so.FindProperty("confetti").objectReferenceValue = confetti;
            var targets = rack.GetComponentsInChildren<Animator>(true).ToList();
            var binAnim = bin.transform.Find("FlashRing")?.GetComponent<Animator>();
            if (binAnim != null) targets.Add(binAnim);
            var arr = so.FindProperty("flashTargets");
            arr.arraySize = targets.Count;
            for (int i = 0; i < targets.Count; i++) arr.GetArrayElementAtIndex(i).objectReferenceValue = targets[i];
            so.FindProperty("stampImage").objectReferenceValue = stampImg;
            var sprites = so.FindProperty("gradeSprites");
            sprites.arraySize = 4;
            for (int i = 0; i < 4; i++) sprites.GetArrayElementAtIndex(i).objectReferenceValue = GradeSprite(i);
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
