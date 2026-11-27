using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using QCXR.Visual;
using Object = UnityEngine.Object;

namespace QCXR.EditorTools
{
    /// Menú QCXR > Escena > Reparar referencias del arte + botones con el dedo.
    /// Vuelve a enlazar, POR NOMBRE y sobre la escena tal como está (sin regenerarla), las referencias que apuntan
    /// DENTRO de prefabs de arte (luces de estantes, cubo, banda, efectos, tapas de botón). Si un prefab se regeneró,
    /// sus componentes internos cambian de identificador y la escena pierde esas referencias; esto las recupera.
    /// También añade el XRPokeFilter a los botones (pulsar con el dedo). Guarda la escena una sola vez.
    public static class QCXRSceneRelink
    {
        const string ScenePath = "Assets/_Project/Scenes/QCXR_MainScene.unity";

        static IEnumerable<T> All<T>(Scene s) where T : Component => s.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<T>(true));
        static Transform Named(Scene s, string n) => s.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).FirstOrDefault(t => t.name == n);

        [MenuItem("QCXR/Escena/Reparar referencias del arte + botones con el dedo")]
        public static int Run()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return -1;
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            int before = CountBroken(scene, out var brokenBefore);
            Debug.Log($"[QCXR Relink] Referencias rotas antes: {before}" + (before > 0 ? "\n  " + string.Join("\n  ", brokenBefore) : ""));

            // ── Banda ──
            var belt = Named(scene, "Env_ConveyorBelt");
            var conveyor = All<ConveyorBelt>(scene).FirstOrDefault();
            if (belt != null && conveyor != null)
            {
                var so = new SerializedObject(conveyor);
                so.FindProperty("beltRenderer").objectReferenceValue = belt.Find("BeltSurface")?.GetComponent<MeshRenderer>();
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            // ── Puente de presentación ──
            var bridge = All<PresentationBridge>(scene).FirstOrDefault();
            if (bridge != null)
            {
                var so = new SerializedObject(bridge);
                if (belt != null) so.FindProperty("beltAnimator").objectReferenceValue = belt.GetComponent<Animator>();
                var rack = Named(scene, "Env_ShelfRack");
                var targets = rack != null ? rack.GetComponentsInChildren<Animator>(true).Where(a => a.name == "BayLight").ToList() : new List<Animator>();
                var ring = Named(scene, "Env_DiscardBin")?.Find("FlashRing")?.GetComponent<Animator>();
                if (ring != null) targets.Add(ring);
                var arr = so.FindProperty("flashTargets");
                arr.arraySize = targets.Count;
                for (int i = 0; i < targets.Count; i++) arr.GetArrayElementAtIndex(i).objectReferenceValue = targets[i];
                void Ps(string field, string obj) { var t = Named(scene, obj); if (t != null) so.FindProperty(field).objectReferenceValue = t.GetComponent<ParticleSystem>(); }
                Ps("repairFlash", "FX_RepairFlash"); Ps("hammerDust", "FX_HammerDust"); Ps("confetti", "FX_Confetti");
                void An(string field, string obj) { var t = Named(scene, obj); if (t != null) so.FindProperty(field).objectReferenceValue = t.GetComponent<Animator>(); }
                An("hammerAnimator", "Tool_Hammer"); An("screwdriverAnimator", "Tool_Screwdriver"); An("magnifierAnimator", "Tool_Magnifier");
                so.ApplyModifiedPropertiesWithoutUndo();
                Debug.Log($"[QCXR Relink] PresentationBridge: {targets.Count} destinos de destello (5 estantes + cubo).");
            }

            // ── Botones: tapa animada en onPressed + pulsables con el dedo ──
            var field = typeof(XRButton).GetField("onPressed", BindingFlags.NonPublic | BindingFlags.Instance);
            foreach (var xr in All<XRButton>(scene))
            {
                var cap = xr.transform.Find("Cap")?.GetComponent<Animator>();
                var evt = (UnityEvent)field.GetValue(xr);
                if (evt == null) { evt = new UnityEvent(); field.SetValue(xr, evt); }
                for (int i = evt.GetPersistentEventCount() - 1; i >= 0; i--)
                {
                    var target = evt.GetPersistentTarget(i);
                    if (target == null || target == cap || (target is Animator && evt.GetPersistentMethodName(i) == "SetTrigger"))
                        UnityEventTools.RemovePersistentListener(evt, i);
                }
                if (cap != null) UnityEventTools.AddStringPersistentListener(evt, new UnityAction<string>(cap.SetTrigger), "Press");
                EditorUtility.SetDirty(xr);
                QCXRButtonPokeSetup.Configure(xr.gameObject);
                Debug.Log($"[QCXR Relink] {xr.name}: tapa {(cap != null ? "enlazada" : "NO encontrada")} · XRPokeFilter (pulsar con el dedo)");
            }

            int after = CountBroken(scene, out var brokenAfter);
            Debug.Log($"[QCXR Relink] Referencias rotas después: {after}" + (after > 0 ? "\n  " + string.Join("\n  ", brokenAfter) : ""));
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            return after;
        }

        /// Referencia rota = hay un identificador guardado pero el objeto no se puede resolver.
        public static int CountBroken(Scene scene, out List<string> where)
        {
            where = new List<string>();
            foreach (var c in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Component>(true)))
            {
                if (c == null) continue;
                var it = new SerializedObject(c).GetIterator();
                while (it.NextVisible(true))
                {
                    if (it.propertyType != SerializedPropertyType.ObjectReference || it.objectReferenceValue != null) continue;
                    object id = it.objectReferenceEntityIdValue;
                    if (!id.Equals(Activator.CreateInstance(id.GetType()))) where.Add($"{c.gameObject.name}.{c.GetType().Name}.{it.propertyPath}");
                }
            }
            return where.Count;
        }

        public static void BatchRun()
        {
            int code = 0;
            try { int broken = Run(); if (broken != 0) code = 1; }
            catch (Exception e) { Debug.LogException(e); code = 1; }
            Debug.Log("[QCXR Art] Batch terminado con código " + code);
            if (Application.isBatchMode) EditorApplication.Exit(code);
        }
    }
}
