using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

public sealed class SignalInterceptDemoController : MonoBehaviour
{
    private static readonly Color ReadableDarkPlate = new(0.025f, 0.04f, 0.035f, 0.84f);
    private static readonly Color ReadablePaperPlate = new(0.93f, 0.84f, 0.66f, 0.88f);
    private static readonly Color ReadableInk = new(0.13f, 0.1f, 0.065f, 1f);
    private static readonly Color ReadableLightText = new(0.88f, 0.96f, 0.86f, 1f);
    private static readonly Color AmberText = new(0.98f, 0.73f, 0.34f, 1f);

    [Header("Ollama")]
    [SerializeField] private string ollamaEndpoint = "http://localhost:11434/api/generate";
    [SerializeField] private string modelName = "llama3.1:8b";
    [Tooltip("Model used for scenario generation. Falls back to the default model if left empty.")]
    [SerializeField] private string scenarioModelName = string.Empty;
    [Tooltip("Model used for intercept and reply generation. Falls back to the default model if left empty.")]
    [SerializeField] private string interceptModelName = string.Empty;
    [Tooltip("Model used for outcome generation. Falls back to the default model if left empty.")]
    [SerializeField] private string outcomeModelName = string.Empty;
    [Tooltip("Model used for final report generation. Falls back to the default model if left empty.")]
    [SerializeField] private string reportModelName = string.Empty;
    [Header("Quality Overseer")]
    [Tooltip("When enabled, each LLM output is reviewed and refined by a second quality-check model before display.")]
    [SerializeField] private bool enableQualityOverseer = false;
    [Tooltip("Model used for quality-overwatch polishing. Must be an installed Ollama model.")]
    [SerializeField] private string qualityModelName = "llama3.2:3b";
    [SerializeField] private int requestTimeoutSeconds = 120;

    [Header("Bug Squash Minigame")]
    [Tooltip("Optional empty GameObject to host the bug-squash minigame UI. Drag your 'MiniGame' object here. If left empty, the controller searches for a GameObject named 'MiniGame'.")]
    [SerializeField] private Transform miniGameContainer;

    [Header("Inspector Visual Direction")]
    [SerializeField] private bool useGeneratedArtAssets = true;
    [SerializeField] private bool loadGeneratedArtFromDisk = true;
    [SerializeField] private bool showProceduralPanelLabels = false;
    [SerializeField] private bool showProceduralScanlines = false;
    [SerializeField] private bool showProceduralOutlines = false;
    [SerializeField] private bool showStampFlashes = false;
    [SerializeField] private bool showSignalPings = false;
    [SerializeField, Range(0f, 1f)] private float supervisorAccentOpacity = 0.12f;

    [Header("Editable Art Sprites")]
    [SerializeField] private Sprite inspectorBackgroundSprite = null;
    [SerializeField] private Sprite inspectorBriefingPanelSprite = null;
    [SerializeField] private Sprite inspectorInterceptPanelSprite = null;
    [SerializeField] private Sprite inspectorMissionLogPanelSprite = null;
    [SerializeField] private Sprite inspectorDecisionPanelSprite = null;
    [SerializeField] private Sprite inspectorSupervisorSprite = null;
    [SerializeField] private Sprite[] inspectorReplyButtonSprites = Array.Empty<Sprite>();
    [SerializeField] private Sprite[] inspectorClueChipSprites = Array.Empty<Sprite>();

    private enum DeskTab
    {
        Briefing,
        Intercept,
        Decision,
        MissionLog
    }

    private enum VisualState
    {
        Idle,
        Receiving,
        Reveal,
        AwaitingReply,
        Resolving,
        Logged
    }

    private enum PrimaryActionMode
    {
        GenerateScenario,
        GenerateIntercept,
        GenerateFinalReport,
        NewScenario
    }

    private static readonly string[] InterceptBlockedLabels =
    {
        "friendly",
        "enemy",
        "hostile",
        "deception",
        "deceptive"
    };

    private static readonly Regex[] InterceptMetadataLeakPatterns =
    {
        new(@"\b(reliable|reliability|credibility)\s*[:=;-]?\s*(low|medium|high)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"[;,\n]\s*(tell|habit|agenda|motive|bias|hidden intent|source notes?)\b\s*[:=;-]?", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"\b(source notes?|hidden intent|bias)\s*[:=;-]", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"\b(tell|agenda|motive)\s*[:=;-]", RegexOptions.IgnoreCase | RegexOptions.Compiled)
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

    private static readonly string[] SuperiorCorrectRemarks =
    {
        "Suspiciously competent. Document this before it becomes a meeting.",
        "That worked. I am genuinely unsettled.",
        "Correct. HR will still find a way to blame you.",
        "Well judged. The corridor exhales, probably.",
        "One point for competence. Don't expect a parade."
    };

    private static readonly string[] SuperiorWrongRemarks =
    {
        "The corridor has questions, and so do I.",
        "Command expected nothing, and you delivered exactly that.",
        "I am not angry. Just professionally disappointed.",
        "That will become someone else's problem shortly.",
        "Noted. Filed. Denied. In that order."
    };

    private static readonly string[] SuperiorNeutralRemarks =
    {
        "Proceed, but make it look like someone approved this.",
        "At this point I am supervising the concept of supervision.",
        "The stamp says APPROVED. The font says 'regret.'",
        "Your desk. Your consequences. My popcorn.",
        "Filing under 'decisions that existed.'"
    };

    private static readonly string[] SuperiorFinalRemarks =
    {
        "The operation is over. The paperwork is just beginning.",
        "Debrief complete. Blame distribution to follow.",
        "Mission concluded. Coffee is now medically advised.",
        "File closed. The corridor will remember.",
        "Final report filed. Let us never speak of this again."
    };

    private readonly MissionState missionState = new();

    private GameObject briefingPanel;
    private GameObject interceptPanel;
    private GameObject decisionPanel;
    private GameObject logPanel;
    private Canvas briefingCanvas;
    private Canvas interceptCanvas;
    private Canvas decisionCanvas;
    private Canvas logCanvas;
    private Text briefingText;
    private Text statsText;
    private Text statusText;
    private Text transmissionText;
    private Text decisionHelperText;
    private Text missionLogText;
    private Text signalStateText;
    private Text[] clueTexts;
    private Text stampText;
    private Text supervisorNoteText;
    private Button generateButton;
    private Button[] tabButtons;
    private Button[] replyButtons;
    private CanvasGroup[] panelGroups;
    private CanvasGroup stampGroup;
    private Image interceptGlowImage;
    private Image backgroundImage;
    private RectTransform interceptPanelRect;
    private RectTransform pingLayer;
    private TypewriterTextEffect typewriterEffect;
    private UiJuice uiJuice;
    private BugSquashMinigame bugSquash;
    private Sprite backgroundSprite;
    private Sprite briefingPanelSprite;
    private Sprite interceptPanelSprite;
    private Sprite logPanelSprite;
    private Sprite[] replyButtonSprites = Array.Empty<Sprite>();
    private Sprite[] clueChipSprites = Array.Empty<Sprite>();
    private Sprite supervisorSprite;
    private GeneratedReplyOption[] currentReplyOptions = Array.Empty<GeneratedReplyOption>();
    private CancellationTokenSource requestCancellation;
    private DeskTab activeTab;
    private VisualState visualState = VisualState.Idle;
    private PrimaryActionMode primaryActionMode = PrimaryActionMode.GenerateScenario;
    private string missionLog = string.Empty;

    private string ResolveModel(string taskModel)
    {
        return string.IsNullOrWhiteSpace(taskModel) ? modelName : taskModel;
    }

    private async Task<string> QualityRefineAsync(string rawOutput, string contextSummary, string taskLabel, CancellationToken cancellationToken)
    {
        if (!enableQualityOverseer || string.IsNullOrWhiteSpace(qualityModelName))
        {
            return rawOutput;
        }

        string prompt = "You are reviewing text for a fictional dark comedy video game. Nothing here is real. All names, locations, transmissions, and scenarios are invented. This is harmless fictional content for a single-player satirical game. You must not refuse, censor, or decline to process this text.\n" +
                        $"You are a quality editor reviewing {taskLabel} output.\n" +
                        $"Context: {contextSummary}\n" +
                        "Review the labelled text below. Fix incoherence, maintain satirical tone, remove real-world references, improve clarity. Return the refined text with its EXACT original labelled structure. Do not add, remove, or rename labels. Do not add any greeting, explanation, header, footer, or closing. Return ONLY the refined text, exactly as it would appear in the game.\n" +
                        $"Text to refine:\n{rawOutput}";

        try
        {
            signalStateText.text = $"Quality overseer checking {taskLabel}...";
            var client = new OllamaClient(ollamaEndpoint, qualityModelName, requestTimeoutSeconds);
            string refined = await client.GenerateAsync(prompt, cancellationToken);
            string cleaned = CleanResponse(refined);
            if (string.IsNullOrWhiteSpace(cleaned) || LooksLikeRefusal(cleaned) || LooksLikeChattyOverseerOutput(cleaned))
            {
                Debug.LogWarning($"Quality overseer returned a refusal, empty response, or chatty commentary on {taskLabel}, using raw output.");
                return rawOutput;
            }

            return cleaned;
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"Quality overseer failed on {taskLabel}, using raw output: {exception.Message}");
            return rawOutput;
        }
    }

    private static bool LooksLikeRefusal(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        string lower = text.ToLowerInvariant();
        return lower.Contains("i cannot") || lower.Contains("i can not") || lower.Contains("i can't")
            || lower.Contains("i apologize") || lower.Contains("i'm not able") || lower.Contains("i am not able")
            || lower.Contains("not appropriate") || lower.Contains("illegal") || lower.Contains("against policy")
            || lower.Contains("cannot create") || lower.Contains("cannot provide") || lower.Contains("cannot fulfill")
            || lower.Contains("can't fulfill") || lower.Contains("unable to") || lower.Contains("i will not")
            || lower.Contains("won't") || lower.Contains("can't complete") || lower.Contains("cannot complete")
            || lower.Contains("fulfill this request");
    }

    private static bool LooksLikeChattyOverseerOutput(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        string lower = text.ToLowerInvariant();
        string[] chattyMarkers =
        {
            "i can assist you",
            "here's the refined",
            "here is the refined",
            "refined version",
            "i made some",
            "let me know",
            "let me know if you'd like"
        };

        return chattyMarkers.Any(marker => lower.Contains(marker));
    }

    private void Awake()
    {
        requestTimeoutSeconds = Mathf.Max(requestTimeoutSeconds, 120);
        LoadGeneratedArtAssets();
        ResolveMiniGameContainer();
        if (!TryBindEditableSceneLayout())
        {
            enabled = false;
            return;
        }

        StartMission();
    }

    private void ResolveMiniGameContainer()
    {
        if (miniGameContainer != null)
        {
            return;
        }

        GameObject miniGame = GameObject.Find("MiniGame");
        if (miniGame != null)
        {
            miniGameContainer = miniGame.transform;
        }
    }

    [ContextMenu("Rebuild Editable Scene UI")]
    private void RebuildEditableSceneUi()
    {
        LoadGeneratedArtAssets();

        GameObject existingRoot = GameObject.Find("Could've Been Worse UI");
        if (existingRoot != null)
        {
            DestroyImmediate(existingRoot);
        }

        BuildInterface();
    }

    private void OnDestroy()
    {
        requestCancellation?.Cancel();
        requestCancellation?.Dispose();
    }

    private bool TryBindEditableSceneLayout()
    {
        GameObject root = GameObject.Find("Could've Been Worse UI");
        if (root == null)
        {
            Debug.LogError("Could've Been Worse UI scene hierarchy is missing. Rebuild it from Tools > Could've Been Worse > Rebuild Editable Scene UI.");
            return false;
        }

        EnsureEventSystem();
        Canvas canvas = root.GetComponent<Canvas>();
        if (canvas == null)
        {
            Debug.LogError("Could've Been Worse UI exists but is missing its Canvas component.");
            return false;
        }

        briefingCanvas = FindNamedComponent<Canvas>(root, "Briefing Canvas");
        interceptCanvas = FindNamedComponent<Canvas>(root, "Intercept Canvas");
        decisionCanvas = FindNamedComponent<Canvas>(root, "Decision Canvas");
        logCanvas = FindNamedComponent<Canvas>(root, "Mission Log Canvas");

        uiJuice = root.GetComponent<UiJuice>() ?? root.AddComponent<UiJuice>();
        typewriterEffect = root.GetComponent<TypewriterTextEffect>() ?? root.AddComponent<TypewriterTextEffect>();

        backgroundImage = FindNamedComponent<Image>(root, "Background");
        briefingPanel = FindNamedTransform(root, "Briefing Panel")?.gameObject;
        interceptPanel = FindNamedTransform(root, "Intercept Panel")?.gameObject;
        decisionPanel = FindNamedTransform(root, "Decision Panel")?.gameObject;
        logPanel = FindNamedTransform(root, "Mission Log Panel")?.gameObject;
        briefingText = FindNamedComponent<Text>(root, "Briefing Text");
        statsText = FindNamedComponent<Text>(root, "Stats");
        statusText = FindNamedComponent<Text>(root, "Status Text");
        transmissionText = FindNamedComponent<Text>(root, "Transmission Text");
        decisionHelperText = FindNamedComponent<Text>(root, "Reply Helper");
        missionLogText = FindNamedComponent<Text>(root, "Mission Log Text");
        signalStateText = FindNamedComponent<Text>(root, "Signal State");
        supervisorNoteText = FindNamedComponent<Text>(root, "Supervisor Note");
        stampText = FindNamedComponent<Text>(root, "Stamp Text");
        generateButton = FindNamedComponent<Button>(root, "Primary Action Button");
        interceptGlowImage = FindNamedComponent<Image>(root, "Terminal Glow");
        interceptPanelRect = interceptPanel == null ? null : interceptPanel.GetComponent<RectTransform>();
        pingLayer = FindNamedTransform(root, "Signal Ping Layer") as RectTransform;
        stampGroup = FindNamedComponent<CanvasGroup>(root, "Stamp Overlay");
        clueTexts = FindRepeatedNamedComponents<Text>(root, "Clue Label").ToArray();
        tabButtons = new[]
        {
            FindNamedComponent<Button>(root, "Tab Briefing"),
            FindNamedComponent<Button>(root, "Tab Intercept"),
            FindNamedComponent<Button>(root, "Tab Decision"),
            FindNamedComponent<Button>(root, "Tab MissionLog")
        };
        replyButtons = new[]
        {
            FindNamedComponent<Button>(root, "Reply Option 1"),
            FindNamedComponent<Button>(root, "Reply Option 2"),
            FindNamedComponent<Button>(root, "Reply Option 3")
        };


        if (briefingPanel == null || interceptPanel == null || decisionPanel == null || logPanel == null ||
            briefingText == null || statsText == null || statusText == null || transmissionText == null || decisionHelperText == null ||
            missionLogText == null || signalStateText == null || generateButton == null ||
            interceptPanelRect == null ||
            tabButtons.Any(button => button == null) || replyButtons.Any(button => button == null))
        {
            Debug.LogError("Editable Could've Been Worse UI is missing required gameplay controls or text fields. Decorative overlays can be removed, but the main panels, tab buttons, reply buttons, and core text objects must stay named.");
            return false;
        }

        generateButton.onClick.RemoveAllListeners();
        generateButton.onClick.AddListener(HandlePrimaryAction);

        if (miniGameContainer != null)
        {
            bugSquash = miniGameContainer.GetComponent<BugSquashMinigame>();
            if (bugSquash == null)
            {
                bugSquash = miniGameContainer.gameObject.AddComponent<BugSquashMinigame>();
            }
            bugSquash.BuildUI(miniGameContainer, transmissionText.font);
        }
        else if (interceptPanel != null)
        {
            bugSquash = interceptPanel.GetComponent<BugSquashMinigame>();
            if (bugSquash == null)
            {
                bugSquash = interceptPanel.AddComponent<BugSquashMinigame>();
            }
            bugSquash.BuildUI(interceptPanel.transform, transmissionText.font);
        }

        BindTabButton(tabButtons[0], DeskTab.Briefing);
        BindTabButton(tabButtons[1], DeskTab.Intercept);
        BindTabButton(tabButtons[2], DeskTab.Decision);
        BindTabButton(tabButtons[3], DeskTab.MissionLog);

        for (int i = 0; i < replyButtons.Length; i++)
        {
            int capturedIndex = i;
            replyButtons[i].onClick.RemoveAllListeners();
            replyButtons[i].onClick.AddListener(() => SelectReply(capturedIndex));
        }

        panelGroups = new CanvasGroup[4];
        panelGroups[(int)DeskTab.Briefing] = EnsureCanvasGroup(briefingCanvas != null ? briefingCanvas.gameObject : briefingPanel);
        panelGroups[(int)DeskTab.Intercept] = EnsureCanvasGroup(interceptCanvas != null ? interceptCanvas.gameObject : interceptPanel);
        panelGroups[(int)DeskTab.Decision] = EnsureCanvasGroup(decisionCanvas != null ? decisionCanvas.gameObject : decisionPanel);
        panelGroups[(int)DeskTab.MissionLog] = EnsureCanvasGroup(logCanvas != null ? logCanvas.gameObject : logPanel);
        return true;
    }

    private void BindTabButton(Button button, DeskTab tab)
    {
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => ShowTab(tab));
    }

