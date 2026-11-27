#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;
using Object = UnityEngine.Object;

namespace QCXR.EditorTools
{
    // Un clic genera la escena .unity, el contenido editable y la conexión a la partida.
    public static class QCXRMenuSceneGenerator
    {
        const string GamePath = "Assets/_Project/Scenes/QCXR_MainScene.unity";
        const string MenuPath = "Assets/_Project/Scenes/QCXR_Menu.unity";
        const string DataPath = "Assets/_Project/Config/UI/QCXRMenuContent.asset";

        static readonly Color White = new Color(0.96f, 0.98f, 1f, 1f);
        static readonly Color Muted = new Color(0.60f, 0.71f, 0.79f, 1f);
        static readonly Color Cyan = new Color(0.25f, 0.85f, 0.92f, 1f);
        static readonly Color Navy = new Color(0.025f, 0.051f, 0.086f, 1f);
        static readonly Color Surface = new Color(0.061f, 0.115f, 0.164f, 1f);
        static readonly Color Border = new Color(0.14f, 0.28f, 0.36f, 1f);

        static readonly Dictionary<string, TMP_Text> t = new Dictionary<string, TMP_Text>();
        static readonly Dictionary<string, Button> b = new Dictionary<string, Button>();
        static readonly List<TMP_Text> allTexts = new List<TMP_Text>();
        static readonly List<Image> backgrounds = new List<Image>();
        static readonly List<Image> surfaces = new List<Image>();
        static readonly List<Image> accents = new List<Image>();
        static TMP_FontAsset defaultFont;

