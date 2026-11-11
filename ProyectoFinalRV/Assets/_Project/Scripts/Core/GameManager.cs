using UnityEngine;
using UnityEngine.SceneManagement;

namespace QCXR
{
    /// Máquina de estados: Boot → Onboarding → Playing → Finished.
    /// Solo decide el estado; no sabe nada de puntaje, banda ni herramientas.
    public class GameManager : MonoBehaviour
    {
        [SerializeField] bool skipOnboarding = false;   // pruebas rápidas

        public GameState State { get; private set; } = GameState.Boot;

        void OnEnable()
        {
            GameEvents.OnboardingCompleted += OnOnboardingCompleted;
            GameEvents.EndRequested += OnEndRequested;
            GameEvents.ButtonPressed += OnButton;
        }

        void OnDisable()
        {
            GameEvents.OnboardingCompleted -= OnOnboardingCompleted;
            GameEvents.EndRequested -= OnEndRequested;
            GameEvents.ButtonPressed -= OnButton;
        }

        void Start()
        {
            SetState(GameState.Boot);
            SetState(skipOnboarding ? GameState.Playing : GameState.Onboarding);
        }

        void OnOnboardingCompleted() { if (State == GameState.Onboarding) SetState(GameState.Playing); }

        void OnEndRequested(EndReason reason)
        {
            if (State != GameState.Playing) return;
            SetState(GameState.Finished);
            GameEvents.RaiseGameEnded(reason);
        }

        void OnButton(string id)
        {
            if (State != GameState.Finished) return;
            if (id == "Restart") SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
            else if (id == "Quit") Application.Quit();
        }

        void SetState(GameState s) { State = s; GameEvents.RaiseGameStateChanged(s); }
    }

    /// Base para sistemas que solo trabajan durante la partida (Playing).
    public abstract class GameSystem : MonoBehaviour
    {
        protected bool IsPlaying { get; private set; }

        protected virtual void OnEnable()  { GameEvents.GameStateChanged += HandleState; }
        protected virtual void OnDisable() { GameEvents.GameStateChanged -= HandleState; }

        void HandleState(GameState s)
        {
            bool was = IsPlaying;
            IsPlaying = s == GameState.Playing;
            if (!was && IsPlaying) OnPlayStart();
            else if (was && !IsPlaying) OnPlayEnd();
        }

        protected virtual void OnPlayStart() { }
        protected virtual void OnPlayEnd() { }
    }
}
