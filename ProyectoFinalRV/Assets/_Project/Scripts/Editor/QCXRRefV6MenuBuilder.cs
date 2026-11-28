#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using QCXR.MenuReferenciaV6;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace QCXR.MenuReferenciaV6.Editor
{
    /// Menú 3D editable inspirado en la referencia "Opción 3".
    /// Es UNA herramienta de editor: no modifica QCXR_MainScene ni necesita SDK adicional.
    public static class QCXRRefV6MenuBuilder
    {
        private const string MenuScene = "Assets/_Project/Scenes/QCXR_Menu_Ref3_Editable.unity";
        private const string GameScene = "Assets/_Project/Scenes/QCXR_MainScene.unity";
        private const string MatFolder = "Assets/_Project/Materials/QCXR_Menu_Ref3_Editable";
        private const string EnvFolder = "Assets/_Project/Prefabs/Environment/";
        private static readonly Dictionary<string, Material> Materials = new Dictionary<string, Material>();
        private static readonly Color Orange = Hex("#D86C29");
        private static readonly Color OrangeGlow = Hex("#FF9D38");
        private static readonly Color Wall = Hex("#B2A28E");
        private static readonly Color Coal = Hex("#202A33");
        private static readonly Color Dark = Hex("#101C28");
        private static readonly Color Steel = Hex("#56616C");
        private static readonly Color Cream = Hex("#EEE8DD");
        private static readonly Color Teal = Hex("#5FE7E7");
        private static readonly Color Green = Hex("#64EBA5");
        private static readonly Color Red = Hex("#FA736F");

        [MenuItem("QCXR/Menu REFERENCIA 3 EDITABLE/1 - Crear escena 3D completa")]
        public static void Create()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog("Menú XR", "Detén Play antes de generar el menú.", "OK");
                return;
            }
            if (!File.Exists(GameScene))
            {
                EditorUtility.DisplayDialog("Menú XR", "No encontré QCXR_MainScene en Assets/_Project/Scenes. No se modificó nada.", "OK");
                return;
            }
            if (File.Exists(MenuScene) && !EditorUtility.DisplayDialog("Menú XR", "El menú editable ya existe. ¿Reconstruir SOLO esa escena? Se perderían las ediciones manuales de ESA escena.", "Reconstruir", "Cancelar"))
                return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EnsureFolder("Assets/_Project/Scenes");
            EnsureFolder(MatFolder);
            Materials.Clear();

            // Clonamos el XR Origin y el EventSystem que ya están configurados en la fábrica.
            // No creamos un rig nuevo genérico porque eso fue la causa de varios clics fallidos.
            Scene source = default;
            Scene target = default;
            GameObject rigCopy = null;
            GameObject eventCopy = null;
            var additional = new List<GameObject>();
            try
            {
                target = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                source = EditorSceneManager.OpenScene(GameScene, OpenSceneMode.Additive);
                Transform rig = FindTransform(source, "XR Origin");
                Transform es = FindTransform(source, "EventSystem");
                if (rig == null || es == null)
                    throw new InvalidOperationException("La escena principal no tiene XR Origin o EventSystem. Necesito reutilizar ambos para conservar los controles XR.");

                rigCopy = UnityEngine.Object.Instantiate(rig.gameObject);
                rigCopy.name = "XR Origin (XR Rig) - MENU";
                rigCopy.transform.SetParent(null);
                eventCopy = UnityEngine.Object.Instantiate(es.gameObject);
                eventCopy.name = "EventSystem";
                eventCopy.transform.SetParent(null);

                foreach (var root in source.GetRootGameObjects())
                {
                    if (root.name.IndexOf("XR Interaction Manager", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        root.name.IndexOf("XR Device Simulator", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        root.name.IndexOf("XR Interaction Simulator", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        additional.Add(UnityEngine.Object.Instantiate(root));
                    }
                }
                // Primero transferimos los clones al destino y SOLO LUEGO cerramos la fuente.
                // Si cerramos la fuente antes, Unity destruye esos GameObjects y rompe los botones.
                MoveRoot(rigCopy, target);
                MoveRoot(eventCopy, target);
                foreach (var go in additional) MoveRoot(go, target);
                EditorSceneManager.CloseScene(source, true);
                source = default;
                SceneManager.SetActiveScene(target);

                // El rig se coloca a nivel de suelo. Los XR Origin desplazan la cámara por tracking.
                rigCopy.transform.position = new Vector3(0f, 0f, -1.68f);
                rigCopy.transform.rotation = Quaternion.identity;
                foreach (var mb in rigCopy.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (mb == null) continue;
                    string typeName = mb.GetType().Name;
                    if (typeName.Contains("MoveProvider") || typeName.Contains("TurnProvider") ||
                        typeName.Contains("TeleportationProvider") || typeName.Contains("LocomotionMediator"))
                        mb.enabled = false;
                }

                var input = eventCopy.GetComponent<XRUIInputModule>();
                if (input == null) eventCopy.AddComponent<XRUIInputModule>();
                foreach (var other in eventCopy.GetComponents<BaseInputModule>())
                    if (!(other is XRUIInputModule)) other.enabled = false;
                var mainCamera = rigCopy.GetComponentInChildren<Camera>(true);
                if (mainCamera == null)
                    Debug.LogWarning("[QCXR Menu V6] El XR Origin copiado no contiene Camera. Revisa la plantilla original de la fábrica.");

                Transform sceneRoot = Group("QCXR_MENU_REFERENCIA3_EDITABLE", null);
                Transform environment = Group("01_FABRICA_3D", sceneRoot);
                Transform decorators = Group("02_ELEMENTOS_DECORATIVOS", sceneRoot);
                Transform menu = Group("03_MENU_INTERACTIVO_ESPACIAL", sceneRoot);
                Transform status = Group("04_CARTELES_Y_TITULO_EDITABLES", sceneRoot);
                Transform logic = Group("05_CONTROL_Y_ANIMACIONES", sceneRoot);

                BuildRoom(environment);
                BuildConveyor(environment, decorators);
                BuildRobots(decorators);
                BuildWorkshopDetails(environment, decorators);
                BuildTitle(status);
                var statusLabel = BuildStatus(status);
                var playButton = BuildChoice(menu, "INICIAR INSPECCIÓN", new Vector3(-0.24f, 1.40f, 0.02f), new Vector2(700, 210), Teal, true);
                var quitButton = BuildChoice(menu, "SALIR", new Vector3(1.63f, 1.43f, 0.84f), new Vector2(430, 180), Red, false);
                BuildSign(status, "Control de calidad", "CONTROL DE\nCALIDAD", new Vector3(-1.89f, 2.02f, 2.25f), new Vector2(410f, 138f), Coal);
                BuildSign(status, "Línea de producción", "LÍNEA DE\nPRODUCCIÓN  »»", new Vector3(1.93f, 2.01f, 2.25f), new Vector2(470f, 138f), Coal);

                var controllerObj = new GameObject("QCXRRefV6Controller");
                controllerObj.transform.SetParent(logic, false);
                var controller = controllerObj.AddComponent<QCXRRefV6Controller>();
                var so = new SerializedObject(controller);
                SetObjectRef(so, "iniciarInspeccion", playButton);
                SetObjectRef(so, "salir", quitButton);
                SetObjectRef(so, "estado", statusLabel);
                so.FindProperty("escenaPrincipal").stringValue = "QCXR_MainScene";
                so.ApplyModifiedPropertiesWithoutUndo();

                var sunObj = new GameObject("LuzGeneral_Industrial");
                sunObj.transform.SetParent(sceneRoot, false);
                var sun = sunObj.AddComponent<Light>();
                sun.type = LightType.Directional;
                sun.color = new Color(1f, .94f, .83f);
                sun.intensity = 1.1f;
                sun.shadows = LightShadows.Soft;
                sunObj.transform.rotation = Quaternion.Euler(48, -31, 0);
                AddLight(sceneRoot, "LuzCalida", new Vector3(-0.8f, 2.8f, 0.2f), new Color(1f, 0.66f, 0.36f), 3.2f, 5.5f);
                AddLight(sceneRoot, "LuzAzulSuave", new Vector3(-1.7f, 2.15f, 1.88f), new Color(0.36f, 0.7f, 1f), 1.2f, 3.3f);
                RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
                RenderSettings.ambientSkyColor = new Color(.59f, .61f, .65f);
                RenderSettings.ambientEquatorColor = new Color(.46f, .44f, .42f);
                RenderSettings.ambientGroundColor = new Color(.23f, .23f, .23f);

                EditorSceneManager.SaveScene(target, MenuScene);
                AddToBuild();
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Selection.activeTransform = sceneRoot;
                Debug.Log("[QCXR V6] ESCENA CREADA: " + MenuScene + ". QCXR_MainScene NO fue modificada.");
                EditorUtility.DisplayDialog("QCXR Menú VR V6", "Escena creada. Ahora ejecuta 2 - Validar conexiones. Después entra en Play para probar clic XR o Enter.\n\nTodo el escenario, textos, botones y materiales quedan EDITABLES desde Unity.", "Entendido");
            }
            catch (Exception ex)
            {
                Debug.LogError("[QCXR V6] No fue posible generar el menú: " + ex);
                if (source.IsValid() && source.isLoaded) EditorSceneManager.CloseScene(source, true);
                EditorUtility.DisplayDialog("QCXR Menú VR V6", "Se detuvo la creación sin modificar la fábrica.\n" + ex.Message, "OK");
            }
        }

        [MenuItem("QCXR/Menu REFERENCIA 3 EDITABLE/2 - Validar conexiones")]
        public static void Validate()
        {
            if (!File.Exists(MenuScene))
            {
                EditorUtility.DisplayDialog("QCXR V6", "La escena aún no existe. Ejecuta la opción 1.", "OK");
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.OpenScene(MenuScene, OpenSceneMode.Single);
            var ctrl = FindComponent<QCXRRefV6Controller>(scene);
            var es = FindComponent<EventSystem>(scene);
            var uiModule = FindComponent<XRUIInputModule>(scene);
            var raycasters = FindComponents<TrackedDeviceGraphicRaycaster>(scene);
            var cameras = FindComponents<Camera>(scene);
            var canvases = FindComponents<Canvas>(scene);
            int buttons = FindComponents<Button>(scene).Count;
            bool refs = false;
            if (ctrl != null)
            {
                var so = new SerializedObject(ctrl);
                refs = so.FindProperty("iniciarInspeccion")?.objectReferenceValue != null &&
                       so.FindProperty("salir")?.objectReferenceValue != null;
            }
            bool listed = EditorBuildSettings.scenes.Any(s => s.path == MenuScene && s.enabled) &&
                          EditorBuildSettings.scenes.Any(s => s.path == GameScene && s.enabled);
            bool rig = FindTransform(scene, "XR Origin") != null;
            string summary =
                "XR Origin original: " + (rig ? "OK" : "FALTA") + "\n" +
                "Cámara XR: " + (cameras.Count > 0 ? "OK" : "FALTA") + "\n" +
                "EventSystem: " + (es != null ? "OK" : "FALTA") + "\n" +
                "XRUIInputModule: " + (uiModule != null ? "OK" : "FALTA") + "\n" +
                "Canvases World Space: " + canvases.Count + "\n" +
                "Raycasters XR: " + raycasters.Count + "\n" +
                "Botones: " + buttons + "\n" +
                "Controlador y referencias: " + (ctrl != null && refs ? "OK" : "FALTA") + "\n" +
                "Escenas en Build: " + (listed ? "OK" : "FALTA") +
                "\n\nNOTA: Debes hacer clic en Play y probar los controles reales/simulados; esta validación no simula interacciones.";
            Debug.Log("[QCXR V6] Diagnóstico:\n" + summary);
            EditorUtility.DisplayDialog("QCXR V6 · Validación", summary, "OK");
        }

        [MenuItem("QCXR/Menu REFERENCIA 3 EDITABLE/3 - Abrir menú creado")]
        public static void Open()
        {
            if (!File.Exists(MenuScene))
            {
                EditorUtility.DisplayDialog("QCXR V6", "No se ha creado el menú. Ejecuta primero la opción 1.", "OK");
                return;
            }
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                EditorSceneManager.OpenScene(MenuScene, OpenSceneMode.Single);
        }

        private static void MoveRoot(GameObject go, Scene scene)
        {
            go.transform.SetParent(null);
            SceneManager.MoveGameObjectToScene(go, scene);
        }
        private static Transform FindTransform(Scene scene, string query)
        {
            foreach (var root in scene.GetRootGameObjects())
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                    if (t.name.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0) return t;
            return null;
        }
        private static T FindComponent<T>(Scene scene) where T : Component
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                var component = root.GetComponentInChildren<T>(true);
                if (component != null) return component;
            }
            return null;
        }
        private static List<T> FindComponents<T>(Scene scene) where T : Component
        {
            var list = new List<T>();
            foreach (var root in scene.GetRootGameObjects()) list.AddRange(root.GetComponentsInChildren<T>(true));
            return list;
        }
        private static void AddToBuild()
        {
            var arr = EditorBuildSettings.scenes;
            var other = arr.Where(s => s.path != MenuScene && s.path != GameScene).ToList();
            var next = new List<EditorBuildSettingsScene>
            {
                new EditorBuildSettingsScene(MenuScene, true),
                new EditorBuildSettingsScene(GameScene, true)
            };
            next.AddRange(other);
            EditorBuildSettings.scenes = next.ToArray();
        }
        private static void SetObjectRef(SerializedObject so, string name, UnityEngine.Object obj)
        {
            var prop = so.FindProperty(name);
            if (prop == null) throw new InvalidOperationException("No existe el campo del controlador: " + name);
            prop.objectReferenceValue = obj;
        }
        private static void EnsureFolder(string folder)
        {
            var parts = folder.Split('/');
            string path = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = path + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(path, parts[i]);
                path = next;
            }
        }

        // ─── ESCENARIO Y MÁQUINAS ────────────────────────────────────
        private static void BuildRoom(Transform root)
        {
            GameObject roomPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(EnvFolder + "Env_Room.prefab");
            if (roomPrefab != null)
            {
                var room = (GameObject)PrefabUtility.InstantiatePrefab(roomPrefab, root);
                room.name = "NaveIndustrial_ArteDelJuego";
                room.transform.localPosition = Vector3.zero;
            }
            else
            {
                Cube(root, "SueloIndustria", new Vector3(0, -0.1f, .45f), new Vector3(6.4f, .2f, 6.2f), "Steel", Coal, true);
                Cube(root, "Fondo", new Vector3(0, 1.7f, 2.65f), new Vector3(6.4f, 3.4f, .13f), "Wall", Wall, false);
                Cube(root, "MuroIzq", new Vector3(-3.0f, 1.7f, 0), new Vector3(.13f, 3.4f, 5.3f), "Wall", Wall, false);
                Cube(root, "MuroDer", new Vector3(3.0f, 1.7f, 0), new Vector3(.13f, 3.4f, 5.3f), "Wall", Wall, false);
            }
            // Suelo seguro bajo los pies (invisible, con colisión) para evitar caídas.
            var floor = Cube(root, "SueloFisicoSeguridad", new Vector3(0, -.09f, -1.55f), new Vector3(2.8f, .16f, 2.9f), "Floor", Coal, true);
            floor.GetComponent<MeshRenderer>().enabled = false;
        }
        private static void BuildConveyor(Transform environment, Transform decor)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(EnvFolder + "Env_ConveyorBelt.prefab");
            Renderer beltRender = null;
            if (prefab != null)
            {
                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, environment);
                go.name = "Banda_OriginalDelJuego_VISUAL";
                go.transform.position = new Vector3(0, 0, .88f);
                Transform surface = FindChild(go.transform, "BeltSurface");
                if (surface != null) beltRender = surface.GetComponent<Renderer>();
            }
            else
            {
                Cube(environment, "Banda_Estructura", new Vector3(0, .7f, .88f), new Vector3(3.35f, .36f, .64f), "Orange", Orange, false);
                var top = Cube(environment, "Banda_Superficie", new Vector3(0, .91f, .88f), new Vector3(3.15f, .035f, .54f), "Coal", Coal, false);
                beltRender = top.GetComponent<Renderer>();
            }
            var package = Group("Caja_DeMuestra_EditaAqui", decor);
            package.position = new Vector3(.23f, 1.08f, .88f);
            Cube(package, "CajaCarton", Vector3.zero, new Vector3(.29f, .24f, .24f), "Carton", Hex("#C9A87B"), false);
            Cube(package, "BandaNaranja", new Vector3(0, .09f, -.123f), new Vector3(.25f, .035f, .01f), "Orange", Orange, false);
            Cube(package, "Etiqueta", new Vector3(.055f, -.005f, -.127f), new Vector3(.07f, .063f, .011f), "Cream", Cream, false);
            var anim = decor.gameObject.AddComponent<QCXRRefV6Belt>();
            var so = new SerializedObject(anim);
            SetObjectRef(so, "superficie", beltRender);
            SetObjectRef(so, "productoDecorativo", package);
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        private static void BuildRobots(Transform root)
        {
            BuildScanner(root, "Escáner_IZQ", -1.30f);
            BuildScanner(root, "Escáner_DER", 1.30f);
        }
        private static void BuildScanner(Transform root, string name, float x)
        {
            Transform group = Group(name, root);
            group.position = new Vector3(x, 0, .89f);
            Cube(group, "Columna", new Vector3(0, .88f, 0), new Vector3(.27f, 1.6f, .27f), "Orange", Orange, false);
            Cube(group, "Carcasa", new Vector3(0, 1.75f, 0), new Vector3(.52f, .23f, .40f), "Cream", Cream, false);
            Cube(group, "Visor", new Vector3(0, 1.64f, -.17f), new Vector3(.54f, .10f, .18f), "Coal", Coal, false);
            Cube(group, "BarraLuminosa", new Vector3(0, 1.60f, -.26f), new Vector3(.31f, .023f, .025f), "CreamGlow", Cream, false);
            Cube(group, "BrazoSuperior", new Vector3(x < 0 ? .14f : -.14f, 1.24f, .15f), new Vector3(.17f, .55f, .16f), "Steel", Steel, false);
            Cube(group, "BrazoInferior", new Vector3(x < 0 ? .17f : -.17f, 1.0f, .25f), new Vector3(.13f, .29f, .16f), "Orange", Orange, false);
            var sway = group.gameObject.AddComponent<QCXRRefV6Ambient>();
            var so = new SerializedObject(sway);
            so.FindProperty("efecto").enumValueIndex = 2;
            so.FindProperty("velocidad").floatValue = .75f;
            so.FindProperty("intensidad").floatValue = 2.3f;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        private static void BuildWorkshopDetails(Transform room, Transform decor)
        {
            // Vigas estructurales; ventana de inspección; tuberías, barandas, escalones.
            for (int s = -1; s <= 1; s += 2)
            {
                Cube(room, "VigaNaranja", new Vector3(s * 2.55f, 1.64f, .28f), new Vector3(.16f, 3.25f, .18f), "Orange", Orange, false);
                Cube(room, "VigaNaranjaFondo", new Vector3(s * 2.55f, 1.64f, 2.30f), new Vector3(.16f, 3.25f, .18f), "Orange", Orange, false);
            }
            Cube(room, "TrabeTecho", new Vector3(0, 3.1f, .45f), new Vector3(5.4f, .15f, .18f), "Orange", Orange, false);
            for (int i = 0; i < 3; i++)
            {
                Cube(room, "TuberíaSuperior", new Vector3(0, 2.76f - i * .12f, 1.85f + i * .20f), new Vector3(5.05f, .055f, .065f),
                    i == 2 ? "PipeGreen" : "Steel", i == 2 ? Hex("#3C835B") : Steel, false);
            }
            for (int i = 0; i < 4; i++)
            {
                var lamp = Cube(room, "LuminariaCenital", new Vector3(-1.65f + i * 1.1f, 2.97f, -.05f), new Vector3(.72f, .085f, .24f), "WarmLight", OrangeGlow, false);
            }
            // Ventanal a la izquierda para dar profundidad sin copiar una textura plana de la referencia.
            Cube(room, "VentanalAzulFondo", new Vector3(-1.75f, 1.48f, 2.43f), new Vector3(1.14f, .95f, .032f), "Window", Hex("#314B5B"), false);
            for (int i = 0; i < 3; i++)
                Cube(room, "VentanalMarco", new Vector3(-2.24f + i * .48f, 1.48f, 2.40f), new Vector3(.035f, .95f, .038f), "Coal", Coal, false);
            for (int i = 0; i < 2; i++)
                Cube(room, "PaletaDeCajas", new Vector3(-2.23f + i * .31f, .22f, 1.55f), new Vector3(.25f, .30f, .25f), "Carton", Hex("#C9A87B"), false);
            // Plataforma derecha y baranda, con letrero Salir delante.
            Cube(decor, "PlataformaDerecha", new Vector3(1.72f, .34f, 1.35f), new Vector3(1.55f, .18f, 1.42f), "Steel", Steel, false);
            for (int i = 0; i < 3; i++)
            {
                Cube(decor, "Escalón", new Vector3(1.80f, .08f + i * .1f, .34f + i * .21f), new Vector3(.86f, .11f, .27f), "Coal", Coal, false);
                Cube(decor, "BandaDeAdvertencia", new Vector3(1.8f, .14f + i * .1f, .30f + i * .21f), new Vector3(.75f, .012f, .04f), "Orange", Orange, false);
            }
            for (int i = 0; i < 3; i++)
                Cube(decor, "PosteBaranda", new Vector3(2.26f, .75f, .65f + .39f * i), new Vector3(.05f, .78f, .05f), "Orange", Orange, false);
            Cube(decor, "Pasamanos", new Vector3(2.26f, 1.12f, 1.04f), new Vector3(.05f, .05f, .85f), "Orange", Orange, false);
            Cube(decor, "GabineteElectrico", new Vector3(2.17f, 1.55f, 2.08f), new Vector3(.66f, .90f, .22f), "Coal", Coal, false);
            for (int i = 0; i < 3; i++)
                Cube(decor, "LED_Gabinete", new Vector3(1.95f + .21f * i, 1.82f, 1.945f), new Vector3(.055f, .055f, .028f), i == 0 ? "Red" : "Green", i == 0 ? Red : Green, false);
            // Detalles del piso y zona de seguridad
            for (int i = 0; i < 11; i++)
                Cube(decor, "RayasDePiso", new Vector3(-.72f + .14f * i, -.002f, -.83f), new Vector3(.08f, .004f, .04f), "Orange", Orange, false);
        }
        // ─── TÍTULO SUSPENDIDO Y SEÑALES ───────────────────────────
        private static void BuildTitle(Transform parent)
        {
            var title = Group("TITULO_HOLOGRAFICO_MOVER_AQUI", parent);
            title.position = new Vector3(0, 2.50f, 1.66f);
            var ring = Group("ARO_GIRATORIO", title);
            const int n = 24;
            for (int i = 0; i < n; i++)
            {
                float a = 2 * Mathf.PI * i / n;
                float x = Mathf.Cos(a) * 1.32f;
                float z = Mathf.Sin(a) * .43f;
                var segment = Cube(ring, "SegmentoAro", new Vector3(x, .21f, z), new Vector3(.16f, .032f, .045f), i % 3 == 0 ? "OrangeGlow" : "Teal", i % 3 == 0 ? OrangeGlow : Teal, false);
                segment.transform.localRotation = Quaternion.Euler(0, -a * Mathf.Rad2Deg + 90, 0);
            }
            var anim = ring.gameObject.AddComponent<QCXRRefV6Ambient>();
            var so = new SerializedObject(anim);
            so.FindProperty("efecto").enumValueIndex = 1;
            so.FindProperty("velocidad").floatValue = .24f;
            so.FindProperty("intensidad").floatValue = 1f;
            so.ApplyModifiedPropertiesWithoutUndo();
            var titleCanvas = CanvasAt(title, "CanvasTitulo_Editable", new Vector3(0, .03f, -.48f), new Vector2(1150, 450), .0027f);
            var main = Text(titleCanvas.transform, "TITULO_EDITABLE", "CONTROL DE\nCALIDAD XR", new Vector2(1080, 390), Vector2.zero, 114, Cream, FontStyles.Bold);
            main.alignment = TextAlignmentOptions.Center;
            main.enableAutoSizing = true;
            main.fontSizeMin = 65;
            // Fondo no un panel único: letras sueltas dentro del aro de techo.
            Cube(title, "SegmentoInferiorLuminico", new Vector3(0, -.29f, -.07f), new Vector3(.9f, .035f, .05f), "OrangeGlow", OrangeGlow, false);
        }
        private static TMP_Text BuildStatus(Transform parent)
        {
            var go = CanvasAt(parent, "EstadoCentral", new Vector3(.35f, 1.90f, 2.25f), new Vector2(550, 140), .0018f);
            var bg = Image(go.transform, "EstadoPanel", new Vector2(530, 128), Vector2.zero, new Color(.06f, .11f, .13f, .90f), false);
            bg.gameObject.AddComponent<Outline>().effectColor = Green;
            var lbl = Text(go.transform, "ESTADO_EDITABLE", "✓  ESTACIÓN PREPARADA", new Vector2(490, 105), Vector2.zero, 50, Green, FontStyles.Bold);
            return lbl;
        }
        private static void BuildSign(Transform parent, string name, string content, Vector3 position, Vector2 size, Color color)
        {
            var canvas = CanvasAt(parent, name + "_EDITABLE", position, size, .002f);
            var label = Text(canvas.transform, "Texto", content, size - new Vector2(12f, 12f), Vector2.zero, 49, color, FontStyles.Bold);
            label.alignment = TextAlignmentOptions.Center;
        }
        // ─── BOTONES: UI XR FUNCIONAL, CADA UNO EN SU PROPIO CANVAS ───
        private static Button BuildChoice(Transform parent, string content, Vector3 position, Vector2 size, Color glow, bool main)
        {
            string id = main ? "INICIAR_INSPECCION" : "SALIR";
            var root = Group("BOTON_" + id + "_MOVER_AQUI", parent);
            root.position = position;
            // Base 3D para proyectar el holograma; SIN collider delante de la UI.
            float floor = main ? .46f : .32f;
            var baseRoot = Group("ProyectorIndustrial", root);
            baseRoot.localPosition = new Vector3(0, -floor, .09f);
            var col = Cube(baseRoot, "Base", new Vector3(0, -.17f, 0), new Vector3(main ? .66f : .40f, .36f, .45f), "Orange", Orange, false);
            Cube(baseRoot, "Rejilla", new Vector3(0, -.11f, -.234f), new Vector3(.36f, .11f, .015f), "Coal", Coal, false);
            var rim = Cylinder(baseRoot, "Emisor", new Vector3(0, .055f, 0), main ? .43f : .25f, .035f, main ? "Teal" : "Red", glow);
            var halo = Cylinder(baseRoot, "HazLuzInferior", new Vector3(0, .15f, 0), main ? .36f : .19f, .022f, main ? "Teal" : "Red", glow);
            halo.AddComponent<QCXRRefV6Ambient>();

            // RectTransforms: no usa una pantalla gigante. Cada botón es una pieza espacial aparte.
            var canvas = CanvasAt(root, "CanvasInteractivo_" + id, Vector3.zero, size, .002f);
            var panel = Image(canvas.transform, "AreaClicXR", size - new Vector2(12, 16), Vector2.zero,
                new Color(.075f, .19f, .21f, main ? .86f : .82f), true);
            var border = panel.gameObject.AddComponent<Outline>();
            border.effectColor = glow;
            border.effectDistance = new Vector2(4f, 4f);
            var button = panel.gameObject.AddComponent<Button>();
            button.targetGraphic = panel;
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.13f, 1.13f, 1.13f, 1f);
            colors.pressedColor = new Color(.60f, .96f, .85f, 1f);
            colors.selectedColor = colors.highlightedColor;
            colors.fadeDuration = .08f;
            button.colors = colors;
            // Un texto grande directamente DENTRO de la zona de clic, sin absorber raycasts.
            var label = Text(panel.transform, "TEXTO_EDITABLE", main ? "◇   INICIAR\n      INSPECCIÓN  »»" : "↪   SALIR", size - new Vector2(55, 33), Vector2.zero, main ? 62 : 74, Cream, FontStyles.Bold);
            label.raycastTarget = false;
            label.alignment = TextAlignmentOptions.Center;
            root.gameObject.AddComponent<QCXRRefV6Ambient>();
            return button;
        }

        // ─── FUNCIONES PARA JERARQUÍA EDITABLE ─────────────────────
        private static Transform Group(string name, Transform parent)
        {
            var t = new GameObject(name).transform;
            if (parent != null) t.SetParent(parent, false);
            return t;
        }
        private static Material GetMat(string name, Color color)
        {
            Material mat;
            if (Materials.TryGetValue(name, out mat)) return mat;
            string path = MatFolder + "/" + name + ".mat";
            mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                if (shader == null) throw new Exception("No se encontró URP Lit ni Standard Shader.");
                mat = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(mat, path);
            }
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
            if (name.Contains("Glow") || name == "Teal" || name == "Green" || name == "Red" || name == "WarmLight")
            {
                mat.EnableKeyword("_EMISSION");
                if (mat.HasProperty("_EmissionColor")) mat.SetColor("_EmissionColor", color * 0.9f);
            }
            EditorUtility.SetDirty(mat);
            Materials[name] = mat;
            return mat;
        }
        private static GameObject Cube(Transform parent, string name, Vector3 localPos, Vector3 size, string matName, Color col, bool collider)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = GetMat(matName, col);
            if (!collider) UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }
        private static GameObject Cylinder(Transform parent, string name, Vector3 pos, float diameter, float height, string matName, Color col)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = new Vector3(diameter, height * .5f, diameter);
            go.GetComponent<Renderer>().sharedMaterial = GetMat(matName, col);
            UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }
        private static GameObject CanvasAt(Transform parent, string name, Vector3 position, Vector2 dimensions, float scale)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            // El frente de una Canvas World Space debe mirar al jugador, que está en Z negativo.
            go.transform.localRotation = Quaternion.Euler(0, 180, 0);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = dimensions;
            rt.localScale = Vector3.one * scale;
            go.AddComponent<TrackedDeviceGraphicRaycaster>();
            return go;
        }
        private static RectTransform RectAt(Transform parent, string name, Vector2 position, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(.5f, .5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = position;
            return rt;
        }
        private static Image Image(Transform parent, string name, Vector2 size, Vector2 pos, Color color, bool raycastTarget)
        {
            var im = RectAt(parent, name, pos, size).gameObject.AddComponent<Image>();
            im.color = color;
            im.raycastTarget = raycastTarget;
            return im;
        }
        private static TextMeshProUGUI Text(Transform parent, string name, string content, Vector2 size, Vector2 pos, float fontSize, Color color, FontStyles style)
        {
            var text = RectAt(parent, name, pos, size).gameObject.AddComponent<TextMeshProUGUI>();
            text.text = content;
            text.color = color;
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.alignment = TextAlignmentOptions.Center;
            text.raycastTarget = false;
            text.enableAutoSizing = true;
            text.fontSizeMin = fontSize * .60f;
            text.fontSizeMax = fontSize;
            return text;
        }
        private static Transform FindChild(Transform root, string partial)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name.IndexOf(partial, StringComparison.OrdinalIgnoreCase) >= 0) return t;
            return null;
        }
        private static void AddLight(Transform parent, string name, Vector3 at, Color color, float strength, float range)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = at;
            var l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.intensity = strength;
            l.range = range;
            l.color = color;
            l.shadows = LightShadows.None;
        }
        private static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out Color c);
            return c;
        }
    }
}
#endif
