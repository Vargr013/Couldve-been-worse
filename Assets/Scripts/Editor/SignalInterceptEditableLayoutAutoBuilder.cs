using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class SignalInterceptEditableLayoutAutoBuilder
{
    static SignalInterceptEditableLayoutAutoBuilder()
    {
        EditorApplication.delayCall += EnsureEditableLayoutExists;
    }

    [MenuItem("Tools/Could've Been Worse/Rebuild Editable Scene UI")]
    private static void RebuildEditableLayoutFromMenu()
    {
        RebuildEditableLayout(true);
    }

    public static void RebuildOperationGreylineSceneForBatch()
    {
        Scene scene = EditorSceneManager.OpenScene("Assets/Scenes/OperationGreylineVisualScene.unity");
        RebuildEditableLayout(true);
        EditorSceneManager.SaveScene(scene);
    }

    private static void EnsureEditableLayoutExists()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        Scene scene = SceneManager.GetActiveScene();
        if (scene.name != "OperationGreylineVisualScene" || GameObject.Find("Could've Been Worse UI") != null)
        {
            return;
        }

        RebuildEditableLayout(false);
    }

    private static void RebuildEditableLayout(bool force)
    {
        SignalInterceptDemoController controller = Object.FindFirstObjectByType<SignalInterceptDemoController>();
        if (controller == null)
        {
            if (force)
            {
                Debug.LogWarning("No SignalInterceptDemoController found in the active scene.");
            }

            return;
        }

        if (!force && GameObject.Find("Could've Been Worse UI") != null)
        {
            return;
        }

        MethodInfo rebuildMethod = typeof(SignalInterceptDemoController).GetMethod(
            "RebuildEditableSceneUi",
            BindingFlags.Instance | BindingFlags.NonPublic);

        if (rebuildMethod == null)
        {
            Debug.LogWarning("Could've Been Worse editable UI rebuild method was not found.");
            return;
        }

        rebuildMethod.Invoke(controller, null);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Debug.Log("Could've Been Worse editable scene UI was rebuilt. Save the scene to keep the visual layout.");
    }
}
