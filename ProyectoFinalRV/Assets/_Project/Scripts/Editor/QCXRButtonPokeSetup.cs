using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Filtering;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace QCXR.EditorTools
{
    // Menú QCXR > Botones > Permitir pulsar con el dedo (poke).
    // En XRI 3 el Poke Interactor del rig tiene "Require Poke Filter" activado: solo puede seleccionar (y por tanto
    // disparar XRButton) los interactables que tienen un XRPokeFilter. Sin él, los botones solo respondían al agarre.
    // Este paso añade el filtro a los Btn_* de QCXR_MainScene (pulsación hacia abajo, -Y, ángulo 45°, igual que el
    // botón de ejemplo de XRI). Solo añade ese componente: no toca nada más de la escena.
    public static class QCXRButtonPokeSetup
    {
        const string ScenePath = "Assets/_Project/Scenes/QCXR_MainScene.unity";

        // Configura un XRPokeFilter en el botón (lo crea si falta). Reutilizable desde el generador de escena.
        public static XRPokeFilter Configure(GameObject button)
        {
            var filter = button.GetComponent<XRPokeFilter>();
            if (filter == null) filter = button.AddComponent<XRPokeFilter>();
            var so = new SerializedObject(filter);
            so.FindProperty("m_Interactable").objectReferenceValue = button.GetComponent<XRSimpleInteractable>();
            so.FindProperty("m_PokeCollider").objectReferenceValue = button.GetComponent<Collider>();
            var cfg = so.FindProperty("m_PokeConfiguration");
            cfg.FindPropertyRelative("m_UseConstant").boolValue = true;
            var v = cfg.FindPropertyRelative("m_ConstantValue");
            v.FindPropertyRelative("m_PokeDirection").enumValueIndex = (int)PokeAxis.NegativeY;   // se pulsa desde arriba
            v.FindPropertyRelative("m_InteractionDepthOffset").floatValue = 0f;
            v.FindPropertyRelative("m_EnablePokeAngleThreshold").boolValue = true;
            v.FindPropertyRelative("m_PokeAngleThreshold").floatValue = 45f;
            so.ApplyModifiedPropertiesWithoutUndo();
            return filter;
        }

        [MenuItem("QCXR/Botones/Permitir pulsar con el dedo (poke)")]
        public static bool Apply()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return false;
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var buttons = scene.GetRootGameObjects()
                .SelectMany(r => r.GetComponentsInChildren<XRButton>(true))   // incluye Btn_ResetBelt, que empieza apagado
                .Select(b => b.gameObject).ToList();
            foreach (var b in buttons)
            {
                var f = Configure(b);
                var so = new SerializedObject(f);
                var dir = (PokeAxis)so.FindProperty("m_PokeConfiguration").FindPropertyRelative("m_ConstantValue").FindPropertyRelative("m_PokeDirection").enumValueIndex;
                bool ok = so.FindProperty("m_Interactable").objectReferenceValue != null && so.FindProperty("m_PokeCollider").objectReferenceValue != null && dir == PokeAxis.NegativeY;
                Debug.Log($"[QCXR Poke] {(ok ? "OK   " : "FALLA")} {b.name}: XRPokeFilter (dirección {dir}, collider {(so.FindProperty("m_PokeCollider").objectReferenceValue != null ? "sí" : "no")}, interactable {(so.FindProperty("m_Interactable").objectReferenceValue != null ? "sí" : "no")})");
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[QCXR Poke] {buttons.Count} botones configurados y escena guardada.");
            return buttons.Count == 2;
        }

        public static void BatchApply()
        {
            int code = 0;
            try { if (!Apply()) code = 1; }
            catch (System.Exception e) { Debug.LogException(e); code = 1; }
            Debug.Log("[QCXR Art] Batch terminado con código " + code);
            if (Application.isBatchMode) EditorApplication.Exit(code);
        }
    }
}
