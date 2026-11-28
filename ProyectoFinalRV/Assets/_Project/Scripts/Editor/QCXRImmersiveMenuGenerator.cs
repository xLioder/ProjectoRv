#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using QCXR.MenuImmersive;
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
    /// <summary>
    /// Escena 3D independiente: entorno industrial, banda en funcionamiento,
    /// productos reales del proyecto y consola interactiva VR. No modifica la fabrica.
    /// </summary>
    public static class QCXRImmersiveMenuGenerator
    {
        private const string GamePath = "Assets/_Project/Scenes/QCXR_MainScene.unity";
        private const string MenuPath = "Assets/_Project/Scenes/QCXR_MenuInmersivo.unity";
        private const string ContentPath = "Assets/_Project/Config/UI/QCXRMenuImmersiveContent.asset";
        private const string MaterialFolder = "Assets/_Project/Materials/MenuImmersivoV3";

        private static readonly Color Naranja = RGB(236, 98, 27);
        private static readonly Color Pared = RGB(137, 118, 95);
        private static readonly Color Grafito = RGB(23, 34, 40);
        private static readonly Color Acero = RGB(69, 73, 72);
        private static readonly Color AceroClaro = RGB(113, 119, 112);
        private static readonly Color Oxido = RGB(96, 57, 36);
        private static readonly Color Suelo = RGB(65, 67, 64);
        private static readonly Color Negro = RGB(18, 24, 27);
        private static readonly Color Verde = RGB(66, 205, 105);
        private static readonly Color Blanco = RGB(238, 234, 219);
        private static readonly Color Amarillo = RGB(242, 181, 48);

        private static readonly Dictionary<string, Material> Mats = new Dictionary<string, Material>();
        private static readonly Dictionary<string, TMP_Text> Textos = new Dictionary<string, TMP_Text>();
        private static readonly List<TMP_Text> Todos = new List<TMP_Text>();
        private static TMP_FontAsset Fuente;
        private static Button BotonInicio;
        private static Button BotonSalida;
        private static Image ImagenInicio;
        private static Image ImagenSalida;
        private static Image ImagenAcento;

        private static Color RGB(int r, int g, int b)
        {
            return new Color(r/255f, g/255f, b/255f, 1f);
        }

        [MenuItem("QCXR/Menu Inmersivo V3/1 - Crear nueva escena 3D")]
        public static void Crear()
        {
            if (EditorApplication.isPlaying)
            {
                EditorUtility.DisplayDialog("QCXR", "Primero detén Play.", "OK");
                return;
            }
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(GamePath) == null)
            {
                EditorUtility.DisplayDialog("No se encontró la fábrica", GamePath, "OK");
                return;
            }
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(MenuPath) != null)
            {
                EditorUtility.DisplayDialog("Protección de trabajo", "La escena QCXR_MenuInmersivo ya existe y no se sobrescribirá. Puedes abrirla o duplicarla antes de editar.", "OK");
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            Mats.Clear(); Textos.Clear(); Todos.Clear();
            Fuente = TMP_Settings.defaultFontAsset;
            Scene original = EditorSceneManager.OpenScene(GamePath, OpenSceneMode.Single);
            GameObject rigOrigen = null;
            GameObject eventoOrigen = null;
            List<GameObject> simuladores = new List<GameObject>();
            foreach (GameObject root in original.GetRootGameObjects())
            {
                if (rigOrigen == null && (root.name.Contains("XR Origin") || root.name.Contains("XR Rig"))) rigOrigen = root;
                if (eventoOrigen == null && root.GetComponent<EventSystem>() != null) eventoOrigen = root;
                if (root.name.Contains("XR Interaction Simulator")) simuladores.Add(root);
            }
            if (!rigOrigen || !eventoOrigen)
            {
                EditorUtility.DisplayDialog("Rig XR no encontrado", "Se necesita XR Origin y EventSystem de QCXR_MainScene. No se han sobrescrito archivos.", "OK");
                return;
            }

            Scene escena = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(escena);
            GameObject rig = CopiarObjeto(rigOrigen, escena);
            GameObject evento = CopiarObjeto(eventoOrigen, escena);
            foreach (GameObject simulador in simuladores) CopiarObjeto(simulador, escena);
            PrepararXR(rig);
            PrepararEventos(evento);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = RGB(133, 126, 112);
            RenderSettings.fog = false;

            GameObject entorno = new GameObject("01 - ESCENARIO INDUSTRIAL 3D (real)");
            ConstruirEntorno(entorno.transform);
            ConstruirBanda(entorno.transform);
            ConstruirDecoracion(entorno.transform);
            GameObject consola = ConstruirConsola(entorno.transform);
            CrearInterfazConsola(consola.transform, rig.GetComponentInChildren<Camera>(true));

            QCXRImmersiveMenuContent contenido = ObtenerContenido();
            GameObject rootControlador = new GameObject("02 - CONTROL DEL MENU (solo esta escena)");
            QCXRImmersiveMenuController controlador = rootControlador.AddComponent<QCXRImmersiveMenuController>();
            Conectar(controlador, contenido);
            controlador.AplicarContenido();

            Directory.CreateDirectory("Assets/_Project/Scenes");
            EditorSceneManager.SaveScene(escena, MenuPath);
            EditorSceneManager.CloseScene(original, true);
            SceneManager.SetActiveScene(escena);
            ActualizarBuild();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeGameObject = consola;
            EditorGUIUtility.PingObject(consola);
            EditorUtility.DisplayDialog("QCXR - Escena creada", "Escena: QCXR_MenuInmersivo.unity\n\nNo se sobrescribieron el juego ni los menús anteriores.\n\nSiguiente: QCXR > Menu Inmersivo V3 > 3 - Validar estructura.", "OK");
        }

        [MenuItem("QCXR/Menu Inmersivo V3/2 - Abrir escena creada")]
        public static void Abrir()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(MenuPath) == null)
            {
                EditorUtility.DisplayDialog("QCXR", "Primero ejecuta la opción 1.", "OK");
                return;
            }
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                EditorSceneManager.OpenScene(MenuPath, OpenSceneMode.Single);
        }

        [MenuItem("QCXR/Menu Inmersivo V3/3 - Validar estructura")]
        public static void Validar()
        {
            List<string> pruebas = new List<string>();
            int errores = 0;
            Action<bool, string> check = (ok, nombre) => { pruebas.Add((ok ? "OK  " : "FALTA  ") + nombre); if (!ok) errores++; };
            Scene activa = SceneManager.GetActiveScene();
            check(activa.path == MenuPath, "Escena de menú inmersivo activa");
            var controller = Object.FindFirstObjectByType<QCXRImmersiveMenuController>();
            check(controller != null, "Controlador independiente del menú");
            EventSystem ev = Object.FindFirstObjectByType<EventSystem>();
            check(ev && ev.GetComponent<XRUIInputModule>() != null, "EventSystem + XRUIInputModule");
            Canvas canvas = null;
            GameObject canvasGo = GameObject.Find("Consola - Canvas UI XR");
            if (canvasGo) canvas = canvasGo.GetComponent<Canvas>();
            check(canvas && canvas.renderMode == RenderMode.WorldSpace, "Canvas adherido al terminal 3D");
            check(canvas && canvas.GetComponent<TrackedDeviceGraphicRaycaster>(), "XR Raycaster");
            Button[] botones = canvas ? canvas.GetComponentsInChildren<Button>(true) : new Button[0];
            check(botones.Length == 2 && botones[0].interactable && botones[1].interactable, "Dos botones activos");
            GameObject piso = GameObject.Find("SUELO - Hormigon con colision");
            check(piso && piso.GetComponent<BoxCollider>() && !piso.GetComponent<BoxCollider>().isTrigger, "Suelo sólido");
            check(GameObject.Find("BANDA - Exposicion de productos") != null, "Banda industrial en el escenario");
            check(GameObject.Find("PRODUCTO - Exposicion central") != null, "Producto 3D decorativo");
            check(Camera.main != null, "Camara del XR Origin");
            GameObject rig = GameObject.Find("XR Origin (XR Rig)");
            check(rig != null, "XR Origin reutilizado del juego");
            if (rig)
            {
                bool locomocion = false;
                foreach (MonoBehaviour b in rig.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (!b || !b.enabled) continue;
                    string t = b.GetType().Name;
                    if (t.Contains("MoveProvider") || t.Contains("TurnProvider") || t.Contains("TeleportationProvider") || t.Contains("ClimbProvider")) locomocion = true;
                }
                check(!locomocion, "Locomocion desactivada solo en el menu");
            }
            var builds = EditorBuildSettings.scenes;
            check(builds.Length > 1 && builds[0].path == MenuPath && builds[0].enabled && builds[1].path == GamePath && builds[1].enabled, "Build: menu 0 / juego 1");
            if (controller)
            {
                SerializedObject so = new SerializedObject(controller);
                foreach (string n in new[] { "contenido", "botonIniciar", "botonSalir", "iniciarTexto", "salirTexto", "titulo" })
                {
                    SerializedProperty p = so.FindProperty(n);
                    check(p != null && p.objectReferenceValue != null, "Referencia: " + n);
                }
            }
            string texto = string.Join("\n", pruebas);
            Debug.Log("[QCXR Menu Inmersivo V3] VALIDADOR\n" + texto);
            EditorUtility.DisplayDialog(errores == 0 ? "Estructura lista para probar" : "Revisa estos puntos", texto + "\n\nLa validacion NO sustituye el Play, el clic XR ni la prueba en Meta Quest.", "OK");
        }

        [MenuItem("QCXR/Menu Inmersivo V3/4 - Aplicar contenido editado")]
        public static void ActualizarTextos()
        {
            var controlador = Object.FindFirstObjectByType<QCXRImmersiveMenuController>();
            if (!controlador)
            {
                EditorUtility.DisplayDialog("QCXR", "Primero abre QCXR_MenuInmersivo.", "OK"); return;
            }
            controlador.AplicarContenido();
            EditorSceneManager.MarkSceneDirty(controlador.gameObject.scene);
            Debug.Log("[QCXR] Textos actualizados. Guarda con Ctrl+S.");
        }

        private static GameObject CopiarObjeto(GameObject source, Scene escena)
        {
            GameObject copia = Object.Instantiate(source);
            copia.name = source.name;
            SceneManager.MoveGameObjectToScene(copia, escena);
            return copia;
        }

        private static void PrepararXR(GameObject rig)
        {
            rig.transform.position = Vector3.zero;
            rig.transform.rotation = Quaternion.identity;
            foreach (MonoBehaviour comp in rig.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (!comp) continue;
                string nombre = comp.GetType().Name;
                if (nombre.Contains("MoveProvider") || nombre.Contains("TurnProvider") ||
                    nombre.Contains("TeleportationProvider") || nombre.Contains("ClimbProvider") ||
                    nombre.Contains("CharacterControllerDriver")) comp.enabled = false;
            }
            // El menu no requiere colision fisica del jugador; evita gravedad no deseada.
            foreach (CharacterController cc in rig.GetComponentsInChildren<CharacterController>(true)) cc.enabled = false;
            foreach (Rigidbody rb in rig.GetComponentsInChildren<Rigidbody>(true))
                if (rb.gameObject == rig) { rb.useGravity = false; rb.isKinematic = true; }
            Camera cam = rig.GetComponentInChildren<Camera>(true);
            if (cam) cam.tag = "MainCamera";
            // Solo mallas gigantes de manos de prueba, no los interactores ni los rayos.
            foreach (Renderer render in rig.GetComponentsInChildren<Renderer>(true))
            {
                Transform t = render.transform;
                bool mano = false;
                while (t && t != rig.transform)
                {
                    string n = t.name.ToLowerInvariant();
                    if (n.Contains("hand model") || n.Contains("hand visual") || n.Contains("mano visual")) { mano = true; break; }
                    t = t.parent;
                }
                if (mano) render.enabled = false;
            }
        }

        private static void PrepararEventos(GameObject evento)
        {
            XRUIInputModule xr = evento.GetComponent<XRUIInputModule>();
            if (xr == null)
            {
                foreach (BaseInputModule other in evento.GetComponents<BaseInputModule>())
                    Object.DestroyImmediate(other);
                evento.AddComponent<XRUIInputModule>();
            }
            else
            {
                foreach (BaseInputModule other in evento.GetComponents<BaseInputModule>())
                    if (other != xr) other.enabled = false;
            }
        }

        private static Material Mat(string nombre, Color c, bool emision = false)
        {
            if (Mats.TryGetValue(nombre, out Material m)) return m;
            Directory.CreateDirectory(MaterialFolder);
            string path = MaterialFolder + "/" + nombre + ".mat";
            m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!m)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                if (!shader) shader = Shader.Find("Standard");
                if (!shader) shader = Shader.Find("Unlit/Color");
                if (!shader) throw new InvalidOperationException("No se encontró un shader compatible");
                m = new Material(shader) { name = nombre };
                AssetDatabase.CreateAsset(m, path);
            }
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Color")) m.SetColor("_Color", c);
            if (emision && m.HasProperty("_EmissionColor"))
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", c * .65f);
            }
            EditorUtility.SetDirty(m);
            Mats[nombre] = m;
            return m;
        }
        private static GameObject Objeto(string nombre, Transform parent)
        {
            var go = new GameObject(nombre);
            go.transform.SetParent(parent, false);
            return go;
        }
        private static GameObject Cubo(string nombre, Transform parent, Vector3 pos, Vector3 size, Material mat, bool collider = false)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = nombre;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = size;
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            if (!collider) Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }
        private static GameObject Cilindro(string nombre, Transform parent, Vector3 pos, float radio, float altura,
            Material mat, Vector3 eje = default(Vector3))
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = nombre;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = new Vector3(radio * 2f, altura * .5f, radio * 2f);
            if (eje != Vector3.zero) go.transform.localRotation = Quaternion.FromToRotation(Vector3.up, eje.normalized);
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }
        private static void Barra(string name, Transform p, Vector3 desde, Vector3 hasta, float radio, Material mat)
        {
            Vector3 d = hasta - desde;
            Cilindro(name, p, (hasta + desde) * .5f, radio, d.magnitude, mat, d);
        }

        private static void ConstruirEntorno(Transform root)
        {
            Material concreto = Mat("01_Piso_Industrial", Suelo);
            Material beige = Mat("02_Pared_Fabrica_Beige", Pared);
            Material acero = Mat("03_Acero_Oscuro", Acero);
            Material metal = Mat("04_Acero_Claro", AceroClaro);
            Material naranja = Mat("05_Naranja_Estructura", Naranja);
            Material oscuro = Mat("06_Grafito", Grafito);
            Material amarillo = Mat("07_Advertencia", Amarillo);
            Material negro = Mat("08_Negro", Negro);
            Material oxido = Mat("09_Oxido", Oxido);
            Cubo("SUELO - Hormigon con colision", root, new Vector3(0,-.12f,2.1f), new Vector3(7.6f,.24f,8.6f), concreto, true);
            Cubo("PARED - Fondo industrial", root, new Vector3(0,2.05f,5.7f), new Vector3(7.6f,4.2f,.18f), beige);
            Cubo("PARED - Izquierda", root, new Vector3(-3.8f,2.05f,2.1f), new Vector3(.17f,4.2f,7.4f), beige);
            Cubo("PARED - Derecha", root, new Vector3(3.8f,2.05f,2.1f), new Vector3(.17f,4.2f,7.4f), beige);
            Cubo("TECHO - Acero estructural", root, new Vector3(0,4.12f,2.1f), new Vector3(7.7f,.18f,7.6f), oscuro);

            // Perfiles metalicos reales, traviesas, tubos y paneles; la habitacion no es un cubo vacio.
            for (int i = 0; i < 4; i++)
            {
                float z = -.6f + i * 2.02f;
                Cubo("Pilar naranja L " + i, root, new Vector3(-3.48f,2.0f,z), new Vector3(.24f,4f,.23f), naranja);
                Cubo("Pilar naranja R " + i, root, new Vector3(3.48f,2.0f,z), new Vector3(.24f,4f,.23f), naranja);
                Cubo("Traviesa superior " + i, root, new Vector3(0,3.74f,z), new Vector3(7.05f,.22f,.20f), naranja);
                Cubo("Guia lateral L " + i, root, new Vector3(-3.5f,.42f,z), new Vector3(.18f,.70f,.12f), acero);
                Cubo("Guia lateral R " + i, root, new Vector3(3.5f,.42f,z), new Vector3(.18f,.70f,.12f), acero);
            }
            for (int k = 0; k < 3; k++)
            {
                float x = -2.15f + k * 2.15f;
                Barra("Tubo de ventilacion " + k, root, new Vector3(x,3.52f,-.6f), new Vector3(x,3.52f,5.55f), .065f, metal);
                Cubo("Canaleta industrial " + k, root, new Vector3(x,3.38f,2.0f), new Vector3(.17f,.07f,5.4f), acero);
            }
            for (int i=0; i<8; i++)
            {
                float z = -.3f + i*.7f;
                Cubo("Segmento seguridad izquierda " + i,root,new Vector3(-3.5f,.01f,z),new Vector3(.46f,.012f,.085f), i%2==0 ? amarillo:negro);
                Cubo("Segmento seguridad derecha " + i,root,new Vector3(3.5f,.01f,z),new Vector3(.46f,.012f,.085f),i%2==0 ? amarillo:negro);
            }
            Cubo("Zocalo de metal",root,new Vector3(0,.23f,5.50f),new Vector3(7.4f,.46f,.16f),acero);
            Cubo("Ventanilla de mantenimiento izquierda",root,new Vector3(-2.45f,1.6f,5.54f),new Vector3(1.6f,1.3f,.08f),oscuro);
            Cubo("Marco ventana superior",root,new Vector3(-2.45f,2.32f,5.48f),new Vector3(1.73f,.10f,.14f),naranja);
            Cubo("Marco ventana inferior",root,new Vector3(-2.45f,.90f,5.48f),new Vector3(1.73f,.10f,.14f),naranja);
            for (int i=0; i<3; i++)
                Cubo("Rejilla ventana " + i,root,new Vector3(-2.45f,.98f+i*.42f,5.42f),new Vector3(1.57f,.045f,.05f),metal);
            Cubo("Gabinete electrico",root,new Vector3(2.57f,1.48f,5.48f),new Vector3(1.2f,1.5f,.20f),acero);
            Cubo("Puerta gabinete",root,new Vector3(2.57f,1.48f,5.34f),new Vector3(1.07f,1.36f,.07f),oscuro);
            Cubo("Tirador",root,new Vector3(2.96f,1.48f,5.26f),new Vector3(.06f,.30f,.08f),metal);
            TextMeshPro senal = Letra3D("ROTULO - Control de Calidad",root,"CONTROL DE CALIDAD XR",3.6f,new Vector3(0,2.88f,5.43f),Quaternion.Euler(0,180,0),Blanco);
            senal.fontStyle = FontStyles.Bold;

            GameObject direccional = Objeto("LUZ - Ambiente industrial",root);
            Light sol = direccional.AddComponent<Light>();
            sol.type=LightType.Directional; sol.intensity=.78f; sol.color=RGB(255,226,192);
            direccional.transform.rotation=Quaternion.Euler(42,-20,0);
            for (int i=0; i<3; i++)
            {
                float x=-2.7f+i*2.7f;
                Cubo("Carcasa luz cenital "+i,root,new Vector3(x,3.79f,1.75f),new Vector3(.83f,.10f,.34f),acero);
                Cubo("Difusor de luz "+i,root,new Vector3(x,3.73f,1.75f),new Vector3(.73f,.02f,.27f),Mat("10_Luz_Caliente",RGB(242,223,167),true));
            }
        }

        private static void ConstruirBanda(Transform root)
        {
            Transform belt = Objeto("BANDA - Exposicion de productos",root).transform;
            Material acero=Mat("03_Acero_Oscuro",Acero);
            Material metal=Mat("04_Acero_Claro",AceroClaro);
            Material naranja=Mat("05_Naranja_Estructura",Naranja);
            Material goma=Mat("11_Goma_Banda",RGB(27,31,31));
            Material beige=Mat("12_Pieza_Clara",RGB(208,203,176));
            Cubo("Banda - Faja",belt,new Vector3(0,.84f,3.45f),new Vector3(5.0f,.10f,.80f),goma);
            Cubo("Banda - Chasis frontal",belt,new Vector3(0,.68f,3.01f),new Vector3(5.10f,.25f,.11f),naranja);
            Cubo("Banda - Chasis trasero",belt,new Vector3(0,.68f,3.89f),new Vector3(5.10f,.25f,.11f),naranja);
            Cubo("Carril frontal",belt,new Vector3(0,1.01f,3.03f),new Vector3(5.0f,.095f,.055f),metal);
            Cubo("Carril trasero",belt,new Vector3(0,1.01f,3.86f),new Vector3(5.0f,.095f,.055f),metal);
            for (int i=0;i<15;i++)
            {
                float x=-2.34f+i*.334f;
                GameObject roll=Cilindro("Rodillo en movimiento "+i,belt,new Vector3(x,.895f,3.45f),.045f,.75f,metal,Vector3.forward);
                roll.AddComponent<QCXRMenuAmbientAnimation>().ConfigurarRotacion(Vector3.up,75f);
            }
            for (int i=0;i<4;i++)
            {
                float x=-2.25f+i*1.5f;
                Cubo("Pata industrial - adelante "+i,belt,new Vector3(x,.34f,3.1f),new Vector3(.12f,.68f,.12f),acero);
                Cubo("Pata industrial - atras "+i,belt,new Vector3(x,.34f,3.8f),new Vector3(.12f,.68f,.12f),acero);
            }
            Cubo("Zona de inspeccion iluminada",belt,new Vector3(0,.901f,3.45f),new Vector3(.80f,.012f,.75f),Mat("13_Estacion_Verde",Verde,true));
            // En lo posible usan los modelos 3D originales, sin controladores de juego.
            ProductoExhibicion("PRODUCTO - Exposicion central",belt,new Vector3(0,1.15f,3.45f),"Assets/_Project/Prefabs/Products/Product_Robot.prefab",beige);
            ProductoExhibicion("PRODUCTO - Exposicion derecha",belt,new Vector3(1.53f,1.12f,3.45f),"Assets/_Project/Prefabs/Products/Product_Radio.prefab",beige);
            Cubo("Cabina del escaner - izquierda",belt,new Vector3(-.58f,1.53f,3.45f),new Vector3(.10f,1.25f,.13f),naranja);
            Cubo("Cabina del escaner - derecha",belt,new Vector3(.58f,1.53f,3.45f),new Vector3(.10f,1.25f,.13f),naranja);
            Cubo("Cabina del escaner - traviesa",belt,new Vector3(0,2.16f,3.45f),new Vector3(1.32f,.10f,.13f),metal);
            Cubo("Escaner de linea",belt,new Vector3(0,2.12f,3.36f),new Vector3(.75f,.05f,.07f),Mat("14_Escaner",Verde,true));
            foreach (float x in new[] {-2.45f,2.45f})
                Cilindro("Engranaje de extremo",belt,new Vector3(x,.68f,3.01f),.19f,.08f,metal,Vector3.forward)
                    .AddComponent<QCXRMenuAmbientAnimation>().ConfigurarRotacion(Vector3.up,32f);
        }

        private static void ProductoExhibicion(string nombre, Transform root, Vector3 pos, string prefabPath, Material alternativa)
        {
            GameObject anchor=Objeto(nombre,root);
            anchor.transform.localPosition=pos;
            GameObject prefab=AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab)
            {
                GameObject modelo=(GameObject)PrefabUtility.InstantiatePrefab(prefab,anchor.transform);
                modelo.name="Modelo original decorativo (scripts inactivos)";
                modelo.transform.localPosition=Vector3.zero;
                modelo.transform.localRotation=Quaternion.identity;
                modelo.transform.localScale=Vector3.one*.60f;
                foreach (MonoBehaviour b in modelo.GetComponentsInChildren<MonoBehaviour>(true)) if (b) b.enabled=false;
                foreach (Collider c in modelo.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
                foreach (Rigidbody r in modelo.GetComponentsInChildren<Rigidbody>(true)) Object.DestroyImmediate(r);
            }
            else
            {
                Cubo("Producto industrial de respaldo",anchor.transform,Vector3.zero,new Vector3(.32f,.28f,.30f),alternativa);
                Cubo("Base del producto",anchor.transform,new Vector3(0,-.17f,0),new Vector3(.22f,.07f,.22f),Mat("03_Acero_Oscuro",Acero));
            }
            anchor.AddComponent<QCXRMenuAmbientAnimation>().ConfigurarOscilacion(Vector3.up,.013f,1f);
        }

        private static void ConstruirDecoracion(Transform root)
        {
            Material acero=Mat("03_Acero_Oscuro",Acero);
            Material metal=Mat("04_Acero_Claro",AceroClaro);
            Material naranja=Mat("05_Naranja_Estructura",Naranja);
            Material oscuro=Mat("06_Grafito",Grafito);
            Material oxido=Mat("09_Oxido",Oxido);
            Material verde=Mat("13_Estacion_Verde",Verde,true);
            Material rojo=Mat("15_Rojo_Alarma",RGB(220,52,27),true);

            for (int i=0;i<3;i++)
            {
                float z=2.65f+i*.85f;
                Cubo("Rack de cajas - soporte "+i,root,new Vector3(-3.02f,1.15f,z),new Vector3(.12f,2.30f,.10f),acero);
                Cubo("Rack de cajas - charola "+i,root,new Vector3(-2.72f,.55f+i*.46f,z),new Vector3(.60f,.08f,.58f),metal);
                Cubo("Contenedor de recambio "+i,root,new Vector3(-2.72f,.68f+i*.46f,z),new Vector3(.36f,.22f,.42f),i%2==0?oxido:oscuro);
            }
            Cubo("Zona de herramientas - mesa",root,new Vector3(2.77f,.66f,2.82f),new Vector3(.96f,.13f,.71f),metal);
            for (int i=0;i<4;i++)
            {
                float z=2.5f+i*.18f;
                Cubo("Herramienta ornamental "+i,root,new Vector3(2.58f+i*.14f,.78f,z),new Vector3(.28f,.075f,.09f),i%2==0?naranja:acero);
            }
            Cubo("Gabinete de energia lateral",root,new Vector3(2.9f,1.29f,4.5f),new Vector3(.65f,1.08f,.39f),oscuro);
            for (int i=0;i<3;i++)
                Cubo("Pantalla LED industrial "+i,root,new Vector3(2.67f+i*.17f,1.54f,4.24f),new Vector3(.09f,.09f,.025f),i==2?rojo:verde);
            Barra("Cable de energia",root,new Vector3(3.02f,1.84f,4.5f),new Vector3(3.02f,3.65f,4.5f),.045f,acero);
            Cubo("Rotulo cautela",root,new Vector3(-1.4f,2.15f,5.45f),new Vector3(1.7f,.20f,.06f),naranja);
            TextMeshPro txt = Letra3D("MARCA - Linea activa",root,"LINEA AUTOMATIZADA   /   AREA 01",1.70f,new Vector3(-1.4f,2.15f,5.40f),Quaternion.Euler(0,180,0),Blanco);
            txt.fontStyle=FontStyles.Bold;
            GameObject baliza=Objeto("Baliza de inspeccion (verde)",root);
            baliza.transform.localPosition=new Vector3(1.15f,2.45f,3.5f);
            Cubo("Base de baliza",baliza.transform,Vector3.zero,new Vector3(.14f,.12f,.14f),acero);
            Cubo("Luz de estado",baliza.transform,new Vector3(0,.16f,0),new Vector3(.12f,.19f,.12f),verde);
            Light l=baliza.AddComponent<Light>(); l.type=LightType.Point; l.color=Verde; l.range=1.6f; l.intensity=.3f; l.shadows=LightShadows.None;
            baliza.AddComponent<QCXRMenuAmbientAnimation>().ConfigurarLuz(.9f);
        }

        private static GameObject ConstruirConsola(Transform root)
        {
            // Terminal real: panel industrial 3D en soporte metalico, no pantalla flotando en un cubo.
            GameObject consola=Objeto("TERMINAL 3D - Puesto de control de turno",root);
            consola.transform.position=new Vector3(0,1.40f,1.62f);
            consola.transform.rotation=Quaternion.Euler(0,180,0);
            Material acero=Mat("03_Acero_Oscuro",Acero);
            Material metal=Mat("04_Acero_Claro",AceroClaro);
            Material naranja=Mat("05_Naranja_Estructura",Naranja);
            Material negro=Mat("08_Negro",Negro);
            Material goma=Mat("11_Goma_Banda",RGB(27,31,31));
            Cubo("Bastidor de pantalla",consola.transform,new Vector3(0,.03f,-.12f),new Vector3(1.68f,1.13f,.20f),acero);
            Cubo("Cristal protector oscuro",consola.transform,new Vector3(0,.03f,.00f),new Vector3(1.54f,.94f,.028f),negro);
            Cubo("Marco superior naranja",consola.transform,new Vector3(0,.61f,.015f),new Vector3(1.70f,.055f,.08f),naranja);
            Cubo("Marco inferior",consola.transform,new Vector3(0,-.54f,.015f),new Vector3(1.70f,.065f,.07f),metal);
            Cubo("Marco izquierdo",consola.transform,new Vector3(-.83f,.02f,.01f),new Vector3(.058f,1.11f,.08f),metal);
            Cubo("Marco derecho",consola.transform,new Vector3(.83f,.02f,.01f),new Vector3(.058f,1.11f,.08f),metal);
            Cubo("Cubierta de control inferior",consola.transform,new Vector3(0,-.67f,-.22f),new Vector3(1.55f,.15f,.50f),acero);
            Cubo("Ribete del panel",consola.transform,new Vector3(0,-.61f,.03f),new Vector3(1.37f,.035f,.08f),naranja);
            Cubo("Tubo de soporte",consola.transform,new Vector3(0,-1.10f,-.30f),new Vector3(.22f,.86f,.22f),metal);
            Cubo("Pie de apoyo anclado",consola.transform,new Vector3(0,-1.48f,-.29f),new Vector3(.77f,.12f,.52f),acero);
            Cilindro("Dial selector decorativo",consola.transform,new Vector3(.61f,-.66f,.04f),.12f,.045f,naranja,Vector3.forward);
            for (int i=0;i<4;i++)
                Cubo("Tornillo frontal "+i,consola.transform,new Vector3(i%2==0?-.78f:.78f,i<2?-.51f:.55f,.047f),new Vector3(.028f,.028f,.012f),metal);
            return consola;
        }

        private static void CrearInterfazConsola(Transform consola, Camera cam)
        {
            GameObject go=new GameObject("Consola - Canvas UI XR",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler));
            go.transform.SetParent(consola,false);
            go.transform.localPosition=new Vector3(0,.035f,.030f);
            go.transform.localRotation=Quaternion.identity;
            Canvas c=go.GetComponent<Canvas>();
            c.renderMode=RenderMode.WorldSpace;
            c.sortingOrder=100;
            c.worldCamera=cam;
            if (cam) cam.tag="MainCamera";
            go.AddComponent<TrackedDeviceGraphicRaycaster>();
            RectTransform root=go.GetComponent<RectTransform>();
            root.sizeDelta=new Vector2(1280,760);
            root.localScale=Vector3.one*.00113f;
            go.GetComponent<CanvasScaler>().dynamicPixelsPerUnit=10;
            RectTransform contenido=Rect("Panel de control mecanico",root,Vector2.zero,new Vector2(1280,760));
            Panel("Barra superior",contenido,new Vector2(0,337),new Vector2(1220,6),Naranja);
            Text("cabecera",contenido,new Vector2(0,285),new Vector2(1100,54),"UAO IMMERSIVE SOLUTIONS / PLANTA 01",26,Amarillo,TextAlignmentOptions.Left);
            Text("titulo",contenido,new Vector2(-210,138),new Vector2(690,185),"CONTROL DE\nCALIDAD XR",67,Blanco,TextAlignmentOptions.Left);
            Text("bajada",contenido,new Vector2(-220,19),new Vector2(680,75),"Toma tu puesto en la linea de inspeccion.",27,Blanco,TextAlignmentOptions.Left);
            Panel("Separador vertical",contenido,new Vector2(198,16),new Vector2(4,390),AceroClaro);
            Text("detalle",contenido,new Vector2(365,117),new Vector2(364,140),"Inspecciona  ·  Repara  ·  Clasifica",27,Amarillo,TextAlignmentOptions.Left);
            Text("estado",contenido,new Vector2(363,-23),new Vector2(370,87),"SISTEMA PREPARADO / ESTACION 01",25,Verde,TextAlignmentOptions.Left);
            Text("ayuda",contenido,new Vector2(360,-130),new Vector2(362,82),"Al comenzar, sigue el tutorial de la fabrica.",22,Blanco,TextAlignmentOptions.Left);
            BotonInicio=Boton("BOTON FISICO UI - Iniciar",contenido,new Vector2(-340,-155),new Vector2(640,96),"iniciarTexto","INICIAR TURNO",Naranja);
            BotonSalida=Boton("BOTON FISICO UI - Salir",contenido,new Vector2(-340,-270),new Vector2(640,82),"salirTexto","SALIR",Grafito);
            ImagenInicio=BotonInicio.GetComponent<Image>();
            ImagenSalida=BotonSalida.GetComponent<Image>();
            ImagenAcento=Panel("Indicador naranja",contenido,new Vector2(567,286),new Vector2(20,20),Naranja);
            Text("aviso",contenido,new Vector2(0,-352),new Vector2(1150,35),"",22,Amarillo,TextAlignmentOptions.Center);
        }

        private static RectTransform Rect(string nombre,Transform parent,Vector2 pos,Vector2 size)
        {
            var go=new GameObject(nombre,typeof(RectTransform));
            go.transform.SetParent(parent,false);
            RectTransform r=go.GetComponent<RectTransform>();
            r.anchorMin=r.anchorMax=new Vector2(.5f,.5f);
            r.pivot=new Vector2(.5f,.5f);
            r.sizeDelta=size;
            r.anchoredPosition=pos;
            r.localScale=Vector3.one;
            return r;
        }
        private static Image Panel(string nombre, Transform parent, Vector2 pos, Vector2 size, Color color)
        {
            RectTransform r=Rect(nombre,parent,pos,size);
            Image i=r.gameObject.AddComponent<Image>();i.color=color;i.raycastTarget=false;
            return i;
        }
        private static TextMeshProUGUI Text(string field, Transform p, Vector2 pos, Vector2 size, string s, float fontSize, Color color, TextAlignmentOptions a)
        {
            RectTransform r=Rect("Texto - "+field,p,pos,size);
            TextMeshProUGUI t=r.gameObject.AddComponent<TextMeshProUGUI>();
            if (Fuente) t.font=Fuente;
            t.text=s;t.fontSize=fontSize;t.color=color;t.alignment=a;
            t.textWrappingMode=TextWrappingModes.Normal;
            t.overflowMode=TextOverflowModes.Ellipsis;
            t.raycastTarget=false;
            Textos[field]=t;Todos.Add(t);
            return t;
        }
        private static Button Boton(string nombre,Transform parent,Vector2 pos,Vector2 size,string field,string texto,Color color)
        {
            RectTransform r=Rect(nombre,parent,pos,size);
            Image fondo=r.gameObject.AddComponent<Image>();fondo.color=color;fondo.raycastTarget=true;
            Button b=r.gameObject.AddComponent<Button>();b.targetGraphic=fondo;
            ColorBlock c=b.colors;
            c.normalColor=Color.white;
            c.highlightedColor=RGB(238,231,203);
            c.pressedColor=RGB(185,205,184);
            c.selectedColor=RGB(219,225,198);
            c.fadeDuration=.08f;
            b.colors=c;
            Text(field,r,Vector2.zero,size-new Vector2(22,14),texto,35,Blanco,TextAlignmentOptions.Center);
            return b;
        }
        private static TextMeshPro Letra3D(string name,Transform p,string value,float font,Vector3 pos,Quaternion rotation,Color color)
        {
            GameObject go=Objeto(name,p);
            go.transform.localPosition=pos;go.transform.localRotation=rotation;
            go.transform.localScale=Vector3.one*.06f;
            var t=go.AddComponent<TextMeshPro>();
            if (Fuente) t.font=Fuente;
            t.text=value;
            t.fontSize=font;
            t.color=color;
            t.alignment=TextAlignmentOptions.Center;
            t.enableAutoSizing=false;
            t.textWrappingMode=TextWrappingModes.NoWrap;
            Textos["marcaEscenario"]=t;
            Todos.Add(t);
            return t;
        }

        private static QCXRImmersiveMenuContent ObtenerContenido()
        {
            Directory.CreateDirectory("Assets/_Project/Config/UI");
            QCXRImmersiveMenuContent o=AssetDatabase.LoadAssetAtPath<QCXRImmersiveMenuContent>(ContentPath);
            if (o) return o;
            o=ScriptableObject.CreateInstance<QCXRImmersiveMenuContent>();
            AssetDatabase.CreateAsset(o,ContentPath);
            return o;
        }
        private static void Conectar(QCXRImmersiveMenuController controller,QCXRImmersiveMenuContent asset)
        {
            SerializedObject so=new SerializedObject(controller);
            Asignar(so,"contenido",asset);
            Asignar(so,"botonIniciar",BotonInicio);
            Asignar(so,"botonSalir",BotonSalida);
            Asignar(so,"botonIniciarFondo",ImagenInicio);
            Asignar(so,"botonSalirFondo",ImagenSalida);
            Asignar(so,"acento",ImagenAcento);
            foreach (KeyValuePair<string,TMP_Text> kv in Textos) Asignar(so,kv.Key,kv.Value);
            SerializedProperty texts=so.FindProperty("todosLosTextos");
            texts.arraySize=Todos.Count;
            for (int i=0;i<Todos.Count;i++) texts.GetArrayElementAtIndex(i).objectReferenceValue=Todos[i];
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(controller);
        }
        private static void Asignar(SerializedObject so,string property,Object obj)
        {
            SerializedProperty p=so.FindProperty(property);
            if (p != null) p.objectReferenceValue=obj;
        }
        private static void ActualizarBuild()
        {
            List<EditorBuildSettingsScene> lista=new List<EditorBuildSettingsScene>
            {
                new EditorBuildSettingsScene(MenuPath,true),new EditorBuildSettingsScene(GamePath,true)
            };
            foreach (EditorBuildSettingsScene e in EditorBuildSettings.scenes)
            {
                if (e.path==MenuPath || e.path==GamePath) continue;
                // Menus anteriores solo se preservan en assets, desactivados del build.
                bool anterior=e.path.Contains("QCXR_Menu");
                lista.Add(new EditorBuildSettingsScene(e.path,!anterior && e.enabled));
            }
            EditorBuildSettings.scenes=lista.ToArray();
        }
    }
}
#endif
