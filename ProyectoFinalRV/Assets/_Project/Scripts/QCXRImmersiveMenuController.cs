using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace QCXR.MenuImmersive
{
    // Este componente existe SOLO en QCXR_MenuInmersivo; nunca en la escena de juego.
    public sealed class QCXRImmersiveMenuController : MonoBehaviour
    {
        [Header("Contenido editable")]
        [SerializeField] private QCXRImmersiveMenuContent contenido;
        [SerializeField] private TMP_Text cabecera;
        [SerializeField] private TMP_Text titulo;
        [SerializeField] private TMP_Text bajada;
        [SerializeField] private TMP_Text estado;
        [SerializeField] private TMP_Text detalle;
        [SerializeField] private TMP_Text ayuda;
        [SerializeField] private TMP_Text marcaEscenario;
        [SerializeField] private TMP_Text iniciarTexto;
        [SerializeField] private TMP_Text salirTexto;
        [SerializeField] private TMP_Text aviso;
        [SerializeField] private TMP_Text[] todosLosTextos;
        [SerializeField] private Image botonIniciarFondo;
        [SerializeField] private Image botonSalirFondo;
        [SerializeField] private Image acento;
        [SerializeField] private Button botonIniciar;
        [SerializeField] private Button botonSalir;
        [SerializeField] private string escenaJuego = "QCXR_MainScene";
        private bool cambiandoEscena;

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
            // Permite comprobar la transicion sin visor. No sustituye la prueba XR.
            if (Keyboard.current != null && Keyboard.current.enterKey.wasPressedThisFrame)
                IniciarJuego();
        }
#endif
        public void IniciarJuego()
        {
            if (cambiandoEscena) return;
            if (!Application.CanStreamedLevelBeLoaded(escenaJuego))
            {
                string mensaje = "ERROR: QCXR_MainScene no esta en Build Profiles > Scene List.";
                if (aviso) aviso.text = mensaje;
                Debug.LogError("[QCXR Menu] " + mensaje);
                return;
            }
            cambiandoEscena = true;
            if (botonIniciar) botonIniciar.interactable = false;
            Debug.Log("[QCXR Menu] Cargando " + escenaJuego);
            SceneManager.LoadScene(escenaJuego, LoadSceneMode.Single);
        }
        public void Salir()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
        [ContextMenu("Aplicar contenido editable")]
        public void AplicarContenido()
        {
            if (!contenido) return;
            Set(cabecera, contenido.cabecera);
            Set(titulo, contenido.titulo);
            Set(bajada, contenido.bajada);
            Set(estado, contenido.estado);
            Set(detalle, contenido.detalle);
            Set(ayuda, contenido.ayuda);
            Set(marcaEscenario, contenido.marcaEscenario);
            Set(iniciarTexto, contenido.iniciar);
            Set(salirTexto, contenido.salir);
            if (contenido.fuente && todosLosTextos != null)
                foreach (TMP_Text t in todosLosTextos) if (t) t.font = contenido.fuente;
            if (titulo) titulo.color = contenido.blanco;
            if (marcaEscenario) marcaEscenario.color = contenido.blanco;
            if (estado) estado.color = contenido.verde;
            if (acento) acento.color = contenido.naranja;
            if (botonIniciarFondo) botonIniciarFondo.color = contenido.naranja;
            if (botonSalirFondo) botonSalirFondo.color = contenido.azulGrafito;
        }
        private static void Set(TMP_Text text, string value)
        {
            if (text) text.text = value;
        }
    }
}
