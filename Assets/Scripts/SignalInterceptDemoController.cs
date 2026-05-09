using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

public sealed class SignalInterceptDemoController : MonoBehaviour
{
    [SerializeField] private string ollamaEndpoint = "http://localhost:11434/api/generate";
    [SerializeField] private string modelName = "llama3.1:8b";
    [SerializeField] private int requestTimeoutSeconds = 30;

    private enum DeskTab
    {
        Briefing,
        Intercept,
        Decision,
        MissionLog
    }

    private static readonly string[] InterceptBlockedLabels =
    {
        "friendly",
        "enemy",
        "hostile",
        "deception",
        "deceptive"
    };

    private static readonly string[] RealWorldReferences =
    {
        "russia",
        "ukraine",
        "nato",
        "china",
        "israel",
        "gaza",
        "hamas",
        "iran",
        "syria",
        "united states",
        "america"
    };

    private readonly MissionState missionState = new();

    private GameObject briefingPanel;
    private GameObject interceptPanel;
    private GameObject decisionPanel;
    private GameObject logPanel;
    private Text briefingText;
    private Text statsText;
    private Text statusText;
    private Text transmissionText;
    private Text decisionHelperText;
    private Text missionLogText;
    private Button generateButton;
    private Button[] tabButtons;
    private Button[] replyButtons;
    private GeneratedReplyOption[] currentReplyOptions = Array.Empty<GeneratedReplyOption>();
    private CancellationTokenSource requestCancellation;
    private DeskTab activeTab;
    private string missionLog = string.Empty;

    private void Awake()
    {
        BuildInterface();
        StartMission();
    }

    private void OnDestroy()
    {
        requestCancellation?.Cancel();
        requestCancellation?.Dispose();
    }

    private void StartMission()
    {
        briefingText.text = InterceptPromptBuilder.BuildMissionBriefingText();
        transmissionText.text = "Awaiting intercept. Command has requested certainty, preferably before evidence.";
        decisionHelperText.text = "Generate an intercept first. Then choose one reply and let the paperwork discover consequences.";
        missionLog = "Mission desk online. The supervisor has not sighed yet, but the day is young.";
        missionLogText.text = missionLog;
        statusText.text = $"Ollama required: {ollamaEndpoint}";
        generateButton.interactable = true;
        SetReplyButtonsEnabled(false);
        ShowTab(DeskTab.Briefing);
        RefreshStats();
    }

    private async void GenerateIntercept()
    {
        BeginRequest();
        InterceptClassification pendingHiddenTruth = ChooseHiddenTruth();
        int pendingRoundNumber = missionState.RoundNumber + 1;
        GeneratedReplyOption[] pendingOptions = BuildReplyOptions(pendingHiddenTruth);

        SetReplyButtonsEnabled(false);
        generateButton.interactable = false;
        transmissionText.text = "Listening for transmission...";
        decisionHelperText.text = "Reply options are being drafted by the department that once renamed a mistake as Phase Two.";
        statusText.text = "Contacting local Ollama model...";
        ShowTab(DeskTab.Intercept);

        try
        {
            var client = new OllamaClient(ollamaEndpoint, modelName, requestTimeoutSeconds);
            string prompt = InterceptPromptBuilder.BuildInterceptAndRepliesPrompt(
                pendingHiddenTruth,
                pendingOptions,
                pendingRoundNumber,
                missionState.RiskLevel,
                missionState.SupervisorPatience);
            string retryPrompt = InterceptPromptBuilder.BuildInterceptAndRepliesRetryPrompt(
                pendingHiddenTruth,
                pendingOptions,
                pendingRoundNumber,
                missionState.RiskLevel,
                missionState.SupervisorPatience);

            GeneratedInterceptPackage package = await RequestValidPackage(client, prompt, retryPrompt, pendingOptions, requestCancellation.Token);

            missionState.StartNextRound(pendingHiddenTruth);
            missionState.SetCurrentIntercept(package.Intercept);
            currentReplyOptions = package.Options;
            transmissionText.text = package.Intercept;
            ApplyReplyOptions(package.Options);
            statusText.text = "Intercept received. Pick the least career-ending reply.";
            SetReplyButtonsEnabled(true);
            ShowTab(DeskTab.Decision);
            RefreshStats();
        }
        catch (OperationCanceledException)
        {
            statusText.text = "Ollama request cancelled.";
            generateButton.interactable = true;
        }
        catch (GeneratedTextValidationException exception)
        {
            statusText.text = "Ollama responded, but the intercept package failed validation. Try generating again.\n" + exception.Message;
            transmissionText.text = "No valid intercept available.";
            Debug.LogWarning(exception.Message);
            generateButton.interactable = true;
        }
        catch (Exception exception)
        {
            statusText.text = "Ollama is not responding. Start Ollama and install/select the configured model.\n" + exception.Message;
            transmissionText.text = "No live intercept available.";
            generateButton.interactable = true;
        }
    }