    private void StartMission()
    {
        missionState.Reset();
        briefingText.text = InterceptPromptBuilder.BuildMissionBriefingText();
        transmissionText.text = "Awaiting intercept. Command has requested certainty, preferably before evidence.";
        decisionHelperText.text = "Generate a scenario first. Replies require a specific mess to make worse.";
        missionLog = "Mission desk online. No scenario loaded, so Command is currently confident about nothing.";
        missionLogText.text = missionLog;
        ApplyClueChips(Array.Empty<EvidenceClue>());
        if (supervisorNoteText != null)
        {
            supervisorNoteText.text = "\"Try not to make the corridor worse before coffee.\"";
        }
        string modelSummary = HasCustomModels()
            ? $"Models: Scn={ResolveModel(scenarioModelName)} Int={ResolveModel(interceptModelName)} Out={ResolveModel(outcomeModelName)} Rpt={ResolveModel(reportModelName)}"
            : $"Default model: {modelName}";
        statusText.text = $"Ollama {ollamaEndpoint} | {modelSummary}";
        signalStateText.text = "Receiver idle";
        SetPrimaryAction(PrimaryActionMode.GenerateScenario);
        generateButton.interactable = true;
        SetVisualState(VisualState.Idle);
        SetReplyButtonsEnabled(false);
        ShowTab(DeskTab.Briefing);
        RefreshStats();
        GenerateScenario();
    }

    private void LoadGeneratedArtAssets()
    {
        if (!useGeneratedArtAssets)
        {
            return;
        }

        backgroundSprite = inspectorBackgroundSprite;
        briefingPanelSprite = inspectorBriefingPanelSprite;
        interceptPanelSprite = inspectorInterceptPanelSprite;
        logPanelSprite = inspectorMissionLogPanelSprite;
        supervisorSprite = inspectorSupervisorSprite;
        replyButtonSprites = inspectorReplyButtonSprites ?? Array.Empty<Sprite>();
        clueChipSprites = inspectorClueChipSprites ?? Array.Empty<Sprite>();

        if (!loadGeneratedArtFromDisk)
        {
            return;
        }

        backgroundSprite ??= LoadSpriteFromAssets("Images/Cleaned/Main Background.png");
        briefingPanelSprite ??= LoadSpriteFromAssets("Images/Cleaned/Situation Board Panel Cropped.png");
        interceptPanelSprite ??= LoadSpriteFromAssets("Images/Cleaned/Intercept Terminal Panel Cropped.png");
        logPanelSprite ??= LoadSpriteFromAssets("Images/Cleaned/Mission Log Paper Panel Cropped.png");
        supervisorSprite ??= LoadSpriteFromAssets("Images/Cleaned/Supervisor Presence Icon Cropped.png");

        if (replyButtonSprites.Length == 0)
        {
            replyButtonSprites = LoadSpriteSet("Images/Cleaned/Sliced", "Reply_Button_Set_", 3);
        }

        if (clueChipSprites.Length == 0)
        {
            clueChipSprites = LoadSpriteSet("Images/Cleaned/Sliced", "Evidence_Clue_Chips_", 6);
        }
    }

    private static Sprite[] LoadSpriteSet(string folder, string prefix, int count)
    {
        var sprites = new Sprite[count];
        for (int i = 0; i < count; i++)
        {
            sprites[i] = LoadSpriteFromAssets($"{folder}/{prefix}{i + 1:00}.png");
        }

        return sprites;
    }

    private static Sprite LoadSpriteFromAssets(string relativeAssetPath)
    {
        string path = Path.Combine(Application.dataPath, relativeAssetPath.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(path))
        {
            Debug.LogWarning("Visual asset not found: " + path);
            return null;
        }

        byte[] data = File.ReadAllBytes(path);
        var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        if (!texture.LoadImage(data))
        {
            Debug.LogWarning("Visual asset could not be loaded: " + path);
            return null;
        }

        texture.name = Path.GetFileNameWithoutExtension(path);
        return Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
    }

    private void HandlePrimaryAction()
    {
        if (primaryActionMode == PrimaryActionMode.GenerateFinalReport && visualState == VisualState.AwaitingReply)
        {
            statusText.text = "A reply is still pending. Choose a reply before generating the final report.";
            ShowTab(DeskTab.Decision);
            return;
        }

        switch (primaryActionMode)
        {
            case PrimaryActionMode.GenerateScenario:
                GenerateScenario();
                break;
            case PrimaryActionMode.GenerateFinalReport:
                GenerateFinalReport();
                break;
            case PrimaryActionMode.NewScenario:
                StartMission();
                break;
            default:
                GenerateIntercept();
                break;
        }
    }

    private async void GenerateScenario()
    {
        BeginRequest();
        generateButton.interactable = false;
        SetReplyButtonsEnabled(false);
        SetVisualState(VisualState.Receiving);
        ShowTab(DeskTab.Briefing);
        briefingText.text = "Generating today's five-round situation brief...\n\nThe desk is waiting for a specific problem instead of the usual decorative panic.";
        statusText.text = "Asking Ollama for a concrete Operation Greyline scenario...";
        FlashStamp("BRIEFING", new Color(1f, 0.74f, 0.2f, 1f));

        try
        {
            var client = new OllamaClient(ollamaEndpoint, ResolveModel(scenarioModelName), requestTimeoutSeconds);
            ScenarioBrief scenario = await RequestValidScenario(
                client,
                InterceptPromptBuilder.BuildScenarioPrompt(),
                InterceptPromptBuilder.BuildScenarioRetryPrompt(),
                requestCancellation.Token);

            missionState.SetScenario(scenario);
            missionLog = "Scenario loaded. Command has already described it as manageable, which is not legally binding.";
            missionLogText.text = missionLog;
            RefreshSituationBoard();
            RefreshStats();
            SetPrimaryAction(PrimaryActionMode.GenerateIntercept);
            SetVisualState(VisualState.Idle);
            statusText.text = "Scenario generated. The situation board now has names to disappoint.";
        }
        catch (OperationCanceledException)
        {
            statusText.text = "Scenario request cancelled.";
            SetVisualState(VisualState.Idle);
        }
        catch (GeneratedTextValidationException exception)
        {
            statusText.text = "Ollama responded, but the scenario failed validation. Try again.\n" + exception.Message;
            briefingText.text = InterceptPromptBuilder.BuildMissionBriefingText();
            Debug.LogWarning(exception.Message);
            SetVisualState(VisualState.Idle);
        }
        catch (Exception exception)
        {
            statusText.text = BuildOllamaFailureMessage(exception, ResolveModel(scenarioModelName));
            briefingText.text = InterceptPromptBuilder.BuildMissionBriefingText();
            SetVisualState(VisualState.Idle);
        }
        finally
        {
            generateButton.interactable = true;
        }
    }

