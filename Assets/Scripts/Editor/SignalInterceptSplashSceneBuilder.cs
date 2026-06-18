using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Video;

public static class SignalInterceptSplashSceneBuilder
{
    private const string SplashScenePath = "Assets/Scenes/SplashScene.unity";
    private const string TargetScenePath = "Assets/Scenes/OperationGreylineVisualScene.unity";
    private const string SplashVideoPath = "Assets/Video/Splash - Trim.mp4";

    [MenuItem("Tools/Could've Been Worse/Rebuild Splash Scene")]
    public static void RebuildSplashScene()
    {
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        scene.name = "SplashScene";

        BuildSplashHierarchy();
        EditorSceneManager.SaveScene(scene, SplashScenePath);
        EnsureBuildSettings();
        AssetDatabase.SaveAssets();
    }

    private static void BuildSplashHierarchy()
    {
        var cameraObject = new GameObject("Splash Camera");
        var camera = cameraObject.AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.015f, 0.018f, 0.016f, 1f);
        camera.orthographic = true;
        camera.depth = 100;

        var root = new GameObject("Splash Screen");
        var canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 500;
        root.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        root.GetComponent<CanvasScaler>().referenceResolution = new Vector2(1280f, 720f);
        root.GetComponent<CanvasScaler>().matchWidthOrHeight = 0.5f;
        root.AddComponent<GraphicRaycaster>();
        root.AddComponent<CanvasGroup>();

        var videoObject = new GameObject("Splash Video");
        videoObject.transform.SetParent(root.transform, false);
        var rect = videoObject.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        var rawImage = videoObject.AddComponent<RawImage>();
        rawImage.color = Color.white;

        var overlayObject = new GameObject("Loading Plate");
        overlayObject.transform.SetParent(root.transform, false);
        var overlayRect = overlayObject.AddComponent<RectTransform>();
        overlayRect.anchorMin = new Vector2(0f, 0f);
        overlayRect.anchorMax = new Vector2(1f, 0f);
        overlayRect.pivot = new Vector2(0.5f, 0f);
        overlayRect.offsetMin = new Vector2(0f, 0f);
        overlayRect.offsetMax = new Vector2(0f, 88f);
        var overlay = overlayObject.AddComponent<Image>();
        overlay.color = new Color(0f, 0f, 0f, 0.55f);

        var textObject = new GameObject("Loading Text");
        textObject.transform.SetParent(overlayObject.transform, false);
        var textRect = textObject.AddComponent<RectTransform>();
        textRect.anchorMin = new Vector2(0f, 0f);
        textRect.anchorMax = new Vector2(1f, 1f);
        textRect.offsetMin = new Vector2(40f, 14f);
        textRect.offsetMax = new Vector2(-40f, -12f);
        var text = textObject.AddComponent<Text>();
        text.text = "Starting Operation Greyline...";
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = 22;
        text.color = new Color(0.88f, 0.96f, 0.86f, 1f);
        text.alignment = TextAnchor.MiddleLeft;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;

        var player = root.AddComponent<VideoPlayer>();
        player.playOnAwake = false;
        player.isLooping = false;
        player.audioOutputMode = VideoAudioOutputMode.Direct;
        player.clip = AssetDatabase.LoadAssetAtPath<VideoClip>(SplashVideoPath);

        var controller = root.AddComponent<SplashScreenController>();
        var serializedController = new SerializedObject(controller);
        serializedController.FindProperty("targetSceneName").stringValue = "OperationGreylineVisualScene";
        serializedController.FindProperty("splashClip").objectReferenceValue = player.clip;
        serializedController.FindProperty("videoPlayer").objectReferenceValue = player;
        serializedController.FindProperty("videoImage").objectReferenceValue = rawImage;
        serializedController.FindProperty("canvasGroup").objectReferenceValue = root.GetComponent<CanvasGroup>();
        serializedController.FindProperty("minimumSplashSeconds").floatValue = 3.5f;
        serializedController.FindProperty("maximumSplashSeconds").floatValue = 12f;
        serializedController.FindProperty("fadeOutSeconds").floatValue = 0.35f;
        serializedController.FindProperty("allowSkipAfterMinimum").boolValue = true;
        serializedController.ApplyModifiedPropertiesWithoutUndo();

        var eventSystem = new GameObject("EventSystem");
        eventSystem.AddComponent<EventSystem>();
        eventSystem.AddComponent<InputSystemUIInputModule>();
    }

    private static void EnsureBuildSettings()
    {
        EditorBuildSettings.scenes = EditorBuildSettings.scenes
            .Where(scene => scene.path != SplashScenePath && scene.path != TargetScenePath)
            .Prepend(new EditorBuildSettingsScene(TargetScenePath, true))
            .Prepend(new EditorBuildSettingsScene(SplashScenePath, true))
            .ToArray();
    }
}