    private async void SelectReply(int index)
    {
        if (index < 0 || index >= currentReplyOptions.Length)
        {
            statusText.text = "That reply option has not been generated yet.";
            return;
        }

        GeneratedReplyOption selectedReply = currentReplyOptions[index];
        BeginRequest();
        SetReplyButtonsEnabled(false);
        generateButton.interactable = false;
        statusText.text = "Sending reply and waiting for the supervisor to convert it into a lesson...";

        DecisionResult result = missionState.ResolveReply(selectedReply);
        RefreshStats();
        ShowTab(DeskTab.MissionLog);

        try
        {
            var client = new OllamaClient(ollamaEndpoint, modelName, requestTimeoutSeconds);
            string outcomePrompt = InterceptPromptBuilder.BuildOutcomePrompt(
                missionState.CurrentIntercept,
                selectedReply,
                result);
            string retryPrompt = InterceptPromptBuilder.BuildOutcomeRetryPrompt(outcomePrompt);

            string outcome = await RequestValidText(client, outcomePrompt, retryPrompt, ValidateOutcome, requestCancellation.Token);
            AppendMissionLog(BuildDecisionSummary(selectedReply, result, outcome));
            statusText.text = "Reply logged. Command will misunderstand it shortly.";
        }
        catch (OperationCanceledException)
        {
            statusText.text = "Outcome request cancelled.";
        }
        catch (GeneratedTextValidationException exception)
        {
            AppendMissionLog(BuildDecisionSummary(selectedReply, result, "The supervisor writes, \"Even the summary refused to be associated with this.\""));
            statusText.text = "Ollama responded, but the outcome failed validation. You can continue.\n" + exception.Message;
            Debug.LogWarning(exception.Message);
        }
        catch (Exception exception)
        {
            AppendMissionLog(BuildDecisionSummary(selectedReply, result, "The supervisor writes, \"No outcome returned. We will treat this silence as policy.\""));
            statusText.text = "Ollama is not responding. Start Ollama and install/select the configured model.\n" + exception.Message;
        }
        finally
        {
            generateButton.GetComponentInChildren<Text>().text = "Next Intercept";
            generateButton.interactable = true;
        }
    }

    private void BeginRequest()
    {
        requestCancellation?.Cancel();
        requestCancellation?.Dispose();
        requestCancellation = new CancellationTokenSource();
    }

    private async Task<GeneratedInterceptPackage> RequestValidPackage(
        OllamaClient client,
        string prompt,
        string retryPrompt,
        GeneratedReplyOption[] replyProfiles,
        CancellationToken cancellationToken)
    {
        try
        {
            return await SendAndParsePackage(client, prompt, replyProfiles, cancellationToken);
        }
        catch (GeneratedTextValidationException firstFailure)
        {
            Debug.LogWarning("Ollama package failed validation. Retrying once with stricter prompt.\n" + firstFailure.Message);
            return await SendAndParsePackage(client, retryPrompt, replyProfiles, cancellationToken);
        }
    }

