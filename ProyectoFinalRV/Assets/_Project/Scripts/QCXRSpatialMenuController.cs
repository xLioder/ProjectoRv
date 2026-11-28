using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace QCXR.MenuSpatial
{
    // Solo menu. No modifica GameManager, tutorial, banda ni herramientas.
    public sealed class QCXRSpatialMenuController : MonoBehaviour
    {
        [Header("Contenido y botones")]
        [SerializeField] private QCXRSpatialMenuContent contenido;
        [SerializeField] private Button botonIniciar;
        [SerializeField] private Button botonSalir;
        [SerializeField] private TMP_Text superior;
        [SerializeField] private TMP_Text titulo;
        [SerializeField] private TMP_Text subtitulo;
        [SerializeField] private TMP_Text iniciar;
        [SerializeField] private TMP_Text salir;
        [SerializeField] private TMP_Text zonaIzquierda;
        [SerializeField] private TMP_Text zonaDerecha;
        [SerializeField] private TMP_Text estado;
        [SerializeField] private TMP_Text pie;
        [SerializeField] private TMP_Text aviso;
        [SerializeField] private TMP_Text[] textos;
        [SerializeField] private Image fondoIniciar;
        [SerializeField] private Image fondoSalir;
        [SerializeField] private string nombreEscenaJuego = "QCXR_MainScene";
        private bool abriendo;

        private void Awake()
        {
            AplicarContenido();
            if (botonIniciar) botonIniciar.onClick.AddListener(IniciarJuego);
            if (botonSalir) botonSalir.onClick.AddListener(Salir);
            if (aviso) aviso.text = string.Empty;
        }

        private void OnDestroy()
        {
            if (botonIniciar) botonIniciar.onClick.RemoveListener(IniciarJuego);
            if (botonSalir) botonSalir.onClick.RemoveListener(Salir);
        }

#if UNITY_EDITOR
        private void Update()
        {
            // Prueba del enlace SIN visor (no sustituye prueba de clic XR).
            if (Keyboard.current != null && Keyboard.current.enterKey.wasPressedThisFrame)
                IniciarJuego();
        }
#endif

        public void IniciarJuego()
        {
            if (abriendo) return;
            if (!Application.CanStreamedLevelBeLoaded(nombreEscenaJuego))
            {
                string error = "QCXR_MainScene falta en Build Profiles > Scene List.";
                if (aviso) aviso.text = error;
                Debug.LogError("[QCXR Menu espacial] " + error);
                return;
            }
            abriendo = true;
            if (botonIniciar) botonIniciar.interactable = false;
            SceneManager.LoadScene(nombreEscenaJuego, LoadSceneMode.Single);
        }

        public void Salir()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        [ContextMenu("Aplicar textos y fuente")]
        public void AplicarContenido()
        {
            if (!contenido) return;
            Poner(superior, contenido.superior);
            Poner(titulo, contenido.titulo);
            Poner(subtitulo, contenido.subtitulo);
            Poner(iniciar, contenido.iniciar);
            Poner(salir, contenido.salir);
            Poner(zonaIzquierda, contenido.zonaIzquierda);
            Poner(zonaDerecha, contenido.zonaDerecha);
            Poner(estado, contenido.estado);
            Poner(pie, contenido.pie);
            if (textos != null)
                foreach (TMP_Text t in textos)
                    if (t && t != aviso)
                    {
                        if (contenido.fuentePersonalizada) t.font = contenido.fuentePersonalizada;
                        t.color = contenido.texto;
                    }
            if (estado) estado.color = contenido.acentoVerde;
            if (iniciar) iniciar.color = contenido.acentoVerde;
            if (subtitulo) subtitulo.color = contenido.acentoNaranja;
            if (fondoIniciar) fondoIniciar.color = contenido.tarjetaBoton;
            if (fondoSalir) fondoSalir.color = contenido.tarjetaBoton;
        }

        private static void Poner(TMP_Text objeto, string valor)
        {
            if (objeto) objeto.text = valor;
        }
    }
}
