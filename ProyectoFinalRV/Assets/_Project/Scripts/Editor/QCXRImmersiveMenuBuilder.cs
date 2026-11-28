using System;
using System.Collections.Generic;
using TMPro;
using QCXR.Menu;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace QCXR.EditorTools
{
    /// Genera una escena de menú inmersivo inspirada en la Opción 3.
    /// Prioriza que quede funcional en VR con botones reales y cambio de escena.
    public static class QCXRImmersiveMenuBuilder
    {
        private const string ScenePath = "Assets/_Project/Scenes/QCXR_Menu.unity";
        private const string MainScenePath = "Assets/_Project/Scenes/QCXR_MainScene.unity";
        private const string EnvPrefabDir = "Assets/_Project/Prefabs/Environment/";

        private static readonly Color Orange = new Color(0.92f, 0.45f, 0.12f);
        private static readonly Color Dark = new Color(0.12f, 0.14f, 0.16f);
        private static readonly Color Light = new Color(0.92f, 0.92f, 0.9f);
        private static readonly Color HoloGreen = new Color(0.28f, 0.95f, 0.82f, 0.85f);
        private static readonly Color DangerRed = new Color(1f, 0.42f, 0.38f, 0.9f);
        private static readonly Color StatusGreen = new Color(0.45f, 1f, 0.55f);

        private class MenuRefs
        {
            public Button startButton;
            public Button exitButton;
            public TMP_Text statusText;
            public Transform ringRoot;
            public readonly List<Transform> animatedObjects = new();
        }

        [MenuItem("QCXR/Menú VR inmersivo/1. Crear o reconstruir menú Opción 3")]
        public static void BuildMenuScene()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            QCXRPrefabBuilder.EnsureFolder("Assets/_Project/Scenes");

            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            scene.name = "QCXR_Menu";

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.52f, 0.54f, 0.58f);
            RenderSettings.ambientEquatorColor = new Color(0.35f, 0.34f, 0.32f);
            RenderSettings.ambientGroundColor = new Color(0.18f, 0.18f, 0.18f);

            var root = new GameObject("QCXR_MenuRoot").transform;
            var environment = Group("Environment", root);
            var props = Group("Props", root);
            var ui = Group("UI", root);
            var logic = Group("Logic", root);

            CleanupDefaultCamera();
            SetupLights();
            BuildBaseEnvironment(environment);
            var refs = BuildMenuComposition(props, ui);

            var xrRoot = EnsureXRRig(new Vector3(0f, 0f, -2.15f));
            EnsureEventSystem();
            BuildController(logic, refs, xrRoot);
            AddBuildScenes();

            EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), ScenePath);
            Selection.activeTransform = root;

            EditorUtility.DisplayDialog(
                "QCXR · Menú inmersivo",
                "Listo. Se creó la escena Assets/_Project/Scenes/QCXR_Menu.unity\n\n" +
                "Siguiente paso:\n" +
                "1. Abre QCXR_Menu si no quedó abierta.\n" +
                "2. Dale Play y prueba los botones con el XR Device Simulator o el Quest.\n" +
                "3. Verifica que 'Iniciar inspección' cargue QCXR_MainScene.\n" +
                "4. Si quieres, ajusta textos y posiciones directamente en el Inspector.",
                "OK"
            );
        }

        [MenuItem("QCXR/Menú VR inmersivo/2. Abrir escena del menú")]
        public static void OpenMenuScene()
        {
            if (!System.IO.File.Exists(ScenePath))
            {
                EditorUtility.DisplayDialog("QCXR", "Aún no existe QCXR_Menu.unity. Primero ejecuta:\nQCXR > Menú VR inmersivo > 1. Crear o reconstruir menú Opción 3", "OK");
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        private static void CleanupDefaultCamera()
        {
            var cam = Camera.main;
            if (cam != null) UnityEngine.Object.DestroyImmediate(cam.gameObject);
        }

        private static void SetupLights()
        {
            var sun = GameObject.Find("Directional Light");
            if (sun == null)
            {
                sun = new GameObject("Directional Light");
                sun.AddComponent<Light>();
            }

            var light = sun.GetComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.18f;
            light.color = new Color(1f, 0.95f, 0.88f);
            light.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(48f, -28f, 0f);

            var fill = new GameObject("Warm Fill Light");
            var fillLight = fill.AddComponent<Light>();
            fillLight.type = LightType.Point;
            fillLight.intensity = 5.5f;
            fillLight.range = 10f;
            fillLight.color = new Color(1f, 0.62f, 0.28f);
            fill.transform.position = new Vector3(0f, 3f, -0.5f);
        }

        private static void BuildBaseEnvironment(Transform parent)
        {
            // Si existen prefabs del proyecto, los usamos para mantener la misma estética.
            var roomPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(EnvPrefabDir + "Env_Room.prefab");
            var beltPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(EnvPrefabDir + "Env_ConveyorBelt.prefab");
            var consolePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(EnvPrefabDir + "Env_ButtonConsole.prefab");

            if (roomPrefab != null)
            {
                var room = (GameObject)PrefabUtility.InstantiatePrefab(roomPrefab, parent);
                room.name = "Room";
                room.transform.position = Vector3.zero;
            }
            else
            {
                BuildFallbackRoom(parent);
            }

            if (beltPrefab != null)
            {
                var belt = (GameObject)PrefabUtility.InstantiatePrefab(beltPrefab, parent);
                belt.name = "Conveyor";
                belt.transform.position = new Vector3(0f, 0f, 0.75f);
            }
            else
            {
                BuildFallbackConveyor(parent);
            }

            // consola decorativa a la derecha (como la imagen)
            if (consolePrefab != null)
            {
                var console = (GameObject)PrefabUtility.InstantiatePrefab(consolePrefab, parent);
                console.name = "RightConsole";
                console.transform.position = new Vector3(1.95f, 0f, -0.42f);
                console.transform.rotation = Quaternion.Euler(0f, -26f, 0f);
            }
            else
            {
                Box("RightConsole", parent, new Vector3(1.95f, 0.42f, -0.42f), new Vector3(0.5f, 0.84f, 0.45f), new Color(0.28f, 0.28f, 0.3f), true);
            }

            BuildRightPlatform(parent);
            BuildWallSigns(parent);
            BuildCeilingPipes(parent);
            BuildDecorBoxes(parent);
            BuildFloorGrates(parent);
        }

        private static MenuRefs BuildMenuComposition(Transform props, Transform ui)
        {
            var refs = new MenuRefs();

            BuildTitleRig(props, refs);
            BuildRobots(props, refs);
            refs.statusText = BuildStatusSign(ui, new Vector3(0.35f, 1.55f, 1.4f), Quaternion.identity, StatusGreen, "ESTACIÓN\nPREPARADA", new Vector2(380f, 120f));
            refs.startButton = BuildStartButton(ui, props, refs);
            refs.exitButton = BuildExitButton(ui, props, refs);

            return refs;
        }

        private static void BuildTitleRig(Transform parent, MenuRefs refs)
        {
            var titleRoot = new GameObject("TitleRig").transform;
            titleRoot.SetParent(parent, false);
            titleRoot.position = new Vector3(0.15f, 2.25f, 1.1f);

            // aro holográfico
            var ringRoot = new GameObject("TitleRing").transform;
            ringRoot.SetParent(titleRoot, false);
            refs.ringRoot = ringRoot;

            const int segments = 14;
            float radiusX = 1.18f;
            float radiusZ = 0.38f;
            for (int i = 0; i < segments; i++)
            {
                float a = i / (float)segments * Mathf.PI * 2f;
                var seg = Box("RingSeg_" + i, ringRoot,
                    new Vector3(Mathf.Cos(a) * radiusX, 0f, Mathf.Sin(a) * radiusZ),
                    new Vector3(0.22f, 0.03f, 0.05f),
                    i % 2 == 0 ? new Color(0.25f, 0.75f, 1f) : Orange,
                    false);
                seg.transform.localRotation = Quaternion.Euler(0f, -Mathf.Rad2Deg * a, 0f);
            }
            ringRoot.gameObject.AddComponent<QCXRMenuFloating>().GetType();
            var ringAnim = ringRoot.GetComponent<QCXRMenuFloating>();
            SetFloating(ringAnim, Vector3.zero, 0f, 0f, Vector3.up, 18f, 0f, 0f);

            var haloTop = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            haloTop.name = "HaloTop";
            haloTop.transform.SetParent(titleRoot, false);
            haloTop.transform.localPosition = new Vector3(0f, 0.18f, 0f);
            haloTop.transform.localScale = new Vector3(0.75f, 0.008f, 0.75f);
            Paint(haloTop, Orange, true);
            UnityEngine.Object.DestroyImmediate(haloTop.GetComponent<Collider>());

            var haloBottom = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            haloBottom.name = "HaloBottom";
            haloBottom.transform.SetParent(titleRoot, false);
            haloBottom.transform.localPosition = new Vector3(0f, -0.18f, 0f);
            haloBottom.transform.localScale = new Vector3(0.52f, 0.008f, 0.52f);
            Paint(haloBottom, new Color(0.25f, 0.75f, 1f), true);
            UnityEngine.Object.DestroyImmediate(haloBottom.GetComponent<Collider>());

            // texto principal
            CreateWorldTMP(titleRoot, "TitleText", "CONTROL DE\nCALIDAD", 4.2f, Light, new Vector3(-0.12f, 0.04f, 0.02f), new Vector2(6.5f, 1.5f), TextAlignmentOptions.CenterGeoAligned);
            CreateWorldTMP(titleRoot, "XRText", "XR", 4.2f, Orange, new Vector3(1.18f, 0.02f, 0.02f), new Vector2(1.2f, 1.0f), TextAlignmentOptions.CenterGeoAligned);
        }

        private static TMP_Text BuildStatusSign(Transform parent, Vector3 pos, Quaternion rot, Color color, string text, Vector2 size)
        {
            var canvas = CreateWorldCanvas("StatusCanvas", parent, pos, rot, size, 0.002f);
            var bg = MakePanel(canvas.transform, "Bg", size, new Color(0.06f, 0.09f, 0.1f, 0.85f));
            var outline = bg.gameObject.AddComponent<Outline>();
            outline.effectColor = color;
            outline.effectDistance = new Vector2(2f, 2f);
            var t = CreateTMPUGUI(canvas.transform, "StatusText", text, size, 42f, color, FontStyles.Bold);
            t.alignment = TextAlignmentOptions.Center;
            return t;
        }

        private static Button BuildStartButton(Transform ui, Transform props, MenuRefs refs)
        {
            var pedestal = BuildPedestal(props, "StartPedestal", new Vector3(-0.18f, 0.58f, 0.9f), Orange);
            pedestal.GetComponent<QCXRMenuFloating>();
            refs.animatedObjects.Add(pedestal);

            var projector = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            projector.name = "Projector";
            projector.transform.SetParent(pedestal, false);
            projector.transform.localPosition = new Vector3(0f, 0.22f, 0f);
            projector.transform.localScale = new Vector3(0.18f, 0.025f, 0.18f);
            Paint(projector, new Color(0.12f, 0.18f, 0.2f), true);
            UnityEngine.Object.DestroyImmediate(projector.GetComponent<Collider>());

            var beam = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            beam.name = "HoloBeam";
            beam.transform.SetParent(pedestal, false);
            beam.transform.localPosition = new Vector3(0f, 0.63f, 0f);
            beam.transform.localScale = new Vector3(0.22f, 0.2f, 0.22f);
            Paint(beam, new Color(HoloGreen.r, HoloGreen.g, HoloGreen.b, 0.25f), true);
            UnityEngine.Object.DestroyImmediate(beam.GetComponent<Collider>());

            var canvas = CreateWorldCanvas("StartButtonCanvas", ui, new Vector3(-0.18f, 1.33f, 0.92f), Quaternion.identity, new Vector2(620f, 230f), 0.0022f);
            var button = BuildHologramButton(canvas.transform, "Iniciar\nInspección", HoloGreen, new Vector2(560f, 170f));
            refs.animatedObjects.Add(canvas.transform);

            return button;
        }

        private static Button BuildExitButton(Transform ui, Transform props, MenuRefs refs)
        {
            var pedestal = BuildPedestal(props, "ExitPedestal", new Vector3(1.82f, 0.62f, 1.8f), new Color(0.55f, 0.22f, 0.22f));
            refs.animatedObjects.Add(pedestal);

            var beam = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            beam.name = "ExitBeam";
            beam.transform.SetParent(pedestal, false);
            beam.transform.localPosition = new Vector3(0f, 0.43f, 0f);
            beam.transform.localScale = new Vector3(0.16f, 0.16f, 0.16f);
            Paint(beam, new Color(DangerRed.r, DangerRed.g, DangerRed.b, 0.2f), true);
            UnityEngine.Object.DestroyImmediate(beam.GetComponent<Collider>());

            var canvas = CreateWorldCanvas("ExitButtonCanvas", ui, new Vector3(1.82f, 1.36f, 1.8f), Quaternion.Euler(0f, -33f, 0f), new Vector2(420f, 170f), 0.0021f);
            var button = BuildHologramButton(canvas.transform, "SALIR", DangerRed, new Vector2(360f, 110f));
            refs.animatedObjects.Add(canvas.transform);
            return button;
        }

        private static Transform BuildPedestal(Transform parent, string name, Vector3 position, Color accent)
        {
            var root = new GameObject(name).transform;
            root.SetParent(parent, false);
            root.position = position;

            var body = Box("Body", root, position + new Vector3(0f, 0.16f, 0f), new Vector3(0.36f, 0.32f, 0.36f), Dark, true);
            var frame = Box("Frame", root, position + new Vector3(0f, 0.02f, 0f), new Vector3(0.4f, 0.04f, 0.4f), accent, true);
            var basePlate = Box("BasePlate", root, position + new Vector3(0f, 0.32f, 0f), new Vector3(0.28f, 0.04f, 0.28f), accent, false);
            Paint(basePlate, accent, true);

            var anim = root.gameObject.AddComponent<QCXRMenuFloating>();
            SetFloating(anim, Vector3.up, 0.02f, 1.15f, Vector3.zero, 0f, 0.01f, 1.3f);
            return root;
        }

        private static Button BuildHologramButton(Transform parent, string label, Color outlineColor, Vector2 size)
        {
            var frame = MakePanel(parent, "Frame", size, new Color(outlineColor.r, outlineColor.g, outlineColor.b, 0.14f));
            frame.color = new Color(outlineColor.r, outlineColor.g, outlineColor.b, 0.17f);
            var outline = frame.gameObject.AddComponent<Outline>();
            outline.effectColor = outlineColor;
            outline.effectDistance = new Vector2(3f, 3f);

            var button = frame.gameObject.AddComponent<Button>();
            var colors = button.colors;
            colors.normalColor = new Color(1f, 1f, 1f, 1f);
            colors.highlightedColor = new Color(1f, 1f, 1f, 1f);
            colors.pressedColor = new Color(0.9f, 0.9f, 0.9f, 1f);
            colors.selectedColor = new Color(1f, 1f, 1f, 1f);
            colors.fadeDuration = 0.05f;
            button.colors = colors;

            var text = CreateTMPUGUI(parent, "Label", label.ToUpperInvariant(), size, 46f, Color.white, FontStyles.Bold);
            text.alignment = TextAlignmentOptions.Center;

            return button;
        }

        private static void BuildRobots(Transform parent, MenuRefs refs)
        {
            BuildRobot(parent, refs, "RobotLeft", new Vector3(-1.55f, 0.2f, 0.72f), Quaternion.Euler(0f, 35f, 0f));
            BuildRobot(parent, refs, "RobotRight", new Vector3(1.28f, 0.2f, 1.15f), Quaternion.Euler(0f, -20f, 0f));
        }

        private static void BuildRobot(Transform parent, MenuRefs refs, string name, Vector3 pos, Quaternion rot)
        {
            var root = new GameObject(name).transform;
            root.SetParent(parent, false);
            root.position = pos;
            root.rotation = rot;

            Box("Torso", root, pos + new Vector3(0f, 0.55f, 0f), new Vector3(0.22f, 0.5f, 0.18f), Orange, false);
            Box("Core", root, pos + new Vector3(0.02f, 0.56f, -0.02f), new Vector3(0.1f, 0.34f, 0.05f), new Color(0.18f, 0.22f, 0.28f), false);
            Box("Head", root, pos + new Vector3(0f, 0.9f, 0f), new Vector3(0.26f, 0.18f, 0.24f), new Color(0.92f, 0.9f, 0.82f), false);
            Box("Visor", root, pos + new Vector3(0f, 0.84f, -0.03f), new Vector3(0.28f, 0.05f, 0.12f), new Color(0.25f, 0.28f, 0.34f), false);
            BuildLimb(root, pos + new Vector3(-0.09f, 0.28f, 0f), 0.11f);
            BuildLimb(root, pos + new Vector3(0.09f, 0.28f, 0f), -0.11f);

            var anim = root.gameObject.AddComponent<QCXRMenuFloating>();
            SetFloating(anim, Vector3.up, 0.03f, 0.9f, Vector3.zero, 0f, 0f, 0f);
            refs.animatedObjects.Add(root);
        }

        private static void BuildLimb(Transform parent, Vector3 basePos, float xOffset)
        {
            var upper = Box("UpperLimb", parent, basePos + new Vector3(xOffset, 0.15f, 0f), new Vector3(0.08f, 0.2f, 0.08f), Orange, false);
            upper.transform.rotation = Quaternion.Euler(0f, 0f, xOffset > 0 ? -12f : 12f);
            var lower = Box("LowerLimb", parent, basePos + new Vector3(xOffset * 1.15f, -0.02f, 0f), new Vector3(0.07f, 0.22f, 0.07f), new Color(0.16f, 0.18f, 0.22f), false);
            lower.transform.rotation = Quaternion.Euler(0f, 0f, xOffset > 0 ? 8f : -8f);
        }

        private static void BuildController(Transform parent, MenuRefs refs, Transform xrRoot)
        {
            var controllerGo = new GameObject("QCXRMenuController");
            controllerGo.transform.SetParent(parent, false);
            var controller = controllerGo.AddComponent<QCXRMenuController>();
            var so = new SerializedObject(controller);
            so.FindProperty("startButton").objectReferenceValue = refs.startButton;
            so.FindProperty("exitButton").objectReferenceValue = refs.exitButton;
            so.FindProperty("statusText").objectReferenceValue = refs.statusText;
            so.FindProperty("gameplaySceneName").stringValue = "QCXR_MainScene";
            if (xrRoot != null) so.FindProperty("xrOriginRoot").objectReferenceValue = xrRoot;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void BuildFallbackRoom(Transform parent)
        {
            Box("Floor", parent, new Vector3(0f, -0.05f, 0.75f), new Vector3(6.4f, 0.1f, 5.2f), new Color(0.3f, 0.31f, 0.33f), true);
            Box("BackWall", parent, new Vector3(0f, 1.6f, 2.95f), new Vector3(6.4f, 3.2f, 0.1f), new Color(0.76f, 0.74f, 0.7f), false);
            Box("LeftWall", parent, new Vector3(-3.15f, 1.6f, 0.8f), new Vector3(0.1f, 3.2f, 4.3f), new Color(0.7f, 0.68f, 0.64f), false);
            Box("RightWall", parent, new Vector3(3.15f, 1.6f, 0.8f), new Vector3(0.1f, 3.2f, 4.3f), new Color(0.7f, 0.68f, 0.64f), false);
        }

        private static void BuildFallbackConveyor(Transform parent)
        {
            Box("ConveyorBase", parent, new Vector3(0f, 0.55f, 1.05f), new Vector3(3.6f, 0.35f, 0.92f), Orange, true);
            Box("ConveyorTop", parent, new Vector3(0f, 0.78f, 1.05f), new Vector3(3.25f, 0.06f, 0.66f), new Color(0.18f, 0.18f, 0.2f), false);
        }

        private static void BuildRightPlatform(Transform parent)
        {
            Box("PlatformFloor", parent, new Vector3(1.82f, 0.52f, 1.75f), new Vector3(1.3f, 0.08f, 1.0f), new Color(0.33f, 0.34f, 0.37f), true);
            BuildStep(parent, new Vector3(1.58f, 0.15f, 1.25f), new Vector3(0.65f, 0.1f, 0.3f));
            BuildStep(parent, new Vector3(1.72f, 0.26f, 1.45f), new Vector3(0.8f, 0.1f, 0.3f));
            BuildStep(parent, new Vector3(1.82f, 0.37f, 1.65f), new Vector3(1.0f, 0.1f, 0.3f));
            BuildRail(parent, new Vector3(1.35f, 0.62f, 1.55f));
            BuildRail(parent, new Vector3(2.28f, 0.62f, 1.55f));
        }

        private static void BuildStep(Transform parent, Vector3 pos, Vector3 scale)
        {
            var step = Box("Step", parent, pos, scale, new Color(0.28f, 0.29f, 0.32f), true);
            var stripe = Box("StepLight", parent, pos + new Vector3(0f, 0.055f, 0.12f), new Vector3(scale.x * 0.72f, 0.015f, 0.04f), Orange, false);
            Paint(stripe, Orange, true);
        }

        private static void BuildRail(Transform parent, Vector3 pos)
        {
            Box("RailPost", parent, pos + new Vector3(0f, 0.25f, -0.25f), new Vector3(0.05f, 0.5f, 0.05f), Orange, false);
            Box("RailPost", parent, pos + new Vector3(0f, 0.25f, 0.25f), new Vector3(0.05f, 0.5f, 0.05f), Orange, false);
            Box("RailBar", parent, pos + new Vector3(0f, 0.45f, 0f), new Vector3(0.05f, 0.05f, 0.55f), Orange, false);
        }

        private static void BuildWallSigns(Transform parent)
        {
            BuildFlatSign(parent, new Vector3(-2.0f, 1.75f, 2.25f), Quaternion.identity, new Vector2(1.25f, 0.45f), Light, Dark, "CONTROL DE\nCALIDAD");
            BuildFlatSign(parent, new Vector3(2.15f, 1.78f, 2.0f), Quaternion.Euler(0f, -90f, 0f), new Vector2(1.5f, 0.45f), Light, Dark, "LÍNEA DE\nPRODUCCIÓN »»");
        }

        private static void BuildFlatSign(Transform parent, Vector3 pos, Quaternion rot, Vector2 sizeMeters, Color signColor, Color textColor, string text)
        {
            var board = Box("SignBoard", parent, pos, new Vector3(sizeMeters.x, sizeMeters.y, 0.06f), signColor, false);
            board.transform.rotation = rot;
            var canvas = CreateWorldCanvas("SignCanvas", board.transform, Vector3.forward * -0.032f, Quaternion.identity, new Vector2(sizeMeters.x * 300f, sizeMeters.y * 300f), 0.0033f);
            var label = CreateTMPUGUI(canvas.transform, "Label", text, new Vector2(sizeMeters.x * 300f, sizeMeters.y * 300f), 34f, textColor, FontStyles.Bold);
            label.alignment = TextAlignmentOptions.Center;
        }

        private static void BuildCeilingPipes(Transform parent)
        {
            for (int i = 0; i < 3; i++)
            {
                float z = 2.45f - i * 0.23f;
                var pipe = Box("Pipe_" + i, parent, new Vector3(-0.35f + i * 0.35f, 2.42f, z), new Vector3(5.2f, 0.08f, 0.08f), i == 2 ? new Color(0.18f, 0.55f, 0.25f) : new Color(0.4f, 0.42f, 0.46f), false);
                pipe.transform.rotation = Quaternion.Euler(0f, 0f, 0f);
            }

            for (int i = 0; i < 4; i++)
            {
                var beam = Box("Beam_" + i, parent, new Vector3(-2.2f + i * 1.45f, 2.3f, 1.0f), new Vector3(0.14f, 2.6f, 0.14f), Orange, false);
                var lamp = Box("Lamp_" + i, parent, new Vector3(-2.2f + i * 1.45f, 2.35f, 0.1f), new Vector3(0.52f, 0.08f, 0.22f), new Color(0.95f, 0.78f, 0.45f), false);
                Paint(lamp, new Color(1f, 0.75f, 0.35f), true);
            }
        }

        private static void BuildDecorBoxes(Transform parent)
        {
            Box("CrateA", parent, new Vector3(-1.2f, 0.22f, 1.55f), new Vector3(0.34f, 0.24f, 0.34f), new Color(0.66f, 0.48f, 0.28f), true);
            Box("CrateB", parent, new Vector3(-0.85f, 0.16f, 1.7f), new Vector3(0.2f, 0.12f, 0.2f), new Color(0.66f, 0.48f, 0.28f), true);
            Box("CrateC", parent, new Vector3(0.75f, 0.18f, 1.62f), new Vector3(0.24f, 0.16f, 0.24f), new Color(0.66f, 0.48f, 0.28f), true);
        }

        private static void BuildFloorGrates(Transform parent)
        {
            Box("Grate1", parent, new Vector3(0f, 0.01f, -0.35f), new Vector3(0.7f, 0.01f, 0.5f), new Color(0.18f, 0.18f, 0.18f), false);
            Box("Grate2", parent, new Vector3(1.28f, 0.01f, 0.05f), new Vector3(0.6f, 0.01f, 0.4f), new Color(0.18f, 0.18f, 0.18f), false);
        }

        private static Transform EnsureXRRig(Vector3 worldPosition)
        {
            var originType = Type.GetType("Unity.XR.CoreUtils.XROrigin, Unity.XR.CoreUtils");
            Component origin = originType != null ? UnityEngine.Object.FindFirstObjectByType(originType) as Component : null;

            if (origin == null)
            {
                foreach (var guid in AssetDatabase.FindAssets("XR Origin t:Prefab"))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    if (!path.Contains("Starter Assets") || !path.Contains("XR Origin")) continue;
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                    go.transform.position = worldPosition;
                    go.transform.rotation = Quaternion.identity;
                    origin = originType != null ? go.GetComponentInChildren(originType) : go.transform;
                    break;
                }
            }

            Transform root = origin != null ? origin.transform : null;
            if (root != null)
            {
                root.position = worldPosition;
                root.rotation = Quaternion.identity;
            }
            return root;
        }

        private static void EnsureEventSystem()
        {
            if (UnityEngine.Object.FindFirstObjectByType<EventSystem>() != null) return;

            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<XRUIInputModule>();
        }

        private static void AddBuildScenes()
        {
            var scenes = new List<EditorBuildSettingsScene>();
            if (System.IO.File.Exists(ScenePath)) scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
            if (System.IO.File.Exists(MainScenePath)) scenes.Add(new EditorBuildSettingsScene(MainScenePath, true));
            if (scenes.Count > 0) EditorBuildSettings.scenes = scenes.ToArray();
        }

        // ───────────────── helpers ─────────────────
        private static Transform Group(string name, Transform parent)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            return t;
        }

        private static GameObject Box(string name, Transform parent, Vector3 position, Vector3 scale, Color color, bool collider)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            go.transform.localScale = scale;
            if (!collider) UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
            Paint(go, color, true);
            return go;
        }

        private static void Paint(GameObject go, Color color, bool useStandard)
        {
            var renderer = go.GetComponent<Renderer>();
            if (renderer == null) return;

            var material = new Material(Shader.Find(useStandard ? "Standard" : "Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            material.color = color;
            if (material.HasProperty("_EmissionColor"))
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", color * 0.35f);
            }
            renderer.sharedMaterial = material;
        }

        private static Canvas CreateWorldCanvas(string name, Transform parent, Vector3 localPos, Quaternion localRot, Vector2 size, float scale)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = localRot;

            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = size;
            rt.localScale = Vector3.one * scale;

            go.AddComponent<GraphicRaycaster>();
            go.AddComponent<TrackedDeviceGraphicRaycaster>();
            return canvas;
        }

        private static Image MakePanel(Transform parent, string name, Vector2 size, Color color)
        {
            var rt = CreateRect(name, parent, Vector2.zero, size);
            var image = rt.gameObject.AddComponent<Image>();
            image.color = color;
            return image;
        }

        private static TMP_Text CreateTMPUGUI(Transform parent, string name, string text, Vector2 size, float fontSize, Color color, FontStyles style)
        {
            var rt = CreateRect(name, parent, Vector2.zero, size);
            var tmp = rt.gameObject.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.color = color;
            tmp.fontStyle = style;
            tmp.enableAutoSizing = true;
            tmp.fontSizeMin = fontSize * 0.5f;
            tmp.fontSizeMax = fontSize;
            return tmp;
        }

        private static TMP_Text CreateWorldTMP(Transform parent, string name, string text, float fontSize, Color color, Vector3 localPos, Vector2 rectSize, TextAlignmentOptions alignment)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            var tmp = go.AddComponent<TextMeshPro>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.color = color;
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = alignment;
            tmp.rectTransform.sizeDelta = rectSize;
            return tmp;
        }

        private static RectTransform CreateRect(string name, Transform parent, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return rt;
        }

        private static void SetFloating(QCXRMenuFloating floating, Vector3 floatAxis, float floatAmp, float floatSpeed, Vector3 rotAxis, float rotSpeed, float scalePulse, float scaleSpeed)
        {
            if (floating == null) return;
            var so = new SerializedObject(floating);
            so.FindProperty("floatAxis").vector3Value = floatAxis;
            so.FindProperty("floatAmplitude").floatValue = floatAmp;
            so.FindProperty("floatSpeed").floatValue = floatSpeed;
            so.FindProperty("rotationAxis").vector3Value = rotAxis;
            so.FindProperty("rotationSpeed").floatValue = rotSpeed;
            so.FindProperty("scalePulse").floatValue = scalePulse;
            so.FindProperty("scaleSpeed").floatValue = scaleSpeed;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
