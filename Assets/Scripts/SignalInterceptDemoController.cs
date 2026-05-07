using System;
using System.Linq;
using System.Threading;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

public sealed class SignalInterceptDemoController : MonoBehaviour
{
    [SerializeField] private string ollamaEndpoint = "http://localhost:11434/api/generate";
    [SerializeField] private string modelName = "llama3.1:8b";
    [SerializeField] private int requestTimeoutSeconds = 30;
    [SerializeField] private string initialTransmissionText = "Awaiting intercept...";

    private static readonly string[] BlockedLabels =
    {
        "friendly",
        "enemy",
        "hostile",
        "deception"
    };

    private Text statusText;
    private Text transmissionText;
    private Button generateButton;
    private CancellationTokenSource requestCancellation;

    private void Awake()
    {
        BuildInterface();
        SetWaitingState();
    }

    private void OnDestroy()
    {
        requestCancellation?.Cancel();
        requestCancellation?.Dispose();
    }

    private async void GenerateIntercept()
    {
        requestCancellation?.Cancel();
        requestCancellation?.Dispose();
        requestCancellation = new CancellationTokenSource();

        SetBusyState();

        try
        {
            var client = new OllamaClient(ollamaEndpoint, modelName, requestTimeoutSeconds);
            string cleanedResponse = await RequestValidIntercept(client, InterceptPromptBuilder.BuildPrompt(), requestCancellation.Token);

            Debug.Log($"Ollama response received:\n{cleanedResponse}");
            transmissionText.text = cleanedResponse;
            statusText.text = "Live Ollama response received.";
        }
        catch (OperationCanceledException)
        {
            statusText.text = "Ollama request cancelled.";
        }
        catch (InterceptValidationException exception)
        {
            statusText.text = "Ollama responded, but the message failed validation. Try generating again.\n" + exception.Message;
            transmissionText.text = "No valid intercept available.";
            Debug.LogWarning(exception.Message);
        }
        catch (Exception exception)
        {
            statusText.text = "Ollama is not responding. Start Ollama and install/select the configured model.\n" + exception.Message;
            transmissionText.text = "No live intercept available.";
        }
        finally
        {
            generateButton.interactable = true;
        }
    }

    private async System.Threading.Tasks.Task<string> RequestValidIntercept(OllamaClient client, string prompt, CancellationToken cancellationToken)
    {
        try
        {
            return await SendAndValidate(client, prompt, cancellationToken);
        }
        catch (InterceptValidationException firstFailure)
        {
            Debug.LogWarning("Ollama response failed validation. Retrying once with stricter prompt.\n" + firstFailure.Message);
            return await SendAndValidate(client, InterceptPromptBuilder.BuildRetryPrompt(), cancellationToken);
        }
    }

    private async System.Threading.Tasks.Task<string> SendAndValidate(OllamaClient client, string prompt, CancellationToken cancellationToken)
    {
        Debug.Log($"Sending Ollama query to {modelName} at {ollamaEndpoint}:\n{prompt}");

        string rawResponse = await client.GenerateAsync(prompt, cancellationToken);
        string cleanedResponse = CleanResponse(rawResponse);
        ValidateResponse(cleanedResponse);

        return cleanedResponse;
    }

    private void SetWaitingState()
    {
        statusText.text = $"Ollama required: {ollamaEndpoint}";
        transmissionText.text = initialTransmissionText;
        generateButton.interactable = true;
    }

    private void SetBusyState()
    {
        statusText.text = "Contacting local Ollama model...";
        transmissionText.text = "Listening for transmission...";
        generateButton.interactable = false;
    }

    private static string CleanResponse(string response)
    {
        return (response ?? string.Empty).Trim().Trim('"', '\'', '`');
    }

    private static void ValidateResponse(string response)
    {
        if (string.IsNullOrWhiteSpace(response))
        {
            throw new InterceptValidationException("Ollama returned an empty response.");
        }

        string lowerResponse = response.ToLowerInvariant();
        if (BlockedLabels.Any(label => lowerResponse.Contains(label)))
        {
            throw new InterceptValidationException("Ollama response revealed a blocked classification label.");
        }
    }

    private void BuildInterface()
    {
        EnsureEventSystem();

        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null)
        {
            font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        }

        Canvas canvas = CreateCanvas();
        Image background = CreateImage("Background", canvas.transform, new Color(0.055f, 0.07f, 0.08f, 1f));
        StretchToParent(background.rectTransform, 0f, 0f, 0f, 0f);