    private async Task<GeneratedInterceptPackage> SendAndParsePackage(
        OllamaClient client,
        string prompt,
        GeneratedReplyOption[] replyProfiles,
        CancellationToken cancellationToken)
    {
        Debug.Log($"Sending Ollama query to {modelName} at {ollamaEndpoint}:\n{prompt}");

        string rawResponse = await client.GenerateAsync(prompt, cancellationToken);
        string cleanedResponse = CleanResponse(rawResponse);
        GeneratedInterceptPackage package = ParsePackage(cleanedResponse, replyProfiles);
        ValidateIntercept(package.Intercept);
        foreach (GeneratedReplyOption option in package.Options)
        {
            ValidateReplyOption(option.Text);
        }

        Debug.Log($"Ollama response received:\n{cleanedResponse}");
        return package;
    }

    private async Task<string> RequestValidText(
        OllamaClient client,
        string prompt,
        string retryPrompt,
        Action<string> validator,
        CancellationToken cancellationToken)
    {
        try
        {
            return await SendAndValidate(client, prompt, validator, cancellationToken);
        }
        catch (GeneratedTextValidationException firstFailure)
        {
            Debug.LogWarning("Ollama response failed validation. Retrying once with stricter prompt.\n" + firstFailure.Message);
            return await SendAndValidate(client, retryPrompt, validator, cancellationToken);
        }
    }

    private async Task<string> SendAndValidate(OllamaClient client, string prompt, Action<string> validator, CancellationToken cancellationToken)
    {
        Debug.Log($"Sending Ollama query to {modelName} at {ollamaEndpoint}:\n{prompt}");

        string rawResponse = await client.GenerateAsync(prompt, cancellationToken);
        string cleanedResponse = CleanResponse(rawResponse);
        validator(cleanedResponse);

        Debug.Log($"Ollama response received:\n{cleanedResponse}");
        return cleanedResponse;
    }

    private void ApplyReplyOptions(GeneratedReplyOption[] options)
    {
        for (int i = 0; i < replyButtons.Length; i++)
        {
            Text label = replyButtons[i].GetComponentInChildren<Text>();
            label.text = i < options.Length ? options[i].Text : "No reply generated.";
        }

        decisionHelperText.text = "Choose one reply. The hidden meaning still matters, but now it has the decency to sound like a person wrote it.";
    }

    private void RefreshStats()
    {
        statsText.text = $"Round: {missionState.RoundNumber}    Correct: {missionState.CorrectDecisions}    Risk: {missionState.RiskLevel}    Model: {modelName}";
    }

    private void AppendMissionLog(string entry)
    {
        missionLog = string.IsNullOrWhiteSpace(missionLog) ? entry : missionLog + "\n\n" + entry;
        missionLogText.text = missionLog;
    }

    private string BuildDecisionSummary(GeneratedReplyOption selectedReply, DecisionResult result, string outcome)
    {
        string assessment = result.WasCorrect ? "The reply somehow helped" : "The reply made the corridor more interesting";
        return $"Round {missionState.RoundNumber}: {assessment}.\nYou chose: \"{selectedReply.Text}\"\n{outcome}";
    }

    private void SetReplyButtonsEnabled(bool enabled)
    {
        if (replyButtons == null)
        {
            return;
        }

        foreach (Button button in replyButtons)
        {
            button.interactable = enabled;
        }
    }

    private void ShowTab(DeskTab tab)
    {
        activeTab = tab;
        briefingPanel.SetActive(tab == DeskTab.Briefing);
        interceptPanel.SetActive(tab == DeskTab.Intercept);
        decisionPanel.SetActive(tab == DeskTab.Decision);
        logPanel.SetActive(tab == DeskTab.MissionLog);
        UpdateTabStates();
    }

    private void UpdateTabStates()
    {
        if (tabButtons == null)
        {
            return;
        }

        string selected = activeTab.ToString();
        foreach (Button button in tabButtons)
        {
            bool isSelected = button.name.EndsWith(selected, StringComparison.Ordinal);
            button.GetComponent<Image>().color = isSelected ? new Color(0.32f, 0.44f, 0.34f, 1f) : new Color(0.12f, 0.16f, 0.16f, 1f);
        }
    }