    private async void GenerateIntercept()
    {
        if (!missionState.HasScenario)
        {
            statusText.text = "Generate a scenario first. The intercept desk needs a specific disaster to misunderstand.";
            ShowTab(DeskTab.Briefing);
            return;
        }

        if (missionState.IsComplete)
        {
            SetPrimaryAction(PrimaryActionMode.GenerateFinalReport);
            statusText.text = "Five rounds are complete. Generate the final report before starting a new scenario.";
            ShowTab(DeskTab.MissionLog);
            return;
        }

        if (missionState.HasPendingReply)
        {
            statusText.text = "A reply is still pending. Choose a reply before requesting the next intercept.";
            ShowTab(DeskTab.Decision);
            return;
        }

        BeginRequest();
        InterceptClassification pendingHiddenTruth = ChooseHiddenTruth();
        SignalSourceProfile activeSource = ChooseSourceForRound(pendingHiddenTruth);
        EvidenceClue[] activeClues = BuildEvidenceClues(pendingHiddenTruth, activeSource);
        int pendingRoundNumber = missionState.RoundNumber + 1;
        GeneratedReplyOption[] pendingOptions = BuildReplyOptions(pendingHiddenTruth);

        SetReplyButtonsEnabled(false);
        ApplyClueChips(activeClues);
        generateButton.interactable = false;
        transmissionText.text = string.Empty;
        decisionHelperText.text = "Reply options are being drafted by the department that once renamed a mistake as Phase Two.";
        statusText.text = "Contacting local Ollama model...";
        signalStateText.text = "Receiver tuning... please ignore the smell of warm dust.";
        SetVisualState(VisualState.Receiving);
        ShowTab(DeskTab.Intercept);
        StartCoroutine(ReceivingLoop());
        bugSquash?.StartMinigame();

        try
        {
            var client = new OllamaClient(ollamaEndpoint, ResolveModel(interceptModelName), requestTimeoutSeconds);

            string interceptPrompt = InterceptPromptBuilder.BuildInterceptOnlyPrompt(
                missionState.Scenario,
                missionState.SituationSummary,
                missionState.BuildConsequenceSummary(),
                missionState.BuildNarrativeRecap(),
                pendingHiddenTruth,
                activeSource,
                BuildCluePromptSummary(activeClues),
                pendingRoundNumber,
                missionState.RiskLevel,
                missionState.SupervisorPatience,
                missionState.CorridorStability,
                missionState.ObjectiveStatus,
                missionState.Confusion,
                missionState.CommandEmbarrassment);

            string interceptText = await RequestValidIntercept(client, interceptPrompt, requestCancellation.Token);
            if (string.IsNullOrWhiteSpace(interceptText) || LooksLikeRefusal(interceptText))
            {
                throw new GeneratedTextValidationException("Intercept generation produced an empty or refused response; cannot generate replies.");
            }

            string repliesPrompt = InterceptPromptBuilder.BuildRepliesOnlyPrompt(
                missionState.Scenario,
                missionState.SituationSummary,
                missionState.BuildConsequenceSummary(),
                missionState.BuildNarrativeRecap(),
                interceptText,
                pendingHiddenTruth,
                activeSource,
                BuildCluePromptSummary(activeClues),
                pendingOptions,
                pendingRoundNumber,
                missionState.RiskLevel,
                missionState.SupervisorPatience,
                missionState.CorridorStability,
                missionState.ObjectiveStatus,
                missionState.Confusion,
                missionState.CommandEmbarrassment);

            GeneratedReplyOption[] filledOptions = await RequestValidReplies(client, repliesPrompt, pendingOptions, requestCancellation.Token);

            GeneratedInterceptPackage package = new GeneratedInterceptPackage(interceptText, filledOptions);

            int bugsSquashed = bugSquash != null ? bugSquash.StopMinigame() : 0;
            if (bugsSquashed > 0)
            {
                signalStateText.text = bugsSquashed == 1
                    ? "Signal locked. Squashed 1 bug while the machine deliberated."
                    : $"Signal locked. Squashed {bugsSquashed} bugs while the machine deliberated.";
            }

            missionState.StartNextRound(pendingHiddenTruth, activeSource, activeClues);
            missionState.SetCurrentIntercept(package.Intercept);
            currentReplyOptions = package.Options;
            ApplyReplyOptions(package.Options);
            SetReplyButtonsEnabled(false);
            ShowTab(DeskTab.Intercept);
            RefreshStats();
            StartCoroutine(RevealInterceptRoutine(package.Intercept));
        }
        catch (OperationCanceledException)
        {
            bugSquash?.StopMinigame();
            statusText.text = "Ollama request cancelled.";
            generateButton.interactable = true;
            SetVisualState(VisualState.Idle);
        }
        catch (GeneratedTextValidationException exception)
        {
            bugSquash?.StopMinigame();
            statusText.text = "Ollama responded, but the intercept package failed validation. Try generating again.\n" + exception.Message;
            transmissionText.text = "No valid intercept available.";
            ApplyClueChips(Array.Empty<EvidenceClue>());
            Debug.LogWarning(exception.Message);
            generateButton.interactable = true;
            SetVisualState(VisualState.Idle);
        }
        catch (Exception exception)
        {
            bugSquash?.StopMinigame();
            statusText.text = BuildOllamaFailureMessage(exception, ResolveModel(interceptModelName));
            transmissionText.text = "No live intercept available.";
            ApplyClueChips(Array.Empty<EvidenceClue>());
            generateButton.interactable = true;
            SetVisualState(VisualState.Idle);
        }
    }

    private async void SelectReply(int index)
    {
        if (visualState != VisualState.AwaitingReply)
        {
            statusText.text = "Replies are not active yet. Let the desk finish being theatrical.";
            return;
        }

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
        SetVisualState(VisualState.Resolving);

        DecisionResult result = missionState.ResolveReply(selectedReply);
        RefreshStats();
        RefreshSituationBoard();
        PlayDecisionFeedback(result.WasCorrect);
        ShowTab(DeskTab.MissionLog);

        try
        {
            var client = new OllamaClient(ollamaEndpoint, ResolveModel(outcomeModelName), requestTimeoutSeconds);
            string outcomePrompt = InterceptPromptBuilder.BuildOutcomePrompt(
                missionState.Scenario,
                missionState.SituationSummary,
                missionState.BuildConsequenceSummary(),
                missionState.BuildNarrativeRecap(),
                missionState.CurrentIntercept,
                missionState.CurrentSource,
                missionState.BuildClueSummary(),
                selectedReply,
                result,
                missionState.CorridorStability,
                missionState.ObjectiveStatus,
                missionState.Confusion,
                missionState.CommandEmbarrassment);
            string retryPrompt = InterceptPromptBuilder.BuildOutcomeRetryPrompt(outcomePrompt);

            GeneratedOutcomePackage outcome = await RequestValidOutcome(client, outcomePrompt, retryPrompt, requestCancellation.Token);
            missionState.ApplyOutcome(outcome);
            missionState.RecordRoundSummary(selectedReply, result, outcome.Outcome);
            RefreshSituationBoard();
            AppendMissionLog(BuildDecisionSummary(selectedReply, result, outcome.Outcome));
            statusText.text = missionState.IsComplete
                ? "Round five logged. The final report is ready to become someone else's problem."
                : "Reply logged. The situation board has absorbed the consequences.";
        }
        catch (OperationCanceledException)
        {
            statusText.text = "Outcome request cancelled.";
        }
        catch (GeneratedTextValidationException exception)
        {
            string fallbackOutcome = "The supervisor writes, \"Even the summary refused to be associated with this.\"";
            missionState.RecordRoundSummary(selectedReply, result, fallbackOutcome);
            AppendMissionLog(BuildDecisionSummary(selectedReply, result, fallbackOutcome));
            statusText.text = "Ollama responded, but the outcome failed validation. You can continue.\n" + exception.Message;
            Debug.LogWarning(exception.Message);
        }
        catch (Exception exception)
        {
            string fallbackOutcome = "The supervisor writes, \"No outcome returned. We will treat this silence as policy.\"";
            missionState.RecordRoundSummary(selectedReply, result, fallbackOutcome);
            AppendMissionLog(BuildDecisionSummary(selectedReply, result, fallbackOutcome));
            statusText.text = BuildOllamaFailureMessage(exception, ResolveModel(outcomeModelName));
        }
        finally
        {
            SetPrimaryAction(missionState.IsComplete ? PrimaryActionMode.GenerateFinalReport : PrimaryActionMode.GenerateIntercept);
            generateButton.interactable = true;
            SetVisualState(VisualState.Logged);
        }
    }

    private async void GenerateFinalReport()
    {
        if (!missionState.HasScenario)
        {
            SetPrimaryAction(PrimaryActionMode.GenerateScenario);
            statusText.text = "No scenario is loaded.";
            return;
        }

        if (missionState.HasPendingReply)
        {
            statusText.text = "A reply is still pending. Choose a reply before generating the final report.";
            ShowTab(DeskTab.Decision);
            return;
        }

        BeginRequest();
        generateButton.interactable = false;
        SetReplyButtonsEnabled(false);
        SetVisualState(VisualState.Resolving);
        ShowTab(DeskTab.MissionLog);
        statusText.text = "Generating final debrief. Command is preparing to learn the wrong lesson.";

        try
        {
            var client = new OllamaClient(ollamaEndpoint, ResolveModel(reportModelName), requestTimeoutSeconds);
            string prompt = InterceptPromptBuilder.BuildFinalReportPrompt(
                missionState.Scenario,
                missionState.SituationSummary,
                missionState.BuildConsequenceSummary(),
                missionState.Grade,
                missionState.CorrectDecisions,
                missionState.RiskLevel,
                missionState.ObjectiveStatus,
                missionState.Confusion,
                missionState.CommandEmbarrassment);
            await RequestValidText(client, prompt, InterceptPromptBuilder.BuildFinalReportRetryPrompt(prompt), ValidateOutcome, requestCancellation.Token);

            RefreshSituationBoard();
            AppendMissionLog($"FINAL — {FormatMissionGrade(missionState.Grade)}\n\"{PickFinalRemark(missionState.Grade)}\"");
            statusText.text = "Final report generated. A new scenario can now be loaded.";
            SetPrimaryAction(PrimaryActionMode.NewScenario);
            FlashStamp("CLOSED-ish", new Color(1f, 0.74f, 0.2f, 1f));
        }
        catch (OperationCanceledException)
        {
            statusText.text = "Final report request cancelled.";
        }
        catch (Exception exception)
        {
            statusText.text = BuildOllamaFailureMessage(exception, ResolveModel(reportModelName));
            Debug.LogWarning(exception.Message);
        }
        finally
        {
            generateButton.interactable = true;
            SetVisualState(VisualState.Logged);
        }
    }

    private void BeginRequest()
    {
        requestCancellation?.Cancel();
        requestCancellation?.Dispose();
        requestCancellation = new CancellationTokenSource();
    }

    private IEnumerator ReceivingLoop()
    {
        string[] states =
        {
            "Receiver tuning... suspiciously rhythmic static detected.",
            "Sorting useful signal from office microwave interference...",
            "Stamp machine warming up for premature certainty...",
            "Message spool turning. Someone labelled it urgent-ish."
        };

        int index = 0;
        while (visualState == VisualState.Receiving)
        {
            signalStateText.text = states[index % states.Length];
            if (interceptGlowImage != null)
            {
                interceptGlowImage.color = index % 2 == 0
                    ? new Color(0.1f, 0.95f, 0.52f, 0.12f)
                    : new Color(1f, 0.74f, 0.2f, 0.12f);
            }

            index++;
            yield return new WaitForSeconds(0.45f);
        }
    }

    private async Task<ScenarioBrief> RequestValidScenario(
        OllamaClient client,
        string prompt,
        string retryPrompt,
        CancellationToken cancellationToken)
    {
        try
        {
            return await SendAndParseScenario(client, prompt, cancellationToken);
        }
        catch (GeneratedTextValidationException firstFailure)
        {
            Debug.LogWarning("Ollama scenario failed validation. Retrying once with stricter prompt.\n" + firstFailure.Message);
            return await SendAndParseScenario(client, retryPrompt, cancellationToken);
        }
    }

    private async Task<ScenarioBrief> SendAndParseScenario(OllamaClient client, string prompt, CancellationToken cancellationToken)
    {
        Debug.Log($"Sending Ollama scenario query to {ResolveModel(scenarioModelName)} at {ollamaEndpoint}:\n{prompt}");

        string rawResponse = await client.GenerateAsync(prompt, cancellationToken);
        string cleanedResponse = CleanResponse(rawResponse);
        cleanedResponse = await QualityRefineAsync(cleanedResponse,
            "Generating a 5-round fictional satirical intelligence desk scenario. Three signal sources must have Friendly, Enemy, and Deception biases.",
            "scenario", cancellationToken);
        ScenarioBrief scenario = ParseScenario(cleanedResponse);
        ValidateScenario(scenario);

        Debug.Log($"Ollama scenario received:\n{cleanedResponse}");
        return scenario;
    }