        [MenuItem("QCXR/Menú VR/1. Crear escena de menú independiente")]
        public static void Generate()
        {
            if (EditorApplication.isPlaying)
            {
                EditorUtility.DisplayDialog("QCXR", "Sal de Play antes de crear la escena.", "Entendido");
                return;
            }
            if (!File.Exists(GamePath))
            {
                EditorUtility.DisplayDialog("QCXR", "No encontré " + GamePath + ". Abre el proyecto correcto.", "Entendido");
                return;
            }
            if (File.Exists(MenuPath))
            {
                if (EditorUtility.DisplayDialog("El menú ya existe", "Ya tienes QCXR_Menu.unity. No lo sobrescribiré para proteger tus cambios. ¿Quieres abrirlo?", "Abrir menú", "Cancelar"))
                    EditorSceneManager.OpenScene(MenuPath, OpenSceneMode.Single);
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            t.Clear(); b.Clear(); allTexts.Clear(); backgrounds.Clear(); surfaces.Clear(); accents.Clear();

            // Reusar el XR Rig y el EventSystem REAL de los compañeros.
            Scene fuente = EditorSceneManager.OpenScene(GamePath, OpenSceneMode.Single);
            GameObject originalXR = null;
            GameObject originalES = null;
            var copiarExtra = new List<GameObject>();
            foreach (GameObject go in fuente.GetRootGameObjects())
            {
                if (go.name.Contains("XR Origin") || go.name.Contains("XR Rig")) originalXR = go;
                if (go.GetComponent<EventSystem>() != null) originalES = go;
                if (go.name.Contains("Interaction Manager") || go.name.Contains("XR Interaction Simulator")) copiarExtra.Add(go);
            }
            if (originalXR == null || originalES == null)
            {
                EditorUtility.DisplayDialog("Faltan objetos XR", "La escena original debe tener XR Origin y EventSystem. No se modificó el juego.", "Entendido");
                return;
            }

            Scene menu = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(menu);
            CloneInto(originalXR, menu);
            GameObject evento = CloneInto(originalES, menu);
            foreach (GameObject go in copiarExtra) CloneInto(go, menu);

            // Solo agregar un Input Module si el del juego ya no existía.
            if (evento.GetComponent<XRUIInputModule>() == null)
            {
                foreach (BaseInputModule module in evento.GetComponents<BaseInputModule>()) Object.DestroyImmediate(module);
                evento.AddComponent<XRUIInputModule>();
            }

            GameObject decorado = new GameObject("Escenario 3D / sala industrial ligera");
            BuildEnvironment(decorado.transform);

            var anchor = new GameObject("Menu VR / posicion frente al jugador", typeof(RectTransform));
            RectTransform anchorRT = anchor.GetComponent<RectTransform>();
            anchorRT.position = new Vector3(0f, 1.6f, 2.25f);
            anchorRT.rotation = Quaternion.Euler(0f, 180f, 0f);
            BuildFrame(anchor.transform);

            var canvasObject = new GameObject("Canvas World Space - Menu", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            canvasObject.transform.SetParent(anchor.transform, false);
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 100;
            Camera cameraXR = anchor.GetComponentInChildren<Camera>(true);
            if (!cameraXR)
            {
                // La cámara vive en el rig (otro GameObject raíz).
                foreach (GameObject root in menu.GetRootGameObjects())
                {
                    cameraXR = root.GetComponentInChildren<Camera>(true);
                    if (cameraXR) break;
                }
            }
            if (cameraXR) canvas.worldCamera = cameraXR;
            canvasObject.AddComponent<TrackedDeviceGraphicRaycaster>();
            canvasObject.AddComponent<GraphicRaycaster>(); // Click del mouse, cuando el Input Module lo permita.
            canvasObject.GetComponent<CanvasScaler>().dynamicPixelsPerUnit = 10f;
            RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();
            canvasRect.sizeDelta = new Vector2(1600f, 960f);
            canvasRect.localScale = Vector3.one * 0.00135f;

            BuildMain(canvasRect);
            GameObject overlay = BuildHowTo(canvasRect);
            overlay.SetActive(false);
            GameObject manager = new GameObject("QCXRMenuController");
            QCXRMenuController controller = manager.AddComponent<QCXRMenuController>();
            QCXRMenuContent asset = GetOrCreateContent();
            WireController(controller, anchor.transform, overlay, asset);
            controller.AplicarContenido();

            Directory.CreateDirectory("Assets/_Project/Scenes");
            EditorSceneManager.SaveScene(menu, MenuPath);
            EditorSceneManager.CloseScene(fuente, true);
            SceneManager.SetActiveScene(menu);
            UpdateBuildScenes();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeObject = controller;
            EditorUtility.DisplayDialog("¡Menú VR creado!", "Guardé QCXR_Menu.unity como escena independiente. El menú ya carga QCXR_MainScene.\n\nPara editar textos y fuentes: Assets/_Project/Config/UI/QCXRMenuContent.asset", "Perfecto");
        }

        [MenuItem("QCXR/Menú VR/2. Actualizar textos y tipografías")]
        public static void UpdateContent()
        {
            QCXRMenuController[] controllers = Object.FindObjectsByType<QCXRMenuController>(FindObjectsSortMode.None);
            if (controllers.Length == 0)
            {
                EditorUtility.DisplayDialog("QCXR", "Abre primero QCXR_Menu.unity desde Assets/_Project/Scenes.", "Entendido");
                return;
            }
            foreach (QCXRMenuController c in controllers)
            {
                c.AplicarContenido();
                EditorUtility.SetDirty(c);
                EditorSceneManager.MarkSceneDirty(c.gameObject.scene);
            }
            EditorUtility.DisplayDialog("QCXR", "Se aplicaron los textos, tipografías y colores. Recuerda guardar la escena (Ctrl + S).", "OK");
        }

        [MenuItem("QCXR/Menú VR/3. Abrir escena del menú")]
        public static void OpenMenu()
        {
            if (File.Exists(MenuPath) && EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                EditorSceneManager.OpenScene(MenuPath, OpenSceneMode.Single);
            else if (!File.Exists(MenuPath))
                EditorUtility.DisplayDialog("QCXR", "Primero utiliza '1. Crear escena de menú independiente'.", "OK");
        }

        static GameObject CloneInto(GameObject source, Scene target)
        {
            GameObject clone = Object.Instantiate(source);
            clone.name = source.name;
            SceneManager.MoveGameObjectToScene(clone, target);
            return clone;
        }

        static QCXRMenuContent GetOrCreateContent()
        {
            QCXRMenuContent data = AssetDatabase.LoadAssetAtPath<QCXRMenuContent>(DataPath);
            if (data != null) return data;
            Directory.CreateDirectory("Assets/_Project/Config/UI");
            data = ScriptableObject.CreateInstance<QCXRMenuContent>();
            AssetDatabase.CreateAsset(data, DataPath);
            AssetDatabase.SaveAssets();
            return data;
        }

        static void UpdateBuildScenes()
        {
            var list = new List<EditorBuildSettingsScene>
            {
                new EditorBuildSettingsScene(MenuPath, true),
                new EditorBuildSettingsScene(GamePath, true)
            };
            foreach (EditorBuildSettingsScene s in EditorBuildSettings.scenes)
                if (s.path != MenuPath && s.path != GamePath) list.Add(s);
            EditorBuildSettings.scenes = list.ToArray();
        }

        static void BuildEnvironment(Transform parent)
        {
            // Escenario sin assets externos: 15 piezas primitivas + luces.
            Material floor = MaterialFor("Suelo acero oscuro", new Color(0.055f, 0.086f, 0.12f));
            Material metal = MaterialFor("Pared industrial", new Color(0.08f, 0.13f, 0.17f));
            Material rail = MaterialFor("Estructura de acento", new Color(0.075f, 0.35f, 0.39f));
            Cuboid("Suelo", parent, new Vector3(0,-0.06f,3.2f), new Vector3(12,0.12f,12), floor);
            Cuboid("Pared del fondo", parent, new Vector3(0,2.6f,6.4f), new Vector3(12,5.2f,0.3f), metal);
            Cuboid("Pilar izquierdo", parent, new Vector3(-4.4f,2.1f,5.9f), new Vector3(0.5f,4.2f,0.6f), floor);
            Cuboid("Pilar derecho", parent, new Vector3(4.4f,2.1f,5.9f), new Vector3(0.5f,4.2f,0.6f), floor);
            Cuboid("Franja superior", parent, new Vector3(0,3.85f,6.1f), new Vector3(7f,0.07f,0.1f), rail);
            for (int i=0; i<6; i++)
            {
                float x = -4.8f + i*1.92f;
                Cuboid("Luz de suelo_"+i, parent, new Vector3(x,0.02f,5.3f), new Vector3(0.95f,0.02f,0.05f), rail);
            }
            GameObject light = new GameObject("Iluminacion ambiente", typeof(Light));
            light.transform.SetParent(parent, false);
            light.transform.rotation = Quaternion.Euler(35, -25, 0);
            Light l = light.GetComponent<Light>();
            l.type = LightType.Directional;
            l.color = new Color(0.65f, 0.83f, 1f);
            l.intensity = 0.7f;
        }

        static void BuildFrame(Transform parent)
        {
            Material bezel = MaterialFor("Marco del menu", new Color(0.015f,0.028f,0.046f));
            Material bright = MaterialFor("Marco luminoso", new Color(0.14f,0.53f,0.57f));
            Cuboid("Marco de pantalla (3D)", parent, new Vector3(0,0,-0.045f), new Vector3(2.34f,1.43f,0.055f), bezel, true);
            Cuboid("Linea superior luminosa", parent, new Vector3(0,0.716f,-0.024f), new Vector3(2.26f,0.008f,0.02f), bright, true);
            Cuboid("Linea inferior luminosa", parent, new Vector3(0,-0.714f,-0.024f), new Vector3(2.26f,0.008f,0.02f), bright, true);
        }

        static Material MaterialFor(string name, Color color)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (!shader) shader = Shader.Find("Standard");
            if (!shader) shader = Shader.Find("Unlit/Color");
            const string folder = "Assets/_Project/Materials/Menu";
            Directory.CreateDirectory(folder);
            string path = folder + "/" + name.Replace(" ", "_") + ".mat";
            Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!m)
            {
                m = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(m, path);
            }
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
            if (m.HasProperty("_Color")) m.SetColor("_Color", color);
            EditorUtility.SetDirty(m);
            return m;
        }

        static GameObject Cuboid(string name, Transform parent, Vector3 position, Vector3 scale, Material material, bool local = false)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            if (local) go.transform.localPosition = position;
            else go.transform.position = position;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = material;
            var collider = go.GetComponent<Collider>();
            if (collider) Object.DestroyImmediate(collider);
            return go;
        }

