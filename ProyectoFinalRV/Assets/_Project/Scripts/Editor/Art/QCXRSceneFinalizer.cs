using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using QCXR.Visual;
using Object = UnityEngine.Object;

namespace QCXR.EditorTools.Art
{
    /// Fase 6 · Prefabs y escena final. Lo llama QCXRSceneAssembler al final del montaje.
    ///  1) Manos VR en los controladores del XR Origin existente (sin sustituirlo).
    ///  2) Jerarquía del brief: GameManager · Environment · Factory · Products · Tools · XR Origin · UI · Effects.
    ///     Solo se MUEVEN y RENOMBRAN objetos: las referencias serializadas son por objeto, no por nombre.
    ///  3) Productos de referencia (EditorOnly, desactivados) colocados en su estante.
    ///  4) QCXR_MainScene como primera escena en Build Settings.
    public static class QCXRSceneFinalizer
    {
        const string HandDir = "Assets/_Project/Prefabs/Hands";

        public static void Finalize(Scene scene)
        {
            // Búsqueda en vivo (incluye objetos creados durante el montaje y desactivados)
            Func<string, Transform> find = n =>
                scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).FirstOrDefault(t => t != null && t.name == n)
                ?? throw new Exception("Fase 6: no existe en la escena: " + n);
            SetupHands(find);
            AddProductReferences(find);   // antes de ordenar la raíz
            Reorganize(scene, find);
            SetBuildScene();
            Debug.Log("[QCXR Art] Fase 6: jerarquía final\n" + DumpHierarchy(scene));
        }

        // ═════════════════════════ MANOS ═════════════════════════