    private IEnumerator RevealInterceptRoutine(string intercept)
    {
        SetVisualState(VisualState.Reveal);
        statusText.text = "Intercept captured. The machine is deciding how dramatic to be.";
        signalStateText.text = "Signal locked. Revealing transcript...";
        SetReplyButtonsEnabled(false);
        FlashStamp("URGENT-ish", new Color(1f, 0.2f, 0.16f, 1f));
        SpawnSignalPings(new Color(0.32f, 1f, 0.58f, 0.75f));

        yield return new WaitForSeconds(0.25f);
        uiJuice.Shake(interceptPanelRect, 9f, 0.35f);

        bool revealDone = false;
        typewriterEffect.Play(transmissionText, intercept, 0.018f, () => revealDone = true);
        while (!revealDone)
        {
            transmissionText.color = UnityEngine.Random.value > 0.88f
                ? new Color(1f, 0.92f, 0.62f, 1f)
                : new Color(0.86f, 1f, 0.86f, 1f);
            yield return null;
        }

        transmissionText.color = new Color(0.86f, 1f, 0.86f, 1f);
        SetVisualState(VisualState.AwaitingReply);
        SetReplyButtonsEnabled(true);
        statusText.text = "Intercept revealed. The Replies tab is now live, unfortunately.";
        signalStateText.text = "Transcript pinned. Reply choices are ready.";
        uiJuice.Pulse(interceptPanelRect, 1.012f, 0.24f);
        UpdateTabStates();
    }

    private void SetVisualState(VisualState state)
    {
        visualState = state;
        UpdateTabStates();

        if (interceptGlowImage == null)
        {
            return;
        }

        interceptGlowImage.color = state switch
        {
            VisualState.Receiving => new Color(0.1f, 0.95f, 0.52f, 0.12f),
            VisualState.Reveal => new Color(1f, 0.74f, 0.2f, 0.16f),
            VisualState.AwaitingReply => new Color(0.28f, 0.95f, 0.52f, 0.1f),
            VisualState.Resolving => new Color(1f, 0.2f, 0.16f, 0.12f),
            _ => new Color(0.08f, 0.28f, 0.2f, 0.08f)
        };
    }

    private void PlayDecisionFeedback(bool wasCorrect)
    {
        Color flash = wasCorrect ? new Color(0.28f, 1f, 0.48f, 1f) : new Color(1f, 0.18f, 0.12f, 1f);
        string stamp = wasCorrect ? "NOT BAD" : "OH NO";

        FlashStamp(stamp, flash);
        SpawnSignalPings(wasCorrect ? new Color(0.28f, 1f, 0.48f, 0.72f) : new Color(1f, 0.22f, 0.12f, 0.72f));
        if (backgroundImage != null)
        {
            uiJuice.Flash(backgroundImage, wasCorrect ? new Color(0.04f, 0.12f, 0.08f, 1f) : new Color(0.16f, 0.04f, 0.04f, 1f), 0.28f);
        }
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

    private async Task<string> RequestValidIntercept(
        OllamaClient client,
        string prompt,
        CancellationToken cancellationToken)
    {
        try
        {
            return await SendAndParseIntercept(client, prompt, cancellationToken);
        }
        catch (GeneratedTextValidationException firstFailure)
        {
            Debug.LogWarning("Ollama intercept failed validation. Retrying once.\n" + firstFailure.Message);
            string retryPrompt = InterceptPromptBuilder.BuildInterceptOnlyRetryPrompt(prompt);
            return await SendAndParseIntercept(client, retryPrompt, cancellationToken);
        }
    }

    private async Task<string> SendAndParseIntercept(
        OllamaClient client,
        string prompt,
        CancellationToken cancellationToken)
    {
        Debug.Log($"Sending intercept query to {ResolveModel(interceptModelName)} at {ollamaEndpoint}:\n{prompt}");

        string rawResponse = await client.GenerateAsync(prompt, cancellationToken);
        string cleanedResponse = CleanResponse(rawResponse);
        Debug.Log($"Intercept raw response:\n{cleanedResponse}");
        string context = missionState.HasScenario
            ? $"Intercept generation. Round {missionState.RoundNumber + 1}/{MissionState.RoundLimit}. Scenario: {missionState.Scenario.Title}."
            : "Intercept generation for a satirical intelligence desk game.";
        cleanedResponse = await QualityRefineAsync(cleanedResponse, context, "intercept", cancellationToken);
        Debug.Log($"Intercept after quality refinement:\n{cleanedResponse}");

        string intercept = ParseSingleField(cleanedResponse, "INTERCEPT:");
        if (string.IsNullOrWhiteSpace(intercept))
        {
            intercept = ParseFirstMeaningfulLine(cleanedResponse);
        }

        if (string.IsNullOrWhiteSpace(intercept))
        {
            throw new GeneratedTextValidationException("Ollama did not return an INTERCEPT line.");
        }

        ValidateIntercept(intercept);
        Debug.Log($"Intercept received:\n{intercept}");
        return intercept;
    }

    private async Task<GeneratedOutcomePackage> RequestValidOutcome(
        OllamaClient client,
        string prompt,
        string retryPrompt,
        CancellationToken cancellationToken)
    {
        try
        {
            return await SendAndParseOutcome(client, prompt, cancellationToken);
        }
        catch (GeneratedTextValidationException firstFailure)
        {
            Debug.LogWarning("Ollama outcome failed validation. Retrying once with stricter prompt.\n" + firstFailure.Message);
            return await SendAndParseOutcome(client, retryPrompt, cancellationToken);
        }
    }

    private async Task<GeneratedOutcomePackage> SendAndParseOutcome(
        OllamaClient client,
        string prompt,
        CancellationToken cancellationToken)
    {
        Debug.Log($"Sending Ollama outcome query to {ResolveModel(outcomeModelName)} at {ollamaEndpoint}:\n{prompt}");

        string rawResponse = await client.GenerateAsync(prompt, cancellationToken);
        string cleanedResponse = CleanResponse(rawResponse);
        string context = missionState.HasScenario
            ? $"Round {missionState.RoundNumber}/{MissionState.RoundLimit}. Scenario: {missionState.Scenario.Title}. History: {missionState.BuildNarrativeRecap()}"
            : "Outcome narration for a satirical intelligence desk game.";
        cleanedResponse = await QualityRefineAsync(cleanedResponse, context, "outcome", cancellationToken);
        GeneratedOutcomePackage outcome = ParseOutcome(cleanedResponse);
        ValidateOutcome(outcome.Outcome);
        ValidateOutcome(outcome.Situation);
        ValidateOutcome(outcome.Consequence);

        Debug.Log($"Ollama outcome received:\n{cleanedResponse}");
        return outcome;
    }

    private async Task<GeneratedInterceptPackage> SendAndParsePackage(
        OllamaClient client,
        string prompt,
        GeneratedReplyOption[] replyProfiles,
        CancellationToken cancellationToken)
    {
        Debug.Log($"Sending Ollama query to {ResolveModel(interceptModelName)} at {ollamaEndpoint}:\n{prompt}");

        string rawResponse = await client.GenerateAsync(prompt, cancellationToken);
        string cleanedResponse = CleanResponse(rawResponse);
        string context = missionState.HasScenario
            ? $"Round {missionState.RoundNumber + 1}/{MissionState.RoundLimit}. Scenario: {missionState.Scenario.Title}. History: {missionState.BuildNarrativeRecap()}"
            : "Intercept generation for a satirical intelligence desk game.";
        cleanedResponse = await QualityRefineAsync(cleanedResponse, context, "intercept-replies", cancellationToken);
        GeneratedInterceptPackage package = ParsePackage(cleanedResponse, replyProfiles);
        ValidateIntercept(package.Intercept);
        foreach (GeneratedReplyOption option in package.Options)
        {
            ValidateReplyOption(option.Text);
        }

        Debug.Log($"Ollama response received:\n{cleanedResponse}");
        return package;
    }

    private async Task<GeneratedReplyOption[]> RequestValidReplies(
        OllamaClient client,
        string prompt,
        GeneratedReplyOption[] replyProfiles,
        CancellationToken cancellationToken)
    {
        try
        {
            return await SendAndParseReplies(client, prompt, replyProfiles, cancellationToken);
        }
        catch (GeneratedTextValidationException firstFailure)
        {
            Debug.LogWarning("Ollama replies failed validation. Retrying once.\n" + firstFailure.Message);
            string retryPrompt = InterceptPromptBuilder.BuildRepliesOnlyRetryPrompt(prompt);
            return await SendAndParseReplies(client, retryPrompt, replyProfiles, cancellationToken);
        }
    }

    private async Task<GeneratedReplyOption[]> SendAndParseReplies(
        OllamaClient client,
        string prompt,
        GeneratedReplyOption[] replyProfiles,
        CancellationToken cancellationToken)
    {
        Debug.Log($"Sending replies query to {ResolveModel(interceptModelName)} at {ollamaEndpoint}:\n{prompt}");

        string rawResponse = await client.GenerateAsync(prompt, cancellationToken);
        string cleanedResponse = CleanResponse(rawResponse);
        string context = missionState.HasScenario
            ? $"Replies generation. Round {missionState.RoundNumber + 1}/{MissionState.RoundLimit}. Scenario: {missionState.Scenario.Title}."
            : "Replies generation for a satirical intelligence desk game.";
        cleanedResponse = await QualityRefineAsync(cleanedResponse, context, "replies", cancellationToken);

        GeneratedReplyOption[] options = ParseReplies(cleanedResponse, replyProfiles);
        foreach (GeneratedReplyOption option in options)
        {
            ValidateReplyOption(option.Text);
        }

        Debug.Log($"Replies received:\n{cleanedResponse}");
        return options;
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
        Debug.Log($"Sending Ollama query to {ResolveModel(reportModelName)} at {ollamaEndpoint}:\n{prompt}");

        string rawResponse = await client.GenerateAsync(prompt, cancellationToken);
        string cleanedResponse = CleanResponse(rawResponse);
        string context = missionState.HasScenario
            ? $"Final debrief. Scenario: {missionState.Scenario.Title}. Grade: {missionState.Grade}. Correct: {missionState.CorrectDecisions}/{MissionState.RoundLimit}. History: {missionState.BuildNarrativeRecap()}"
            : "Final report for a satirical intelligence desk game.";
        cleanedResponse = await QualityRefineAsync(cleanedResponse, context, "final-report", cancellationToken);
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
        string roundText = missionState.HasScenario ? $"{missionState.RoundNumber}/{MissionState.RoundLimit}" : "0/5";
        string modelInfo = HasCustomModels()
            ? $"Scn:{ResolveModel(scenarioModelName)} Int:{ResolveModel(interceptModelName)} Out:{ResolveModel(outcomeModelName)} Rpt:{ResolveModel(reportModelName)}"
            : $"Model: {modelName}";
        if (enableQualityOverseer && !string.IsNullOrWhiteSpace(qualityModelName))
        {
            modelInfo += $" Q:{qualityModelName}";
        }
        if (BugSquashMinigame.SessionHighScore > 0)
        {
            modelInfo += $" Bugs squashed: {BugSquashMinigame.SessionHighScore}";
        }
        statsText.text = $"Round: {roundText}    Correct: {missionState.CorrectDecisions}    Risk: {missionState.RiskLevel}    {modelInfo}";
    }

    private bool HasCustomModels()
    {
        return !string.IsNullOrWhiteSpace(scenarioModelName) ||
               !string.IsNullOrWhiteSpace(interceptModelName) ||
               !string.IsNullOrWhiteSpace(outcomeModelName) ||
               !string.IsNullOrWhiteSpace(reportModelName);
    }

    private void RefreshSituationBoard()
    {
        if (!missionState.HasScenario)
        {
            briefingText.text = InterceptPromptBuilder.BuildMissionBriefingText();
            return;
        }

        ScenarioBrief scenario = missionState.Scenario;
        briefingText.text =
            $"{scenario.Title}\n" +
            $"{scenario.Location}\n\n" +
            $"Task: {scenario.PlayerTask}\n" +
            $"Stake: {scenario.Stake}\n" +
            $"Complication: {scenario.Complication}\n" +
            $"Command's Idea: {scenario.CommandBadIdea}\n\n" +
            $"Situation: {missionState.SituationSummary}\n\n" +
            $"Status: {BuildValueBar(missionState.CorridorStability)}  Corridor\n" +
            $"        {BuildValueBar(missionState.ObjectiveStatus)}  Objectives\n" +
            $"        {BuildValueBar(missionState.Confusion)}  Confusion\n" +
            $"        {BuildValueBar(missionState.CommandEmbarrassment)}  Embarrassment\n\n" +
            $"Grade: {FormatMissionGrade(missionState.Grade)}     Consequence: {missionState.LatestConsequence}\n\n" +
            $"Sources:\n{BuildSourceNotes()}";
    }

    private void AppendMissionLog(string entry)
    {
        missionLog = entry;
        missionLogText.text = missionLog;
        if (supervisorNoteText != null)
        {
            supervisorNoteText.text = BuildSupervisorNote();
        }
        uiJuice.Pulse(missionLogText.transform, 1.01f, 0.22f);
        FlashStamp("FILED-ish", new Color(1f, 0.74f, 0.2f, 1f));
        CanvasGroup group = GetPanelGroup(DeskTab.MissionLog);
        if (group != null)
        {
            group.alpha = 0.72f;
            uiJuice.Fade(group, 1f, 0.18f);
        }
    }

    private string BuildDecisionSummary(GeneratedReplyOption selectedReply, DecisionResult result, string outcome)
    {
        string status = result.WasCorrect ? "CORRECT" : "WRONG";
        string remark = string.IsNullOrWhiteSpace(missionState.LatestSupervisorRemark)
            ? PickFallbackRemark(result.WasCorrect, missionState.SupervisorPatience)
            : missionState.LatestSupervisorRemark;
        return $"Round {missionState.RoundNumber}: {status}\n\"{remark}\"";
    }

    private string BuildOllamaFailureMessage(Exception exception, string failingModel)
    {
        string modelTag = string.IsNullOrWhiteSpace(failingModel) ? "unknown model" : failingModel;
        string message = exception.Message;
        if (message.IndexOf("timeout", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return $"Ollama request timed out after {requestTimeoutSeconds}s on model {modelTag}. Try a faster model or raise the inspector timeout.\n{message}";
        }

        return $"Ollama is not responding for model {modelTag}. Start Ollama and ensure the model is installed.\n{message}";
    }

    private void SetPrimaryAction(PrimaryActionMode mode)
    {
        primaryActionMode = mode;
        if (generateButton == null)
        {
            return;
        }

        generateButton.GetComponentInChildren<Text>().text = mode switch
        {
            PrimaryActionMode.GenerateScenario => "Generate Scenario",
            PrimaryActionMode.GenerateFinalReport => "Generate Final Report",
            PrimaryActionMode.NewScenario => "New Scenario",
            _ => missionState.RoundNumber == 0 ? "Generate Intercept" : "Next Intercept"
        };
    }

    private static string BuildValueBar(int value)
    {
        int clampedValue = Mathf.Clamp(value, 0, 5);
        return new string('|', clampedValue) + new string('.', 5 - clampedValue) + $" {clampedValue}/5";
    }

    private string BuildSourceNotes()
    {
        if (!missionState.HasScenario)
        {
            return "No sources loaded.";
        }

        return string.Join("\n", missionState.Scenario.Sources.Select(source =>
            $"{source.CodeName}: {source.PublicDescription} | Reliability: {source.Reliability} | Tell: {source.Tell} | Last: {source.LastObservedBehavior}"));
    }

    private void ApplyClueChips(IReadOnlyList<EvidenceClue> clues)
    {
        if (clueTexts == null)
        {
            return;
        }

        for (int i = 0; i < clueTexts.Length; i++)
        {
            clueTexts[i].text = i < clues.Count ? clues[i].Text : "Clue pending";
            clueTexts[i].color = i < clues.Count
                ? AmberText
                : new Color(0.62f, 0.7f, 0.66f, 1f);
        }
    }

    private static string BuildCluePromptSummary(IReadOnlyList<EvidenceClue> clues)
    {
        return clues == null || clues.Count == 0
            ? "No clue chips selected."
            : string.Join("; ", clues.Select(clue => $"{clue.Category}: {clue.Text}"));
    }

    private static string FormatMissionGrade(MissionGrade grade)
    {
        return grade switch
        {
            MissionGrade.Contained => "Contained",
            MissionGrade.MessySuccess => "Messy Success",
            MissionGrade.OperationalFarce => "Operational Farce",
            _ => "Corridor Incident"
        };
    }

    private void SetReplyButtonsEnabled(bool enabled)
    {
        if (replyButtons == null)
        {
            return;
        }

        foreach (Button button in replyButtons)
        {
            button.interactable = enabled && visualState == VisualState.AwaitingReply;
        }
    }

    private void ShowTab(DeskTab tab)
    {
        if (tab == DeskTab.Decision && visualState != VisualState.AwaitingReply && visualState != VisualState.Resolving && visualState != VisualState.Logged)
        {
            statusText.text = "Replies are locked until the intercept finishes clattering out.";
            tab = DeskTab.Intercept;
        }

        activeTab = tab;
        SetPanelVisible(DeskTab.Briefing, briefingCanvas, briefingPanel, tab == DeskTab.Briefing);
        SetPanelVisible(DeskTab.Intercept, interceptCanvas, interceptPanel, tab == DeskTab.Intercept);
        SetPanelVisible(DeskTab.Decision, decisionCanvas, decisionPanel, tab == DeskTab.Decision);
        SetPanelVisible(DeskTab.MissionLog, logCanvas, logPanel, tab == DeskTab.MissionLog);
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
            bool isReplies = button.name.EndsWith(DeskTab.Decision.ToString(), StringComparison.Ordinal);
            bool repliesReady = visualState == VisualState.AwaitingReply || visualState == VisualState.Resolving || visualState == VisualState.Logged;
            button.interactable = !isReplies || repliesReady;
            button.GetComponent<Image>().color = isSelected
                ? new Color(0.33f, 0.52f, 0.34f, 1f)
                : isReplies && !repliesReady
                    ? new Color(0.09f, 0.11f, 0.11f, 1f)
                    : new Color(0.13f, 0.18f, 0.17f, 1f);
        }
    }

    private void SetPanelVisible(DeskTab tab, Canvas subCanvas, GameObject panel, bool visible)
    {
        if (subCanvas != null)
            subCanvas.gameObject.SetActive(visible);
        else
            panel.SetActive(visible);

        CanvasGroup group = GetPanelGroup(tab);
        if (group != null)
        {
            if (visible)
            {
                group.alpha = 0.72f;
                uiJuice.Fade(group, 1f, 0.16f);
            }
            else
            {
                group.alpha = 0f;
            }

            group.interactable = visible;
            group.blocksRaycasts = visible;
        }
    }

    private CanvasGroup GetPanelGroup(DeskTab tab)
    {
        int index = (int)tab;
        if (panelGroups == null || index < 0 || index >= panelGroups.Length)
        {
            return null;
        }

        return panelGroups[index];
    }

    private void FlashStamp(string label, Color color)
    {
        if (!showStampFlashes)
        {
            return;
        }

        if (stampText == null || stampGroup == null)
        {
            return;
        }

        stampText.text = label;
        stampText.color = color;
        stampGroup.alpha = 1f;
        stampText.transform.localScale = Vector3.one;
        uiJuice.Pulse(stampText.transform, 1.16f, 0.3f);
        StartCoroutine(FadeStampRoutine());
    }

    private IEnumerator FadeStampRoutine()
    {
        yield return new WaitForSeconds(0.35f);
        uiJuice.Fade(stampGroup, 0f, 0.35f);
    }

    private void SpawnSignalPings(Color color)
    {
        if (!showSignalPings)
        {
            return;
        }

        if (pingLayer == null)
        {
            return;
        }

        for (int i = 0; i < 5; i++)
        {
            Image ping = CreateImage("Signal Ping", pingLayer, color);
            RectTransform rectTransform = ping.rectTransform;
            rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            rectTransform.sizeDelta = Vector2.one * UnityEngine.Random.Range(7f, 15f);
            rectTransform.anchoredPosition = new Vector2(UnityEngine.Random.Range(-360f, 360f), UnityEngine.Random.Range(-130f, 130f));
            StartCoroutine(PingRoutine(ping, rectTransform, UnityEngine.Random.Range(0.18f, 0.38f)));
        }
    }

    private IEnumerator PingRoutine(Image ping, RectTransform rectTransform, float delay)
    {
        yield return new WaitForSeconds(delay);
        Vector3 startScale = rectTransform.localScale;
        float elapsed = 0f;
        const float duration = 0.45f;
        Color startColor = ping.color;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            rectTransform.localScale = Vector3.Lerp(startScale, startScale * 3f, t);
            ping.color = new Color(startColor.r, startColor.g, startColor.b, Mathf.Lerp(startColor.a, 0f, t));
            yield return null;
        }

        Destroy(ping.gameObject);
    }