        // RectTransform children: coordenadas en píxeles de diseño (1600 x 960).
        static RectTransform Rect(string name, Transform parent, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = pos;
            rt.localScale = Vector3.one;
            return rt;
        }

        static Image ImageBox(string name, Transform parent, Vector2 pos, Vector2 size, Color color, bool clickable=false)
        {
            var rt = Rect(name, parent, pos, size);
            Image im = rt.gameObject.AddComponent<Image>();
            im.color = color;
            im.raycastTarget = clickable;
            return im;
        }

        static TextMeshProUGUI Text(string id, Transform parent, Vector2 pos, Vector2 size,
            string value, float font, Color color, TextAlignmentOptions align=TextAlignmentOptions.Left)
        {
            var rt = Rect(id, parent, pos, size);
            TextMeshProUGUI tmp = rt.gameObject.AddComponent<TextMeshProUGUI>();
            if (!defaultFont) defaultFont = TMP_Settings.defaultFontAsset;
            if (defaultFont) tmp.font = defaultFont;
            tmp.text = value;
            tmp.fontSize = font;
            tmp.alignment = align;
            tmp.color = color;
            tmp.raycastTarget = false;
            tmp.textWrappingMode = TextWrappingModes.Normal;
            tmp.overflowMode = TextOverflowModes.Ellipsis;
            tmp.margin = new Vector4(0,0,0,0);
            t[id] = tmp;
            allTexts.Add(tmp);
            return tmp;
        }

