using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.InputSystem;
using QCXR.Visual;

namespace QCXR.EditorTools.Art
{
    /// Menú QCXR > Arte > Fase 3 · Manos. Genera guantes izquierdo y derecho con rig de huesos,
    /// clips de pose/acción, AnimatorController (+ override para la izquierda) y prefabs.
    ///
    /// Marcos de referencia
    ///  · Modelado (M): muñeca en el origen, dedos hacia +Z, palma hacia -Y. Derecha: pulgar en -X.
    ///  · Agarre (raíz del prefab): origen en el centro del puño cerrado, +Z = eje del puño (sale por
    ///    el lado del índice), igual que el Attach de las herramientas. HandOffset convierte M → agarre.
    ///  · Los dedos se flexionan con rotación +X local; la izquierda es el espejo exacto (X → -X).
    public static class QCXRHandBuilder
    {
        const string ArtDir = QCXRArtBuilder.ArtRoot + "/Hands";
        const string AnimDir = "Assets/_Project/Animations/Hands";
        const string PrefabDir = "Assets/_Project/Prefabs/Hands";
        const string InputPath = "Assets/Samples/XR Interaction Toolkit/3.6.1/Starter Assets/XRI Default Input Actions.inputactions";

        /// Centro del puño (donde queda el eje del mango) en el marco M.
        public static readonly Vector3 FistCenter = new Vector3(0f, -0.027f, 0.088f);

        static readonly string[] Fingers = { "Index", "Middle", "Ring", "Pinky" };
        static readonly float[] FingerX = { -0.0285f, -0.0095f, 0.0095f, 0.0285f };
        static readonly float[] FingerSpread = { -3f, 0f, 3f, 7f };
        static readonly float[][] FingerLen =
        {
            new[] { 0.035f, 0.025f, 0.022f }, new[] { 0.040f, 0.028f, 0.024f },
            new[] { 0.037f, 0.026f, 0.022f }, new[] { 0.029f, 0.020f, 0.019f }
        };
        static readonly float[] FingerR = { 0.0098f, 0.0102f, 0.0098f, 0.0088f };
        static readonly float[] ThumbLen = { 0.035f, 0.028f, 0.024f };
        static readonly Vector3 ThumbBase = new Vector3(-0.032f, -0.008f, 0.022f);
        static readonly Vector3 ThumbRest = new Vector3(15f, -45f, 45f);

        class Bone { public string name; public Transform t; public Quaternion restRight; public int index; }

        // ═════════════════════════ POSES (grados, marco derecho) ═════════════════════════
        // Dedos: (proximal, medio, distal) de flexión. Pulgar: (base, medio, punta).

        struct Pose
        {
            public Vector3 index, middle, ring, pinky, thumb;
            public Vector3 thumbTwist;   // rotación extra (x,y,z) de Thumb1
            public Pose(Vector3 i, Vector3 m, Vector3 r, Vector3 p, Vector3 t, Vector3 tt = default)
            { index = i; middle = m; ring = r; pinky = p; thumb = t; thumbTwist = tt; }
            public Vector3 Finger(int f) => f == 0 ? index : f == 1 ? middle : f == 2 ? ring : pinky;
        }

        static Vector3 V(float a, float b, float c) => new Vector3(a, b, c);
        static readonly Vector3 FistF = V(85, 95, 60);