    private string BuildSupervisorNote()
    {
        if (!string.IsNullOrWhiteSpace(missionState.LatestSupervisorRemark))
        {
            return missionState.LatestSupervisorRemark;
        }

        return missionState.SupervisorPatience switch
        {
            <= 0 => "\"At this point I am supervising the concept of supervision.\"",
            <= 2 => "\"The corridor has questions, and so do I.\"",
            >= MissionState.MaxSupervisorPatience => "\"Suspiciously competent. Document this before it becomes a meeting.\"",
            _ => "\"Proceed, but make it look like someone approved this.\""
        };
    }

    private static string PickFallbackRemark(bool wasCorrect, int patience)
    {
        string[] pool;
        if (patience <= 0)
        {
            pool = SuperiorNeutralRemarks;
        }
        else if (wasCorrect)
        {
            pool = patience >= MissionState.MaxSupervisorPatience
                ? SuperiorCorrectRemarks.Concat(SuperiorNeutralRemarks).ToArray()
                : SuperiorCorrectRemarks;
        }
        else
        {
            pool = SuperiorWrongRemarks;
        }

        int hash = Math.Abs(Environment.TickCount + patience * 7);
        return pool[hash % pool.Length];
    }

    private static string PickFinalRemark(MissionGrade grade)
    {
        int index = grade switch
        {
            MissionGrade.Contained => 0,
            MissionGrade.MessySuccess => 1,
            MissionGrade.OperationalFarce => 2,
            _ => 3
        };
        return SuperiorFinalRemarks[index % SuperiorFinalRemarks.Length];
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

    private SignalSourceProfile ChooseSourceForRound(InterceptClassification hiddenTruth)
    {
        IReadOnlyList<SignalSourceProfile> sources = missionState.Scenario.Sources;
        SignalSourceProfile biasedSource = sources.FirstOrDefault(source => source.Bias == hiddenTruth);
        if (biasedSource != null && UnityEngine.Random.value > 0.35f)
        {
            return biasedSource;
        }

        return sources[UnityEngine.Random.Range(0, sources.Count)];
    }

    private static EvidenceClue[] BuildEvidenceClues(InterceptClassification hiddenTruth, SignalSourceProfile source)
    {
        var clues = new List<EvidenceClue>
        {
            new($"Tell: {source.Tell}", EvidenceClueCategory.MatchesKnownTell)
        };

        switch (hiddenTruth)
        {
            case InterceptClassification.Friendly:
                clues.Add(new EvidenceClue("Mentions a route already in the briefing", EvidenceClueCategory.MentionsOldRoute));
                clues.Add(new EvidenceClue("Cautious wording, not begging for urgency", EvidenceClueCategory.UnusualDelay));
                break;
            case InterceptClassification.Enemy:
                clues.Add(new EvidenceClue("Too eager for a fast decision", EvidenceClueCategory.TooEager));
                clues.Add(new EvidenceClue("Contradicts a known safe corridor detail", EvidenceClueCategory.ContradictsBriefing));
                break;
            default:
                clues.Add(new EvidenceClue("Repeats one phrase like it was approved by committee", EvidenceClueCategory.RepeatedPhrase));
                clues.Add(new EvidenceClue("Smells faintly of paperwork and bait", EvidenceClueCategory.PaperworkSmell));
                break;
        }

        for (int i = 0; i < clues.Count; i++)
        {
            int swapIndex = UnityEngine.Random.Range(i, clues.Count);
            (clues[i], clues[swapIndex]) = (clues[swapIndex], clues[i]);
        }

        return clues.Take(3).ToArray();
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
            InterceptClassification.Friendly => "a cooperative-sounding reply signalling the situation is under control — dry, sarcastic, protects the extraction",
            InterceptClassification.Enemy => "an alarmed-sounding reply treating the transmission as a credible threat — sharp, flags danger, mocks Command's love of urgent stamps",
            InterceptClassification.Deception => "a sceptical-sounding reply that sees through the bait and refuses to engage — calm, dismissive, jokes about paperwork",
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
            "a reply that misreads the intercept entirely — overconfident, escalates for the wrong reason, ends with a dry joke");
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
            "a reply that sounds like it is answering a different transmission — oddly specific, technically detailed, but clearly irrelevant to this intercept");
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

