using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace QCXR
{
    // Exclusivo del MENU; nunca modifica GameManager ni el tutorial de la fabrica.
    public sealed class QCXRMenuFinalController : MonoBehaviour
    {
        [Header("Contenido editable")]
        [SerializeField] private QCXRMenuFinalContent contenido;
        [Header("Botones creados por el generador")]
        [SerializeField] private Button botonIniciar;
        [SerializeField] private Button botonSalir;
        [SerializeField] private TMP_Text insignia;
        [SerializeField] private TMP_Text titulo;
        [SerializeField] private TMP_Text descripcion;
        [SerializeField] private TMP_Text etiquetaInicio;
        [SerializeField] private TMP_Text etiquetaSalir;
        [SerializeField] private TMP_Text tarjetaTitulo;
        [SerializeField] private TMP_Text tarjetaDetalle;
        [SerializeField] private TMP_Text pie;
        [SerializeField] private TMP_Text indicador;
        [SerializeField] private TMP_Text aviso;
        [SerializeField] private TMP_Text[] todosLosTextos;
        [SerializeField] private Image fondoUI;
        [SerializeField] private Image tarjetaUI;
        [SerializeField] private Image franjaNaranja;
        [SerializeField] private Image franjaVerde;
        [SerializeField] private Image imagenBotonInicio;
        [SerializeField] private Transform panelRaiz;
        [SerializeField, Range(1.2f, 2.1f)] private float distanciaAlJugador = 1.65f;
        [SerializeField] private string escenaJuego = "QCXR_MainScene";

        private bool ocupado;

        private void Awake()
        {
            AplicarContenido();
            if (botonIniciar) botonIniciar.onClick.AddListener(Iniciar);
            if (botonSalir) botonSalir.onClick.AddListener(Salir);
            if (aviso) aviso.text = string.Empty;
        }

        private void Start()
        {
            // Reposiciona UNA sola vez ante el visor/simulador, sin perseguir la cabeza.
            Camera cam = Camera.main;
            if (!cam || !panelRaiz) return;
            Vector3 direccion = Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up);
            if (direccion.sqrMagnitude < 0.0001f) direccion = Vector3.forward;
            direccion.Normalize();
            Vector3 pos = cam.transform.position + direccion * distanciaAlJugador;
            pos.y = cam.transform.position.y - 0.03f;
            panelRaiz.position = pos;
            // Importante: panel frontal (sin textos espejados).
            panelRaiz.rotation = Quaternion.LookRotation(-direccion, Vector3.up);
        }

        private void Update()
        {
#if UNITY_EDITOR
            // Prueba de la transicion aunque el simulador no envie eventos de puntero.
            // NO sustituye la prueba del clic XR real.
            if (Keyboard.current != null && Keyboard.current.enterKey.wasPressedThisFrame)
                Iniciar();
#endif
        }

        public void Iniciar()
        {
            if (ocupado) return;
            if (!Application.CanStreamedLevelBeLoaded(escenaJuego))
            {
                string mensaje = "Falta QCXR_MainScene en Build Profiles > Scene List.";
                if (aviso) aviso.text = mensaje;
                Debug.LogError("[QCXR Menu Final] " + mensaje);
                return;
            }
            ocupado = true;
            if (botonIniciar) botonIniciar.interactable = false;
            Debug.Log("[QCXR Menu Final] Entrando a QCXR_MainScene.");
            SceneManager.LoadScene(escenaJuego, LoadSceneMode.Single);
        }

        public void Salir()
        {
            Debug.Log("[QCXR Menu Final] Saliendo del juego.");
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        [ContextMenu("Aplicar contenido y tipografia")]
        public void AplicarContenido()
        {
            if (!contenido) return;
            Set(insignia, contenido.insignia);
            Set(titulo, contenido.titulo);
            Set(descripcion, contenido.descripcion);
            Set(etiquetaInicio, contenido.botonInicio);
            Set(etiquetaSalir, contenido.botonSalir);
            Set(tarjetaTitulo, contenido.tarjetaTitulo);
            Set(tarjetaDetalle, contenido.tarjetaDetalle);
            Set(pie, contenido.instruccionInferior);
            Set(indicador, contenido.indicadorEstado);
            if (contenido.tipografia && todosLosTextos != null)
                foreach (TMP_Text tmp in todosLosTextos) if (tmp) tmp.font = contenido.tipografia;
            if (fondoUI) fondoUI.color = contenido.fondo;
            if (tarjetaUI) tarjetaUI.color = contenido.superficie;
            if (franjaNaranja) franjaNaranja.color = contenido.naranja;
            if (franjaVerde) franjaVerde.color = contenido.verde;
            if (imagenBotonInicio) imagenBotonInicio.color = contenido.naranja;
            if (titulo) titulo.color = contenido.texto;
        }

        private static void Set(TMP_Text target, string value)
        {
            if (target) target.text = value;
        }
    }
}