        static readonly Dictionary<string, Pose> Poses = new Dictionary<string, Pose>
        {
            ["Open"] = new Pose(V(6, 8, 4), V(8, 10, 5), V(10, 12, 6), V(12, 14, 8), V(0, 6, 4)),
            ["Fist"] = new Pose(FistF, FistF, FistF, FistF, V(25, 40, 35), V(0, 15, 0)),
            ["Grab"] = new Pose(V(38, 42, 22), V(42, 46, 24), V(46, 48, 26), V(50, 50, 28), V(18, 18, 12), V(0, 10, 0)),
            ["HoldHammer"] = new Pose(V(62, 82, 45), V(64, 84, 46), V(66, 86, 48), V(68, 88, 50), V(30, 42, 25), V(0, 18, 0)),
            ["HoldScrewdriver"] = new Pose(V(50, 68, 40), V(64, 84, 46), V(66, 86, 48), V(68, 88, 50), V(22, 30, 18), V(0, 12, 0)),
            ["HoldMagnifier"] = new Pose(V(52, 70, 38), V(60, 80, 44), V(64, 84, 46), V(66, 86, 48), V(26, 34, 20), V(0, 14, 0)),
            ["Point"] = new Pose(V(2, 3, 2), FistF, FistF, FistF, V(28, 38, 28), V(0, 15, 0)),
            ["Press"] = new Pose(V(14, 20, 10), FistF, FistF, FistF, V(28, 38, 28), V(0, 15, 0)),
            ["Inspect"] = new Pose(V(12, 16, 8), V(35, 40, 24), V(45, 50, 30), V(52, 56, 34), V(10, 16, 10)),
            ["IndexCurl"] = new Pose(V(58, 70, 40), V(10, 12, 6), V(12, 14, 8), V(14, 16, 9), V(14, 20, 12), V(0, 8, 0)),
        };

        // ═════════════════════════ ENTRADA ═════════════════════════

        [MenuItem("QCXR/Arte/Fase 3 · Manos")]
        public static void BuildHands()
        {
            QCXRPrefabBuilder.EnsureFolder(ArtDir);
            QCXRPrefabBuilder.EnsureFolder(AnimDir + "/Right");
            QCXRPrefabBuilder.EnsureFolder(AnimDir + "/Left");
            QCXRPrefabBuilder.EnsureFolder(PrefabDir);

            var right = BuildSide(false, out var rClips);
            var left = BuildSide(true, out var lClips);

            var controller = BuildController(rClips);
            var overrideCtrl = new AnimatorOverrideController(controller) { name = "Hand_L" };
            var pairs = new List<KeyValuePair<AnimationClip, AnimationClip>>();
            foreach (var kv in rClips) pairs.Add(new KeyValuePair<AnimationClip, AnimationClip>(kv.Value, lClips[kv.Key]));
            overrideCtrl.ApplyOverrides(pairs);
            string ovPath = AnimDir + "/Hand_L.overrideController";
            AssetDatabase.DeleteAsset(ovPath);
            AssetDatabase.CreateAsset(overrideCtrl, ovPath);

            SaveHandPrefab(right, controller, false);
            SaveHandPrefab(left, overrideCtrl, true);
            AssetDatabase.SaveAssets();
            Debug.Log("[QCXR Art] Fase 3: manos izquierda y derecha con rig, poses y Animator.");
        }

        // ═════════════════════════ RIG + MALLA ═════════════════════════

        static Quaternion Mirror(Quaternion q) => new Quaternion(q.x, -q.y, -q.z, q.w);
        static Vector3 Mirror(Vector3 v) => new Vector3(-v.x, v.y, v.z);