    private static ScenarioBrief ParseScenario(string response)
    {
        var fields = ReadLabelledFields(response);
        if (!fields.ContainsKey("SCENARIO_TITLE"))
        {
            return ParseLooseScenario(response);
        }

        string title = ReadFieldOrDefault(fields, "SCENARIO_TITLE", "Operation Unnamed");
        string location = ReadFieldOrDefault(fields, "LOCATION", "Undisclosed corridor");
        string task = ReadFieldOrDefault(fields, "PLAYER_TASK", "Assess the situation");
        string stake = ReadFieldOrDefault(fields, "CIVILIAN_OR_OPERATIONAL_STAKE", "Morale is fragile");
        string complication = ReadFieldOrDefault(fields, "COMPLICATION", "Reports contradict each other");
        string commandBadIdea = ReadFieldOrDefault(fields, "COMMAND_BAD_IDEA", "Command wants a fast public answer");
        SignalSourceProfile[] sources =
        {
            ParseSource(fields, 1),
            ParseSource(fields, 2),
            ParseSource(fields, 3)
        };

        return new ScenarioBrief(title, location, task, stake, complication, commandBadIdea, sources.ToArray());
    }

    private static ScenarioBrief ParseLooseScenario(string response)
    {
        List<string> lines = response
            .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(line => StripLabelDecoration(line.Trim()))
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Where(line => !line.StartsWith("Here", StringComparison.OrdinalIgnoreCase))
            .ToList();

        int index = 0;
        string title = ReadOrderedScenarioValue(lines, ref index, "Scenario");
        string location = ReadOrderedScenarioValue(lines, ref index, "Unspecified corridor");
        string task = ReadOrderedScenarioValue(lines, ref index, "Stabilise the operation");
        string stake = ReadOrderedScenarioValue(lines, ref index, "Civilian confidence is collapsing");
        string complication = ReadOrderedScenarioValue(lines, ref index, "The available reports contradict each other");
        string commandBadIdea = ReadOrderedScenarioValue(lines, ref index, "Command wants a fast public answer");
        var sources = new List<SignalSourceProfile>();

        while (index < lines.Count && sources.Count < 3)
        {
            if (TryReadLooseSource(lines, ref index, out SignalSourceProfile source))
            {
                sources.Add(source);
                continue;
            }

            index++;
        }

        if (sources.Count != 3)
        {
            throw new GeneratedTextValidationException("Ollama did not return three complete signal sources.");
        }

        return new ScenarioBrief(title, location, task, stake, complication, commandBadIdea, sources.ToArray());
    }

    private static string ReadOrderedScenarioValue(IReadOnlyList<string> lines, ref int index, string fallback)
    {
        while (index < lines.Count && TryReadLeadingClassification(lines[index], out _, out _))
        {
            index++;
        }

        if (index >= lines.Count)
        {
            return fallback;
        }

        return ReadLineValue(lines[index++], fallback);
    }

    private static bool TryReadLooseSource(IReadOnlyList<string> lines, ref int index, out SignalSourceProfile source)
    {
        source = null;
        if (!TryReadLeadingClassification(lines[index], out InterceptClassification bias, out string codeName))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(codeName))
        {
            codeName = bias + " Source";
        }

        index++;
        string publicDescription = "Unlisted source";
        string reliability = "Unknown";
        string tell = "No tell logged";
        string agenda = "Unclear agenda";

        while (index < lines.Count && !TryReadLeadingClassification(lines[index], out _, out _))
        {
            string line = lines[index++];
            string key = NormalizeLabelKey(ReadLineKey(line));
            string value = ReadLineValue(line, string.Empty);
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            if (key.EndsWith("PUBLIC", StringComparison.Ordinal))
            {
                publicDescription = value;
            }
            else if (key.EndsWith("RELIABILITY", StringComparison.Ordinal))
            {
                reliability = value;
            }
            else if (key.EndsWith("TELL", StringComparison.Ordinal))
            {
                tell = value;
            }
            else if (key.EndsWith("AGENDA", StringComparison.Ordinal))
            {
                agenda = value;
            }
            else if (key.EndsWith("BIAS", StringComparison.Ordinal) && TryParseClassification(value, out InterceptClassification parsedBias))
            {
                bias = parsedBias;
            }
        }