        Text title = CreateText("Title", canvas.transform, font, "Signal Intercept", 42, FontStyle.Bold, TextAnchor.UpperLeft);
        title.color = new Color(0.86f, 0.96f, 0.88f, 1f);
        AnchorTop(title.rectTransform, 48f, 42f, 48f, 58f);

        Text configText = CreateText("Config", canvas.transform, font, $"Model: {modelName}    Endpoint: {ollamaEndpoint}", 16, FontStyle.Normal, TextAnchor.UpperLeft);
        configText.color = new Color(0.62f, 0.72f, 0.68f, 1f);
        AnchorTop(configText.rectTransform, 50f, 106f, 50f, 28f);

        Image readoutPanel = CreateImage("Transmission Panel", canvas.transform, new Color(0.1f, 0.14f, 0.13f, 1f));
        StretchToParent(readoutPanel.rectTransform, 48f, 152f, 48f, 182f);

        transmissionText = CreateText("Transmission Text", readoutPanel.transform, font, initialTransmissionText, 30, FontStyle.Normal, TextAnchor.MiddleLeft);
        transmissionText.color = new Color(0.86f, 1f, 0.9f, 1f);
        StretchToParent(transmissionText.rectTransform, 28f, 24f, 28f, 24f);

        statusText = CreateText("Status Text", canvas.transform, font, string.Empty, 18, FontStyle.Normal, TextAnchor.UpperLeft);
        statusText.color = new Color(0.94f, 0.82f, 0.58f, 1f);
        AnchorBottom(statusText.rectTransform, 50f, 112f, 50f, 58f);

        generateButton = CreateButton("Generate Intercept Button", canvas.transform, font, "Generate Intercept");
        AnchorBottom(generateButton.GetComponent<RectTransform>(), 48f, 38f, 48f, 56f);
        generateButton.onClick.AddListener(GenerateIntercept);
    }

    private static Canvas CreateCanvas()
    {
        var canvasObject = new GameObject("Signal Intercept UI", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        var scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f, 720f);
        scaler.matchWidthOrHeight = 0.5f;

        return canvas;
    }

    private static Image CreateImage(string name, Transform parent, Color color)
    {
        var imageObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        imageObject.transform.SetParent(parent, false);
        var image = imageObject.GetComponent<Image>();
        image.color = color;
        return image;
    }

    private static Text CreateText(string name, Transform parent, Font font, string text, int size, FontStyle style, TextAnchor alignment)
    {
        var textObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        textObject.transform.SetParent(parent, false);

        var textComponent = textObject.GetComponent<Text>();
        textComponent.font = font;
        textComponent.text = text;
        textComponent.fontSize = size;
        textComponent.fontStyle = style;
        textComponent.alignment = alignment;
        textComponent.horizontalOverflow = HorizontalWrapMode.Wrap;
        textComponent.verticalOverflow = VerticalWrapMode.Truncate;

        return textComponent;
    }

    private static Button CreateButton(string name, Transform parent, Font font, string label)
    {
        Image buttonImage = CreateImage(name, parent, new Color(0.28f, 0.53f, 0.35f, 1f));
        var button = buttonImage.gameObject.AddComponent<Button>();
        button.targetGraphic = buttonImage;

        Text buttonText = CreateText("Label", button.transform, font, label, 24, FontStyle.Bold, TextAnchor.MiddleCenter);
        buttonText.color = Color.white;
        StretchToParent(buttonText.rectTransform, 8f, 8f, 8f, 8f);

        return button;
    }

    private static void EnsureEventSystem()
    {
        if (FindFirstObjectByType<EventSystem>() != null)
        {
            return;
        }

        var eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        eventSystem.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
    }

    private static void StretchToParent(RectTransform rectTransform, float left, float top, float right, float bottom)
    {
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = new Vector2(left, bottom);
        rectTransform.offsetMax = new Vector2(-right, -top);
    }

    private static void AnchorTop(RectTransform rectTransform, float left, float top, float right, float height)
    {
        rectTransform.anchorMin = Vector2.up;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = new Vector2(left, -top - height);
        rectTransform.offsetMax = new Vector2(-right, -top);
    }

    private static void AnchorBottom(RectTransform rectTransform, float left, float bottom, float right, float height)
    {
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.right;
        rectTransform.offsetMin = new Vector2(left, bottom);
        rectTransform.offsetMax = new Vector2(-right, bottom + height);
    }

    private sealed class InterceptValidationException : Exception
    {
        public InterceptValidationException(string message) : base(message)
        {
        }
    }
}