        static GameObject BuildSide(bool left, out Dictionary<string, AnimationClip> clips)
        {
            string side = left ? "L" : "R";
            var root = new GameObject("Hand_" + side);
            var offset = new GameObject("HandOffset").transform;
            offset.SetParent(root.transform, false);
            // M -> agarre (ver encabezado). Derecha: M(-X)->+Z, M(+Y)->+X. Izquierda: M(+X)->+Z, M(-Y)->+X.
            var rot = left ? Quaternion.LookRotation(Vector3.down, Vector3.left) : Quaternion.LookRotation(Vector3.down, Vector3.right);
            Vector3 fist = left ? Mirror(FistCenter) : FistCenter;
            offset.localRotation = rot;
            offset.localPosition = -(rot * fist);

            var bones = new List<Bone>();
            Bone Add(string name, Transform parent, Vector3 posRight, Quaternion restRight)
            {
                var t = new GameObject(name).transform;
                t.SetParent(parent, false);
                t.localPosition = left ? Mirror(posRight) : posRight;
                t.localRotation = left ? Mirror(restRight) : restRight;
                var b = new Bone { name = name, t = t, restRight = restRight, index = bones.Count };
                bones.Add(b);
                return b;
            }

            var wrist = Add("Wrist", offset, Vector3.zero, Quaternion.identity);
            var t1 = Add("Thumb1", wrist.t, ThumbBase, Quaternion.Euler(ThumbRest));
            var t2 = Add("Thumb2", t1.t, new Vector3(0, 0, ThumbLen[0]), Quaternion.identity);
            var t3 = Add("Thumb3", t2.t, new Vector3(0, 0, ThumbLen[1]), Quaternion.identity);
            Add("Thumb_Tip", t3.t, new Vector3(0, 0, ThumbLen[2]), Quaternion.identity);
            for (int f = 0; f < 4; f++)
            {
                var b1 = Add(Fingers[f] + "1", wrist.t, new Vector3(FingerX[f], 0.002f, 0.088f), Quaternion.Euler(0, FingerSpread[f], 0));
                var b2 = Add(Fingers[f] + "2", b1.t, new Vector3(0, 0, FingerLen[f][0]), Quaternion.identity);
                var b3 = Add(Fingers[f] + "3", b2.t, new Vector3(0, 0, FingerLen[f][1]), Quaternion.identity);
                Add(Fingers[f] + "_Tip", b3.t, new Vector3(0, 0, FingerLen[f][2]), Quaternion.identity);
            }

            // Malla con piel rígida: cada pieza pertenece al 100 % a un hueso.
            var meshGo = new GameObject("HandMesh");
            meshGo.transform.SetParent(offset, false);
            var combined = new MeshKit();
            var weights = new List<BoneWeight>();
            var mirror = left ? Matrix4x4.Scale(new Vector3(-1, 1, 1)) : Matrix4x4.identity;
            foreach (var b in bones)
            {
                var part = new MeshKit { Base = meshGo.transform.worldToLocalMatrix * b.t.localToWorldMatrix * mirror };
                BuildPart(part, b.name);
                int before = combined.VertexCount;
                combined.Append(part);
                for (int i = before; i < combined.VertexCount; i++) weights.Add(new BoneWeight { boneIndex0 = b.index, weight0 = 1f });
            }
            var mesh = combined.ToMesh("Hand_" + side);
            mesh.boneWeights = weights.ToArray();
            mesh.bindposes = bones.Select(b => b.t.worldToLocalMatrix * meshGo.transform.localToWorldMatrix).ToArray();
            mesh.RecalculateBounds();
            mesh = SaveAsset(mesh, $"{ArtDir}/Hand_{side}_Mesh.asset");

            var smr = meshGo.AddComponent<SkinnedMeshRenderer>();
            smr.sharedMesh = mesh;
            smr.sharedMaterial = QCXRArtBuilder.Atlas;
            smr.bones = bones.Select(b => b.t).ToArray();
            smr.rootBone = wrist.t;
            smr.quality = SkinQuality.Bone1;
            smr.updateWhenOffscreen = false;
            smr.localBounds = new Bounds(new Vector3(0, -0.01f, 0.05f), new Vector3(0.2f, 0.16f, 0.26f));
            smr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;

            // Referencias útiles para integración
            var grip = new GameObject("GripPoint").transform;   // = centro del puño = Attach de las herramientas
            grip.SetParent(root.transform, false);

            clips = BuildClips(root.transform, bones, left);
            return root;
        }

