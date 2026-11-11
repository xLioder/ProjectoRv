using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace QCXR
{
    /// Eventos inesperados durante el turno. Cada evento = un par Begin/End independiente:
    /// 1) Banda atascada  2) Falla eléctrica  3) Pedido urgente.
    /// Para agregar uno nuevo: añade un valor al enum y un par BeginX/EndX.
    public class RandomEventSystem : GameSystem
    {
        enum Kind { BeltJam, LightsOut, RushOrder }

        [SerializeField] GameConfigSO config;

        [Header("Banda atascada")]
        [SerializeField] float jamDuration = 10f;
        [SerializeField] string resetButtonId = "ResetBelt";
        [SerializeField] GameObject sparksFx;
        [SerializeField] GameObject resetButtonObject;   // visible solo durante el atasco

        [Header("Falla eléctrica")]
        [SerializeField] float lightsOutDuration = 8f;
        [SerializeField, Range(0f, 1f)] float lightsOutMinDifficulty = 0.25f;
        [SerializeField] Light[] lights;
        [SerializeField] GameObject emergencyLight;

        [Header("Pedido urgente")]
        [SerializeField] float rushDuration = 12f;
        [SerializeField, Range(0f, 1f)] float rushMinDifficulty = 0.15f;
        [SerializeField] float rushBeltMultiplier = 1.5f;
        [SerializeField] float rushTimeMultiplier = 0.7f;
        [SerializeField] float rushScoreMultiplier = 2f;

        Coroutine loop, flicker;
        Kind? active;
        float difficulty;
        bool jamResolved;
        float[] originalIntensity;

        protected override void OnEnable()  { base.OnEnable();  GameEvents.DifficultyChanged += OnDifficulty; }
        protected override void OnDisable() { base.OnDisable(); GameEvents.DifficultyChanged -= OnDifficulty; }
        void OnDifficulty(float d) => difficulty = d;

        protected override void OnPlayStart() => loop = StartCoroutine(Loop());

        protected override void OnPlayEnd()
        {
            if (loop != null) StopCoroutine(loop);
            StopAllCoroutines();
            if (active.HasValue) { EndEvent(active.Value); active = null; }
        }

        // ── Planificador ──
        IEnumerator Loop()
        {
            yield return new WaitForSeconds(config.firstEventDelay);
            while (true)
            {
                var pool = new List<Kind> { Kind.BeltJam };
                if (difficulty >= lightsOutMinDifficulty) pool.Add(Kind.LightsOut);
                if (difficulty >= rushMinDifficulty) pool.Add(Kind.RushOrder);

                yield return RunEvent(pool[Random.Range(0, pool.Count)]);
                yield return new WaitForSeconds(Random.Range(config.eventInterval.x, config.eventInterval.y));
            }
        }

        IEnumerator RunEvent(Kind kind)
        {
            active = kind;
            GameEvents.RaiseRandomEventStarted(NameOf(kind), DurationOf(kind));
            GameEvents.RaiseSfx(SfxId.Alarm, transform.position);
            BeginEvent(kind);

            if (kind == Kind.BeltJam)
            {
                for (float t = 0f; t < jamDuration && !jamResolved; t += Time.deltaTime) yield return null;
            }
            else yield return new WaitForSeconds(DurationOf(kind));

            EndEvent(kind);
            active = null;
        }

        string NameOf(Kind k) => k == Kind.BeltJam ? "Banda atascada" : k == Kind.LightsOut ? "Falla eléctrica" : "Pedido urgente";
        float DurationOf(Kind k) => k == Kind.BeltJam ? jamDuration : k == Kind.LightsOut ? lightsOutDuration : rushDuration;

        void BeginEvent(Kind k)
        {
            if (k == Kind.BeltJam) BeginJam();
            else if (k == Kind.LightsOut) BeginLights();
            else BeginRush();
        }

        void EndEvent(Kind k)
        {
            if (k == Kind.BeltJam) EndJam();
            else if (k == Kind.LightsOut) EndLights();
            else EndRush();
            GameEvents.RaiseRandomEventEnded(NameOf(k));
        }

        // ── 1) Banda atascada ──
        void BeginJam()
        {
            jamResolved = false;
            GameEvents.ButtonPressed += OnButton;
            GameEvents.RaiseBeltJamChanged(true);
            GameEvents.RaiseSfx(SfxId.Jam, transform.position);
            GameEvents.RaiseFeedback("¡Banda atascada! Presiona el botón rojo");
            if (sparksFx) sparksFx.SetActive(true);
            if (resetButtonObject) resetButtonObject.SetActive(true);
        }

        void OnButton(string id) { if (id == resetButtonId) jamResolved = true; }

        void EndJam()
        {
            GameEvents.ButtonPressed -= OnButton;
            GameEvents.RaiseBeltJamChanged(false);
            if (sparksFx) sparksFx.SetActive(false);
            if (resetButtonObject) resetButtonObject.SetActive(false);

            if (jamResolved) GameEvents.RaiseFeedback("Banda reparada");
            else GameEvents.RaiseStrikeRequested("Banda atascada sin reparar");
        }

        // ── 2) Falla eléctrica ──
        void BeginLights()
        {
            originalIntensity = new float[lights.Length];
            for (int i = 0; i < lights.Length; i++) originalIntensity[i] = lights[i].intensity;
            if (emergencyLight) emergencyLight.SetActive(true);
            GameEvents.RaiseFeedback("¡Falla eléctrica! Usa la lupa para ver");
            flicker = StartCoroutine(Flicker());
        }

        IEnumerator Flicker()
        {
            while (true)
            {
                for (int i = 0; i < lights.Length; i++) lights[i].intensity = originalIntensity[i] * Random.Range(0.02f, 0.25f);
                yield return new WaitForSeconds(Random.Range(0.05f, 0.3f));
            }
        }

        void EndLights()
        {
            if (flicker != null) StopCoroutine(flicker);
            if (originalIntensity != null)
                for (int i = 0; i < lights.Length; i++) lights[i].intensity = originalIntensity[i];
            if (emergencyLight) emergencyLight.SetActive(false);
        }

        // ── 3) Pedido urgente ──
        void BeginRush()
        {
            GameEvents.RaiseRushChanged(true, rushBeltMultiplier, rushTimeMultiplier, rushScoreMultiplier);
            GameEvents.RaiseFeedback("¡Pedido urgente! Puntos x2");
        }

        void EndRush() => GameEvents.RaiseRushChanged(false, 1f, 1f, 1f);
    }
}
