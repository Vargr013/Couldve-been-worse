using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Video;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public sealed class SplashScreenController : MonoBehaviour
{
    [SerializeField] private string targetSceneName = "OperationGreylineVisualScene";
    [SerializeField] private VideoClip splashClip;
    [SerializeField] private VideoPlayer videoPlayer;
    [SerializeField] private RawImage videoImage;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private float minimumSplashSeconds = 3.5f;
    [SerializeField] private float maximumSplashSeconds = 12f;
    [SerializeField] private float fadeOutSeconds = 0.35f;
    [SerializeField] private bool allowSkipAfterMinimum = true;

    private RenderTexture runtimeTexture;
    private Scene splashScene;
    private bool videoFinished;
    private bool videoFailed;

#if UNITY_EDITOR
    private void Reset()
    {
        EnsureEditableUi();
    }

    private void OnValidate()
    {
        if (!Application.isPlaying)
        {
            EnsureEditableUi();
        }
    }
#endif

    private IEnumerator Start()
    {
        splashScene = gameObject.scene;
        DontDestroyOnLoad(gameObject);
        SetupVideoPlayer();

        AsyncOperation loadOperation = SceneManager.LoadSceneAsync(targetSceneName, LoadSceneMode.Additive);
        if (loadOperation == null)
        {
            Debug.LogError($"Unable to load target scene '{targetSceneName}' from splash screen.");
            yield break;
        }

        StartVideo();

        float startedAt = Time.unscaledTime;
        while (!loadOperation.isDone)
        {
            yield return null;
        }

        Scene targetScene = SceneManager.GetSceneByName(targetSceneName);
        if (targetScene.IsValid())
        {
            SceneManager.SetActiveScene(targetScene);
        }

        while (!CanLeaveSplash(startedAt))
        {
            yield return null;
        }

        yield return FadeOut();

        Destroy(gameObject);

        if (splashScene.IsValid() && splashScene.isLoaded && splashScene.name != targetSceneName)
        {
            SceneManager.UnloadSceneAsync(splashScene);
        }
    }

    private void SetupVideoPlayer()
    {
        EnsureRuntimeUi();

        videoPlayer ??= GetComponentInChildren<VideoPlayer>();
        videoImage ??= GetComponentInChildren<RawImage>();
        canvasGroup ??= GetComponentInChildren<CanvasGroup>();

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
        }

        if (videoPlayer == null || videoImage == null)
        {
            Debug.LogError("Splash screen needs a VideoPlayer and RawImage.");
            videoFailed = true;
            return;
        }

        int textureWidth = Mathf.Max(Screen.width, 1280);
        int textureHeight = Mathf.Max(Screen.height, 720);
        runtimeTexture = new RenderTexture(textureWidth, textureHeight, 0, RenderTextureFormat.ARGB32)
        {
            name = "Runtime Splash Video Texture"
        };
        runtimeTexture.Create();

        videoImage.texture = runtimeTexture;
        videoPlayer.playOnAwake = false;
        videoPlayer.isLooping = false;
        videoPlayer.renderMode = VideoRenderMode.RenderTexture;
        videoPlayer.targetTexture = runtimeTexture;
        videoPlayer.audioOutputMode = VideoAudioOutputMode.Direct;

        if (splashClip != null)
        {
            videoPlayer.clip = splashClip;
        }

        videoPlayer.loopPointReached += HandleVideoFinished;
        videoPlayer.errorReceived += HandleVideoError;
    }

    private void EnsureRuntimeUi()
    {
        EnsureEditableUi();
    }

    private void EnsureEditableUi()
    {
        Canvas canvas = GetComponent<Canvas>();
        if (canvas == null)
        {
            canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 500;
        }

        CanvasScaler scaler = GetComponent<CanvasScaler>();
        if (scaler == null)
        {
            scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            scaler.matchWidthOrHeight = 0.5f;
        }

        if (GetComponent<GraphicRaycaster>() == null)
        {
            gameObject.AddComponent<GraphicRaycaster>();
        }

        canvasGroup ??= GetComponent<CanvasGroup>() ?? gameObject.AddComponent<CanvasGroup>();
        videoPlayer ??= GetComponent<VideoPlayer>() ?? gameObject.AddComponent<VideoPlayer>();

        if (videoImage == null)
        {
            var imageObject = new GameObject("Splash Video");
            imageObject.transform.SetParent(transform, false);
            var rect = imageObject.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            videoImage = imageObject.AddComponent<RawImage>();
            videoImage.color = Color.white;
        }

        if (videoPlayer != null && splashClip != null && videoPlayer.clip == null)
        {
            videoPlayer.clip = splashClip;
        }
    }

    private void StartVideo()
    {
        if (videoPlayer == null || videoPlayer.clip == null)
        {
            videoFailed = true;
            return;
        }

        videoPlayer.Play();
    }

    private void HandleVideoFinished(VideoPlayer source)
    {
        videoFinished = true;
    }

    private void HandleVideoError(VideoPlayer source, string message)
    {
        videoFailed = true;
        Debug.LogWarning($"Splash video failed: {message}");
    }

    private bool CanLeaveSplash(float startedAt)
    {
        float elapsed = Time.unscaledTime - startedAt;
        bool minimumMet = elapsed >= minimumSplashSeconds;
        bool skipRequested = allowSkipAfterMinimum && minimumMet && SkipRequested();
        bool videoDone = videoFinished || videoFailed;
        return (minimumMet && (videoDone || skipRequested)) || elapsed >= maximumSplashSeconds;
    }

    private static bool SkipRequested()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current?.anyKey.wasPressedThisFrame == true ||
               Mouse.current?.leftButton.wasPressedThisFrame == true ||
               Gamepad.current?.buttonSouth.wasPressedThisFrame == true;
#elif ENABLE_LEGACY_INPUT_MANAGER
        return Input.anyKeyDown || Input.GetMouseButtonDown(0);
#else
        return false;
#endif
    }

    private IEnumerator FadeOut()
    {
        if (canvasGroup == null || fadeOutSeconds <= 0f)
        {
            yield break;
        }

        float startedAt = Time.unscaledTime;
        while (Time.unscaledTime - startedAt < fadeOutSeconds)
        {
            float progress = (Time.unscaledTime - startedAt) / fadeOutSeconds;
            canvasGroup.alpha = Mathf.Lerp(1f, 0f, progress);
            yield return null;
        }

        canvasGroup.alpha = 0f;
    }

    private void OnDestroy()
    {
        if (videoPlayer != null)
        {
            videoPlayer.loopPointReached -= HandleVideoFinished;
            videoPlayer.errorReceived -= HandleVideoError;
        }

        if (runtimeTexture != null)
        {
            runtimeTexture.Release();
            Destroy(runtimeTexture);
        }
    }
}