        static void BuildPart(MeshKit k, string bone)
        {
            if (bone == "Wrist")
            {
                // Palma y dorso
                k.Box(new Vector3(0, 0, 0.046f), new Vector3(0.084f, 0.032f, 0.092f), 0.012f, Sw.Orange);
                k.Box(new Vector3(0.004f, -0.0165f, 0.05f), new Vector3(0.07f, 0.005f, 0.068f), 0.004f, Sw.Graphite);
                k.Box(new Vector3(0, 0.0165f, 0.076f), new Vector3(0.078f, 0.006f, 0.02f), 0.003f, Sw.Graphite);
                foreach (float x in FingerX) k.Rivet(new Vector3(x, 0.0195f, 0.076f), Vector3.zero, 0.0028f);
                k.Box(new Vector3(0.006f, 0.0168f, 0.036f), new Vector3(0.04f, 0.004f, 0.03f), 0.002f, Sw.Cream);
                k.Box(new Vector3(-0.028f, -0.006f, 0.026f), new Vector3(0.03f, 0.026f, 0.042f), 0.01f, Sw.Orange, new Vector3(0, -30, 0));
                // Puño del guante
                k.Box(new Vector3(0, 0, -0.026f), new Vector3(0.09f, 0.05f, 0.05f), 0.016f, Sw.Cream);
                k.Box(new Vector3(0, 0, -0.008f), new Vector3(0.093f, 0.053f, 0.012f), 0.016f, Sw.Graphite);
                k.Stripes(new Vector3(0, 0.0252f, -0.032f), new Vector3(90, 0, 0), 0.056f, 0.018f, 7, Sw.HazardYellow, Sw.Graphite);
                return;
            }
            if (bone.EndsWith("_Tip")) return;

            bool thumb = bone.StartsWith("Thumb");
            int seg = bone[bone.Length - 1] - '1';
            float len, r;
            if (thumb) { len = ThumbLen[seg]; r = seg == 0 ? 0.012f : seg == 1 ? 0.0105f : 0.0095f; }
            else
            {
                int f = System.Array.IndexOf(Fingers, bone.Substring(0, bone.Length - 1));
                len = FingerLen[f][seg]; r = FingerR[f];
            }
            bool tipSeg = seg == 2;
            Sw color = tipSeg ? Sw.Graphite : Sw.Orange;
            if (tipSeg)
            {
                // Yema de goma redondeada: caja corta + media esfera en la punta
                k.Box(new Vector3(0, 0, (len - r) * 0.5f), new Vector3(2 * r, 1.9f * r, len - r), r * 0.35f, color);
                k.Lathe(new[] { new Vector2(0, 0), new Vector2(r, 0), new Vector2(r * 0.87f, r * 0.5f), new Vector2(r * 0.5f, r * 0.87f), new Vector2(0, r * 0.95f) },
                        Matrix4x4.TRS(new Vector3(0, 0, len - r), Quaternion.Euler(90, 0, 0), new Vector3(1f, 1f, 0.95f)), color, 10);
            }
            else k.Box(new Vector3(0, 0, len * 0.5f), new Vector3(2 * r, 1.9f * r, len - 0.002f), r * 0.4f, color);
            // Nudillo esférico (cubre la unión al flexionar)
            k.Sphere(Vector3.zero, r * 0.98f, seg == 0 ? Sw.Orange : Sw.GraphiteLight, 12, 6);
            // Placa de refuerzo en el dorso de la falange proximal
            if (seg == 0 && !thumb) k.Box(new Vector3(0, r * 0.95f, len * 0.5f), new Vector3(r * 1.5f, 0.003f, len * 0.55f), 0.001f, Sw.Graphite);
        }

        // ═════════════════════════ CLIPS ═════════════════════════