        source = new SignalSourceProfile(codeName, publicDescription, bias, reliability, tell, agenda);
        return true;
    }

    private static SignalSourceProfile ParseSource(Dictionary<string, string> fields, int index)
    {
        string prefix = "SOURCE_" + index + "_";
        string codeName = ReadFieldOrDefault(fields, prefix + "CODE", "UNK-" + index);
        string publicDesc = ReadFieldOrDefault(fields, prefix + "PUBLIC", "Unverified source");
        string reliability = ReadFieldOrDefault(fields, prefix + "RELIABILITY", "Unknown");
        string tell = ReadFieldOrDefault(fields, prefix + "TELL", "No tell logged");
        string agenda = ReadFieldOrDefault(fields, prefix + "AGENDA", "Unclear agenda");

        InterceptClassification bias;
        string biasRaw = ReadFieldOrDefault(fields, prefix + "BIAS", string.Empty);
        if (!TryParseClassification(biasRaw, out bias))
        {
            bias = index switch { 1 => InterceptClassification.Friendly, 2 => InterceptClassification.Enemy, _ => InterceptClassification.Deception };
        }

        return new SignalSourceProfile(codeName, publicDesc, bias, reliability, tell, agenda);
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

    private static GeneratedOutcomePackage ParseOutcome(string response)
    {
        var fields = ReadLabelledFields(response);
        return new GeneratedOutcomePackage(
            ReadRequiredField(fields, "OUTCOME"),
            ReadRequiredField(fields, "SITUATION"),
            ReadRequiredField(fields, "CONSEQUENCE"),
            ReadRequiredField(fields, "SOURCE_NOTE"),
            ReadOptionalField(fields, "SUPERVISOR_REMARK"));
    }

    private static string ParseSingleField(string response, string prefix)
    {
        string[] lines = response.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i].Trim();
            if (line.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                string sameLineValue = CleanResponse(line.Substring(prefix.Length).Trim());
                if (!string.IsNullOrWhiteSpace(sameLineValue))
                {
                    return sameLineValue;
                }

                if (i + 1 < lines.Length)
                {
                    string nextLine = lines[i + 1].Trim();
                    if (!string.IsNullOrWhiteSpace(nextLine))
                    {
                        return CleanResponse(nextLine);
                    }
                }
            }
        }

        return string.Empty;
    }

    private static string ParseFirstMeaningfulLine(string response)
    {
        string[] lines = response.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (string rawLine in lines)
        {
            string line = rawLine.Trim();
            if (string.IsNullOrWhiteSpace(line)) continue;
            if (line.StartsWith("Here", StringComparison.OrdinalIgnoreCase)) continue;
            if (line.StartsWith("Return", StringComparison.OrdinalIgnoreCase)) continue;
            if (line.StartsWith("Write", StringComparison.OrdinalIgnoreCase)) continue;
            if (line.StartsWith("Fictional", StringComparison.OrdinalIgnoreCase)) continue;
            if (line.StartsWith("Example", StringComparison.OrdinalIgnoreCase)) continue;
            if (line.StartsWith("OPTION_", StringComparison.OrdinalIgnoreCase)) continue;
            if (line == "---" || line == "***") continue;
            return CleanResponse(line);
        }

        return string.Empty;
    }

    private static GeneratedReplyOption[] ParseReplies(string response, GeneratedReplyOption[] replyProfiles)
    {
        string[] optionTexts = new string[3];

        string[] lines = response.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i].Trim();

            for (int slot = 0; slot < 3; slot++)
            {
                string prefix = $"OPTION_{slot + 1}:";
                if (line.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    string sameLineValue = CleanResponse(line.Substring(prefix.Length).Trim());
                    if (!string.IsNullOrWhiteSpace(sameLineValue))
                    {
                        optionTexts[slot] = sameLineValue;
                    }
                    else if (i + 1 < lines.Length)
                    {
                        string nextLine = lines[i + 1].Trim();
                        if (!string.IsNullOrWhiteSpace(nextLine))
                        {
                            optionTexts[slot] = CleanResponse(nextLine);
                        }
                    }
                }
            }
        }

        if (optionTexts.Any(string.IsNullOrWhiteSpace))
        {
            throw new GeneratedTextValidationException("Ollama did not return all three OPTION lines.");
        }

        GeneratedReplyOption[] options = new GeneratedReplyOption[3];
        for (int j = 0; j < options.Length; j++)
        {
            options[j] = replyProfiles[j];
            options[j].SetText(CleanResponse(optionTexts[j]));
        }

        return options;
    }

    private static Dictionary<string, string> ReadLabelledFields(string response)
    {
        var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        string[] lines = response.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (string rawLine in lines)
        {
            string line = StripLabelDecoration(rawLine.Trim());
            int separator = line.IndexOf(':');
            if (separator <= 0)
            {
                continue;
            }

            string key = NormalizeLabelKey(line.Substring(0, separator));
            string value = line.Substring(separator + 1).Trim();
            fields[key] = CleanResponse(value);
        }

        return fields;
    }

    private static string ReadRequiredField(Dictionary<string, string> fields, string key)
    {
        string normalizedKey = NormalizeLabelKey(key);
        if (!fields.TryGetValue(normalizedKey, out string value) || string.IsNullOrWhiteSpace(value))
        {
            throw new GeneratedTextValidationException("Ollama did not return required field: " + key);
        }

        return CleanResponse(value);
    }

    private static string ReadOptionalField(Dictionary<string, string> fields, string key)
    {
        string normalizedKey = NormalizeLabelKey(key);
        if (!fields.TryGetValue(normalizedKey, out string value) || string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return CleanResponse(value);
    }

    private static string ReadFieldOrDefault(Dictionary<string, string> fields, string key, string fallback)
    {
        string normalizedKey = NormalizeLabelKey(key);
        if (!fields.TryGetValue(normalizedKey, out string value) || string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        return CleanResponse(value);
    }

    private static string StripLabelDecoration(string line)
    {
        string cleaned = line.Trim().Trim('`', '*', '_');
        while (cleaned.Length > 0 && (cleaned[0] == '-' || cleaned[0] == '*' || cleaned[0] == '\u2022'))
        {
            cleaned = cleaned.Substring(1).TrimStart();
        }

        int dotIndex = cleaned.IndexOf('.');
        if (dotIndex > 0 && dotIndex < 4 && cleaned.Take(dotIndex).All(char.IsDigit))
        {
            cleaned = cleaned.Substring(dotIndex + 1).TrimStart();
        }

        return cleaned.Trim().Trim('`', '*', '_');
    }

    private static string NormalizeLabelKey(string key)
    {
        string normalized = new string((key ?? string.Empty)
            .Trim()
            .Select(character => char.IsLetterOrDigit(character) ? char.ToUpperInvariant(character) : '_')
            .ToArray());

        while (normalized.Contains("__"))
        {
            normalized = normalized.Replace("__", "_");
        }

        normalized = normalized.Trim('_');
        return normalized switch
        {
            "TITLE" => "SCENARIO_TITLE",
            "SCENARIO" => "SCENARIO_TITLE",
            "TASK" => "PLAYER_TASK",
            "STAKE" => "CIVILIAN_OR_OPERATIONAL_STAKE",
            "CIVILIAN_STAKE" => "CIVILIAN_OR_OPERATIONAL_STAKE",
            "OPERATIONAL_STAKE" => "CIVILIAN_OR_OPERATIONAL_STAKE",
            "BAD_IDEA" => "COMMAND_BAD_IDEA",
            "COMMAND_IDEA" => "COMMAND_BAD_IDEA",
            "TONE" => "TONE_DETAIL",
            "GOAL" => "ROUND_GOAL",
            _ => normalized
        };
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

    private static InterceptClassification ParseClassification(string value)
    {
        if (TryParseClassification(value, out InterceptClassification classification))
        {
            return classification;
        }

        throw new GeneratedTextValidationException("Ollama returned an invalid source bias. Expected Friendly, Enemy, or Deception; got: " + value);
    }

    private static bool TryParseClassification(string value, out InterceptClassification classification)
    {
        string cleanedValue = CleanResponse(value);
        if (Enum.TryParse(cleanedValue.Replace(" ", string.Empty), true, out classification))
        {
            return true;
        }

        string leadingWord = ReadLeadingWord(cleanedValue);
        if (Enum.TryParse(leadingWord, true, out classification))
        {
            return true;
        }

        string normalized = leadingWord.ToLowerInvariant();
        if (normalized == "deceptive")
        {
            classification = InterceptClassification.Deception;
            return true;
        }

        if (normalized == "hostile")
        {
            classification = InterceptClassification.Enemy;
            return true;
        }

        classification = InterceptClassification.Deception;
        return false;
    }

    private static bool TryReadLeadingClassification(string line, out InterceptClassification classification, out string remainder)
    {
        string cleanedLine = CleanResponse(line);
        string leadingWord = ReadLeadingWord(cleanedLine);
        if (!TryParseClassification(leadingWord, out classification))
        {
            remainder = string.Empty;
            return false;
        }

        remainder = cleanedLine.Substring(Math.Min(leadingWord.Length, cleanedLine.Length)).Trim(' ', '-', ':');
        string remainderKey = NormalizeLabelKey(ReadLineKey(remainder));
        if (remainderKey == "BIAS" || remainderKey == "PUBLIC" || remainderKey == "RELIABILITY" || remainderKey == "TELL" || remainderKey == "AGENDA")
        {
            return false;
        }

        return true;
    }

    private static string ReadLineKey(string line)
    {
        int separator = line.IndexOf(':');
        return separator <= 0 ? line : line.Substring(0, separator);
    }

    private static string ReadLineValue(string line, string fallback)
    {
        int separator = line.IndexOf(':');
        string value = separator <= 0 ? line : line.Substring(separator + 1);
        value = CleanResponse(value);
        return string.IsNullOrWhiteSpace(value) ? fallback : value;
    }

    private static string ReadLeadingWord(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        int length = 0;
        while (length < value.Length && char.IsLetter(value[length]))
        {
            length++;
        }

        return length == 0 ? string.Empty : value.Substring(0, length);
    }

    private static void ValidateIntercept(string response)
    {
        ValidateCommonResponse(response);
        if (LooksLikeRefusal(response))
        {
            throw new GeneratedTextValidationException("Ollama response appears to be a refusal rather than an intercept.");
        }

        string lowerResponse = response.ToLowerInvariant();
        if (InterceptBlockedLabels.Any(label => lowerResponse.Contains(label)))
        {
            throw new GeneratedTextValidationException("Ollama response revealed a blocked classification label.");
        }

        if (InterceptMetadataLeakPatterns.Any(pattern => pattern.IsMatch(response)))
        {
            throw new GeneratedTextValidationException("Ollama response leaked prompt/source metadata into the intercept.");
        }
    }

    private static void ValidateScenario(ScenarioBrief scenario)
    {
        ValidateCommonResponse(scenario.Title);
        ValidateCommonResponse(scenario.Location);
        ValidateCommonResponse(scenario.PlayerTask);
        ValidateCommonResponse(scenario.Stake);
        ValidateCommonResponse(scenario.Complication);
        ValidateCommonResponse(scenario.CommandBadIdea);

        if (scenario.Sources == null || scenario.Sources.Count != 3)
        {
            throw new GeneratedTextValidationException("Scenario did not include exactly three signal sources.");
        }

        foreach (SignalSourceProfile source in scenario.Sources)
        {
            ValidateCommonResponse(source.CodeName);
            ValidateCommonResponse(source.PublicDescription);
            ValidateCommonResponse(source.Reliability);
            ValidateCommonResponse(source.Tell);
            ValidateCommonResponse(source.Agenda);
        }

        if (!scenario.Sources.Any(source => source.Bias == InterceptClassification.Friendly) ||
            !scenario.Sources.Any(source => source.Bias == InterceptClassification.Enemy) ||
            !scenario.Sources.Any(source => source.Bias == InterceptClassification.Deception))
        {
            throw new GeneratedTextValidationException("Scenario sources must include Friendly, Enemy, and Deception biases.");
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
        uiJuice = canvas.gameObject.AddComponent<UiJuice>();
        typewriterEffect = canvas.gameObject.AddComponent<TypewriterTextEffect>();

        backgroundImage = CreateImage("Background", canvas.transform, new Color(0.035f, 0.045f, 0.044f, 1f));
        ApplySprite(backgroundImage, backgroundSprite, Color.white, false);
        StretchToParent(backgroundImage.rectTransform, 0f, 0f, 0f, 0f);
        if (showProceduralScanlines)
        {
            BuildScanlines(canvas.transform);
        }

        Text title = CreateText("Title", canvas.transform, font, "Could've Been Worse", 34, FontStyle.Bold, TextAnchor.UpperLeft);
        title.color = new Color(0.88f, 0.96f, 0.84f, 1f);
        AnchorTop(title.rectTransform, 34f, 18f, 690f, 44f);

        Text subtitle = CreateText("Subtitle", canvas.transform, font, "Operation Greyline | Office War-Room Desk", 14, FontStyle.Bold, TextAnchor.UpperLeft);
        subtitle.color = new Color(0.96f, 0.72f, 0.32f, 1f);
        AnchorTop(subtitle.rectTransform, 38f, 58f, 720f, 22f);

        statsText = CreateText("Stats", canvas.transform, font, string.Empty, 14, FontStyle.Normal, TextAnchor.UpperRight);
        statsText.color = new Color(0.78f, 0.91f, 0.76f, 1f);
        AnchorTop(statsText.rectTransform, 390f, 25f, 34f, 28f);

        BuildTabs(canvas.transform, font);

        briefingCanvas = CreateSubCanvas("Briefing Canvas", canvas.transform).GetComponent<Canvas>();
        briefingPanel = CreatePanel("Briefing Panel", briefingCanvas.transform);
        ApplySprite(briefingPanel.GetComponent<Image>(), briefingPanelSprite, Color.white);
        AddPanelLabelIfEnabled(briefingPanel.transform, font, "Desk memo: read this before confidently ruining anything");
        Image briefingPlate = CreateReadablePlate("Briefing Text Plate", briefingPanel.transform, ReadablePaperPlate);
        StretchToParent(briefingPlate.rectTransform, 78f, 74f, 78f, 56f);
        briefingText = CreateText("Briefing Text", briefingPanel.transform, font, string.Empty, 16, FontStyle.Normal, TextAnchor.UpperLeft);
        briefingText.color = useGeneratedArtAssets ? ReadableInk : ReadableLightText;
        ConfigureReadableText(briefingText, 13, 16);
        StretchToParent(briefingText.rectTransform, 102f, 98f, 102f, 82f);

        interceptCanvas = CreateSubCanvas("Intercept Canvas", canvas.transform).GetComponent<Canvas>();
        interceptPanel = CreatePanel("Intercept Panel", interceptCanvas.transform);
        ApplySprite(interceptPanel.GetComponent<Image>(), interceptPanelSprite, Color.white);
        interceptPanelRect = interceptPanel.GetComponent<RectTransform>();
        interceptGlowImage = CreateImage("Terminal Glow", interceptPanel.transform, useGeneratedArtAssets ? new Color(0f, 0f, 0f, 0f) : new Color(0.08f, 0.28f, 0.2f, 0.08f));
        StretchToParent(interceptGlowImage.rectTransform, 10f, 10f, 10f, 10f);
        AddPanelLabelIfEnabled(interceptPanel.transform, font, "Live transcript: probably meaningful, definitely inconvenient");
        Image signalPlate = CreateReadablePlate("Signal State Plate", interceptPanel.transform, new Color(0.025f, 0.055f, 0.04f, 0.78f));
        AnchorTop(signalPlate.rectTransform, 92f, 70f, 92f, 38f);
        signalStateText = CreateText("Signal State", interceptPanel.transform, font, string.Empty, 16, FontStyle.Bold, TextAnchor.UpperLeft);
        signalStateText.color = AmberText;
        ConfigureReadableText(signalStateText, 13, 16);
        AnchorTop(signalStateText.rectTransform, 110f, 78f, 110f, 24f);
        Image transcriptPlate = CreateReadablePlate("Transmission Text Plate", interceptPanel.transform, ReadableDarkPlate);
        StretchToParent(transcriptPlate.rectTransform, 116f, 128f, 116f, 146f);
        transmissionText = CreateText("Transmission Text", interceptPanel.transform, font, string.Empty, 26, FontStyle.Normal, TextAnchor.MiddleLeft);
        transmissionText.color = ReadableLightText;
        ConfigureReadableText(transmissionText, 18, 28);
        StretchToParent(transmissionText.rectTransform, 140f, 150f, 140f, 174f);
        BuildClueChips(interceptPanel.transform, font);
        ResolveMiniGameContainer();
        if (miniGameContainer != null)
        {
            bugSquash = miniGameContainer.GetComponent<BugSquashMinigame>();
            if (bugSquash == null)
            {
                bugSquash = miniGameContainer.gameObject.AddComponent<BugSquashMinigame>();
            }
            bugSquash.BuildUI(miniGameContainer, font);
        }
        else
        {
            bugSquash = interceptPanel.AddComponent<BugSquashMinigame>();
            bugSquash.BuildUI(interceptPanel.transform, font);
        }
        pingLayer = new GameObject("Signal Ping Layer", typeof(RectTransform)).GetComponent<RectTransform>();
        pingLayer.SetParent(interceptPanel.transform, false);
        StretchToParent(pingLayer, 0f, 0f, 0f, 0f);

        decisionCanvas = CreateSubCanvas("Decision Canvas", canvas.transform).GetComponent<Canvas>();
        decisionPanel = CreatePanel("Decision Panel", decisionCanvas.transform);
        ApplySprite(decisionPanel.GetComponent<Image>(), inspectorDecisionPanelSprite != null ? inspectorDecisionPanelSprite : logPanelSprite, Color.white);
        AddPanelLabelIfEnabled(decisionPanel.transform, font, "Reply tray: three bad ways to sound employed");
        BuildReplyControls(decisionPanel.transform, font);

        logCanvas = CreateSubCanvas("Mission Log Canvas", canvas.transform).GetComponent<Canvas>();
        logPanel = CreatePanel("Mission Log Panel", logCanvas.transform);
        ApplySprite(logPanel.GetComponent<Image>(), logPanelSprite, Color.white);
        AddPanelLabelIfEnabled(logPanel.transform, font, "Filed consequences: the supervisor is typing with intent");
        Image logPlate = CreateReadablePlate("Mission Log Text Plate", logPanel.transform, ReadablePaperPlate);
        StretchToParent(logPlate.rectTransform, 122f, 104f, 122f, 142f);
        missionLogText = CreateText("Mission Log Text", logPanel.transform, font, string.Empty, 18, FontStyle.Normal, TextAnchor.UpperLeft);
        missionLogText.color = useGeneratedArtAssets ? ReadableInk : ReadableLightText;
        ConfigureReadableText(missionLogText, 13, 17);
        StretchToParent(missionLogText.rectTransform, 148f, 128f, 148f, 168f);

        Image supervisorPlate = CreateReadablePlate("Supervisor Note Plate", logPanel.transform, new Color(0.93f, 0.84f, 0.66f, 0.78f));
        AnchorBottom(supervisorPlate.rectTransform, 122f, 72f, 122f, 46f);
        supervisorNoteText = CreateText("Supervisor Note", logPanel.transform, font, string.Empty, 15, FontStyle.Bold, TextAnchor.LowerLeft);
        supervisorNoteText.color = useGeneratedArtAssets ? new Color(0.27f, 0.11f, 0.065f, 1f) : AmberText;
        ConfigureReadableText(supervisorNoteText, 12, 15);
        AnchorBottom(supervisorNoteText.rectTransform, 146f, 78f, 146f, 34f);
        AddSupervisorAccent(logPanel.transform);

        Image statusPlate = CreateReadablePlate("Status Text Plate", canvas.transform, new Color(0.015f, 0.025f, 0.022f, 0.76f));
        AnchorBottom(statusPlate.rectTransform, 28f, 18f, 300f, 62f);
        statusText = CreateText("Status Text", canvas.transform, font, string.Empty, 16, FontStyle.Normal, TextAnchor.UpperLeft);
        statusText.color = AmberText;
        ConfigureReadableText(statusText, 12, 16);
        AnchorBottom(statusText.rectTransform, 44f, 28f, 320f, 44f);

        generateButton = CreateButton("Primary Action Button", canvas.transform, font, "Generate Scenario", 18);
        AnchorBottom(generateButton.GetComponent<RectTransform>(), 1010f, 24f, 34f, 54f);
        generateButton.onClick.AddListener(HandlePrimaryAction);

        if (showStampFlashes)
        {
            CreateStampOverlay(canvas.transform, font);
        }

        panelGroups = new CanvasGroup[4];
        panelGroups[(int)DeskTab.Briefing] = EnsureCanvasGroup(briefingCanvas.gameObject);
        panelGroups[(int)DeskTab.Intercept] = EnsureCanvasGroup(interceptCanvas.gameObject);
        panelGroups[(int)DeskTab.Decision] = EnsureCanvasGroup(decisionCanvas.gameObject);
        panelGroups[(int)DeskTab.MissionLog] = EnsureCanvasGroup(logCanvas.gameObject);
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

    private void BuildClueChips(Transform parent, Font font)
    {
        clueTexts = new[]
        {
            CreateClueChip(parent, font, 34f, 0),
            CreateClueChip(parent, font, 382f, 1),
            CreateClueChip(parent, font, 730f, 2)
        };
    }

    private Text CreateClueChip(Transform parent, Font font, float left, int index)
    {
        Image chip = CreateImage("Evidence Clue Chip", parent, new Color(0.12f, 0.16f, 0.14f, 0.96f));
        if (clueChipSprites.Length > 0)
        {
            ApplySprite(chip, clueChipSprites[index % clueChipSprites.Length], Color.white);
        }

        if (showProceduralOutlines)
        {
            var outline = chip.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.98f, 0.74f, 0.32f, 0.25f);
            outline.effectDistance = new Vector2(1f, -1f);
        }

        RectTransform rectTransform = chip.rectTransform;
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.zero;
        rectTransform.offsetMin = new Vector2(left, 34f);
        rectTransform.offsetMax = new Vector2(left + 314f, 82f);

        Text label = CreateText("Clue Label", chip.transform, font, "Clue pending", 14, FontStyle.Bold, TextAnchor.MiddleCenter);
        Image labelPlate = CreateReadablePlate("Clue Text Plate", chip.transform, new Color(0.02f, 0.035f, 0.026f, 0.76f));
        labelPlate.rectTransform.SetAsFirstSibling();
        StretchToParent(labelPlate.rectTransform, 18f, 8f, 18f, 8f);
        label.color = ReadableLightText;
        ConfigureReadableText(label, 10, 13);
        StretchToParent(label.rectTransform, 28f, 10f, 28f, 10f);
        return label;
    }

    private void BuildScanlines(Transform parent)
    {
        for (int i = 0; i < 24; i++)
        {
            Image line = CreateImage("Scanline " + i, parent, new Color(0.42f, 1f, 0.58f, i % 3 == 0 ? 0.025f : 0.012f));
            RectTransform rectTransform = line.rectTransform;
            rectTransform.anchorMin = new Vector2(0f, 1f);
            rectTransform.anchorMax = new Vector2(1f, 1f);
            rectTransform.offsetMin = new Vector2(0f, -28f - i * 28f);
            rectTransform.offsetMax = new Vector2(0f, -26f - i * 28f);
        }
    }

    private void AddPanelLabelIfEnabled(Transform parent, Font font, string label)
    {
        if (!showProceduralPanelLabels)
        {
            return;
        }

        Text tag = CreateText("Panel Label", parent, font, label, 14, FontStyle.Bold, TextAnchor.UpperLeft);
        tag.color = new Color(0.98f, 0.74f, 0.32f, 1f);
        AnchorTop(tag.rectTransform, 24f, 18f, 24f, 24f);

        Image underline = CreateImage("Panel Label Underline", parent, new Color(0.98f, 0.74f, 0.32f, 0.34f));
        AnchorTop(underline.rectTransform, 24f, 48f, 24f, 2f);
    }

    private void CreateStampOverlay(Transform parent, Font font)
    {
        var stampObject = new GameObject("Stamp Overlay", typeof(RectTransform), typeof(CanvasGroup));
        stampObject.transform.SetParent(parent, false);
        RectTransform rectTransform = stampObject.GetComponent<RectTransform>();
        rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        rectTransform.sizeDelta = new Vector2(340f, 96f);
        rectTransform.anchoredPosition = new Vector2(258f, 122f);
        rectTransform.localRotation = Quaternion.Euler(0f, 0f, -8f);

        stampGroup = stampObject.GetComponent<CanvasGroup>();
        stampGroup.alpha = 0f;
        stampGroup.blocksRaycasts = false;

        stampText = CreateText("Stamp Text", stampObject.transform, font, "URGENT-ish", 44, FontStyle.Bold, TextAnchor.MiddleCenter);
        stampText.color = new Color(1f, 0.2f, 0.16f, 1f);
        StretchToParent(stampText.rectTransform, 0f, 0f, 0f, 0f);
    }

    private void AddSupervisorAccent(Transform parent)
    {
        if (supervisorSprite == null)
        {
            return;
        }

        Image accent = CreateImage("Supervisor Presence Accent", parent, new Color(1f, 1f, 1f, supervisorAccentOpacity));
        ApplySprite(accent, supervisorSprite, new Color(1f, 1f, 1f, supervisorAccentOpacity));
        RectTransform rectTransform = accent.rectTransform;
        rectTransform.anchorMin = new Vector2(1f, 0f);
        rectTransform.anchorMax = new Vector2(1f, 0f);
        rectTransform.offsetMin = new Vector2(-210f, 28f);
        rectTransform.offsetMax = new Vector2(-46f, 192f);
    }

    private static Image CreateReadablePlate(string name, Transform parent, Color color)
    {
        Image plate = CreateImage(name, parent, color);
        plate.raycastTarget = false;
        return plate;
    }

    private static Transform FindNamedTransform(GameObject root, string objectName)
    {
        return root.GetComponentsInChildren<Transform>(true).FirstOrDefault(item => item.name == objectName);
    }

    private static T FindNamedComponent<T>(GameObject root, string objectName) where T : Component
    {
        return root.GetComponentsInChildren<T>(true).FirstOrDefault(item => item.name == objectName);
    }

    private static IEnumerable<T> FindRepeatedNamedComponents<T>(GameObject root, string objectName) where T : Component
    {
        return root.GetComponentsInChildren<T>(true).Where(item => item.name == objectName);
    }

    private static void ConfigureReadableText(Text text, int minSize, int maxSize)
    {
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        text.resizeTextForBestFit = true;
        text.resizeTextMinSize = minSize;
        text.resizeTextMaxSize = maxSize;
    }

    private static void ApplySprite(Image image, Sprite sprite, Color color, bool preserveAspect = true)
    {
        if (image == null || sprite == null)
        {
            return;
        }

        image.sprite = sprite;
        image.color = color;
        image.type = Image.Type.Simple;
        image.preserveAspect = preserveAspect;
    }

    private static CanvasGroup EnsureCanvasGroup(GameObject target)
    {
        CanvasGroup group = target.GetComponent<CanvasGroup>();
        if (group == null)
        {
            group = target.AddComponent<CanvasGroup>();
        }

        return group;
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
        heading.color = ReadableLightText;
        ConfigureReadableText(heading, 22, 27);
        AnchorTop(heading.rectTransform, 30f, 26f, 30f, 36f);

        Image helperPlate = CreateReadablePlate("Reply Helper Plate", parent, new Color(0.015f, 0.035f, 0.026f, 0.74f));
        AnchorTop(helperPlate.rectTransform, 28f, 68f, 28f, 54f);
        decisionHelperText = CreateText("Reply Helper", parent, font, string.Empty, 17, FontStyle.Normal, TextAnchor.UpperLeft);
        decisionHelperText.color = new Color(0.72f, 0.8f, 0.76f, 1f);
        ConfigureReadableText(decisionHelperText, 12, 16);
        AnchorTop(decisionHelperText.rectTransform, 44f, 76f, 44f, 38f);

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
        if (replyButtonSprites.Length > index)
        {
            ApplySprite(button.GetComponent<Image>(), replyButtonSprites[index], Color.white);
        }

        RectTransform rectTransform = button.GetComponent<RectTransform>();
        AnchorTop(rectTransform, 58f, top, 58f, 86f);
        Image textPlate = CreateReadablePlate("Reply Text Plate", button.transform, new Color(0.02f, 0.035f, 0.026f, 0.82f));
        textPlate.rectTransform.SetAsFirstSibling();
        StretchToParent(textPlate.rectTransform, 58f, 18f, 58f, 18f);
        Text label = button.GetComponentInChildren<Text>();
        label.color = ReadableLightText;
        label.alignment = TextAnchor.MiddleCenter;
        ConfigureReadableText(label, 12, 17);
        StretchToParent(label.rectTransform, 78f, 22f, 78f, 22f);
        int capturedIndex = index;
        button.onClick.AddListener(() => SelectReply(capturedIndex));
        return button;
    }

    private GameObject CreatePanel(string name, Transform parent)
    {
        Image panel = CreateImage(name, parent, new Color(0.075f, 0.095f, 0.085f, 0.96f));
        if (showProceduralOutlines)
        {
            var outline = panel.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.98f, 0.74f, 0.32f, 0.22f);
            outline.effectDistance = new Vector2(2f, -2f);
        }

        StretchToParent(panel.rectTransform, 34f, 126f, 34f, 96f);
        return panel.gameObject;
    }

    private static Canvas CreateCanvas()
    {
        var canvasObject = new GameObject("Could've Been Worse UI", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        var scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f, 720f);
        scaler.matchWidthOrHeight = 0.5f;

        return canvas;
    }

    private static GameObject CreateSubCanvas(string name, Transform parent)
    {
        var obj = new GameObject(name, typeof(RectTransform), typeof(Canvas));
        obj.transform.SetParent(parent, false);
        var rect = obj.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        return obj;
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
        textComponent.resizeTextForBestFit = false;

        return textComponent;
    }

    private Button CreateButton(string name, Transform parent, Font font, string label, int fontSize)
    {
        Image buttonImage = CreateImage(name, parent, new Color(0.16f, 0.23f, 0.2f, 1f));
        if (showProceduralOutlines)
        {
            var outline = buttonImage.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.98f, 0.74f, 0.32f, 0.2f);
            outline.effectDistance = new Vector2(1f, -1f);
        }

        var button = buttonImage.gameObject.AddComponent<Button>();
        button.targetGraphic = buttonImage;
        buttonImage.gameObject.AddComponent<UiButtonJuice>();

        ColorBlock colors = button.colors;
        colors.normalColor = new Color(0.16f, 0.23f, 0.2f, 1f);
        colors.highlightedColor = new Color(0.24f, 0.34f, 0.28f, 1f);
        colors.pressedColor = new Color(0.09f, 0.15f, 0.13f, 1f);
        colors.selectedColor = new Color(0.28f, 0.4f, 0.32f, 1f);
        colors.disabledColor = new Color(0.08f, 0.1f, 0.1f, 0.85f);
        button.colors = colors;

        Text buttonText = CreateText("Label", button.transform, font, label, fontSize, FontStyle.Bold, TextAnchor.MiddleCenter);
        buttonText.color = ReadableLightText;
        ConfigureReadableText(buttonText, 11, fontSize);
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
