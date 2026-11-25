using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace QCXR.EditorTools
{
    /// <summary>
    /// Crea, con una acción de menú, el nuevo panel de inspección en la escena activa.
    /// No modifica GameUI.cs, GameEvents.cs, ProductFlow.cs ni las herramientas.
    /// </summary>
    public static class QCXRInspectionPanelCreator
    {
        [MenuItem("QCXR/UI/Crear panel de inspección (fase 1)")]
        public static void Create()
        {
            var existingUI = Object.FindFirstObjectByType<GameUI>(FindObjectsInactive.Include);
            if (existingUI == null)
            {
                EditorUtility.DisplayDialog("Control de Calidad XR", "No se encontró GameUI. Abre primero QCXR_MainScene.", "Entendido");
                return;
            }

            var parent = existingUI.transform;
            var existing = parent.Find("InspectionPanelController");
            if (existing != null)
            {
                Selection.activeGameObject = existing.gameObject;
                EditorUtility.DisplayDialog("Control de Calidad XR", "El panel ya existe. Se seleccionó para evitar duplicados.", "Entendido");
                return;
            }

            var controllerGo = new GameObject("InspectionPanelController");
            Undo.RegisterCreatedObjectUndo(controllerGo, "Crear panel de inspección XR");
            controllerGo.transform.SetParent(parent, false);

            var canvasGo = new GameObject("InspectionPanel", typeof(RectTransform), typeof(Canvas));
            canvasGo.transform.SetParent(controllerGo.transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;

            var rect = canvasGo.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(550, 365);
            rect.localScale = Vector3.one * 0.0013f;

            // Ubicación inicial: izquierda y un poco más abajo del HUD existente.
            // Es solo una propuesta editable: ajusta en Scene View a la comodidad real del visor.
            var hud = parent.Find("HUD");
            if (hud != null)
            {
                canvasGo.transform.SetPositionAndRotation(
                    hud.position - hud.right * 0.99f - Vector3.up * 0.31f,
                    hud.rotation);
            }
            else
            {
                canvasGo.transform.SetPositionAndRotation(new Vector3(-0.95f, 1.45f, 1.4f), Quaternion.identity);
            }

            CreateImage(canvasGo.transform, "Fondo", Vector2.zero, rect.sizeDelta,
                new Color(0.07f, 0.12f, 0.18f, 0.96f));
            CreateImage(canvasGo.transform, "BarraSuperior", new Vector2(0, 177), new Vector2(550, 11),
                new Color(0.31f, 0.82f, 0.86f));
            var title = CreateText(canvasGo.transform, "Titulo", "ESTACIÓN DE INSPECCIÓN", 25,
                new Color(0.43f, 0.87f, 0.92f), new Vector2(0, 142), new Vector2(510, 40));
            var product = CreateText(canvasGo.transform, "Producto", "Esperando producto", 36,
                Color.white, new Vector2(0, 84), new Vector2(510, 56));
            var status = CreateText(canvasGo.transform, "Estado", "Estación preparada", 28,
                new Color(0.45f, 0.86f, 0.91f), new Vector2(0, 25), new Vector2(510, 58));
            var hint = CreateText(canvasGo.transform, "Ayuda", "Completa el tutorial para comenzar.", 22,
                new Color(0.83f, 0.91f, 0.95f), new Vector2(0, -66), new Vector2(500, 110));
            var repairs = CreateText(canvasGo.transform, "Reparaciones", "Reparaciones realizadas: 0", 21,
                new Color(0.53f, 0.92f, 0.71f), new Vector2(0, -153), new Vector2(510, 37));

            var controller = controllerGo.AddComponent<InspectionStatusUI>();
            var so = new SerializedObject(controller);
            Assign(so, "panelRoot", canvasGo);
            Assign(so, "productText", product);
            Assign(so, "statusText", status);
            Assign(so, "hintText", hint);
            Assign(so, "repairedText", repairs);
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(controllerGo.scene);
            Selection.activeGameObject = canvasGo;
            Debug.Log("[QCXR UI] Panel de inspección creado. Ajusta su posición en Scene View y guarda la escena.");
        }

        private static void Assign(SerializedObject so, string name, Object value)
        {
            var p = so.FindProperty(name);
            if (p != null) p.objectReferenceValue = value;
        }

        private static Image CreateImage(Transform parent, string name, Vector2 position, Vector2 size, Color color)
        {
            var rt = CreateRect(parent, name, position, size);
            var image = rt.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false; // Panel informativo: nunca bloquea el agarre ni los botones.
            return image;
        }

        private static TMP_Text CreateText(Transform parent, string name, string value, float size,
            Color color, Vector2 position, Vector2 dimensions)
        {
            var rt = CreateRect(parent, name, position, dimensions);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            t.text = value;
            t.fontSize = size;
            t.color = color;
            t.alignment = TextAlignmentOptions.Center;
            t.textWrappingMode = TextWrappingModes.Normal;
            t.raycastTarget = false;
            return t;
        }

        private static RectTransform CreateRect(Transform parent, string name, Vector2 position, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
            return rt;
        }
    }
}