        static Dictionary<string, AnimationClip> BuildClips(Transform root, List<Bone> bones, bool left)
        {
            var clips = new Dictionary<string, AnimationClip>();
            var animated = bones.Where(b => b.name != "Wrist" && !b.name.EndsWith("_Tip")).ToList();
            string side = left ? "L" : "R", dir = AnimDir + (left ? "/Left" : "/Right");

            Quaternion PoseRot(Bone b, Pose p, Vector3 extra = default)
            {
                Vector3 e;
                if (b.name.StartsWith("Thumb"))
                {
                    int s = b.name[5] - '1';
                    e = new Vector3(p.thumb[s], 0, 0);
                    if (s == 0) e += p.thumbTwist;
                }
                else
                {
                    int f = System.Array.IndexOf(Fingers, b.name.Substring(0, b.name.Length - 1));
                    int s = b.name[b.name.Length - 1] - '1';
                    e = new Vector3(p.Finger(f)[s], 0, 0);
                }
                var q = b.restRight * Quaternion.Euler(e + extra);
                return left ? Mirror(q) : q;
            }

            // Poses (estáticas, en bucle)
            foreach (var kv in Poses)
            {
                var clip = NewClip($"Hand_{side}_{kv.Key}", true);
                foreach (var b in animated)
                {
                    var q = PoseRot(b, kv.Value);
                    SetRot(clip, Path(root, b.t), new[] { 0f, 1f / 30f }, new[] { q, q });
                }
                clips[kv.Key] = SaveAsset(clip, $"{dir}/Hand_{side}_{kv.Key}.anim");
            }

            // Transiciones explícitas abrir/cerrar (0.2 s) para quien prefiera no usar el blend
            foreach (var (name, from, to) in new[] { ("Close", "Open", "Grab"), ("Release", "Grab", "Open") })
            {
                var clip = NewClip($"Hand_{side}_{name}", false);
                foreach (var b in animated)
                    SetRot(clip, Path(root, b.t), new[] { 0f, 0.2f }, new[] { PoseRot(b, Poses[from]), PoseRot(b, Poses[to]) });
                clips[name] = SaveAsset(clip, $"{dir}/Hand_{side}_{name}.anim");
            }

            // Acciones ADITIVAS: el primer fotograma es la referencia (= reposo), el resto añade flexión.
            var zero = new Pose();
            AnimationClip Action(string name, bool loop, float[] times, System.Func<Bone, int, Vector3> delta)
            {
                var clip = NewClip($"Hand_{side}_{name}", loop);
                foreach (var b in animated)
                {
                    var qs = new Quaternion[times.Length];
                    for (int i = 0; i < times.Length; i++) qs[i] = PoseRot(b, zero, delta(b, i));
                    SetRot(clip, Path(root, b.t), times, qs);
                }
                return clips[name] = SaveAsset(clip, $"{dir}/Hand_{side}_{name}.anim");
            }
            bool Prox(Bone b) => b.name.EndsWith("1");
            bool Mid(Bone b) => b.name.EndsWith("2");
            bool Index(Bone b) => b.name.StartsWith("Index");
            bool Thumb(Bone b) => b.name.StartsWith("Thumb");

            Action("ActionIdle", true, new[] { 0f, 1f / 30f }, (b, i) => Vector3.zero);
            // Apretón al golpear con el martillo (0.3 s)
            Action("Strike", false, new[] { 0f, 0.07f, 0.3f }, (b, i) => i == 1 && !Thumb(b) ? new Vector3(Prox(b) ? 12 : 8, 0, 0) : Vector3.zero);
            // Pulsar un botón: el índice empuja (0.15 s)
            Action("PressTap", false, new[] { 0f, 0.06f, 0.15f }, (b, i) => i == 1 && Index(b) ? new Vector3(Prox(b) ? 22 : Mid(b) ? 18 : 8, 0, 0) : Vector3.zero);
            // Giro del destornillador: pulgar e índice ruedan alternando (bucle 0.5 s)
            Action("Twist", true, new[] { 0f, 0.125f, 0.25f, 0.375f, 0.5f }, (b, i) =>
            {
                float s = i == 1 ? 1f : i == 3 ? -1f : 0f;
                if (Thumb(b) && b.name == "Thumb1") return new Vector3(0, 10 * s, 0);
                if (Index(b) && Prox(b)) return new Vector3(8 * s, 0, 0);
                return Vector3.zero;
            });
            // Inspección con la lupa: ajuste sutil de índice y pulgar (bucle 2 s)
            Action("InspectLoop", true, new[] { 0f, 0.5f, 1f, 1.5f, 2f }, (b, i) =>
            {
                float s = i == 1 ? 1f : i == 3 ? 0.5f : 0f;
                if (Index(b)) return new Vector3(14 * s * (Prox(b) ? 1f : 0.6f), 0, 0);
                if (b.name == "Thumb2") return new Vector3(8 * s, 0, 0);
                return Vector3.zero;
            });
            return clips;
        }

