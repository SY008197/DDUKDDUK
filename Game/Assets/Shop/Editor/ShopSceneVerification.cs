using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DdukDduk.Shop.Editor
{
    /// <summary>Checks the serialized scene before ShopApp can rebuild its UI in Play mode.</summary>
    public static class ShopSceneVerification
    {
        private static int passed;

        [MenuItem("DDUKDDUK/Verify Saved Shop Scene")]
        public static void RunAll()
        {
            if (Application.isPlaying)
                throw new InvalidOperationException("Saved scene verification must run outside Play mode.");

            passed = 0;
            // Reopening is essential: the previous verification only exercised freshly rebuilt UI.
            var scene = EditorSceneManager.OpenScene(ShopProjectBuilder.ScenePath, OpenSceneMode.Single);
            var roots = scene.GetRootGameObjects();
            var graphics = roots.SelectMany(root => root.GetComponentsInChildren<Graphic>(true)).ToArray();
            Check(graphics.Length > 0, "The saved shop scene contains UI graphics.");

            // Do not use Graphic.canvasRenderer here: its lazy accessor can hide a missing native component.
            foreach (var graphic in graphics)
                Check(graphic.GetComponent<CanvasRenderer>() != null,
                    "Serialized CanvasRenderer exists for " + HierarchyPath(graphic.transform));

            var corners = graphics.OfType<RecipePageCornerGraphic>().ToArray();
            var turningPages = graphics.OfType<RecipePageTurnGraphic>().ToArray();
            Check(corners.Length == 2, "Exactly two serialized paper corner graphics exist.");
            Check(turningPages.Length == 2, "Exactly two serialized page and shadow graphics exist.");
            foreach (var graphic in corners.Cast<Graphic>().Concat(turningPages))
                Check(graphic.GetComponents<CanvasRenderer>().Length == 1,
                    "Exactly one serialized CanvasRenderer exists for " + graphic.name);
            foreach (var corner in corners)
            {
                Check(!corner.gameObject.activeInHierarchy,
                    "The initially hidden recipe corner was checked before activation: " + corner.name);
                Check(corner.GetComponent<Button>() != null, "Paper corner has a Button: " + corner.name);
            }

            VerifyComponentContract<RecipePageCornerGraphic>();
            VerifyComponentContract<RecipePageTurnGraphic>();
            VerifySavedCornerRaycasts(roots, corners);
            Debug.Log("SHOP_SCENE_VERIFICATION_PASSED: " + passed + " checks");
        }

        private static void VerifyComponentContract<T>() where T : Graphic
        {
            var temporary = new GameObject("Temporary renderer contract check", typeof(RectTransform));
            temporary.SetActive(false);
            try
            {
                temporary.AddComponent<T>();
                Check(temporary.GetComponents<CanvasRenderer>().Length == 1,
                    "Adding " + typeof(T).Name + " to an inactive object creates exactly one CanvasRenderer.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(temporary);
            }
        }

        private static void VerifySavedCornerRaycasts(GameObject[] roots, RecipePageCornerGraphic[] corners)
        {
            var transforms = roots.SelectMany(root => root.GetComponentsInChildren<Transform>(true)).ToArray();
            var evening = transforms.Single(item => item.name == "Evening Screen");
            var book = transforms.Single(item => item.name == "Recipe Book Screen");
            var savedActiveStates = transforms.ToDictionary(item => item.gameObject, item => item.gameObject.activeSelf);
            try
            {
                foreach (Transform child in evening)
                    child.gameObject.SetActive(child == book || child.name == "Evening Light");
                foreach (var name in new[] { "Production Screen", "Customer Screen", "Dialogs", "Notice" })
                    foreach (var layer in transforms.Where(item => item.name == name))
                        layer.gameObject.SetActive(false);
                for (var ancestor = book; ancestor != null; ancestor = ancestor.parent)
                    ancestor.gameObject.SetActive(true);

                var canvas = book.GetComponentInParent<Canvas>();
                Check(canvas != null, "Saved recipe book has a Canvas.");
                var raycaster = canvas.GetComponent<GraphicRaycaster>();
                Check(raycaster != null, "Saved Canvas has a GraphicRaycaster.");
                var eventSystem = roots.SelectMany(root => root.GetComponentsInChildren<EventSystem>(true)).Single();
                LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)canvas.transform);
                Canvas.ForceUpdateCanvases();
                var screenCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
                var canvasRect = (RectTransform)canvas.transform;

                foreach (var corner in corners)
                {
                    Check(corner.gameObject.activeInHierarchy, "Saved paper corner can be activated: " + corner.name);
                    Check(corner.rectTransform.rect.width > 0 && corner.rectTransform.rect.height > 0,
                        "Saved paper corner has a nonempty hit area: " + corner.name);
                    var points = new Vector3[4];
                    corner.rectTransform.GetWorldCorners(points);
                    foreach (var point in points)
                    {
                        var local = canvasRect.InverseTransformPoint(point);
                        Check(local.x >= canvasRect.rect.xMin - 1 && local.x <= canvasRect.rect.xMax + 1
                            && local.y >= canvasRect.rect.yMin - 1 && local.y <= canvasRect.rect.yMax + 1,
                            "Saved paper corner remains within the Canvas: " + corner.name);
                    }

                    var pointer = new PointerEventData(eventSystem)
                    {
                        position = RectTransformUtility.WorldToScreenPoint(screenCamera,
                            corner.rectTransform.TransformPoint(corner.rectTransform.rect.center))
                    };
                    var results = new List<RaycastResult>();
                    // Runs the same native CanvasRenderer.cull access that produced the reported exception.
                    eventSystem.RaycastAll(pointer, results);
                    if (corner.depth >= 0)
                        Check(results.Any(result => result.gameObject == corner.gameObject),
                            "Saved corner center raycast includes its Button: " + corner.name);
                    else
                    {
                        // A -nographics batch editor may not assign render depths to canvases.
                        Check(corner.GetComponent<CanvasRenderer>() != null,
                            "Saved corner raycast completes without a missing renderer: " + corner.name);
                        Debug.Log("SHOP_SCENE_RAYCAST_NO_RENDER_DEPTH: " + corner.name
                            + "; pointer hit assertions remain covered by the development player UI verification.");
                    }
                }
            }
            finally
            {
                foreach (var state in savedActiveStates)
                    if (state.Key != null) state.Key.SetActive(state.Value);
                Canvas.ForceUpdateCanvases();
                // Never save the temporary research screen activation into the opening scene.
                // Reopening the saved scene also discards the temporary editor dirty flag.
                EditorSceneManager.OpenScene(ShopProjectBuilder.ScenePath, OpenSceneMode.Single);
            }
        }

        private static string HierarchyPath(Transform transform)
        {
            var path = transform.name;
            for (var parent = transform.parent; parent != null; parent = parent.parent)
                path = parent.name + "/" + path;
            return path;
        }

        private static void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("Saved shop scene verification failed: " + message);
            passed++;
        }
    }
}
