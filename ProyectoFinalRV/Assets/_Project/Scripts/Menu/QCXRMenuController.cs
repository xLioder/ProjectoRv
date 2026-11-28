using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace QCXR.Menu
{
    /// Controla el menú inmersivo tipo "Opción 3".
    /// - Iniciar inspección => carga QCXR_MainScene
    /// - Salir => cierra la app (o sale del Play Mode en Editor)
    /// - Opcionalmente bloquea locomoción en la escena menú
    public class QCXRMenuController : MonoBehaviour
    {
        [Header("Botones")]
        [SerializeField] private Button startButton;
        [SerializeField] private Button exitButton;

        [Header("Escena destino")]
        [SerializeField] private string gameplaySceneName = "QCXR_MainScene";
        [SerializeField] private float loadDelay = 0.35f;

        [Header("Feedback visual")]
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private string idleStatus = "ESTACIÓN PREPARADA";
        [SerializeField] private string loadingStatus = "INICIANDO INSPECCIÓN...";

        [Header("Rig / locomoción")]
        [SerializeField] private Transform xrOriginRoot;
        [SerializeField] private bool disableLocomotionWhileInMenu = true;

        private bool isLoading;

        private void Awake()
        {
            if (startButton != null) startButton.onClick.AddListener(HandleStartClicked);
            if (exitButton != null) exitButton.onClick.AddListener(HandleExitClicked);
        }

        private void Start()
        {
            if (statusText != null) statusText.text = idleStatus;
            if (disableLocomotionWhileInMenu) DisableLocomotionProviders();
        }

        private void OnDestroy()
        {
            if (startButton != null) startButton.onClick.RemoveListener(HandleStartClicked);
            if (exitButton != null) exitButton.onClick.RemoveListener(HandleExitClicked);
        }

        public void HandleStartClicked()
        {
            if (!isLoading)
                StartCoroutine(LoadGameplayRoutine());
        }

        public void HandleExitClicked()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private IEnumerator LoadGameplayRoutine()
        {
            isLoading = true;
            if (statusText != null) statusText.text = loadingStatus;

            yield return new WaitForSeconds(loadDelay);

            if (string.IsNullOrWhiteSpace(gameplaySceneName))
            {
                Debug.LogError("[QCXR Menu] No se definió la escena de juego.");
                if (statusText != null) statusText.text = idleStatus;
                isLoading = false;
                yield break;
            }

            SceneManager.LoadScene(gameplaySceneName, LoadSceneMode.Single);
        }

        private void DisableLocomotionProviders()
        {
            Transform root = xrOriginRoot != null ? xrOriginRoot : FindXROriginLikeRoot();
            if (root == null) return;

            var behaviours = root.GetComponentsInChildren<MonoBehaviour>(true);
            foreach (var behaviour in behaviours)
            {
                if (behaviour == null || behaviour == this) continue;

                string typeName = behaviour.GetType().Name;
                if (typeName.Contains("MoveProvider") ||
                    typeName.Contains("TurnProvider") ||
                    typeName.Contains("TeleportationProvider") ||
                    typeName.Contains("LocomotionMediator"))
                {
                    behaviour.enabled = false;
                }
            }
        }

        private Transform FindXROriginLikeRoot()
        {
            foreach (var t in FindObjectsByType<Transform>(FindObjectsSortMode.None))
            {
                if (t == null) continue;
                string name = t.name.ToLowerInvariant();
                if (name.Contains("xr origin") || name == "xrorigin" || name == "xr rig")
                    return t;
            }
            return null;
        }
    }
}