        static string Path(Transform root, Transform t) => AnimationUtility.CalculateTransformPath(t, root);

        static AnimationClip NewClip(string name, bool loop)
        {
            var clip = new AnimationClip { name = name, frameRate = 60 };
            var s = AnimationUtility.GetAnimationClipSettings(clip);
            s.loopTime = loop;
            AnimationUtility.SetAnimationClipSettings(clip, s);
            return clip;
        }

        static void SetRot(AnimationClip clip, string path, float[] times, Quaternion[] qs)
        {
            for (int i = 1; i < qs.Length; i++) if (Quaternion.Dot(qs[i - 1], qs[i]) < 0f) qs[i] = new Quaternion(-qs[i].x, -qs[i].y, -qs[i].z, -qs[i].w);
            string[] props = { "m_LocalRotation.x", "m_LocalRotation.y", "m_LocalRotation.z", "m_LocalRotation.w" };
            for (int c = 0; c < 4; c++)
            {
                var keys = new Keyframe[times.Length];
                for (int i = 0; i < times.Length; i++) keys[i] = new Keyframe(times[i], qs[i][c]);
                var curve = new AnimationCurve(keys);
                for (int i = 0; i < keys.Length; i++)
                {
                    AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.ClampedAuto);
                    AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.ClampedAuto);
                }
                AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, typeof(Transform), props[c]), curve);
            }
        }

        /// Guarda conservando GUID/fileID si el asset ya existe.
        static T SaveAsset<T>(T obj, string path) where T : Object
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing == null) { AssetDatabase.CreateAsset(obj, path); return obj; }
            EditorUtility.CopySerialized(obj, existing);
            existing.name = System.IO.Path.GetFileNameWithoutExtension(path);
            EditorUtility.SetDirty(existing);
            return existing;
        }

        // ═════════════════════════ ANIMATOR ═════════════════════════

        static AnimatorController BuildController(Dictionary<string, AnimationClip> c)
        {
            string path = AnimDir + "/Hand_R.controller";
            AssetDatabase.DeleteAsset(path);
            var ctrl = AnimatorController.CreateAnimatorControllerAtPath(path);
            ctrl.AddParameter("Grip", AnimatorControllerParameterType.Float);
            ctrl.AddParameter("Trigger", AnimatorControllerParameterType.Float);
            ctrl.AddParameter("Pose", AnimatorControllerParameterType.Int);
            ctrl.AddParameter("Strike", AnimatorControllerParameterType.Trigger);
            ctrl.AddParameter("Press", AnimatorControllerParameterType.Trigger);
            ctrl.AddParameter("Twist", AnimatorControllerParameterType.Bool);
            ctrl.AddParameter("Inspect", AnimatorControllerParameterType.Bool);

            // Capa 0: Base (blend 2D grip × gatillo)
            var baseState = ctrl.CreateBlendTreeInController("Natural", out var tree, 0);
            tree.blendType = BlendTreeType.FreeformCartesian2D;
            tree.blendParameter = "Grip";
            tree.blendParameterY = "Trigger";
            tree.AddChild(c["Open"], new Vector2(0, 0));
            tree.AddChild(c["Point"], new Vector2(1, 0));
            tree.AddChild(c["IndexCurl"], new Vector2(0, 1));
            tree.AddChild(c["Fist"], new Vector2(1, 1));
            baseState.writeDefaultValues = false;
            ctrl.layers[0].name = "Base";

            // Capa 1: Hold (override, peso controlado por HandPoseDriver)
            ctrl.AddLayer("Hold");
            var hold = ctrl.layers[1].stateMachine;
            var holdStates = new (string clip, int pose)[] { ("Grab", 1), ("HoldHammer", 2), ("HoldScrewdriver", 3), ("HoldMagnifier", 4) };
            foreach (var (clip, pose) in holdStates)
            {
                var st = hold.AddState(clip);
                st.motion = c[clip];
                st.writeDefaultValues = false;
                var tr = hold.AddAnyStateTransition(st);
                tr.AddCondition(AnimatorConditionMode.Equals, pose, "Pose");
                tr.duration = 0.12f; tr.hasExitTime = false; tr.canTransitionToSelf = false;
                if (pose == 1) hold.defaultState = st;
            }

            // Capa 2: Action (aditiva)
            ctrl.AddLayer("Action");
            var act = ctrl.layers[2].stateMachine;
            var idle = act.AddState("Idle"); idle.motion = c["ActionIdle"]; idle.writeDefaultValues = false;
            act.defaultState = idle;
            void OneShot(string state, string clip, string trigger)
            {
                var st = act.AddState(state); st.motion = c[clip]; st.writeDefaultValues = false;
                var tin = act.AddAnyStateTransition(st);
                tin.AddCondition(AnimatorConditionMode.If, 0, trigger);
                tin.duration = 0.02f; tin.hasExitTime = false; tin.canTransitionToSelf = true;
                var tout = st.AddTransition(idle); tout.hasExitTime = true; tout.exitTime = 1f; tout.duration = 0.05f;
            }
            void Loop(string state, string clip, string flag)
            {
                var st = act.AddState(state); st.motion = c[clip]; st.writeDefaultValues = false;
                var tin = idle.AddTransition(st); tin.AddCondition(AnimatorConditionMode.If, 0, flag); tin.duration = 0.1f; tin.hasExitTime = false;
                var tout = st.AddTransition(idle); tout.AddCondition(AnimatorConditionMode.IfNot, 0, flag); tout.duration = 0.15f; tout.hasExitTime = false;
            }
            OneShot("Strike", "Strike", "Strike");
            OneShot("Press", "PressTap", "Press");
            Loop("Twist", "Twist", "Twist");
            Loop("Inspect", "InspectLoop", "Inspect");

            var layers = ctrl.layers;
            layers[0].name = "Base";
            layers[1].blendingMode = AnimatorLayerBlendingMode.Override; layers[1].defaultWeight = 0f;
            layers[2].blendingMode = AnimatorLayerBlendingMode.Additive; layers[2].defaultWeight = 1f;
            ctrl.layers = layers;
            EditorUtility.SetDirty(ctrl);
            return ctrl;
        }

        // ═════════════════════════ PREFAB ═════════════════════════

        static void SaveHandPrefab(GameObject root, RuntimeAnimatorController ctrl, bool left)
        {
            var anim = root.AddComponent<Animator>();
            anim.runtimeAnimatorController = ctrl;
            anim.applyRootMotion = false;
            anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            var driver = root.AddComponent<HandPoseDriver>();

            var so = new SerializedObject(driver);
            string map = left ? "XRI Left Interaction" : "XRI Right Interaction";
            var refs = AssetDatabase.LoadAllAssetsAtPath(InputPath).OfType<InputActionReference>().ToList();
            void Bind(string field, string action)
            {
                var r = refs.FirstOrDefault(x => x.action != null && x.action.actionMap.name == map && x.action.name == action);
                if (r == null) { Debug.LogWarning($"[QCXR Art] No se encontró {map}/{action}"); return; }
                var p = so.FindProperty(field);
                p.FindPropertyRelative("m_UseReference").boolValue = true;
                p.FindPropertyRelative("m_Reference").objectReferenceValue = r;
            }
            Bind("gripInput", "Select Value");
            Bind("triggerInput", "Activate Value");
            so.ApplyModifiedPropertiesWithoutUndo();

            // Pose de reposo visible en el editor
            var openClip = AssetDatabase.LoadAssetAtPath<AnimationClip>($"{AnimDir}/{(left ? "Left" : "Right")}/Hand_{(left ? "L" : "R")}_Open.anim");
            if (openClip != null) openClip.SampleAnimation(root, 0f);

            PrefabUtility.SaveAsPrefabAsset(root, $"{PrefabDir}/{root.name}.prefab");
            Object.DestroyImmediate(root);
        }
    }
}
