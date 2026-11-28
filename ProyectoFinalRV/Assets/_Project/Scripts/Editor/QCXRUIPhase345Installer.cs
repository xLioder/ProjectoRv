using QCXR;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace QCXR.EditorTools
{
    // Agrega a la escena abierta el panel de diagnóstico, las métricas del HUD y las de resultados.
    // Respeta la posicion del InspectionPanel.
    // Se puede correr varias veces sin duplicar nada.
    public static class QCXRUIPhase345Installer
    {
        private static readonly Color Background = new Color(0.055f, 0.098f, 0.15f, 0.97f);
        private static readonly Color Surface = new Color(0.105f, 0.18f, 0.255f, 0.98f);
        private static readonly Color Cyan = new Color(0.36f, 0.85f, 0.90f, 1f);
        private static readonly Color Green = new Color(0.40f, 0.88f, 0.69f, 1f);
        private static readonly Color Muted = new Color(0.73f, 0.83f, 0.90f, 1f);
        private static readonly Color Amber = new Color(1f, 0.76f, 0.44f, 1f);

        [MenuItem("QCXR/UI/Instalar mejoras fases 3-4-5")]
        private static void Install()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog("QCXR - Interfaz", "Deten Play antes de instalar las mejoras.", "Aceptar");
                return;
            }

            GameUI ui = Object.FindFirstObjectByType<GameUI>(FindObjectsInactive.Include);
            if (ui == null)
            {
                EditorUtility.DisplayDialog("QCXR - Interfaz", "No encontre GameUI. Abre QCXR_MainScene.", "Aceptar");
                return;
            }

            Transform controller = ui.transform.Find("InspectionPanelController");
            Transform inspection = controller != null ? controller.Find("InspectionPanel") : null;
            Transform hud = ui.transform.Find("HUD");
            Transform results = ui.transform.Find("Results");
            if (inspection == null || hud == null || results == null)
            {
                EditorUtility.DisplayDialog("QCXR - Interfaz",
                    "Falta InspectionPanel (fase 1), HUD o Results. Verifica que estas en la escena correcta.", "Aceptar");
                return;
            }

            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("QCXR - Instalar fases 3 4 5");
            InstallDiagnosis(controller, inspection);
            InstallHud(ui.gameObject, hud);
            InstallResults(ui.gameObject, results);
            Undo.CollapseUndoOperations(undoGroup);

            EditorSceneManager.MarkSceneDirty(ui.gameObject.scene);
            Selection.activeGameObject = ui.gameObject;
            EditorUtility.DisplayDialog("QCXR - Fases 3, 4 y 5",
                "Listo. Se agrego el diagnostico, se mejoro el HUD y se amplio el resumen de resultados.\n\n" +
                "No se cambiaron los scripts de tus companeros ni la posicion del InspectionPanel. " +
                "Guarda la escena (Ctrl+S), ejecuta Play y comprueba los cambios.", "Entendido");
        }

        private static void InstallDiagnosis(Transform controller, Transform inspection)
        {
            // Anade el lateral dentro del Canvas: se oculta junto con el panel de inspeccion.
            RectTransform side = EnsureRect(inspection, "QCXR_Diagnostico", new Vector2(535f, 0f), new Vector2(470f, 365f));
            EnsureImage(side, "Fondo", Vector2.zero, new Vector2(470f, 365f), Background);
            EnsureImage(side, "BordeSuperior", new Vector2(0f, 176f), new Vector2(470f, 8f), Cyan);
            EnsureText(side, "Titulo", "DIAGNOSTICO Y REPARACION", 23, Cyan, new Vector2(0, 140), new Vector2(440, 36));
            TMP_Text stage = EnsureText(side, "Etapa", "ESPERANDO", 24, Amber, new Vector2(0, 89), new Vector2(440, 45));
            EnsureImage(side, "CajaActividad", new Vector2(0, 13), new Vector2(435, 98), Surface);
            TMP_Text activity = EnsureText(side, "Actividad", "Pendiente", 22, Color.white, new Vector2(0, 13), new Vector2(405, 85));
            TMP_Text repairs = EnsureText(side, "Reparaciones", "0 reparaciones", 23, Green, new Vector2(0, -75), new Vector2(430, 44));
            TMP_Text hint = EnsureText(side, "Indicacion", "", 19, Muted, new Vector2(0, -137), new Vector2(430, 63));

            var view = EnsureComponent<QCXRDiagnosticDetailsUI>(controller.gameObject);
            Assign(view, "stageText", stage, "activityText", activity, "repairsText", repairs, "nextStepText", hint);
        }

        private static void InstallHud(GameObject root, Transform hud)
        {
            EnsureText(hud, "QCXR_EtiquetaHUD", "CONTROL DE CALIDAD  /  LINEA DE INSPECCION", 20, Cyan,
                new Vector2(0, 109), new Vector2(740, 33));
            TMP_Text processed = EnsureText(hud, "QCXR_Procesados", "PROCESADOS  0", 20, Muted,
                new Vector2(-300, -87), new Vector2(186, 48));
            TMP_Text fixedText = EnsureText(hud, "QCXR_Reparados", "REPARADOS  0", 20, Green,
                new Vector2(300, -87), new Vector2(186, 48));

            // La barra de tiempo original sigue siendo administrada por GameUI.
            // Reducir su ancho permite colocar etiquetas a los lados sin encimarlas.
            RectTransform slider = hud.Find("ProductTimeBar") as RectTransform;
            if (slider != null)
            {
                Undo.RecordObject(slider, "Ajustar ancho del tiempo por producto");
                slider.sizeDelta = new Vector2(360, slider.sizeDelta.y);
                slider.anchoredPosition = new Vector2(0f, -85f);
            }

            Image bg = hud.Find("Bg") != null ? hud.Find("Bg").GetComponent<Image>() : null;
            if (bg != null)
            {
                Undo.RecordObject(bg, "Estilizar HUD");
                bg.color = Background;
            }

            var view = EnsureComponent<QCXRHudMetricsUI>(root);
            Assign(view, "evaluatedText", processed, "repairsText", fixedText);
        }

        private static void InstallResults(GameObject root, Transform results)
        {
            RectTransform canvas = results as RectTransform;
            if (canvas != null)
            {
                Undo.RecordObject(canvas, "Ampliar pantalla de resultados");
                canvas.sizeDelta = new Vector2(760f, 670f);
            }

            ResizeExisting(results, "Bg", new Vector2(760, 670), new Vector2(0, 0));
            ResizeExisting(results, "Frame", new Vector2(760, 670), new Vector2(0, 0));
            ResizeExisting(results, "Deco", new Vector2(700, 12), new Vector2(0, 314));
            Image bg = results.Find("Bg") != null ? results.Find("Bg").GetComponent<Image>() : null;
            if (bg != null)
            {
                Undo.RecordObject(bg, "Estilizar resultados");
                bg.color = Background;
            }

            MoveExisting(results, "Title", new Vector2(0, 256), new Vector2(720, 72));
            MoveExisting(results, "Grade", new Vector2(-228, 129), new Vector2(200, 154));
            MoveExisting(results, "Score", new Vector2(105, 135), new Vector2(450, 86));
            MoveExisting(results, "Details", new Vector2(0, -32), new Vector2(698, 192));
            MoveExisting(results, "Feedback", new Vector2(0, -156), new Vector2(700, 76));
            MoveExisting(results, "Restart", new Vector2(-176, -291), new Vector2(275, 62));
            MoveExisting(results, "Quit", new Vector2(176, -291), new Vector2(275, 62));

            EnsureImage(results, "QCXR_Separador", new Vector2(0, -203), new Vector2(686, 2), Cyan);
            TMP_Text heading = EnsureText(results, "QCXR_EncabezadoResultados", "REPORTE DE TURNO", 19, Cyan,
                new Vector2(0, 314), new Vector2(700, 29));
            TMP_Text breakdown = EnsureText(results, "QCXR_DesgloseResultados", "", 21, Green,
                new Vector2(0, -231), new Vector2(708, 46));

            var view = EnsureComponent<QCXRResultsMetricsUI>(root);
            Assign(view, "breakdownText", breakdown, "summaryHeading", heading);
        }

        private static void MoveExisting(Transform parent, string name, Vector2 pos, Vector2 size)
        {
            RectTransform rt = parent.Find(name) as RectTransform;
            if (rt == null) return;
            Undo.RecordObject(rt, "Reorganizar UI: " + name);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }

        private static void ResizeExisting(Transform parent, string name, Vector2 size, Vector2 pos)
        {
            MoveExisting(parent, name, pos, size);
        }

        private static T EnsureComponent<T>(GameObject owner) where T : Component
        {
            T component = owner.GetComponent<T>();
            return component != null ? component : Undo.AddComponent<T>(owner);
        }

        private static RectTransform EnsureRect(Transform parent, string name, Vector2 pos, Vector2 size)
        {
            Transform existing = parent.Find(name);
            RectTransform rt;
            if (existing != null)
            {
                rt = existing as RectTransform;
                if (rt == null) throw new System.InvalidOperationException("Objeto existente no es RectTransform: " + name);
                Undo.RecordObject(rt, "Actualizar UI " + name);
            }
            else
            {
                GameObject go = new GameObject(name, typeof(RectTransform));
                Undo.RegisterCreatedObjectUndo(go, "Crear UI " + name);
                rt = go.GetComponent<RectTransform>();
                rt.SetParent(parent, false);
            }
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return rt;
        }

        private static Image EnsureImage(Transform parent, string name, Vector2 pos, Vector2 size, Color color)
        {
            RectTransform rt = EnsureRect(parent, name, pos, size);
            Image image = EnsureComponent<Image>(rt.gameObject);
            Undo.RecordObject(image, "Estilo UI " + name);
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static TMP_Text EnsureText(Transform parent, string name, string value,
            int fontSize, Color color, Vector2 pos, Vector2 size)
        {
            RectTransform rt = EnsureRect(parent, name, pos, size);
            TextMeshProUGUI label = EnsureComponent<TextMeshProUGUI>(rt.gameObject);
            Undo.RecordObject(label, "Texto UI " + name);
            label.fontSize = fontSize;
            label.color = color;
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.Normal; // TMP actual en Unity 6
            label.raycastTarget = false;
            // Reinstalar en escena no debe borrar valores que el juego haya actualizado,
            // solo cambia la plantilla y se reemplazaran con eventos al entrar a Play.
            label.text = value;
            return label;
        }

        private static void Assign(Object component, params object[] properties)
        {
            SerializedObject so = new SerializedObject(component);
            for (int i = 0; i < properties.Length; i += 2)
            {
                string field = (string)properties[i];
                Object target = properties[i + 1] as Object;
                SerializedProperty prop = so.FindProperty(field);
                if (prop == null)
                    throw new System.InvalidOperationException("Falta campo serialized: " + field);
                prop.objectReferenceValue = target;
            }
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(component);
        }
    }
}