    private static GeneratedReplyOption[] BuildReplyOptions(InterceptClassification hiddenTruth)
    {
        var options = new List<GeneratedReplyOption>
        {
            CreateStrongOption(hiddenTruth),
            CreateBadLoudOption(NextWrongClassification(hiddenTruth)),
            CreateBadWeirdOption(SecondWrongClassification(hiddenTruth))
        };

        for (int i = 0; i < options.Count; i++)
        {
            int swapIndex = UnityEngine.Random.Range(i, options.Count);
            (options[i], options[swapIndex]) = (options[swapIndex], options[i]);
        }

        return options.ToArray();
    }

    private static GeneratedReplyOption CreateStrongOption(InterceptClassification classification)
    {
        ResponseAction action = classification switch
        {
            InterceptClassification.Friendly => ResponseAction.Respond,
            InterceptClassification.Enemy => ResponseAction.Escalate,
            InterceptClassification.Deception => ResponseAction.Ignore,
            _ => ResponseAction.Ignore
        };

        string brief = classification switch
        {
            InterceptClassification.Friendly => "a useful but sarcastic reply that protects the extraction without sounding heroic",
            InterceptClassification.Enemy => "a sharp reply that flags danger while making fun of Command's love of urgent stamps",
            InterceptClassification.Deception => "a calm reply that refuses the bait and jokes about not feeding the paperwork machine",
            _ => "a cautious reply that buys time without pretending the nonsense is clear"
        };

        return new GeneratedReplyOption(classification, action, -1, 1, 1, -1, brief);
    }

    private static GeneratedReplyOption CreateBadLoudOption(InterceptClassification classification)
    {
        return new GeneratedReplyOption(
            classification,
            ResponseAction.Escalate,
            -1,
            3,
            0,
            -2,
            "an overconfident reply that turns incomplete information into a loud command decision with a dry joke");
    }

    private static GeneratedReplyOption CreateBadWeirdOption(InterceptClassification classification)
    {
        return new GeneratedReplyOption(
            classification,
            UnityEngine.Random.value > 0.5f ? ResponseAction.Ignore : ResponseAction.Respond,
            0,
            2,
            0,
            -1,
            "a specific, funny reply that technically does something but clearly solves the wrong problem");
    }

    private static InterceptClassification NextWrongClassification(InterceptClassification hiddenTruth)
    {
        return hiddenTruth switch
        {
            InterceptClassification.Friendly => InterceptClassification.Enemy,
            InterceptClassification.Enemy => InterceptClassification.Deception,
            _ => InterceptClassification.Friendly
        };
    }

    private static InterceptClassification SecondWrongClassification(InterceptClassification hiddenTruth)
    {
        return hiddenTruth switch
        {
            InterceptClassification.Friendly => InterceptClassification.Deception,
            InterceptClassification.Enemy => InterceptClassification.Friendly,
            _ => InterceptClassification.Enemy
        };
    }

    private static InterceptClassification ChooseHiddenTruth()
    {
        int roll = UnityEngine.Random.Range(0, 3);
        return roll switch
        {
            0 => InterceptClassification.Friendly,
            1 => InterceptClassification.Enemy,
            _ => InterceptClassification.Deception
        };
    }

    private static GeneratedInterceptPackage ParsePackage(string response, GeneratedReplyOption[] replyProfiles)
    {
        string intercept = string.Empty;
        string[] optionTexts = new string[3];

        string[] lines = response.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (string rawLine in lines)
        {
            string line = rawLine.Trim();
            if (TryReadValue(line, "INTERCEPT:", out string interceptValue))
            {
                intercept = interceptValue;
            }
            else if (TryReadValue(line, "OPTION_1:", out string option1))
            {
                optionTexts[0] = option1;
            }
            else if (TryReadValue(line, "OPTION_2:", out string option2))
            {
                optionTexts[1] = option2;
            }
            else if (TryReadValue(line, "OPTION_3:", out string option3))
            {
                optionTexts[2] = option3;
            }
        }

        if (string.IsNullOrWhiteSpace(intercept) || optionTexts.Any(string.IsNullOrWhiteSpace))
        {
            throw new GeneratedTextValidationException("Ollama did not return INTERCEPT plus all three OPTION lines.");
        }

        GeneratedReplyOption[] options = new GeneratedReplyOption[3];
        for (int i = 0; i < options.Length; i++)
        {
            options[i] = replyProfiles[i];
            options[i].SetText(CleanResponse(optionTexts[i]));
        }

        return new GeneratedInterceptPackage(CleanResponse(intercept), options);
    }

