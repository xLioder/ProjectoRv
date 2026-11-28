using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace QCXR.MenuReferenciaV6
{
    // Controlador independiente: no referencia clases de los menús V1-V5.
    // Los botones usan UI XR (como el primer menú que funcionó).
    public sealed class QCXRRefV6Controller : MonoBehaviour
    {
        [Header("Botones VR (arrastrar en Inspector)")]
        [SerializeField] private Button iniciarInspeccion;
        [SerializeField] private Button salir;
        [Header("Escena principal que ya funciona")]
        [SerializeField] private string escenaPrincipal = "QCXR_MainScene";
        [SerializeField] private TMP_Text estado;
        [SerializeField] private float segundosDeTransicion = 0.3f;
        [Header("Prueba en PC (independiente del simulador XR)")]
        [SerializeField] private bool habilitarEnterParaProbar = true;
        private bool cargando;

        private void OnEnable()
        {
            if (iniciarInspeccion != null) iniciarInspeccion.onClick.AddListener(IniciarInspeccion);
            if (salir != null) salir.onClick.AddListener(Salir);
        }
        private void OnDisable()
        {
            if (iniciarInspeccion != null) iniciarInspeccion.onClick.RemoveListener(IniciarInspeccion);
            if (salir != null) salir.onClick.RemoveListener(Salir);
        }
        private void Update()
        {
            if (!habilitarEnterParaProbar || Keyboard.current == null || cargando) return;
            if (Keyboard.current.enterKey.wasPressedThisFrame || Keyboard.current.numpadEnterKey.wasPressedThisFrame)
                IniciarInspeccion();
        }
        public void IniciarInspeccion()
        {
            if (!cargando) StartCoroutine(CargarJuego());
        }
        private IEnumerator CargarJuego()
        {
            if (!Application.CanStreamedLevelBeLoaded(escenaPrincipal))
            {
                Debug.LogError("[QCXR V6] QCXR_MainScene no está incluida/habilitada en Build Profiles.");
                if (estado != null) estado.text = "ERROR: VERIFICAR ESCENA DE JUEGO";
                yield break;
            }
            cargando = true;
            if (estado != null) estado.text = "INICIANDO INSPECCIÓN...";
            yield return new WaitForSeconds(segundosDeTransicion);
            SceneManager.LoadScene(escenaPrincipal, LoadSceneMode.Single);
        }
        public void Salir()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