        static Button MakeButton(string id, Transform parent, Vector2 pos, Vector2 size,
            string label, bool primary=false)
        {
            Color baseColor = primary ? new Color(0.05f,0.51f,0.62f) : new Color(0.105f,0.19f,0.27f);
            Image bg = ImageBox("BTN - "+id, parent, pos, size, baseColor, true);
            Button button = bg.gameObject.AddComponent<Button>();
            button.targetGraphic = bg;
            ColorBlock block = button.colors;
            block.normalColor = Color.white;
            block.highlightedColor = new Color(0.8f,1.0f,1.0f);
            block.selectedColor = new Color(0.82f,0.95f,1f);
            block.pressedColor = new Color(0.5f,0.85f,0.9f);
            block.fadeDuration = 0.08f;
            button.colors = block;
            Text(id, bg.transform, Vector2.zero, size-new Vector2(12f,12f), label, 31f, White, TextAlignmentOptions.Center);
            b[id] = button;
            return button;
        }

        static void BuildMain(Transform canvas)
        {
            Image background = ImageBox("BASE  /  azul industrial", canvas, Vector2.zero, new Vector2(1600,960), Navy);
            backgrounds.Add(background);
            ImageBox("Marco superior", canvas, new Vector2(0,463), new Vector2(1540,3), Border);
            ImageBox("Marco inferior", canvas, new Vector2(0,-464), new Vector2(1540,3), Border);
            ImageBox("Acento lateral", canvas, new Vector2(-777,0), new Vector2(5,920), Cyan);
            ImageBox("Punto de estado", canvas, new Vector2(690,411), new Vector2(16,16), new Color(0.25f,0.92f,0.6f));
            Text("marca",canvas,new Vector2(-420,404),new Vector2(720,50),"UAO  /  IMMERSIVE SOLUTIONS",25,Cyan);
            Text("version",canvas,new Vector2(450,405),new Vector2(360,40),"XR  ·  PROTOCOLO 2026",24,Muted,TextAlignmentOptions.Right);
            ImageBox("separador",canvas,new Vector2(0,357),new Vector2(1470,2),Border);
            Text("titulo",canvas,new Vector2(-385,207),new Vector2(815,220),"CONTROL DE\nCALIDAD XR",90,White);
            Image linea = ImageBox("Acento de titulo",canvas,new Vector2(-731,45),new Vector2(100,6),Cyan);
            accents.Add(linea);
            Text("descripcion",canvas,new Vector2(-412,-4),new Vector2(760,95),"Bienvenido a la línea de inspección.",28,Muted);
            MakeButton("etiquetaInicio",canvas,new Vector2(-440,-148),new Vector2(675,92),"INICIAR INSPECCIÓN",true);
            MakeButton("etiquetaAyuda",canvas,new Vector2(-440,-261),new Vector2(675,92),"CÓMO JUGAR");
            MakeButton("etiquetaSalida",canvas,new Vector2(-440,-374),new Vector2(675,79),"SALIR");
            Image top = ImageBox("Tarjeta derecha",canvas,new Vector2(450,2),new Vector2(510,598),Surface);
            surfaces.Add(top);
            ImageBox("Borde tarjeta",canvas,new Vector2(450,301),new Vector2(510,4),Border);
            Text("tarjetaTitulo",canvas,new Vector2(443,248),new Vector2(425,59),"TU MISIÓN",36,Cyan);
            Text("tarjetaDescripcion",canvas,new Vector2(443,130),new Vector2(425,164),"Inspecciona, repara y clasifica.",28,White);
            ImageBox("Divisor tarjeta",canvas,new Vector2(450,7),new Vector2(424,2),Border);
            Text("tarjetaDato1",canvas,new Vector2(450,-58),new Vector2(420,60),"05  TIPOS DE PRODUCTO",27,White);
            Text("tarjetaDato2",canvas,new Vector2(450,-132),new Vector2(420,60),"03  HERRAMIENTAS",27,White);
            Text("tarjetaDato3",canvas,new Vector2(450,-206),new Vector2(420,60),"01  LÍNEA DE CALIDAD",27,White);
            Text("etiquetaEstado",canvas,new Vector2(440,-374),new Vector2(500,48),"SISTEMA LISTO  /  ESTACIÓN 01",20,Cyan,TextAlignmentOptions.Center);
            Text("aviso",canvas,new Vector2(0,-422),new Vector2(1400,32),"",19,new Color(1,0.6f,0.45f),TextAlignmentOptions.Center);
            Text("pie",canvas,new Vector2(0,-449),new Vector2(1000,28),"SIMULACIÓN DE ENTRENAMIENTO  /  UNITY XR",18,Muted,TextAlignmentOptions.Center);
        }

