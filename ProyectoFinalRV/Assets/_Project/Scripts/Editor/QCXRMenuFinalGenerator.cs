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
    // RECONSTRUCCION SEGURA: escena NUEVA, nunca sobrescribe la escena de tus companeros.
    public static class QCXRMenuFinalGenerator
    {
        private const string GamePath = "Assets/_Project/Scenes/QCXR_MainScene.unity";
        private const string MenuPath = "Assets/_Project/Scenes/QCXR_MenuFinal.unity";
        private const string ContentPath = "Assets/_Project/Config/UI/QCXRMenuFinalContent.asset";
        private const string OldMenuPath = "Assets/_Project/Scenes/QCXR_Menu.unity";

        private static readonly Color Dark = new Color(0.08f, 0.10f, 0.12f);
        private static readonly Color Slate = new Color(0.14f, 0.17f, 0.19f);
        private static readonly Color Metal = new Color(0.25f, 0.27f, 0.28f);
        private static readonly Color Orange = new Color(0.93f, 0.41f, 0.13f);
        private static readonly Color Green = new Color(0.28f, 0.85f, 0.48f);
        private static readonly Color White = new Color(0.96f, 0.95f, 0.91f);
        private static readonly Color Gray = new Color(0.72f, 0.73f, 0.70f);

        private static readonly Dictionary<string, TMP_Text> Textos = new Dictionary<string, TMP_Text>();
        private static readonly Dictionary<string, Image> Imagenes = new Dictionary<string, Image>();
        private static readonly List<TMP_Text> Todos = new List<TMP_Text>();
        private static TMP_FontAsset Fuente;

        [MenuItem("QCXR/Menu FINAL/1. Crear menu industrial NUEVO (seguro)")]
        public static void Crear()
        {
            if (EditorApplication.isPlaying)
            {
                EditorUtility.DisplayDialog("QCXR", "Sal de Play antes de generar la escena.", "OK");
                return;
            }
            if (!File.Exists(GamePath))
            {
                EditorUtility.DisplayDialog("QCXR", "No se encontro " + GamePath, "OK");
                return;
            }
            if (File.Exists(MenuPath))
            {
                EditorUtility.DisplayDialog("Escena protegida", "QCXR_MenuFinal.unity ya existe. No se ha sobrescrito. Usa 2. Abrir menu final.", "OK");
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            Textos.Clear(); Imagenes.Clear(); Todos.Clear();
            Fuente = TMP_Settings.defaultFontAsset;

            // 1. Cargar la fabrica SOLO para copiar sus componentes XR ya funcionales.
            Scene juego = EditorSceneManager.OpenScene(GamePath, OpenSceneMode.Single);
            GameObject rigOriginal = null;
            GameObject eventOriginal = null;
            List<GameObject> auxiliares = new List<GameObject>();
            foreach (GameObject go in juego.GetRootGameObjects())
            {
                if (rigOriginal == null && (go.name.Contains("XR Origin") || go.name.Contains("XR Rig"))) rigOriginal = go;
                if (eventOriginal == null && go.GetComponent<EventSystem>()) eventOriginal = go;
                if (go.name.Contains("Interaction Manager") || go.name.Contains("XR Interaction Simulator")) auxiliares.Add(go);
            }
            if (!rigOriginal || !eventOriginal)
            {
                EditorUtility.DisplayDialog("Faltan referencias XR", "Abre y revisa la fabrica: se necesita XR Origin y EventSystem. No se modifico ningun archivo.", "OK");
                return;
            }
            Scene menu = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(menu);
            GameObject rig = Copiar(rigOriginal, menu);
            GameObject evento = Copiar(eventOriginal, menu);
            foreach (GameObject extra in auxiliares) Copiar(extra, menu);
            PrepararRigQuieto(rig);
            PrepararEventSystem(evento);

            // 2. Entorno compacto, industrial y con suelo SOLIDO.
            GameObject decorado = new GameObject("MENU - Modulo industrial compacto");
            ConstruirSala(decorado.transform);

            // 3. Panel frontal, legible, a 1.65 m de la camara XR al entrar.
            GameObject panel = new GameObject("MENU - Panel frontal XR");
            panel.transform.position = new Vector3(0f, 1.55f, 1.65f);
            panel.transform.rotation = Quaternion.Euler(0f, 180f, 0f); // cara visible al jugador
            Material marco = Material("Marco de pantalla", Slate);
            Cubo("Marco fisico industrial", panel.transform, new Vector3(0, 0, -0.038f),
                new Vector3(1.82f, 1.20f, 0.055f), marco, false);
            Cubo("Ribete superior naranja", panel.transform, new Vector3(0, .61f, -.025f),
                new Vector3(1.75f, .014f, .018f), Material("Naranja industrial", Orange), false);

            GameObject canvGO = new GameObject("Canvas - Panel de inicio", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            canvGO.transform.SetParent(panel.transform, false);
            Canvas canv = canvGO.GetComponent<Canvas>();
            canv.renderMode = RenderMode.WorldSpace;
            canv.sortingOrder = 120;
            Camera cam = rig.GetComponentInChildren<Camera>(true);
            if (cam)
            {
                cam.tag = "MainCamera";
                canv.worldCamera = cam;
            }
            canvGO.AddComponent<TrackedDeviceGraphicRaycaster>();
            RectTransform rt = canvGO.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(1280, 790);
            rt.localScale = Vector3.one * .00135f;
            canvGO.GetComponent<CanvasScaler>().dynamicPixelsPerUnit = 10f;

            // UI de solo dos botones y panel de mision sencillo.
            RectTransform baseUI = Rect("Marco UI", rt, Vector2.zero, new Vector2(1280, 790));
            Imagenes["fondoUI"] = Caja("Fondo metal oscuro", baseUI, Vector2.zero, new Vector2(1280, 790), Dark);
            Imagenes["franjaNaranja"] = Caja("Acento naranja", baseUI, new Vector2(0, 374), new Vector2(1240, 10), Orange);
            Imagenes["franjaVerde"] = Caja("Luz de estado", baseUI, new Vector2(588, 326), new Vector2(17, 17), Green);
            Texto("insignia", baseUI, new Vector2(-300, 323), new Vector2(620, 52), "UAO  /  CONTROL INDUSTRIAL", 27, Orange);
            Texto("titulo", baseUI, new Vector2(-245, 201), new Vector2(720, 125), "CONTROL DE CALIDAD XR", 69, White);
            Texto("descripcion", baseUI, new Vector2(-266, 98), new Vector2(690, 88), "Inspecciona, repara y clasifica productos.", 28, Gray);
            Button iniciar = Boton("etiquetaInicio", baseUI, new Vector2(-266, -95), new Vector2(640, 112), "INICIAR INSPECCION", Orange);
            Button salir = Boton("etiquetaSalir", baseUI, new Vector2(-266, -242), new Vector2(640, 98), "SALIR", new Color(.26f,.29f,.30f));
            Imagenes["imagenBotonInicio"] = iniciar.GetComponent<Image>();
            Imagenes["tarjetaUI"] = Caja("Ficha de tarea", baseUI, new Vector2(389, -16), new Vector2(405, 560), Slate);
            Caja("Marco ficha naranja", baseUI, new Vector2(389, 266), new Vector2(405, 4), Orange);
            Texto("tarjetaTitulo", baseUI, new Vector2(383, 196), new Vector2(354, 67), "ORDEN DE TRABAJO", 33, Orange);
            Texto("tarjetaDetalle", baseUI, new Vector2(383, 47), new Vector2(345, 200), "Recibe, repara y clasifica.", 29, White);
            Texto("indicador", baseUI, new Vector2(377, -204), new Vector2(350, 67), "SISTEMA EN ESPERA", 21, Green);
            Texto("pie", baseUI, new Vector2(0, -337), new Vector2(1160, 48), "El tutorial se inicia al entrar a la fabrica.", 23, Gray, TextAlignmentOptions.Center);
            Texto("aviso", baseUI, new Vector2(0, -382), new Vector2(1180, 32), "", 22, Orange, TextAlignmentOptions.Center);

            GameObject manager = new GameObject("MENU - Controlador de transicion");
            QCXRMenuFinalController controller = manager.AddComponent<QCXRMenuFinalController>();
            QCXRMenuFinalContent contenido = ObtenerContenido();
            Serializar(controller, iniciar, salir, panel.transform, contenido);
            controller.AplicarContenido();

            Directory.CreateDirectory("Assets/_Project/Scenes");
            EditorSceneManager.SaveScene(menu, MenuPath);
            EditorSceneManager.CloseScene(juego, true);
            SceneManager.SetActiveScene(menu);
            ActualizarBuild();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeGameObject = manager;
            EditorUtility.DisplayDialog("Menu creado", "QCXR_MenuFinal.unity se guardo como escena nueva. QCXR_Menu y QCXR_MainScene siguen intactas. Ejecuta ahora 3. Validar escena.", "OK");
        }

        [MenuItem("QCXR/Menu FINAL/2. Abrir menu final")]
        public static void Abrir()
        {
            if (!File.Exists(MenuPath)) { EditorUtility.DisplayDialog("QCXR", "Primero crea la escena con la opcion 1.", "OK"); return; }
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) EditorSceneManager.OpenScene(MenuPath, OpenSceneMode.Single);
        }

        [MenuItem("QCXR/Menu FINAL/3. Validar escena y botones")]
        public static void Validar()
        {
            int fallos = 0;
            List<string> resultado = new List<string>();
            Action<bool,string> check = (bien,mensaje) => { resultado.Add((bien ? "OK  " : "ERROR  ")+mensaje); if (!bien) fallos++; };
            Scene escena = SceneManager.GetActiveScene();
            check(escena.path == MenuPath, "Escena activa: QCXR_MenuFinal");
            QCXRMenuFinalController c = Object.FindFirstObjectByType<QCXRMenuFinalController>();
            check(c != null, "Controlador de menu");
            GameObject objetoCanvas = GameObject.Find("Canvas - Panel de inicio");
            Canvas canvas = objetoCanvas ? objetoCanvas.GetComponent<Canvas>() : null;
            check(canvas != null && canvas.renderMode == RenderMode.WorldSpace, "Canvas World Space");
            check(canvas != null && canvas.GetComponent<TrackedDeviceGraphicRaycaster>() != null, "Raycaster de UI XR");
            EventSystem es = Object.FindFirstObjectByType<EventSystem>();
            check(es != null && es.GetComponent<XRUIInputModule>() != null, "EventSystem + XRUIInputModule");
            Camera camera = Camera.main;
            check(camera != null, "Camara XR con etiqueta MainCamera");
            Button[] botones = canvas ? canvas.GetComponentsInChildren<Button>(true) : new Button[0];
            check(botones.Length == 2 && botones[0].interactable && botones[1].interactable, "Dos botones habilitados");
            GameObject piso = GameObject.Find("Suelo con colision");
            BoxCollider colPiso = piso ? piso.GetComponent<BoxCollider>() : null;
            check(colPiso != null && colPiso.enabled && !colPiso.isTrigger, "Suelo con colision solida");
            GameObject objetoRig = GameObject.Find("XR Origin (XR Rig)");
            check(objetoRig != null, "XR Origin copiado de la fabrica");
            if (objetoRig)
            {
                bool locomocionActiva = false;
                foreach (MonoBehaviour mb in objetoRig.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (!mb || !mb.enabled) continue;
                    string tipo = mb.GetType().Name;
                    if (tipo.Contains("MoveProvider") || tipo.Contains("TurnProvider") ||
                        tipo.Contains("TeleportationProvider") || tipo.Contains("ClimbProvider"))
                        locomocionActiva = true;
                }
                check(!locomocionActiva, "Locomocion desactivada en el menu");
            }
            var escenas = EditorBuildSettings.scenes;
            check(escenas.Length >= 2 && escenas[0].path == MenuPath && escenas[0].enabled && escenas[1].path == GamePath && escenas[1].enabled,
                "Build: menu final primero, fabrica segunda");
            if (c)
            {
                SerializedObject so = new SerializedObject(c);
                string[] props = { "contenido", "botonIniciar", "botonSalir", "panelRaiz", "etiquetaInicio", "etiquetaSalir" };
                foreach (string prop in props)
                {
                    SerializedProperty p = so.FindProperty(prop);
                    check(p != null && p.objectReferenceValue != null, "Referencia serializada: " + prop);
                }
            }
            string reporte = string.Join("\n", resultado.ToArray());
            Debug.Log("[QCXR Menu Final] VALIDACION\n" + reporte);
            EditorUtility.DisplayDialog(fallos == 0 ? "REVISION ESTATICA CORRECTA" : "FALTAN AJUSTES",
                reporte + "\n\nATENCION: aun hay que probar Play, Enter, clic de los controladores y APK en Quest.", "OK");
        }

        [MenuItem("QCXR/Menu FINAL/4. Aplicar textos y fuente")]
        public static void Actualizar()
        {
            QCXRMenuFinalController controller = Object.FindFirstObjectByType<QCXRMenuFinalController>();
            if (!controller) { EditorUtility.DisplayDialog("QCXR", "Abre QCXR_MenuFinal primero.", "OK"); return; }
            controller.AplicarContenido();
            EditorSceneManager.MarkSceneDirty(controller.gameObject.scene);
            EditorUtility.DisplayDialog("QCXR", "Contenido actualizado. Guarda con Ctrl+S.", "OK");
        }

        private static GameObject Copiar(GameObject source, Scene dest)
        {
            GameObject obj = Object.Instantiate(source);
            obj.name = source.name;
            SceneManager.MoveGameObjectToScene(obj, dest);
            return obj;
        }

        private static void PrepararRigQuieto(GameObject rig)
        {
            rig.transform.position = Vector3.zero;
            rig.transform.rotation = Quaternion.identity;
            // Desactivar SOLO locomocion, sin apagar interactores de UI.
            foreach (MonoBehaviour comp in rig.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (!comp) continue;
                string n = comp.GetType().Name;
                if (n.Contains("MoveProvider") || n.Contains("TurnProvider") ||
                    n.Contains("TeleportationProvider") || n.Contains("ClimbProvider") ||
                    n.Contains("CharacterControllerDriver")) comp.enabled = false;
            }
            Rigidbody cuerpo = rig.GetComponent<Rigidbody>();
            if (cuerpo) { cuerpo.useGravity = false; cuerpo.isKinematic = true; }
            // Oculta las mallas de mano exageradas del simulador, no sus rayos ni interacciones.
            foreach (Renderer r in rig.GetComponentsInChildren<Renderer>(true))
            {
                if (!(r is MeshRenderer) && !(r is SkinnedMeshRenderer)) continue;
                Transform p = r.transform;
                bool mano = false;
                while (p && p != rig.transform)
                {
                    string nombre = p.name.ToLowerInvariant();
                    if (nombre.Contains("hand") || nombre.Contains("mano") ||
                        nombre.Contains("controller model")) { mano = true; break; }
                    p = p.parent;
                }
                if (mano) r.enabled = false;
            }
        }

        private static void PrepararEventSystem(GameObject eventObject)
        {
            XRUIInputModule modulo = eventObject.GetComponent<XRUIInputModule>();
            if (!modulo)
            {
                foreach (BaseInputModule otro in eventObject.GetComponents<BaseInputModule>()) Object.DestroyImmediate(otro);
                eventObject.AddComponent<XRUIInputModule>();
            }
        }

        private static void ConstruirSala(Transform root)
        {
            Material suelo = Material("Piso acero", new Color(.24f,.24f,.23f));
            Material pared = Material("Pared beige fabrica", new Color(.52f,.48f,.41f));
            Material hierro = Material("Hierro gris", Metal);
            Material naranja = Material("Naranja industrial", Orange);
            Material franja = Material("Seguridad verde", Green);
            Cubo("Suelo con colision", root, new Vector3(0,-.095f,1.3f), new Vector3(4.6f,.19f,3.8f), suelo, true);
            Cubo("Pared trasera", root, new Vector3(0,1.45f,2.90f), new Vector3(4.6f,2.9f,.12f), pared, false);
            Cubo("Pared izquierda", root, new Vector3(-2.25f,1.45f,1.20f), new Vector3(.13f,2.9f,3.4f), hierro, false);
            Cubo("Pared derecha", root, new Vector3(2.25f,1.45f,1.20f), new Vector3(.13f,2.9f,3.4f), hierro, false);
            Cubo("Viga naranja izquierda", root, new Vector3(-1.89f,1.55f,2.79f), new Vector3(.15f,3.1f,.16f), naranja, false);
            Cubo("Viga naranja derecha", root, new Vector3(1.89f,1.55f,2.79f), new Vector3(.15f,3.1f,.16f), naranja, false);
            Cubo("Viga naranja superior", root, new Vector3(0,2.7f,2.81f), new Vector3(3.9f,.12f,.18f), naranja, false);
            Cubo("Zocalo metalico", root, new Vector3(0,.14f,2.78f), new Vector3(4.6f,.25f,.2f), hierro, false);
            for (int i=0; i<4; i++)
            {
                float x = -1.55f + i * 1.03f;
                Cubo("Marca de inspeccion "+i,root,new Vector3(x,.012f,.55f),new Vector3(.38f,.006f,.045f),franja,false);
            }
            GameObject luz = new GameObject("Luz industrial", typeof(Light));
            luz.transform.SetParent(root, false);
            luz.transform.rotation = Quaternion.Euler(48,-25,0);
            Light l = luz.GetComponent<Light>();
            l.type = LightType.Directional;
            l.color = new Color(1f,.88f,.75f);
            l.intensity = 0.95f;
        }

        private static Material Material(string nombre, Color color)
        {
            const string carpeta = "Assets/_Project/Materials/MenuFinal";
            Directory.CreateDirectory(carpeta);
            string ruta = carpeta + "/" + nombre.Replace(' ','_') + ".mat";
            Material m = AssetDatabase.LoadAssetAtPath<Material>(ruta);
            if (!m)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                if (!shader) shader = Shader.Find("Standard");
                if (!shader) shader = Shader.Find("Unlit/Color");
                if (!shader) throw new InvalidOperationException("No se encontro shader para materiales de menu.");
                m = new Material(shader);
                AssetDatabase.CreateAsset(m,ruta);
            }
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor",color);
            if (m.HasProperty("_Color")) m.SetColor("_Color",color);
            EditorUtility.SetDirty(m);
            return m;
        }

        private static GameObject Cubo(string nombre, Transform parent, Vector3 pos, Vector3 escala, Material material, bool tieneCollider)
        {
            GameObject obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            obj.name = nombre;
            obj.transform.SetParent(parent, false);
            obj.transform.localPosition = pos;
            obj.transform.localScale = escala;
            obj.GetComponent<MeshRenderer>().sharedMaterial = material;
            Collider col = obj.GetComponent<Collider>();
            if (col && !tieneCollider) Object.DestroyImmediate(col);
            return obj;
        }

        private static RectTransform Rect(string nombre, Transform parent, Vector2 pos, Vector2 size)
        {
            GameObject obj = new GameObject(nombre, typeof(RectTransform));
            obj.transform.SetParent(parent, false);
            RectTransform rt = obj.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(.5f,.5f);
            rt.pivot = new Vector2(.5f,.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = pos;
            rt.localScale = Vector3.one;
            return rt;
        }

        private static Image Caja(string nombre, Transform parent, Vector2 pos, Vector2 size, Color color)
        {
            RectTransform rt = Rect(nombre,parent,pos,size);
            Image im = rt.gameObject.AddComponent<Image>();
            im.color = color;
            im.raycastTarget = false;
            return im;
        }

        private static TextMeshProUGUI Texto(string id, Transform parent, Vector2 pos, Vector2 size, string value,
            float fontSize, Color color, TextAlignmentOptions align=TextAlignmentOptions.Left)
        {
            RectTransform rt = Rect("Texto " + id,parent,pos,size);
            TextMeshProUGUI t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            if (Fuente) t.font = Fuente;
            t.text = value;
            t.fontSize = fontSize;
            t.alignment = align;
            t.color = color;
            t.textWrappingMode = TextWrappingModes.Normal;
            t.overflowMode = TextOverflowModes.Ellipsis;
            t.raycastTarget = false;
            Textos[id] = t;
            Todos.Add(t);
            return t;
        }

        private static Button Boton(string id, Transform parent, Vector2 pos, Vector2 size, string label, Color baseColor)
        {
            RectTransform rt = Rect("BOTON - " + id,parent,pos,size);
            Image imagen = rt.gameObject.AddComponent<Image>();
            imagen.color = baseColor;
            imagen.raycastTarget = true;
            Button boton = rt.gameObject.AddComponent<Button>();
            boton.targetGraphic = imagen;
            ColorBlock colores = boton.colors;
            colores.normalColor = Color.white;
            colores.highlightedColor = new Color(.88f,.93f,.85f);
            colores.selectedColor = new Color(.85f,.91f,.86f);
            colores.pressedColor = new Color(.62f,.74f,.66f);
            colores.fadeDuration = .10f;
            boton.colors = colores;
            Texto(id,rt,Vector2.zero,size-new Vector2(14,12),label,34,White,TextAlignmentOptions.Center);
            return boton;
        }

        private static QCXRMenuFinalContent ObtenerContenido()
        {
            QCXRMenuFinalContent asset = AssetDatabase.LoadAssetAtPath<QCXRMenuFinalContent>(ContentPath);
            if (asset) return asset;
            Directory.CreateDirectory("Assets/_Project/Config/UI");
            asset = ScriptableObject.CreateInstance<QCXRMenuFinalContent>();
            AssetDatabase.CreateAsset(asset, ContentPath);
            return asset;
        }

        private static void Serializar(QCXRMenuFinalController controller, Button iniciar, Button salir,
            Transform raiz, QCXRMenuFinalContent contenido)
        {
            SerializedObject so = new SerializedObject(controller);
            Asignar(so,"contenido",contenido);
            Asignar(so,"botonIniciar",iniciar);
            Asignar(so,"botonSalir",salir);
            Asignar(so,"panelRaiz",raiz);
            foreach (var kv in Textos) Asignar(so,kv.Key,kv.Value);
            foreach (var kv in Imagenes) Asignar(so,kv.Key,kv.Value);
            SerializedProperty textos = so.FindProperty("todosLosTextos");
            textos.arraySize = Todos.Count;
            for (int i=0;i<Todos.Count;i++) textos.GetArrayElementAtIndex(i).objectReferenceValue = Todos[i];
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(controller);
        }

        private static void Asignar(SerializedObject so,string propiedad,Object valor)
        {
            SerializedProperty p=so.FindProperty(propiedad);
            if (p != null) p.objectReferenceValue=valor;
        }

        private static void ActualizarBuild()
        {
            List<EditorBuildSettingsScene> escenas = new List<EditorBuildSettingsScene>();
            escenas.Add(new EditorBuildSettingsScene(MenuPath,true));
            escenas.Add(new EditorBuildSettingsScene(GamePath,true));
            foreach (EditorBuildSettingsScene e in EditorBuildSettings.scenes)
            {
                if (e.path == MenuPath || e.path == GamePath) continue;
                // Conservar el menu viejo pero inactivo para evitar duplicar pantallas.
                if (e.path == OldMenuPath) escenas.Add(new EditorBuildSettingsScene(e.path,false));
                else escenas.Add(e);
            }
            EditorBuildSettings.scenes = escenas.ToArray();
        }
    }
}
#endif
