#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using QCXR.MenuSpatial;
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
    // V4: no terminal gigante. Menu distribuido en el espacio, con 3D ambiental real.
    public static class QCXRSpatialMenuGenerator
    {
        private const string GamePath = "Assets/_Project/Scenes/QCXR_MainScene.unity";
        private const string MenuPath = "Assets/_Project/Scenes/QCXR_MenuEspacial.unity";
        private const string ContentPath = "Assets/_Project/Config/UI/QCXRMenuEspacialContent.asset";
        private const string MaterialFolder = "Assets/_Project/Materials/MenuEspacialV4";

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
        private static Image FondoInicio;
        private static Image FondoSalida;

        private static Color RGB(int r, int g, int b) { return new Color(r / 255f, g / 255f, b / 255f, 1f); }

        [MenuItem("QCXR/Menu ESPACIAL V4/1 - Crear menu espacial NUEVO")]
        public static void Crear()
        {
            if (EditorApplication.isPlaying)
            {
                EditorUtility.DisplayDialog("QCXR", "Primero detén Play.", "OK"); return;
            }
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(GamePath) == null)
            {
                EditorUtility.DisplayDialog("Falta escena de fabrica", GamePath, "OK"); return;
            }
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(MenuPath) != null)
            {
                EditorUtility.DisplayDialog("Escena protegida", "QCXR_MenuEspacial ya existe. No se sobrescribirá ni se borrará tu trabajo. Usa la opción 2 para abrirla.", "OK"); return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Mats.Clear(); Textos.Clear(); Todos.Clear();
            BotonInicio = null; BotonSalida = null; FondoInicio = null; FondoSalida = null;
            Fuente = TMP_Settings.defaultFontAsset;

            Scene original = EditorSceneManager.OpenScene(GamePath, OpenSceneMode.Single);
            GameObject rigOriginal = null;
            GameObject eventoOriginal = null;
            List<GameObject> extras = new List<GameObject>();
            foreach (GameObject go in original.GetRootGameObjects())
            {
                if (!rigOriginal && (go.name.Contains("XR Origin") || go.name.Contains("XR Rig"))) rigOriginal = go;
                if (!eventoOriginal && go.GetComponent<EventSystem>()) eventoOriginal = go;
                if (go.name.Contains("XR Interaction Simulator") || go.name.Contains("XR Interaction Manager")) extras.Add(go);
            }
            if (!rigOriginal || !eventoOriginal)
            {
                EditorUtility.DisplayDialog("Configuracion XR no encontrada", "La fabrica no tiene XR Origin o EventSystem accesible. No se generó la escena.", "OK"); return;
            }
            Scene nueva = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(nueva);
            GameObject rig = Copiar(rigOriginal, nueva);
            GameObject eventos = Copiar(eventoOriginal, nueva);
            foreach (GameObject extra in extras) Copiar(extra, nueva);
            PrepararRig(rig);
            PrepararEvento(eventos);
            Camera cam = rig.GetComponentInChildren<Camera>(true);
            if (cam) cam.tag = "MainCamera";

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = RGB(145, 139, 120);
            RenderSettings.fog = false;

            GameObject escenario = new GameObject("01 - FABRICA 3D (decoracion, sin gameplay)");
            ConstruirEntorno(escenario.transform);
            ConstruirBanda(escenario.transform);
            ConstruirDecoracion(escenario.transform);
            CrearGuiasEspaciales(escenario.transform);
            CrearMenuDistribuido(cam);

            QCXRSpatialMenuContent contenido = ObtenerContenido();
            GameObject goControl = new GameObject("03 - CONTROLADOR MENU ESPACIAL");
            var control = goControl.AddComponent<QCXRSpatialMenuController>();
            Conectar(control, contenido);
            control.AplicarContenido();

            Directory.CreateDirectory("Assets/_Project/Scenes");
            EditorSceneManager.SaveScene(nueva, MenuPath);
            EditorSceneManager.CloseScene(original, true);
            SceneManager.SetActiveScene(nueva);
            ActualizarBuild();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeGameObject = goControl;
            EditorUtility.DisplayDialog("Escena espacial creada", "Creada QCXR_MenuEspacial.unity sin reemplazar otras escenas.\nAhora QCXR > Menu ESPACIAL V4 > 3 - Validar.", "OK");
        }

        [MenuItem("QCXR/Menu ESPACIAL V4/2 - Abrir escena espacial")]
        public static void Abrir()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(MenuPath) == null)
            {
                EditorUtility.DisplayDialog("QCXR", "Ejecuta la opcion 1 primero.", "OK"); return;
            }
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                EditorSceneManager.OpenScene(MenuPath, OpenSceneMode.Single);
        }

        [MenuItem("QCXR/Menu ESPACIAL V4/3 - Validar escena y transicion")]
        public static void Validar()
        {
            var detalles = new List<string>(); int fallos = 0;
            Action<bool, string> ver = (bien, nombre) => { detalles.Add((bien ? "OK   " : "FALTA   ") + nombre); if (!bien) fallos++; };
            ver(SceneManager.GetActiveScene().path == MenuPath, "Escena QCXR_MenuEspacial activa");
            var menu = Object.FindFirstObjectByType<QCXRSpatialMenuController>();
            ver(menu != null, "Controlador del menu independiente");
            var eventSystem = Object.FindFirstObjectByType<EventSystem>();
            ver(eventSystem && eventSystem.GetComponent<XRUIInputModule>(), "EventSystem y XRUIInputModule");
            GameObject izquierda = GameObject.Find("OPCION - INICIAR INSPECCION (flotante)");
            GameObject derecha = GameObject.Find("OPCION - SALIR (flotante)");
            ver(izquierda && izquierda.GetComponent<Canvas>() && izquierda.GetComponent<TrackedDeviceGraphicRaycaster>(), "Canvas XR independiente de iniciar");
            ver(derecha && derecha.GetComponent<Canvas>() && derecha.GetComponent<TrackedDeviceGraphicRaycaster>(), "Canvas XR independiente de salir");
            ver(izquierda && izquierda.GetComponentInChildren<Button>(true) && derecha && derecha.GetComponentInChildren<Button>(true), "Dos botones XR interactivos");
            ver(izquierda && derecha && Vector3.Distance(izquierda.transform.position, derecha.transform.position) > 1f, "Opciones en lugares espaciales distintos");
            ver(GameObject.Find("TEXTO 3D - titulo") && !GameObject.Find("TEXTO 3D - titulo").GetComponentInParent<Canvas>(), "Titulo 3D independiente de Canvas");
            GameObject piso = GameObject.Find("SUELO - Hormigon con colision");
            ver(piso && piso.GetComponent<BoxCollider>() && !piso.GetComponent<BoxCollider>().isTrigger, "Suelo con colision");
            ver(GameObject.Find("BANDA - Exposicion de productos") != null, "Banda industrial 3D");
            GameObject rig = GameObject.Find("XR Origin (XR Rig)");
            if (!rig) { foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects()) if (root.name.Contains("XR Origin")) rig = root; }
            ver(rig != null, "XR Origin copiado del juego");
            if (rig)
            {
                bool movil = false;
                foreach (MonoBehaviour b in rig.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (!b || !b.enabled) continue;
                    string n = b.GetType().Name;
                    if (EsLocomocion(n)) movil = true;
                }
                ver(!movil, "Sin locomocion activa en el menu");
            }
            EditorBuildSettingsScene[] build = EditorBuildSettings.scenes;
            ver(build.Length >= 2 && build[0].path == MenuPath && build[0].enabled && build[1].path == GamePath && build[1].enabled, "Orden de compilacion: Menu 0 / Fabrica 1");
            if (menu)
            {
                SerializedObject so = new SerializedObject(menu);
                foreach (string n in new[] { "contenido", "botonIniciar", "botonSalir", "titulo", "iniciar", "salir" })
                {
                    SerializedProperty p = so.FindProperty(n);
                    ver(p != null && p.objectReferenceValue != null, "Referencia: " + n);
                }
            }
            string resultado = string.Join("\n", detalles);
            Debug.Log("[QCXR Menu Espacial V4] VALIDACION\n" + resultado);
            EditorUtility.DisplayDialog(fallos == 0 ? "Estructura lista para probar" : "Hay puntos por revisar", resultado + "\n\nIMPORTANTE: aun falta probar Play, rayo XR y cambio de escena.", "OK");
        }

        [MenuItem("QCXR/Menu ESPACIAL V4/4 - Actualizar textos y tipografia")]
        public static void ActualizarTextos()
        {
            var controlador = Object.FindFirstObjectByType<QCXRSpatialMenuController>();
            if (!controlador)
            {
                EditorUtility.DisplayDialog("QCXR", "Abre primero QCXR_MenuEspacial.", "OK"); return;
            }
            controlador.AplicarContenido();
            EditorSceneManager.MarkSceneDirty(controlador.gameObject.scene);
            Debug.Log("[QCXR V4] Textos aplicados. Guarda con Ctrl+S.");
        }

        private static GameObject Copiar(GameObject origen, Scene escena)
        {
            GameObject copia = Object.Instantiate(origen);
            copia.name = origen.name;
            SceneManager.MoveGameObjectToScene(copia, escena);
            return copia;
        }
        private static bool EsLocomocion(string nombre)
        {
            return nombre.Contains("MoveProvider") || nombre.Contains("TurnProvider") || nombre.Contains("TeleportationProvider") ||
                nombre.Contains("ClimbProvider") || nombre.Contains("CharacterControllerDriver") || nombre.Contains("LocomotionProvider");
        }
        private static void PrepararRig(GameObject rig)
        {
            rig.transform.position = Vector3.zero;
            rig.transform.rotation = Quaternion.identity;
            foreach (MonoBehaviour b in rig.GetComponentsInChildren<MonoBehaviour>(true))
                if (b && EsLocomocion(b.GetType().Name)) b.enabled = false;
            foreach (CharacterController cc in rig.GetComponentsInChildren<CharacterController>(true)) cc.enabled = false;
            foreach (Rigidbody rb in rig.GetComponentsInChildren<Rigidbody>(true))
                if (rb.gameObject == rig) { rb.useGravity = false; rb.isKinematic = true; }
            // Solo ocultar las mallas de manos, nunca los rays, interactors o entradas XR.
            foreach (SkinnedMeshRenderer mano in rig.GetComponentsInChildren<SkinnedMeshRenderer>(true)) mano.enabled = false;
        }
        private static void PrepararEvento(GameObject evento)
        {
            XRUIInputModule xr = evento.GetComponent<XRUIInputModule>();
            if (!xr)
            {
                foreach (BaseInputModule other in evento.GetComponents<BaseInputModule>()) Object.DestroyImmediate(other);
                evento.AddComponent<XRUIInputModule>();
            }
            else
                foreach (BaseInputModule other in evento.GetComponents<BaseInputModule>())
                    if (other != xr) other.enabled = false;
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
            TextMeshPro senal = Letra3D("ROTULO - Control de Calidad",root,"UAO  /  LINEA DE PRODUCCION",2.8f,new Vector3(0,2.88f,5.43f),Quaternion.Euler(0,180,0),Blanco);
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
                roll.AddComponent<QCXRSpatialAmbientAnimation>().Giro(Vector3.up,75f);
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
                    .AddComponent<QCXRSpatialAmbientAnimation>().Giro(Vector3.up,32f);
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
            anchor.AddComponent<QCXRSpatialAmbientAnimation>().Flotar(.013f,1f);
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
            baliza.AddComponent<QCXRSpatialAmbientAnimation>().Parpadear(.9f);
        }


        private static void CrearGuiasEspaciales(Transform root)
        {
            Material naranja = Mat("05_Naranja_Estructura", Naranja);
            Material grafito = Mat("06_Grafito", Grafito);
            Material metal = Mat("04_Acero_Claro", AceroClaro);
            Material verde = Mat("13_Estacion_Verde", Verde, true);
            // Lineas de seguridad que conectan visualmente las piezas de la fabrica.
            for (int i = 0; i < 6; i++)
            {
                float z = .5f + i * .36f;
                Cubo("Senalizacion piso izquierda " + i, root, new Vector3(-1.8f, .007f, z),
                    new Vector3(.23f, .012f, .045f), i%2 == 0 ? naranja : grafito);
                Cubo("Senalizacion piso derecha " + i, root, new Vector3(1.8f, .007f, z),
                    new Vector3(.23f, .012f, .045f), i%2 == 0 ? naranja : grafito);
            }
            Cubo("Anillo estado - marco vertical izquierdo", root, new Vector3(-.9f, 1.90f, 3.06f),
                new Vector3(.034f, .27f, .04f), metal);
            Cubo("Anillo estado - marco vertical derecho", root, new Vector3(.9f, 1.90f, 3.06f),
                new Vector3(.034f, .27f, .04f), metal);
            Cubo("Anillo estado - luz superior", root, new Vector3(0, 2.05f, 3.06f),
                new Vector3(1.84f, .022f, .04f), verde);
        }

        // Menus: dos superficies interactivas independientes, sin panel comun de fondo.
        private static void CrearMenuDistribuido(Camera cam)
        {
            Transform root = Objeto("02 - MENU ESPACIAL (elementos separados)", null).transform;
            Material naranja = Mat("05_Naranja_Estructura", Naranja);
            Material metal = Mat("04_Acero_Claro", AceroClaro);
            Material verde = Mat("13_Estacion_Verde", Verde, true);
            Material grafito = Mat("06_Grafito", Grafito);
            // Rotulos 3D, visibles desde el frente del jugador, separados del fondo industrial.
            Etiqueta("superior", root, "UAO IMMERSIVE SOLUTIONS / PLANTA 01", 2.2f, new Vector3(0, 2.72f, 2.56f), .065f, Blanco);
            Etiqueta("titulo", root, "CONTROL DE\nCALIDAD XR", 5.1f, new Vector3(0, 2.23f, 2.65f), .063f, Blanco);
            Etiqueta("subtitulo", root, "CENTRO DE INSPECCION INDUSTRIAL", 2.35f, new Vector3(0, 1.86f, 2.49f), .064f, Naranja);
            Etiqueta("estado", root, "LINEA LISTA / TURNO 01", 2.2f, new Vector3(0, 1.19f, 3.12f), .061f, Verde);
            Etiqueta("pie", root, "INSPECCIONA / REPARA / CLASIFICA", 2.0f, new Vector3(0, .98f, 2.65f), .061f, Blanco);
            Etiqueta("zonaIzquierda", root, "RECEPCION DE PRODUCTOS", 1.85f, new Vector3(-2.32f, 1.98f, 3.62f), .055f, Blanco);
            Etiqueta("zonaDerecha", root, "CONTROL DE CALIDAD", 1.85f, new Vector3(2.36f, 1.98f, 3.62f), .055f, Blanco);
            Etiqueta("aviso", root, "", 2f, new Vector3(0, .76f, 2.12f), .055f, Amarillo);

            // Divisores espaciales sueltos; no forman una superficie unica.
            Cubo("Barra naranja titulo", root, new Vector3(0, 2.50f, 2.72f), new Vector3(1.54f, .020f, .027f), naranja);
            Cubo("Tira de orientacion central", root, new Vector3(0, 1.10f, 2.75f), new Vector3(.72f, .016f, .022f), verde);
            for (int i=0; i<5; i++)
            {
                Cubo("Punto guia izquierda " + i, root, new Vector3(-1.68f - i*.06f, 1.33f - i*.055f, 2.01f), new Vector3(.022f, .022f, .016f), metal);
                Cubo("Punto guia derecha " + i, root, new Vector3(1.68f + i*.06f, 1.33f - i*.055f, 2.01f), new Vector3(.022f, .022f, .016f), metal);
            }
            // Cada opcion tiene su propio World Space Canvas y su propio XR Raycaster.
            BotonInicio = BotonFlotante("OPCION - INICIAR INSPECCION (flotante)", root, cam, new Vector3(-.91f, 1.53f, 1.84f), "iniciar", "INICIAR INSPECCION", out FondoInicio);
            BotonSalida = BotonFlotante("OPCION - SALIR (flotante)", root, cam, new Vector3(.91f, 1.53f, 1.84f), "salir", "SALIR", out FondoSalida);
            // Los marcos son piezas 3D separadas, detras de cada boton.
            foreach (float x in new[] { -.91f, .91f })
            {
                Cubo("Pieza visual superior opcion " + x, root, new Vector3(x, 1.70f, 2.04f), new Vector3(.72f, .025f, .02f), naranja);
                Cubo("Pieza visual inferior opcion " + x, root, new Vector3(x, 1.36f, 2.04f), new Vector3(.72f, .018f, .02f), grafito);
            }
        }

        private static TextMeshPro Etiqueta(string clave, Transform parent, string valor, float tamano,
            Vector3 posicion, float escala, Color color)
        {
            GameObject go = Objeto("TEXTO 3D - " + clave, parent);
            go.transform.localPosition = posicion;
            go.transform.localRotation = Quaternion.Euler(0, 180, 0);
            go.transform.localScale = Vector3.one * escala;
            TextMeshPro texto = go.AddComponent<TextMeshPro>();
            if (Fuente) texto.font = Fuente;
            texto.text = valor;
            texto.fontSize = tamano;
            texto.color = color;
            texto.alignment = TextAlignmentOptions.Center;
            texto.textWrappingMode = TextWrappingModes.NoWrap;
            texto.overflowMode = TextOverflowModes.Overflow;
            Textos[clave] = texto;
            Todos.Add(texto);
            return texto;
        }

        private static Button BotonFlotante(string nombre, Transform parent, Camera cam, Vector3 posicion,
            string clave, string rotulo, out Image fondo)
        {
            GameObject canvasGo = new GameObject(nombre, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            canvasGo.transform.SetParent(parent, false);
            canvasGo.transform.localPosition = posicion;
            canvasGo.transform.localRotation = Quaternion.Euler(0, 180, 0);
            RectTransform canvasRect = canvasGo.GetComponent<RectTransform>();
            canvasRect.sizeDelta = new Vector2(940, 300);
            canvasRect.localScale = Vector3.one * .00098f;
            Canvas canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = cam;
            canvas.sortingOrder = 85;
            canvasGo.AddComponent<TrackedDeviceGraphicRaycaster>();
            canvasGo.GetComponent<CanvasScaler>().dynamicPixelsPerUnit = 12;

            GameObject buttonGo = new GameObject("INTERACTUAR - " + rotulo, typeof(RectTransform), typeof(Image), typeof(Button));
            buttonGo.transform.SetParent(canvasGo.transform, false);
            RectTransform r = buttonGo.GetComponent<RectTransform>();
            r.anchorMin = r.anchorMax = new Vector2(.5f, .5f);
            r.sizeDelta = new Vector2(880, 218);
            r.anchoredPosition = Vector2.zero;
            r.localScale = Vector3.one;
            fondo = buttonGo.GetComponent<Image>();
            fondo.color = new Color(.065f,.095f,.12f,.75f);
            fondo.raycastTarget = true;
            Button boton = buttonGo.GetComponent<Button>();
            boton.targetGraphic = fondo;
            var cb = boton.colors;
            cb.normalColor = Color.white;
            cb.highlightedColor = RGB(255, 201, 134);
            cb.pressedColor = RGB(181, 233, 191);
            cb.selectedColor = RGB(247, 221, 173);
            cb.fadeDuration = .07f;
            boton.colors = cb;
            buttonGo.AddComponent<QCXRSpatialButtonFeedback>().Configurar(fondo);
            // Solo el texto/boton tiene geometria de interfaz: el resto es espacio VR.
            GameObject txt = new GameObject("Texto interactivo - " + clave, typeof(RectTransform), typeof(TextMeshProUGUI));
            txt.transform.SetParent(buttonGo.transform, false);
            RectTransform tRect = txt.GetComponent<RectTransform>();
            tRect.anchorMin = tRect.anchorMax = new Vector2(.5f,.5f);
            tRect.anchoredPosition = Vector2.zero;
            tRect.sizeDelta = new Vector2(830, 195);
            TextMeshProUGUI t = txt.GetComponent<TextMeshProUGUI>();
            if (Fuente) t.font = Fuente;
            t.text = rotulo;
            t.fontSize = 66;
            t.color = Blanco;
            t.alignment = TextAlignmentOptions.Center;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.overflowMode = TextOverflowModes.Overflow;
            t.raycastTarget = false;
            Textos[clave] = t;
            Todos.Add(t);
            return boton;
        }

        // Rotulos ambientales existentes de la geometria base, NO se convierten en botones.
        private static TextMeshPro Letra3D(string nombre, Transform parent, string valor, float font,
            Vector3 posicion, Quaternion rotacion, Color color)
        {
            GameObject go = Objeto(nombre, parent);
            go.transform.localPosition = posicion;
            go.transform.localRotation = rotacion;
            go.transform.localScale = Vector3.one * .058f;
            TextMeshPro t = go.AddComponent<TextMeshPro>();
            if (Fuente) t.font = Fuente;
            t.text = valor;
            t.fontSize = font;
            t.color = color;
            t.alignment = TextAlignmentOptions.Center;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            return t;
        }

        private static QCXRSpatialMenuContent ObtenerContenido()
        {
            Directory.CreateDirectory("Assets/_Project/Config/UI");
            QCXRSpatialMenuContent contenido = AssetDatabase.LoadAssetAtPath<QCXRSpatialMenuContent>(ContentPath);
            if (contenido) return contenido;
            contenido = ScriptableObject.CreateInstance<QCXRSpatialMenuContent>();
            AssetDatabase.CreateAsset(contenido, ContentPath);
            return contenido;
        }

        private static void Conectar(QCXRSpatialMenuController controller, QCXRSpatialMenuContent asset)
        {
            var so = new SerializedObject(controller);
            Asignar(so, "contenido", asset);
            Asignar(so, "botonIniciar", BotonInicio);
            Asignar(so, "botonSalir", BotonSalida);
            Asignar(so, "fondoIniciar", FondoInicio);
            Asignar(so, "fondoSalir", FondoSalida);
            foreach (var par in Textos) Asignar(so, par.Key, par.Value);
            SerializedProperty p = so.FindProperty("textos");
            p.arraySize = Todos.Count;
            for (int i=0; i<Todos.Count; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = Todos[i];
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(controller);
        }
        private static void Asignar(SerializedObject so, string nombre, Object valor)
        {
            SerializedProperty p = so.FindProperty(nombre);
            if (p != null) p.objectReferenceValue = valor;
        }
        private static void ActualizarBuild()
        {
            var result = new List<EditorBuildSettingsScene>();
            result.Add(new EditorBuildSettingsScene(MenuPath, true));
            result.Add(new EditorBuildSettingsScene(GamePath, true));
            foreach (EditorBuildSettingsScene e in EditorBuildSettings.scenes)
            {
                if (e.path == MenuPath || e.path == GamePath) continue;
                // Deshabilitar los menus anteriores en el build, pero sin borrar ningun archivo.
                bool menuViejo = e.path.Contains("/QCXR_Menu");
                result.Add(new EditorBuildSettingsScene(e.path, e.enabled && !menuViejo));
            }
            EditorBuildSettings.scenes = result.ToArray();
        }
    }
}
#endif
