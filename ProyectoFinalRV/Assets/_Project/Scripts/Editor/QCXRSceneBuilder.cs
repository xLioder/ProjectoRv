using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace QCXR.EditorTools
{
    // Menú QCXR > Construir escena base. Arma toda la escena en la escena abierta.
    // Requisito previo: haber corrido "Tools > QCXR > Construir layer, config y prefabs".
    public static class QCXRSceneBuilder
    {
        const string Data = "Assets/_Project/Data/GameConfig.asset";
        const string ProdDir = "Assets/_Project/Prefabs/Products/Product_";
        const string ToolDir = "Assets/_Project/Prefabs/Tools/Tool_";
        static readonly string[] ProductNames = { "Robot", "Radio", "Lamp", "Toaster", "Clock" };

        const float BeltZ = 0.75f, BeltTop = 0.9f;

        [MenuItem("Tools/QCXR/Construir escena base")]
        public static void Build()
        {
            var config = AssetDatabase.LoadAssetAtPath<GameConfigSO>(Data);
            var products = new Product[5];
            for (int i = 0; i < 5; i++) products[i] = AssetDatabase.LoadAssetAtPath<Product>(ProdDir + ProductNames[i] + ".prefab");
            var hammer = AssetDatabase.LoadAssetAtPath<GameObject>(ToolDir + "Hammer.prefab");
            var driver = AssetDatabase.LoadAssetAtPath<GameObject>(ToolDir + "Screwdriver.prefab");
            var lupa = AssetDatabase.LoadAssetAtPath<GameObject>(ToolDir + "Magnifier.prefab");

            bool missing = config == null || hammer == null || driver == null || lupa == null;
            foreach (var p in products) if (p == null) missing = true;
            if (missing)
            {
                EditorUtility.DisplayDialog("QCXR", "Faltan prefabs o GameConfig.\nPrimero ejecuta: Tools > QCXR > Construir layer, config y prefabs.", "OK");
                return;
            }

            var old = GameObject.Find("QCXR_Scene");
            if (old != null)
            {
                if (!EditorUtility.DisplayDialog("QCXR", "Ya existe QCXR_Scene. ¿Borrarla y reconstruir?", "Reconstruir", "Cancelar")) return;
                Object.DestroyImmediate(old);
            }

            var root = new GameObject("QCXR_Scene").transform;
            var env = Group("Environment", root);
            var fac = Group("Factory", root);
            var stations = Group("Stations", root);
            var tools = Group("Tools", root);
            var mgr = Group("Managers", root);
            var ui = Group("UI", root);

            BuildEnvironment(env);
            var refs = BuildFactory(fac, config, products);
            BuildToolTable(env, tools, hammer, driver, lupa);
            BuildShelves(stations);
            BuildBin(stations);
            var buttons = BuildButtons(stations);
            BuildManagers(mgr, config, refs, buttons.resetButton);
            BuildUI(ui);
            EnsureXRRigAndEventSystem(mgr);

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Selection.activeTransform = root;
            EditorUtility.DisplayDialog("QCXR",
                "Escena base lista.\n\n• Guarda la escena (Ctrl+S).\n• Si no había XR Origin, agrega uno (Starter Assets) en 0,0,0.\n• Dale Play (o Build & Run al Quest).", "OK");
        }

        // Entorno
        static void BuildEnvironment(Transform env)
        {
            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Floor"; floor.transform.SetParent(env, false);
            floor.transform.localScale = new Vector3(0.6f, 1, 0.6f);
            floor.GetComponent<MeshRenderer>().sharedMaterial = QCXRPrefabBuilder.Mat(new Color(0.35f, 0.37f, 0.4f), false);
        }

        // Banda
        class FactoryRefs { public Transform spawn; public Product[] products; }

        static FactoryRefs BuildFactory(Transform fac, GameConfigSO config, Product[] products)
        {
            Color dark = new Color(0.15f, 0.15f, 0.17f);
            Box("BeltSurface", fac, new Vector3(0, 0.85f, BeltZ), new Vector3(3.4f, 0.1f, 0.5f), dark, true);
            Box("RailFront", fac, new Vector3(0, 0.93f, BeltZ - 0.265f), new Vector3(3.4f, 0.06f, 0.03f), Color.gray, true);
            Box("RailBack", fac, new Vector3(0, 0.93f, BeltZ + 0.265f), new Vector3(3.4f, 0.06f, 0.03f), Color.gray, true);
            foreach (float x in new[] { -1.6f, 1.6f })
                foreach (float z in new[] { BeltZ - 0.2f, BeltZ + 0.2f })
                    Box("Leg", fac, new Vector3(x, 0.425f, z), new Vector3(0.08f, 0.85f, 0.08f), Color.gray, false);
            Box("InspectionMark", fac, new Vector3(0, 0.903f, BeltZ), new Vector3(0.32f, 0.004f, 0.46f), new Color(0.2f, 0.85f, 0.4f), false);

            var zone = new GameObject("BeltZone");
            zone.transform.SetParent(fac, false);
            zone.transform.position = new Vector3(0, 1.0f, BeltZ);
            var bc = zone.AddComponent<BoxCollider>();
            bc.isTrigger = true; bc.size = new Vector3(3.4f, 0.2f, 0.46f);

            var dir = Empty("Direction", zone.transform, Vector3.zero);
            dir.rotation = Quaternion.Euler(0, -90, 0);                       // la banda avanza hacia -X
            var insp = Empty("InspectionPoint", fac, new Vector3(0, 0.95f, BeltZ));
            var end = Empty("BeltEnd", fac, new Vector3(-1.65f, 0.95f, BeltZ));
            var spawn = Empty("SpawnPoint", fac, new Vector3(1.55f, 1.0f, BeltZ));

            var belt = zone.AddComponent<ConveyorBelt>();
            var so = new SerializedObject(belt);
            Ref(so, "config", config);
            Ref(so, "moveDirection", dir);
            Ref(so, "inspectionPoint", insp);
            Ref(so, "beltEnd", end);
            so.FindProperty("floorY").floatValue = 0.5f;
            so.ApplyModifiedPropertiesWithoutUndo();

            return new FactoryRefs { spawn = spawn, products = products };
        }

        static void BuildToolTable(Transform env, Transform tools, GameObject hammer, GameObject driver, GameObject lupa)
        {
            Box("ToolTable", env, new Vector3(0, 0.45f, 0.25f), new Vector3(0.9f, 0.9f, 0.3f), new Color(0.5f, 0.4f, 0.3f), true);
            Place(hammer, tools, new Vector3(-0.28f, 0.97f, 0.25f), new Vector3(0, 0, 90));
            Place(driver, tools, new Vector3(0.0f, 0.95f, 0.25f), new Vector3(0, 0, 90));
            Place(lupa, tools, new Vector3(0.28f, 0.97f, 0.25f), new Vector3(0, 0, 90));
        }

        static void Place(GameObject prefab, Transform parent, Vector3 pos, Vector3 euler)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.transform.position = pos;
            go.transform.rotation = Quaternion.Euler(euler);
        }

        // Estantes
        static void BuildShelves(Transform stations)
        {
            Color wood = new Color(0.45f, 0.33f, 0.22f);
            Box("RackBase", stations, new Vector3(-0.85f, 0.45f, -0.2f), new Vector3(0.4f, 0.9f, 1.0f), wood, true);
            Box("RackTopBoard", stations, new Vector3(-0.85f, 1.3f, -0.2f), new Vector3(0.4f, 0.04f, 0.7f), wood, true);
            foreach (float z in new[] { -0.52f, 0.12f })
                Box("RackPost", stations, new Vector3(-0.85f, 1.1f, z), new Vector3(0.04f, 0.4f, 0.04f), wood, false);

            // tipo, z, y de la fila
            Shelf(stations, ProductType.Robot,   "Robot",     new Vector3(-0.85f, 0.98f, -0.5f),  new Color(0.25f, 0.5f, 0.9f));
            Shelf(stations, ProductType.Radio,   "Radio",     new Vector3(-0.85f, 0.98f, -0.2f),  new Color(0.95f, 0.55f, 0.15f));
            Shelf(stations, ProductType.Lamp,    "Lámpara",   new Vector3(-0.85f, 0.98f, 0.1f),   new Color(1f, 0.92f, 0.45f));
            Shelf(stations, ProductType.Toaster, "Tostadora", new Vector3(-0.85f, 1.38f, -0.35f), new Color(0.78f, 0.8f, 0.84f));
            Shelf(stations, ProductType.Clock,   "Reloj",     new Vector3(-0.85f, 1.38f, -0.05f), new Color(0.3f, 0.75f, 0.4f));
        }

        static void Shelf(Transform parent, ProductType type, string label, Vector3 pos, Color color)
        {
            var go = new GameObject("Shelf_" + type);
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            var col = go.AddComponent<SphereCollider>();
            col.isTrigger = true; col.radius = 0.13f;
            go.AddComponent<XRSocketInteractor>();

            var lbl = Label3D(go.transform, label, pos + new Vector3(0, 0.2f, 0));
            Box("Marker", go.transform, pos + new Vector3(0, -0.075f, 0), new Vector3(0.22f, 0.01f, 0.22f), color, false);

            var st = go.AddComponent<ProductStation>();
            var so = new SerializedObject(st);
            so.FindProperty("kind").enumValueIndex = (int)StationKind.Shelf;
            so.FindProperty("acceptedType").enumValueIndex = (int)type;
            so.FindProperty("label").objectReferenceValue = lbl;
            so.FindProperty("labelText").stringValue = label;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // Cubo de desecho
        static void BuildBin(Transform stations)
        {
            Vector3 c = new Vector3(0.8f, 0.82f, -0.3f);
            Color red = new Color(0.8f, 0.15f, 0.15f);
            Box("BinPedestal", stations, new Vector3(c.x, 0.35f, c.z), new Vector3(0.4f, 0.7f, 0.4f), Color.gray, true);
            Box("BinFloor", stations, new Vector3(c.x, 0.71f, c.z), new Vector3(0.36f, 0.02f, 0.36f), red, true);
            Box("BinWallL", stations, new Vector3(c.x - 0.17f, 0.835f, c.z), new Vector3(0.02f, 0.25f, 0.36f), red, true);
            Box("BinWallR", stations, new Vector3(c.x + 0.17f, 0.835f, c.z), new Vector3(0.02f, 0.25f, 0.36f), red, true);
            Box("BinWallF", stations, new Vector3(c.x, 0.835f, c.z - 0.17f), new Vector3(0.36f, 0.25f, 0.02f), red, true);
            Box("BinWallB", stations, new Vector3(c.x, 0.835f, c.z + 0.17f), new Vector3(0.36f, 0.25f, 0.02f), red, true);

            var go = new GameObject("DiscardBin");
            go.transform.SetParent(stations, false);
            go.transform.position = c;
            var bc = go.AddComponent<BoxCollider>();
            bc.isTrigger = true; bc.size = new Vector3(0.3f, 0.22f, 0.3f);
            var st = go.AddComponent<ProductStation>();
            var so = new SerializedObject(st);
            so.FindProperty("kind").enumValueIndex = (int)StationKind.DiscardBin;
            so.ApplyModifiedPropertiesWithoutUndo();

            Label3D(go.transform, "DESECHO", c + new Vector3(0, 0.4f, 0));
        }

        // Botones
        class ButtonRefs { public GameObject resetButton; }

        static ButtonRefs BuildButtons(Transform stations)
        {
            Box("Console", stations, new Vector3(0.8f, 0.45f, 0.25f), new Vector3(0.4f, 0.9f, 0.3f), new Color(0.25f, 0.27f, 0.3f), true);
            MakeButton(stations, "Btn_StartShift", "StartShift", "INICIAR TURNO", new Vector3(0.7f, 0.925f, 0.25f), new Color(0.2f, 0.8f, 0.3f));
            var reset = MakeButton(stations, "Btn_ResetBelt", "ResetBelt", "REINICIAR BANDA", new Vector3(0.9f, 0.925f, 0.25f), new Color(0.9f, 0.15f, 0.15f));
            reset.SetActive(false);                                    // lo enciende RandomEventSystem durante el atasco
            return new ButtonRefs { resetButton = reset };
        }

        static GameObject MakeButton(Transform parent, string name, string id, string label, Vector3 pos, Color color)
        {
            var go = Box(name, parent, pos, new Vector3(0.12f, 0.05f, 0.12f), color, true);
            go.AddComponent<XRSimpleInteractable>();
            var b = go.AddComponent<XRButton>();
            var so = new SerializedObject(b);
            so.FindProperty("buttonId").stringValue = id;
            so.ApplyModifiedPropertiesWithoutUndo();
            Label3D(go.transform, label, pos + new Vector3(0, 0.12f, 0), 0.7f);
            return go;
        }

        // Managers
        static void BuildManagers(Transform mgr, GameConfigSO config, FactoryRefs f, GameObject resetButton)
        {
            new GameObject("GameManager").transform.SetParent(mgr, false);
            mgr.Find("GameManager").gameObject.AddComponent<GameManager>();

            var sys = new GameObject("Systems");
            sys.transform.SetParent(mgr, false);
            foreach (var type in new[] { typeof(SessionRules), typeof(SessionStats) })
            {
                var so = new SerializedObject(sys.AddComponent(type));
                Ref(so, "config", config);
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            sys.AddComponent<SfxPlayer>();

            // Eventos inesperados
            var sparks = Box("JamIndicator", mgr, new Vector3(0, 1.3f, BeltZ), new Vector3(0.15f, 0.15f, 0.15f), new Color(1f, 0.5f, 0.05f), false);
            sparks.SetActive(false);
            var emergency = new GameObject("EmergencyLight");
            emergency.transform.SetParent(mgr, false);
            emergency.transform.position = new Vector3(0, 2.2f, BeltZ);
            var el = emergency.AddComponent<Light>();
            el.type = LightType.Point; el.color = Color.red; el.range = 4f; el.intensity = 3f;
            emergency.SetActive(false);

            var rg = new GameObject("RandomEvents");
            rg.transform.SetParent(mgr, false);
            var res = rg.AddComponent<RandomEventSystem>();
            var rso = new SerializedObject(res);
            Ref(rso, "config", config);
            Ref(rso, "sparksFx", sparks);
            Ref(rso, "resetButtonObject", resetButton);
            Ref(rso, "emergencyLight", emergency);
            var lights = Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
            var list = new System.Collections.Generic.List<Light>();
            foreach (var l in lights) if (l != el) list.Add(l);
            var arr = rso.FindProperty("lights");
            arr.arraySize = list.Count;
            for (int i = 0; i < list.Count; i++) arr.GetArrayElementAtIndex(i).objectReferenceValue = list[i];
            rso.ApplyModifiedPropertiesWithoutUndo();

            // Flujo de productos
            var flow = new GameObject("ProductFlow");
            flow.transform.SetParent(mgr, false);
            var fso = new SerializedObject(flow.AddComponent<ProductFlow>());
            Ref(fso, "config", config);
            Ref(fso, "spawnPoint", f.spawn);
            var cat = fso.FindProperty("catalog");
            cat.arraySize = f.products.Length;
            for (int i = 0; i < f.products.Length; i++) cat.GetArrayElementAtIndex(i).objectReferenceValue = f.products[i];
            fso.ApplyModifiedPropertiesWithoutUndo();
        }

        // UI
        static void BuildUI(Transform ui)
        {
            var uiRoot = new GameObject("GameUI");
            uiRoot.transform.SetParent(ui, false);
            var gameUI = uiRoot.AddComponent<GameUI>();
            var so = new SerializedObject(gameUI);
            Color bg = new Color(0.05f, 0.07f, 0.12f, 0.85f);

            // HUD
            var hud = WorldCanvas("HUD", uiRoot.transform, new Vector3(0, 1.8f, 1.6f), new Vector2(800, 260));
            Panel(hud.transform, "Bg", bg, Vector2.zero, new Vector2(800, 260));
            var score = Text(hud.transform, "Score", "0", 56, Color.white, new Vector2(-260, 40), new Vector2(260, 90));
            var combo = Text(hud.transform, "Combo", "", 32, new Color(1f, 0.8f, 0.2f), new Vector2(-260, -30), new Vector2(260, 50));
            var time = Text(hud.transform, "Time", "03:00", 64, Color.white, new Vector2(0, 40), new Vector2(240, 100));
            var strikes = Text(hud.transform, "Strikes", "Fallos 0/5", 36, new Color(1f, 0.45f, 0.45f), new Vector2(260, 40), new Vector2(260, 70));
            var bar = MakeSlider(hud.transform, new Vector2(0, -85), new Vector2(560, 26));
            Ref(so, "hudRoot", hud.gameObject);
            Ref(so, "scoreText", score); Ref(so, "comboText", combo); Ref(so, "timeText", time);
            Ref(so, "strikesText", strikes); Ref(so, "productTimeBar", bar);

            // Feedback (siempre visible)
            var fb = WorldCanvas("Feedback", uiRoot.transform, new Vector3(0, 1.4f, 1.6f), new Vector2(800, 160));
            var toast = Text(fb.transform, "Toast", "", 44, new Color(1f, 0.92f, 0.3f), new Vector2(0, 30), new Vector2(780, 70));
            var banner = Text(fb.transform, "Banner", "", 40, new Color(1f, 0.3f, 0.3f), new Vector2(0, -40), new Vector2(780, 60));
            Ref(so, "toastText", toast); Ref(so, "bannerText", banner);

            // Tutorial
            var ins = WorldCanvas("Instructions", uiRoot.transform, new Vector3(0, 1.8f, 1.6f), new Vector2(800, 260));
            Panel(ins.transform, "Bg", bg, Vector2.zero, new Vector2(800, 260));
            var itext = Text(ins.transform, "Text", "", 36, Color.white, new Vector2(0, 25), new Vector2(740, 170));
            var iprog = Text(ins.transform, "Progress", "", 28, new Color(0.6f, 0.8f, 1f), new Vector2(0, -100), new Vector2(740, 40));
            Ref(so, "instructionRoot", ins.gameObject); Ref(so, "instructionText", itext); Ref(so, "progressText", iprog);

            // Reporte (junto al cubo)
            var rep = WorldCanvas("Report", uiRoot.transform, new Vector3(0.8f, 1.35f, -0.3f), new Vector2(500, 420));
            Panel(rep.transform, "Bg", bg, Vector2.zero, new Vector2(500, 420));
            var rtitle = Text(rep.transform, "Title", "Reportar daño", 30, Color.white, new Vector2(0, 170), new Vector2(460, 60));
            string[] names = { "Tornillo flojo", "Abolladura", "Grieta oculta", "Circuito quemado" };
            var rarr = so.FindProperty("reportOptions");
            rarr.arraySize = 4;
            for (int i = 0; i < 4; i++)
            {
                var b = MakeUIButton(rep.transform, "Opt_" + names[i], names[i], new Vector2(0, 85 - i * 80), new Vector2(420, 64), new Color(0.2f, 0.35f, 0.6f));
                var el = rarr.GetArrayElementAtIndex(i);
                el.FindPropertyRelative("button").objectReferenceValue = b;
                el.FindPropertyRelative("type").enumValueIndex = i;
            }
            Ref(so, "reportRoot", rep.gameObject); Ref(so, "reportTitle", rtitle);

            // Resultados
            var res = WorldCanvas("Results", uiRoot.transform, new Vector3(0, 1.5f, 1.2f), new Vector2(700, 560));
            Panel(res.transform, "Bg", bg, Vector2.zero, new Vector2(700, 560));
            var rt = Text(res.transform, "Title", "", 44, Color.white, new Vector2(0, 230), new Vector2(660, 70));
            var grade = Text(res.transform, "Grade", "", 130, new Color(1f, 0.85f, 0.2f), new Vector2(-210, 100), new Vector2(200, 160));
            var fscore = Text(res.transform, "Score", "", 34, Color.white, new Vector2(90, 110), new Vector2(420, 90));
            var details = Text(res.transform, "Details", "", 27, Color.white, new Vector2(0, -50), new Vector2(640, 220));
            var fback = Text(res.transform, "Feedback", "", 26, new Color(0.7f, 0.9f, 1f), new Vector2(0, -180), new Vector2(640, 70));
            var restart = MakeUIButton(res.transform, "Restart", "Reiniciar", new Vector2(-160, -245), new Vector2(260, 64), new Color(0.2f, 0.65f, 0.3f));
            var quit = MakeUIButton(res.transform, "Quit", "Salir", new Vector2(160, -245), new Vector2(260, 64), new Color(0.7f, 0.2f, 0.2f));
            Ref(so, "resultsRoot", res.gameObject); Ref(so, "resultsTitle", rt); Ref(so, "gradeText", grade);
            Ref(so, "finalScoreText", fscore); Ref(so, "detailsText", details); Ref(so, "feedbackText", fback);
            Ref(so, "restartButton", restart); Ref(so, "quitButton", quit);

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static Canvas WorldCanvas(string name, Transform parent, Vector3 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = size;
            rt.localScale = Vector3.one * 0.0015f;
            rt.position = pos;
            Face(go.transform);
            go.AddComponent<TrackedDeviceGraphicRaycaster>();
            return canvas;
        }

        static RectTransform Rect(string name, Transform parent, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos; rt.sizeDelta = size;
            return rt;
        }

        static Image Panel(Transform parent, string name, Color c, Vector2 pos, Vector2 size)
        {
            var img = Rect(name, parent, pos, size).gameObject.AddComponent<Image>();
            img.color = c; img.raycastTarget = false;
            return img;
        }

        static TMP_Text Text(Transform parent, string name, string text, float fontSize, Color c, Vector2 pos, Vector2 size)
        {
            var t = Rect(name, parent, pos, size).gameObject.AddComponent<TextMeshProUGUI>();
            t.text = text; t.fontSize = fontSize; t.color = c;
            t.alignment = TextAlignmentOptions.Center; t.raycastTarget = false;
            return t;
        }

        static Button MakeUIButton(Transform parent, string name, string label, Vector2 pos, Vector2 size, Color c)
        {
            var rt = Rect(name, parent, pos, size);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = c;
            var b = rt.gameObject.AddComponent<Button>();
            b.targetGraphic = img;
            Text(rt, "Label", label, 28, Color.white, Vector2.zero, size);
            return b;
        }

        static Slider MakeSlider(Transform parent, Vector2 pos, Vector2 size)
        {
            var rt = Rect("ProductTimeBar", parent, pos, size);
            var bgImg = rt.gameObject.AddComponent<Image>();
            bgImg.color = new Color(0.2f, 0.2f, 0.25f, 1f); bgImg.raycastTarget = false;

            var fill = Rect("Fill", rt, Vector2.zero, Vector2.zero);
            fill.anchorMin = Vector2.zero; fill.anchorMax = Vector2.one;
            fill.offsetMin = fill.offsetMax = Vector2.zero;
            var fillImg = fill.gameObject.AddComponent<Image>();
            fillImg.color = new Color(0.3f, 0.85f, 0.4f); fillImg.raycastTarget = false;

            var slider = rt.gameObject.AddComponent<Slider>();
            slider.fillRect = fill;
            slider.targetGraphic = null;
            slider.interactable = false;
            slider.minValue = 0; slider.maxValue = 1; slider.value = 1;
            return slider;
        }

        // XR Origin y EventSystem
        static void EnsureXRRigAndEventSystem(Transform mgr)
        {
            var originType = System.Type.GetType("Unity.XR.CoreUtils.XROrigin, Unity.XR.CoreUtils");
            Component origin = originType != null ? Object.FindFirstObjectByType(originType) as Component : null;

            if (origin == null)
            {
                foreach (var guid in AssetDatabase.FindAssets("XR Origin t:Prefab"))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    if (!path.Contains("Starter Assets") || !path.Contains("XR Origin")) continue;
                    var go = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path));
                    go.transform.position = Vector3.zero;
                    origin = originType != null ? go.GetComponentInChildren(originType) : null;
                    if (origin == null) origin = go.transform;
                    break;
                }
            }

            var onb = new GameObject("Onboarding");
            onb.transform.SetParent(mgr, false);
            var so = new SerializedObject(onb.AddComponent<OnboardingManager>());
            if (origin != null) Ref(so, "rig", origin.transform);
            else Debug.LogWarning("[QCXR] No se encontró XR Origin: agrégalo (Starter Assets) y asígnalo en Managers/Onboarding > Rig.");
            so.ApplyModifiedPropertiesWithoutUndo();

            if (Object.FindFirstObjectByType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem");
                es.AddComponent<EventSystem>();
                es.AddComponent<XRUIInputModule>();
            }
        }

        // Utilidades
        static Transform Group(string name, Transform parent)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            return t;
        }

        static Transform Empty(string name, Transform parent, Vector3 worldPos)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.position = worldPos;
            return t;
        }

        static GameObject Box(string name, Transform parent, Vector3 worldPos, Vector3 scale, Color color, bool collider)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.position = worldPos;
            go.transform.localScale = scale;
            if (!collider) Object.DestroyImmediate(go.GetComponent<Collider>());
            go.GetComponent<MeshRenderer>().sharedMaterial = QCXRPrefabBuilder.Mat(color, false);
            return go;
        }

        static TextMeshPro Label3D(Transform parent, string text, Vector3 worldPos, float size = 1.0f)
        {
            var go = new GameObject("Label_" + text);
            go.transform.SetParent(parent, true);
            go.transform.position = worldPos;
            Face(go.transform);
            var t = go.AddComponent<TextMeshPro>();
            t.text = text; t.fontSize = size; t.alignment = TextAlignmentOptions.Center;
            t.rectTransform.sizeDelta = new Vector2(1.2f, 0.3f);
            return t;
        }

        // Gira el objeto para que su frente (+Z) quede mirando hacia el jugador (en el origen).
        static void Face(Transform t)
        {
            Vector3 d = new Vector3(t.position.x, 0, t.position.z);
            if (d.sqrMagnitude < 1e-4f) d = Vector3.forward;
            t.rotation = Quaternion.LookRotation(d, Vector3.up);
        }

        static void Ref(SerializedObject so, string field, Object value)
        {
            var p = so.FindProperty(field);
            if (p == null) { Debug.LogWarning($"[QCXR] Campo no encontrado: {field}"); return; }
            p.objectReferenceValue = value;
        }
    }
}
