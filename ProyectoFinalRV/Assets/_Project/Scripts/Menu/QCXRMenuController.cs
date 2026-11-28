using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.InputSystem;

namespace QCXR
{
    // Controla la escena del menú. Va aparte del juego: no usa GameManager ni el tutorial.
    public sealed class QCXRMenuController : MonoBehaviour
    {
        [Header("Contenido que puedes editar sin programar")]
        [SerializeField] QCXRMenuContent contenido;

        [Header("Referencias de UI creadas por el generador")]
        [SerializeField] TMP_Text marca;
        [SerializeField] TMP_Text titulo;
        [SerializeField] TMP_Text descripcion;
        [SerializeField] TMP_Text etiquetaInicio;
        [SerializeField] TMP_Text etiquetaAyuda;
        [SerializeField] TMP_Text etiquetaSalida;
        [SerializeField] TMP_Text etiquetaEstado;
        [SerializeField] TMP_Text tarjetaTitulo;
        [SerializeField] TMP_Text tarjetaDescripcion;
        [SerializeField] TMP_Text tarjetaDato1;
        [SerializeField] TMP_Text tarjetaDato2;
        [SerializeField] TMP_Text tarjetaDato3;
        [SerializeField] TMP_Text ayudaTitulo;
        [SerializeField] TMP_Text ayudaContenido;
        [SerializeField] TMP_Text etiquetaVolver;
        [SerializeField] TMP_Text pie;
        [SerializeField] TMP_Text aviso;
        [SerializeField] TMP_Text[] textos;

        [SerializeField] Button iniciar;
        [SerializeField] Button comoJugar;
        [SerializeField] Button salir;
        [SerializeField] Button volver;
        [SerializeField] GameObject pantallaAyuda;
        [SerializeField] Transform menuEnElMundo;
        [SerializeField] float distanciaAlJugador = 2.25f;
        [SerializeField] string nombreEscenaJuego = "QCXR_MainScene";
        [SerializeField] Image[] fondos;
        [SerializeField] Image[] superficies;
        [SerializeField] Image[] acentos;
        [SerializeField] Image imagenBotonPrincipal;

        void Awake()
        {
            AplicarContenido();
            if (iniciar) iniciar.onClick.AddListener(IniciarInspeccion);
            if (comoJugar) comoJugar.onClick.AddListener(MostrarInstrucciones);
            if (volver) volver.onClick.AddListener(VolverAlMenu);
            if (salir) salir.onClick.AddListener(Salir);
            if (pantallaAyuda) pantallaAyuda.SetActive(false);
            if (aviso) aviso.text = string.Empty;
        }

        void Start()
        {
            // Se ubica una vez al entrar. No persigue la cabeza del jugador en VR.
            Camera cam = Camera.main;
            if (!menuEnElMundo || !cam) return;
            Vector3 direccion = Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up).normalized;
            if (direccion.sqrMagnitude < 0.01f) direccion = Vector3.forward;
            menuEnElMundo.position = cam.transform.position + direccion * distanciaAlJugador;
            menuEnElMundo.rotation = Quaternion.LookRotation(-direccion, Vector3.up);
        }

        void Update()
        {
#if UNITY_EDITOR
            // Atajo opcional para probar sin visor, incluso si falta el click XR.
            Keyboard teclado = Keyboard.current;
            if (teclado == null) return;
            if (teclado.escapeKey.wasPressedThisFrame && pantallaAyuda && pantallaAyuda.activeSelf) VolverAlMenu();
            else if (teclado.hKey.wasPressedThisFrame) MostrarInstrucciones();
            else if (teclado.enterKey.wasPressedThisFrame && (!pantallaAyuda || !pantallaAyuda.activeSelf)) IniciarInspeccion();
#endif
        }

        public void IniciarInspeccion()
        {
            if (!Application.CanStreamedLevelBeLoaded(nombreEscenaJuego))
            {
                if (aviso) aviso.text = "No se encontró QCXR_MainScene en Build Profiles > Scene List.";
                Debug.LogError("QCXR Menú: la escena '" + nombreEscenaJuego + "' no está incluida en la compilación.");
                return;
            }
            SceneManager.LoadScene(nombreEscenaJuego);
        }

        public void MostrarInstrucciones()
        {
            if (pantallaAyuda) pantallaAyuda.SetActive(true);
        }

        public void VolverAlMenu()
        {
            if (pantallaAyuda) pantallaAyuda.SetActive(false);
        }

        public void Salir()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        [ContextMenu("Aplicar textos, fuente y paleta")]
        public void AplicarContenido()
        {
            if (!contenido) return;
            Set(marca, contenido.marca);
            Set(titulo, contenido.titulo);
            Set(descripcion, contenido.descripcion);
            Set(etiquetaInicio, contenido.etiquetaPrincipal);
            Set(etiquetaAyuda, contenido.etiquetaTutorial);
            Set(etiquetaSalida, contenido.etiquetaSalir);
            Set(etiquetaEstado, contenido.etiquetaEstado);
            Set(tarjetaTitulo, contenido.tarjetaTitulo);
            Set(tarjetaDescripcion, contenido.tarjetaDescripcion);
            Set(tarjetaDato1, contenido.tarjetaDato1);
            Set(tarjetaDato2, contenido.tarjetaDato2);
            Set(tarjetaDato3, contenido.tarjetaDato3);
            Set(ayudaTitulo, contenido.instruccionesTitulo);
            Set(ayudaContenido, contenido.instrucciones);
            Set(etiquetaVolver, contenido.etiquetaVolver);
            Set(pie, contenido.pie);

            if (textos != null)
                foreach (TMP_Text t in textos)
                {
                    if (!t) continue;
                    if (contenido.fuentePersonalizada) t.font = contenido.fuentePersonalizada;
                }

            Paint(fondos, contenido.fondo);
            Paint(superficies, contenido.superficie);
            Paint(acentos, contenido.acento);
            if (imagenBotonPrincipal) imagenBotonPrincipal.color = contenido.botonPrincipal;
            if (titulo) titulo.color = contenido.textoPrincipal;
        }

        static void Set(TMP_Text target, string value) { if (target) target.text = value; }
        static void Paint(Image[] group, Color color)
        {
            if (group == null) return;
            foreach (Image image in group) if (image) image.color = color;
        }
    }
}