        static GameObject BuildHowTo(Transform canvas)
        {
            var root = Rect("PANTALLA - Cómo jugar",canvas,Vector2.zero,new Vector2(1600,960));
            Image fondo = root.gameObject.AddComponent<Image>();
            fondo.color = Navy;
            backgrounds.Add(fondo);
            fondo.raycastTarget = true; // Impide clics sobre los botones del menú detrás.
            ImageBox("Acento superior",root,new Vector2(0,444),new Vector2(1490,5),Cyan);
            Text("ayudaTitulo",root,new Vector2(0,342),new Vector2(1380,92),"MANUAL DEL INSPECTOR",56,Cyan,TextAlignmentOptions.Center);
            Image pane = ImageBox("Tarjeta instrucciones",root,new Vector2(0,14),new Vector2(1310,550),Surface);
            surfaces.Add(pane);
            Text("ayudaContenido",root,new Vector2(0,21),new Vector2(1200,490),"Instrucciones de juego",30,White);
            MakeButton("etiquetaVolver",root,new Vector2(0,-367),new Vector2(555,82),"VOLVER AL MENÚ",true);
            return root.gameObject;
        }

        static void WireController(QCXRMenuController c, Transform anchor, GameObject overlay, QCXRMenuContent content)
        {
            SerializedObject so = new SerializedObject(c);
            Link(so,"contenido",content);
            Link(so,"menuEnElMundo",anchor);
            Link(so,"pantallaAyuda",overlay);
            Link(so,"iniciar",b["etiquetaInicio"]);
            Link(so,"comoJugar",b["etiquetaAyuda"]);
            Link(so,"salir",b["etiquetaSalida"]);
            Link(so,"volver",b["etiquetaVolver"]);
            foreach (KeyValuePair<string,TMP_Text> kv in t)
            {
                SerializedProperty prop = so.FindProperty(kv.Key);
                if (prop != null) prop.objectReferenceValue = kv.Value;
            }
            ArrayProp(so,"textos",allTexts);
            ArrayProp(so,"fondos",backgrounds);
            ArrayProp(so,"superficies",surfaces);
            ArrayProp(so,"acentos",accents);
            // Asignar el Image que sirve de fondo para el botón principal.
            Link(so,"imagenBotonPrincipal",b["etiquetaInicio"].GetComponent<Image>());
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(c);
        }

        static void Link(SerializedObject so, string name, Object value)
        {
            SerializedProperty p=so.FindProperty(name);
            if(p!=null) p.objectReferenceValue=value;
        }
        static void ArrayProp<T>(SerializedObject so,string name,List<T> items) where T : Object
        {
            SerializedProperty p=so.FindProperty(name);
            if(p==null)return;
            p.arraySize=items.Count;
            for(int i=0;i<items.Count;i++)p.GetArrayElementAtIndex(i).objectReferenceValue=items[i];
        }
    }
}
#endif