        static void SetupHands(Func<string, Transform> find)
        {
            foreach (var (side, ctrlName) in new[] { ("L", "Left Controller"), ("R", "Right Controller") })
            {
                var ctrl = find(ctrlName);
                // Ocultar el modelo del mando (instancia de "XR Controller Left/Right" con UniversalController)
                foreach (Transform child in ctrl)
                    if (child.GetComponentsInChildren<Transform>(true).Any(t => t.name == "UniversalController"))
                        child.gameObject.SetActive(false);

                var old = ctrl.Find("Hand_" + side);
                if (old != null) Object.DestroyImmediate(old.gameObject);
                var hand = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>($"{HandDir}/Hand_{side}.prefab"), ctrl);
                hand.transform.localPosition = Vector3.zero;
                hand.transform.localRotation = Quaternion.identity;

                var interactors = ctrl.GetComponentsInChildren<XRBaseInteractor>(true)
                    .Where(i => i is NearFarInteractor || i is XRPokeInteractor || i is XRDirectInteractor).ToList();
                var so = new SerializedObject(hand.GetComponent<HandPoseDriver>());
                var arr = so.FindProperty("interactors");
                arr.arraySize = interactors.Count;
                for (int i = 0; i < interactors.Count; i++) arr.GetArrayElementAtIndex(i).objectReferenceValue = interactors[i];
                so.ApplyModifiedPropertiesWithoutUndo();
                Debug.Log($"[QCXR Art] Mano {side}: {interactors.Count} interactores ({string.Join(", ", interactors.Select(x => x.GetType().Name))})");
            }
        }

        // ═════════════════════════ JERARQUÍA ═════════════════════════

        static Transform Group(string name, Transform parent = null)
        {
            var t = new GameObject(name).transform;
            if (parent != null) t.SetParent(parent, false);
            return t;
        }

        static void Move(Transform t, Transform parent, string rename = null)
        {
            if (t == null) return;
            t.SetParent(parent, true);
            if (rename != null) t.name = rename;
        }

        static void Reorganize(Scene scene, Func<string, Transform> find)
        {
            var oldRoot = find("QCXR_Scene");

            // GameManager (raíz): el objeto con GameManager agrupa a los demás sistemas
            var gm = find("GameManager");
            gm.SetParent(null, true);
            foreach (var n in new[] { "Systems", "RandomEvents", "ProductFlow", "Onboarding", "Presentation" }) Move(find(n), gm);

            // Environment
            var env = find("Environment");
            env.SetParent(null, true);
            var lighting = Group("Lighting", env);
            Move(find("Directional Light"), lighting);
            var vol = SceneRoots(scene).FirstOrDefault(t => t.name == "Global Volume");
            Move(vol, lighting);

            // Factory
            var factory = find("Factory");
            factory.SetParent(null, true);
            var belt = Group("ConveyorBelt", factory);
            foreach (var n in new[] { "BeltSurface", "RailFront", "RailBack" }) Move(find(n), belt, n + "_Collider");
            Move(find("BeltZone"), belt);
            Move(find("Env_ConveyorBelt"), belt);
            Move(find("SpawnPoint"), factory, "ProductSpawnPoint");
            Move(find("InspectionPoint"), factory);
            Move(find("BeltEnd"), factory);

            var table = Group("ToolTable", factory);
            Move(find("ToolTable"), table, "ToolTable_Collider");
            Move(find("Env_ToolBench"), table);

            var console = Group("ButtonConsole", factory);
            Move(find("Console"), console, "Console_Collider");
            Move(find("Env_ButtonConsole"), console);
            Move(find("Btn_StartShift"), console);
            Move(find("Btn_ResetBelt"), console);

            var shelves = Group("Shelves", factory);
            Move(find("RackBase"), shelves, "RackBase_Collider");
            Move(find("RackTopBoard"), shelves, "RackTopBoard_Collider");
            Move(find("Env_ShelfRack"), shelves);
            foreach (var p in new[] { "Robot", "Radio", "Lamp", "Toaster", "Clock" }) Move(find("Shelf_" + p), shelves);

            var bin = Group("DiscardBin", factory);
            var station = SceneRoots(scene).SelectMany(r => r.GetComponentsInChildren<ProductStation>(true))
                .FirstOrDefault(s => new SerializedObject(s).FindProperty("kind").enumValueIndex == (int)StationKind.DiscardBin);
            if (station != null) Move(station.transform, bin, "DiscardBin_Trigger");
            foreach (var n in new[] { "BinPedestal", "BinFloor", "BinWallL", "BinWallR", "BinWallF", "BinWallB" }) Move(find(n), bin, n + "_Collider");
            Move(find("Env_DiscardBin"), bin);

            var indicator = Group("EmergencyIndicator", factory);
            Move(find("JamIndicator"), indicator);
            Move(find("EmergencyLight"), indicator);

            // Tools, UI, Effects a la raíz
            find("Tools").SetParent(null, true);
            find("UI").SetParent(null, true);
            find("Effects").SetParent(null, true);

            // Limpieza de grupos vacíos del montaje anterior
            foreach (var n in new[] { "Stations", "Managers" })
            {
                var t = SceneRoots(scene).SelectMany(r => r.GetComponentsInChildren<Transform>(true)).FirstOrDefault(x => x != null && x.name == n);
                if (t != null && t.childCount == 0) Object.DestroyImmediate(t.gameObject);
            }
            if (oldRoot != null && oldRoot.childCount == 0) Object.DestroyImmediate(oldRoot.gameObject);

            // Orden de la raíz como en el brief
            string[] order = { "GameManager", "Environment", "Factory", "Products", "Tools", "XR Origin (XR Rig)", "UI", "Effects", "EventSystem" };
            int idx = 0;
            foreach (var n in order)
            {
                var t = SceneRoots(scene).FirstOrDefault(r => r.name == n);
                if (t != null) t.SetSiblingIndex(idx++);
            }
        }

        static IEnumerable<Transform> SceneRoots(Scene scene) => scene.GetRootGameObjects().Select(g => g.transform);

        // ═════════════════════════ PRODUCTOS DE REFERENCIA ═════════════════════════

        static void AddProductReferences(Func<string, Transform> find)
        {
            var products = Group("Products");
            var note = Group("_LEEME · los productos los instancia ProductFlow en ProductSpawnPoint", products);
            var refs = Group("ReferenceOnShelves (EditorOnly, desactivado)", products);
            refs.gameObject.tag = "EditorOnly";
            foreach (var p in new[] { "Robot", "Radio", "Lamp", "Toaster", "Clock" })
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{QCXRArtBuilder.ProductPrefabDir}/Product_{p}.prefab");
                var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, refs);
                var attach = find("Shelf_" + p).Find("AttachPoint");
                inst.transform.SetPositionAndRotation(attach.position, attach.rotation);
                inst.tag = "EditorOnly";
            }
            refs.gameObject.SetActive(false);   // nunca activos: el socket los puntuaría
        }

        // ═════════════════════════ BUILD SETTINGS ═════════════════════════

        static void SetBuildScene()
        {
            var list = EditorBuildSettings.scenes.Where(s => s.path != QCXRSceneAssembler.ScenePath).ToList();
            list.Insert(0, new EditorBuildSettingsScene(QCXRSceneAssembler.ScenePath, true));
            EditorBuildSettings.scenes = list.ToArray();
        }

        // ═════════════════════════ INFORME ═════════════════════════

        public static string DumpHierarchy(Scene scene, int maxDepth = 2)
        {
            var sb = new StringBuilder();
            void Walk(Transform t, int d)
            {
                sb.Append(new string(' ', d * 2)).Append(t.gameObject.activeSelf ? "" : "(off) ").AppendLine(t.name);
                if (d < maxDepth) foreach (Transform c in t) Walk(c, d + 1);
            }
            foreach (var r in scene.GetRootGameObjects()) Walk(r.transform, 0);
            return sb.ToString();
        }
    }
}