    private static bool TryReadValue(string line, string prefix, out string value)
    {
        if (line.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            value = line.Substring(prefix.Length).Trim();
            return true;
        }

        value = string.Empty;
        return false;
    }

    private static string CleanResponse(string response)
    {
        return (response ?? string.Empty).Trim().Trim('"', '\'', '`');
    }

    private static void ValidateIntercept(string response)
    {
        ValidateCommonResponse(response);
        string lowerResponse = response.ToLowerInvariant();
        if (InterceptBlockedLabels.Any(label => lowerResponse.Contains(label)))
        {
            throw new GeneratedTextValidationException("Ollama response revealed a blocked classification label.");
        }
    }

    private static void ValidateReplyOption(string response)
    {
        ValidateCommonResponse(response);
        if (response.Length < 12)
        {
            throw new GeneratedTextValidationException("A reply option was too short to be useful.");
        }
    }

    private static void ValidateOutcome(string response)
    {
        ValidateCommonResponse(response);
    }

    private static void ValidateCommonResponse(string response)
    {
        if (string.IsNullOrWhiteSpace(response))
        {
            throw new GeneratedTextValidationException("Ollama returned an empty response.");
        }

        string lowerResponse = response.ToLowerInvariant();
        if (RealWorldReferences.Any(reference => lowerResponse.Contains(reference)))
        {
            throw new GeneratedTextValidationException("Ollama response included a real-world reference.");
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
        Image background = CreateImage("Background", canvas.transform, new Color(0.045f, 0.052f, 0.055f, 1f));
        StretchToParent(background.rectTransform, 0f, 0f, 0f, 0f);

        Text title = CreateText("Title", canvas.transform, font, "Signal Intercept", 34, FontStyle.Bold, TextAnchor.UpperLeft);
        title.color = new Color(0.88f, 0.96f, 0.84f, 1f);
        AnchorTop(title.rectTransform, 34f, 18f, 690f, 44f);

        statsText = CreateText("Stats", canvas.transform, font, string.Empty, 14, FontStyle.Normal, TextAnchor.UpperRight);
        statsText.color = new Color(0.71f, 0.8f, 0.72f, 1f);
        AnchorTop(statsText.rectTransform, 390f, 25f, 34f, 28f);

        BuildTabs(canvas.transform, font);

        briefingPanel = CreatePanel("Briefing Panel", canvas.transform);
        briefingText = CreateText("Briefing Text", briefingPanel.transform, font, string.Empty, 21, FontStyle.Normal, TextAnchor.UpperLeft);
        briefingText.color = new Color(0.83f, 0.91f, 0.83f, 1f);
        StretchToParent(briefingText.rectTransform, 28f, 28f, 28f, 28f);

        interceptPanel = CreatePanel("Intercept Panel", canvas.transform);
        transmissionText = CreateText("Transmission Text", interceptPanel.transform, font, string.Empty, 30, FontStyle.Normal, TextAnchor.MiddleLeft);
        transmissionText.color = new Color(0.88f, 1f, 0.88f, 1f);
        StretchToParent(transmissionText.rectTransform, 32f, 32f, 32f, 32f);

        decisionPanel = CreatePanel("Decision Panel", canvas.transform);
        BuildReplyControls(decisionPanel.transform, font);

        logPanel = CreatePanel("Mission Log Panel", canvas.transform);
        missionLogText = CreateText("Mission Log Text", logPanel.transform, font, string.Empty, 18, FontStyle.Normal, TextAnchor.UpperLeft);
        missionLogText.color = new Color(0.83f, 0.89f, 0.86f, 1f);
        StretchToParent(missionLogText.rectTransform, 28f, 28f, 28f, 28f);

        statusText = CreateText("Status Text", canvas.transform, font, string.Empty, 16, FontStyle.Normal, TextAnchor.UpperLeft);
        statusText.color = new Color(0.94f, 0.82f, 0.58f, 1f);
        AnchorBottom(statusText.rectTransform, 34f, 24f, 430f, 50f);

        generateButton = CreateButton("Generate Intercept Button", canvas.transform, font, "Generate Intercept", 18);
        AnchorBottom(generateButton.GetComponent<RectTransform>(), 1080f, 24f, 34f, 50f);
        generateButton.onClick.AddListener(GenerateIntercept);
    }

    private void BuildTabs(Transform parent, Font font)
    {
        tabButtons = new[]
        {
            CreateTabButton(parent, font, "Briefing", 34f, DeskTab.Briefing),
            CreateTabButton(parent, font, "Intercept", 174f, DeskTab.Intercept),
            CreateTabButton(parent, font, "Replies", 314f, DeskTab.Decision),
            CreateTabButton(parent, font, "Mission Log", 454f, DeskTab.MissionLog)
        };
    }

    private Button CreateTabButton(Transform parent, Font font, string label, float left, DeskTab tab)
    {
        Button button = CreateButton("Tab " + tab, parent, font, label, 16);
        RectTransform rectTransform = button.GetComponent<RectTransform>();
        rectTransform.anchorMin = Vector2.up;
        rectTransform.anchorMax = Vector2.up;
        rectTransform.offsetMin = new Vector2(left, -112f);
        rectTransform.offsetMax = new Vector2(left + 128f, -78f);
        button.onClick.AddListener(() => ShowTab(tab));
        return button;
    }

    private void BuildReplyControls(Transform parent, Font font)
    {
        Text heading = CreateText("Reply Heading", parent, font, "Pick A Reply", 27, FontStyle.Bold, TextAnchor.UpperLeft);
        heading.color = new Color(0.86f, 0.96f, 0.84f, 1f);
        AnchorTop(heading.rectTransform, 30f, 26f, 30f, 36f);

        decisionHelperText = CreateText("Reply Helper", parent, font, string.Empty, 17, FontStyle.Normal, TextAnchor.UpperLeft);
        decisionHelperText.color = new Color(0.72f, 0.8f, 0.76f, 1f);
        AnchorTop(decisionHelperText.rectTransform, 30f, 72f, 30f, 48f);

        replyButtons = new[]
        {
            CreateReplyButton(parent, font, 0, 138f),
            CreateReplyButton(parent, font, 1, 246f),
            CreateReplyButton(parent, font, 2, 354f)
        };
    }

    private Button CreateReplyButton(Transform parent, Font font, int index, float top)
    {
        Button button = CreateButton("Reply Option " + (index + 1), parent, font, "Reply option pending.", 18);
        RectTransform rectTransform = button.GetComponent<RectTransform>();
        AnchorTop(rectTransform, 30f, top, 30f, 82f);
        int capturedIndex = index;
        button.onClick.AddListener(() => SelectReply(capturedIndex));
        return button;
    }

    private static GameObject CreatePanel(string name, Transform parent)
    {
        Image panel = CreateImage(name, parent, new Color(0.075f, 0.09f, 0.09f, 1f));
        StretchToParent(panel.rectTransform, 34f, 126f, 34f, 96f);
        return panel.gameObject;
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

    private static Button CreateButton(string name, Transform parent, Font font, string label, int fontSize)
    {
        Image buttonImage = CreateImage(name, parent, new Color(0.17f, 0.23f, 0.22f, 1f));
        var button = buttonImage.gameObject.AddComponent<Button>();
        button.targetGraphic = buttonImage;

        Text buttonText = CreateText("Label", button.transform, font, label, fontSize, FontStyle.Bold, TextAnchor.MiddleCenter);
        buttonText.color = Color.white;
        StretchToParent(buttonText.rectTransform, 10f, 8f, 10f, 8f);

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

    private sealed class GeneratedInterceptPackage
    {
        public GeneratedInterceptPackage(string intercept, GeneratedReplyOption[] options)
        {
            Intercept = intercept;
            Options = options;
        }

        public string Intercept { get; }
        public GeneratedReplyOption[] Options { get; }
    }

    private sealed class GeneratedTextValidationException : Exception
    {
        public GeneratedTextValidationException(string message) : base(message)
        {
        }
    }
}
