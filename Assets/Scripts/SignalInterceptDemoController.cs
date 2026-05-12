using System;
using System.Collections;
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
    [SerializeField] private int requestTimeoutSeconds = 120;

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
    private GeneratedReplyOption[] currentReplyOptions = Array.Empty<GeneratedReplyOption>();
    private CancellationTokenSource requestCancellation;
    private DeskTab activeTab;
    private VisualState visualState = VisualState.Idle;
    private PrimaryActionMode primaryActionMode = PrimaryActionMode.GenerateScenario;
    private string missionLog = string.Empty;

    private void Awake()
    {
        requestTimeoutSeconds = Mathf.Max(requestTimeoutSeconds, 120);
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
        missionState.Reset();
        briefingText.text = InterceptPromptBuilder.BuildMissionBriefingText();
        transmissionText.text = "Awaiting intercept. Command has requested certainty, preferably before evidence.";
        decisionHelperText.text = "Generate a scenario first. Replies require a specific mess to make worse.";
        missionLog = "Mission desk online. No scenario loaded, so Command is currently confident about nothing.";
        missionLogText.text = missionLog;
        ApplyClueChips(Array.Empty<EvidenceClue>());
        supervisorNoteText.text = "Supervisor note: \"Try not to make the corridor worse before coffee.\"";
        statusText.text = $"Ollama required: {ollamaEndpoint}";
        signalStateText.text = "Receiver idle";
        SetPrimaryAction(PrimaryActionMode.GenerateScenario);
        generateButton.interactable = true;
        SetVisualState(VisualState.Idle);
        SetReplyButtonsEnabled(false);
        ShowTab(DeskTab.Briefing);
        RefreshStats();
        GenerateScenario();
    }

    private void HandlePrimaryAction()
    {
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
            var client = new OllamaClient(ollamaEndpoint, modelName, requestTimeoutSeconds);
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
            statusText.text = BuildOllamaFailureMessage(exception);
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

        try
        {
            var client = new OllamaClient(ollamaEndpoint, modelName, requestTimeoutSeconds);
            string prompt = InterceptPromptBuilder.BuildInterceptAndRepliesPrompt(
                missionState.Scenario,
                missionState.SituationSummary,
                missionState.BuildConsequenceSummary(),
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
            string retryPrompt = InterceptPromptBuilder.BuildInterceptAndRepliesRetryPrompt(
                missionState.Scenario,
                missionState.SituationSummary,
                missionState.BuildConsequenceSummary(),
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

            GeneratedInterceptPackage package = await RequestValidPackage(client, prompt, retryPrompt, pendingOptions, requestCancellation.Token);

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
            statusText.text = "Ollama request cancelled.";
            generateButton.interactable = true;
            SetVisualState(VisualState.Idle);
        }
        catch (GeneratedTextValidationException exception)
        {
            statusText.text = "Ollama responded, but the intercept package failed validation. Try generating again.\n" + exception.Message;
            transmissionText.text = "No valid intercept available.";
            ApplyClueChips(Array.Empty<EvidenceClue>());
            Debug.LogWarning(exception.Message);
            generateButton.interactable = true;
            SetVisualState(VisualState.Idle);
        }
        catch (Exception exception)
        {
            statusText.text = BuildOllamaFailureMessage(exception);
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
            var client = new OllamaClient(ollamaEndpoint, modelName, requestTimeoutSeconds);
            string outcomePrompt = InterceptPromptBuilder.BuildOutcomePrompt(
                missionState.Scenario,
                missionState.SituationSummary,
                missionState.BuildConsequenceSummary(),
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
            AppendMissionLog(BuildDecisionSummary(selectedReply, result, "The supervisor writes, \"Even the summary refused to be associated with this.\""));
            statusText.text = "Ollama responded, but the outcome failed validation. You can continue.\n" + exception.Message;
            Debug.LogWarning(exception.Message);
        }
        catch (Exception exception)
        {
            AppendMissionLog(BuildDecisionSummary(selectedReply, result, "The supervisor writes, \"No outcome returned. We will treat this silence as policy.\""));
            statusText.text = BuildOllamaFailureMessage(exception);
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

        BeginRequest();
        generateButton.interactable = false;
        SetReplyButtonsEnabled(false);
        SetVisualState(VisualState.Resolving);
        ShowTab(DeskTab.MissionLog);
        statusText.text = "Generating final debrief. Command is preparing to learn the wrong lesson.";

        try
        {
            var client = new OllamaClient(ollamaEndpoint, modelName, requestTimeoutSeconds);
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
            string report = await RequestValidText(client, prompt, InterceptPromptBuilder.BuildFinalReportRetryPrompt(prompt), ValidateOutcome, requestCancellation.Token);

            RefreshSituationBoard();
            AppendMissionLog($"FINAL REPORT - {FormatMissionGrade(missionState.Grade)}\n" + report);
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
            statusText.text = BuildOllamaFailureMessage(exception);
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
        Debug.Log($"Sending Ollama scenario query to {modelName} at {ollamaEndpoint}:\n{prompt}");

        string rawResponse = await client.GenerateAsync(prompt, cancellationToken);
        string cleanedResponse = CleanResponse(rawResponse);
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
        uiJuice.Flash(backgroundImage, wasCorrect ? new Color(0.04f, 0.12f, 0.08f, 1f) : new Color(0.16f, 0.04f, 0.04f, 1f), 0.28f);
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
        Debug.Log($"Sending Ollama outcome query to {modelName} at {ollamaEndpoint}:\n{prompt}");

        string rawResponse = await client.GenerateAsync(prompt, cancellationToken);
        string cleanedResponse = CleanResponse(rawResponse);
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
        string roundText = missionState.HasScenario ? $"{missionState.RoundNumber}/{MissionState.RoundLimit}" : "0/5";
        statsText.text = $"Round: {roundText}    Correct: {missionState.CorrectDecisions}    Risk: {missionState.RiskLevel}    Model: {modelName}";
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
            $"Command's Bad Idea: {scenario.CommandBadIdea}\n\n" +
            $"Situation: {missionState.SituationSummary}\n\n" +
            $"Corridor Stability: {BuildValueBar(missionState.CorridorStability)}\n" +
            $"Objective Status: {BuildValueBar(missionState.ObjectiveStatus)}\n" +
            $"Confusion: {BuildValueBar(missionState.Confusion)}\n" +
            $"Command Embarrassment: {BuildValueBar(missionState.CommandEmbarrassment)}\n\n" +
            $"Current Grade: {FormatMissionGrade(missionState.Grade)}\n" +
            $"Latest Consequence: {missionState.LatestConsequence}\n\n" +
            $"Source Notes:\n{BuildSourceNotes()}\n\n" +
            $"Desk Detail: {scenario.ToneDetail}";
    }

    private void AppendMissionLog(string entry)
    {
        missionLog = string.IsNullOrWhiteSpace(missionLog) ? entry : missionLog + "\n\n" + entry;
        missionLogText.text = missionLog;
        supervisorNoteText.text = BuildSupervisorNote();
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
        string assessment = result.WasCorrect ? "The reply somehow helped" : "The reply made the corridor more interesting";
        string source = missionState.CurrentSource == null ? "Unknown source" : missionState.CurrentSource.CodeName;
        return $"Round {missionState.RoundNumber}: {assessment}.\nSource: {source}\nClues: {missionState.BuildClueSummary()}\nYou chose: \"{selectedReply.Text}\"\n{outcome}";
    }

    private string BuildOllamaFailureMessage(Exception exception)
    {
        string message = exception.Message;
        if (message.IndexOf("timeout", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return $"Ollama request timed out after {requestTimeoutSeconds} seconds. The current prompt is larger now, so use a faster model or raise the inspector timeout if this machine is still thinking.\n{message}";
        }

        return "Ollama is not responding. Start Ollama and install/select the configured model.\n" + message;
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
                ? new Color(0.98f, 0.78f, 0.38f, 1f)
                : new Color(0.45f, 0.52f, 0.48f, 1f);
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
        SetPanelVisible(DeskTab.Briefing, briefingPanel, tab == DeskTab.Briefing);
        SetPanelVisible(DeskTab.Intercept, interceptPanel, tab == DeskTab.Intercept);
        SetPanelVisible(DeskTab.Decision, decisionPanel, tab == DeskTab.Decision);
        SetPanelVisible(DeskTab.MissionLog, logPanel, tab == DeskTab.MissionLog);
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

    private void SetPanelVisible(DeskTab tab, GameObject panel, bool visible)
    {
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
        return missionState.SupervisorPatience switch
        {
            <= 0 => "Supervisor note: \"At this point I am supervising the concept of supervision.\"",
            <= 2 => "Supervisor note: \"The corridor has questions, and so do I.\"",
            >= MissionState.MaxSupervisorPatience => "Supervisor note: \"Suspiciously competent. Document this before it becomes a meeting.\"",
            _ => "Supervisor note: \"Proceed, but make it look like someone approved this.\""
        };
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

    private static ScenarioBrief ParseScenario(string response)
    {
        var fields = ReadLabelledFields(response);
        string title = ReadRequiredField(fields, "SCENARIO_TITLE");
        string location = ReadRequiredField(fields, "LOCATION");
        string task = ReadRequiredField(fields, "PLAYER_TASK");
        string stake = ReadRequiredField(fields, "CIVILIAN_OR_OPERATIONAL_STAKE");
        string complication = ReadRequiredField(fields, "COMPLICATION");
        string commandBadIdea = ReadRequiredField(fields, "COMMAND_BAD_IDEA");
        string toneDetail = ReadRequiredField(fields, "TONE_DETAIL");
        string roundGoal = ReadRequiredField(fields, "ROUND_GOAL");
        SignalSourceProfile[] sources =
        {
            ParseSource(fields, 1),
            ParseSource(fields, 2),
            ParseSource(fields, 3)
        };

        return new ScenarioBrief(title, location, task, stake, complication, commandBadIdea, toneDetail, roundGoal, sources);
    }

    private static SignalSourceProfile ParseSource(Dictionary<string, string> fields, int index)
    {
        string prefix = "SOURCE_" + index + "_";
        return new SignalSourceProfile(
            ReadRequiredField(fields, prefix + "CODE"),
            ReadRequiredField(fields, prefix + "PUBLIC"),
            ParseClassification(ReadRequiredField(fields, prefix + "BIAS")),
            ReadRequiredField(fields, prefix + "RELIABILITY"),
            ReadRequiredField(fields, prefix + "TELL"),
            ReadRequiredField(fields, prefix + "AGENDA"));
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
            ReadRequiredField(fields, "SOURCE_NOTE"));
    }

    private static Dictionary<string, string> ReadLabelledFields(string response)
    {
        var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        string[] lines = response.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (string rawLine in lines)
        {
            string line = rawLine.Trim();
            int separator = line.IndexOf(':');
            if (separator <= 0)
            {
                continue;
            }

            string key = line.Substring(0, separator).Trim();
            string value = line.Substring(separator + 1).Trim();
            fields[key] = CleanResponse(value);
        }

        return fields;
    }

    private static string ReadRequiredField(Dictionary<string, string> fields, string key)
    {
        if (!fields.TryGetValue(key, out string value) || string.IsNullOrWhiteSpace(value))
        {
            throw new GeneratedTextValidationException("Ollama did not return required field: " + key);
        }

        return CleanResponse(value);
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
        if (Enum.TryParse(value.Replace(" ", string.Empty), true, out InterceptClassification classification))
        {
            return classification;
        }

        throw new GeneratedTextValidationException("Ollama returned an invalid source bias: " + value);
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

    private static void ValidateScenario(ScenarioBrief scenario)
    {
        ValidateCommonResponse(scenario.Title);
        ValidateCommonResponse(scenario.Location);
        ValidateCommonResponse(scenario.PlayerTask);
        ValidateCommonResponse(scenario.Stake);
        ValidateCommonResponse(scenario.Complication);
        ValidateCommonResponse(scenario.CommandBadIdea);
        ValidateCommonResponse(scenario.ToneDetail);
        ValidateCommonResponse(scenario.RoundGoal);

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
        StretchToParent(backgroundImage.rectTransform, 0f, 0f, 0f, 0f);
        BuildScanlines(canvas.transform);

        Text title = CreateText("Title", canvas.transform, font, "Signal Intercept", 34, FontStyle.Bold, TextAnchor.UpperLeft);
        title.color = new Color(0.88f, 0.96f, 0.84f, 1f);
        AnchorTop(title.rectTransform, 34f, 18f, 690f, 44f);

        Text subtitle = CreateText("Subtitle", canvas.transform, font, "Operation Greyline | Office War-Room Desk", 14, FontStyle.Bold, TextAnchor.UpperLeft);
        subtitle.color = new Color(0.96f, 0.72f, 0.32f, 1f);
        AnchorTop(subtitle.rectTransform, 38f, 58f, 720f, 22f);

        statsText = CreateText("Stats", canvas.transform, font, string.Empty, 14, FontStyle.Normal, TextAnchor.UpperRight);
        statsText.color = new Color(0.78f, 0.91f, 0.76f, 1f);
        AnchorTop(statsText.rectTransform, 390f, 25f, 34f, 28f);

        BuildTabs(canvas.transform, font);

        briefingPanel = CreatePanel("Briefing Panel", canvas.transform);
        AddPanelLabel(briefingPanel.transform, font, "Desk memo: read this before confidently ruining anything");
        briefingText = CreateText("Briefing Text", briefingPanel.transform, font, string.Empty, 16, FontStyle.Normal, TextAnchor.UpperLeft);
        briefingText.color = new Color(0.91f, 0.94f, 0.82f, 1f);
        StretchToParent(briefingText.rectTransform, 32f, 62f, 32f, 32f);

        interceptPanel = CreatePanel("Intercept Panel", canvas.transform);
        interceptPanelRect = interceptPanel.GetComponent<RectTransform>();
        interceptGlowImage = CreateImage("Terminal Glow", interceptPanel.transform, new Color(0.08f, 0.28f, 0.2f, 0.08f));
        StretchToParent(interceptGlowImage.rectTransform, 10f, 10f, 10f, 10f);
        AddPanelLabel(interceptPanel.transform, font, "Live transcript: probably meaningful, definitely inconvenient");
        signalStateText = CreateText("Signal State", interceptPanel.transform, font, string.Empty, 16, FontStyle.Bold, TextAnchor.UpperLeft);
        signalStateText.color = new Color(0.97f, 0.72f, 0.32f, 1f);
        AnchorTop(signalStateText.rectTransform, 32f, 44f, 32f, 28f);
        transmissionText = CreateText("Transmission Text", interceptPanel.transform, font, string.Empty, 30, FontStyle.Normal, TextAnchor.MiddleLeft);
        transmissionText.color = new Color(0.86f, 1f, 0.86f, 1f);
        StretchToParent(transmissionText.rectTransform, 36f, 82f, 36f, 112f);
        BuildClueChips(interceptPanel.transform, font);
        pingLayer = new GameObject("Signal Ping Layer", typeof(RectTransform)).GetComponent<RectTransform>();
        pingLayer.SetParent(interceptPanel.transform, false);
        StretchToParent(pingLayer, 0f, 0f, 0f, 0f);

        decisionPanel = CreatePanel("Decision Panel", canvas.transform);
        AddPanelLabel(decisionPanel.transform, font, "Reply tray: three bad ways to sound employed");
        BuildReplyControls(decisionPanel.transform, font);

        logPanel = CreatePanel("Mission Log Panel", canvas.transform);
        AddPanelLabel(logPanel.transform, font, "Filed consequences: the supervisor is typing with intent");
        missionLogText = CreateText("Mission Log Text", logPanel.transform, font, string.Empty, 18, FontStyle.Normal, TextAnchor.UpperLeft);
        missionLogText.color = new Color(0.9f, 0.9f, 0.78f, 1f);
        StretchToParent(missionLogText.rectTransform, 32f, 64f, 32f, 86f);

        supervisorNoteText = CreateText("Supervisor Note", logPanel.transform, font, string.Empty, 15, FontStyle.Bold, TextAnchor.LowerLeft);
        supervisorNoteText.color = new Color(0.98f, 0.73f, 0.34f, 1f);
        AnchorBottom(supervisorNoteText.rectTransform, 32f, 24f, 32f, 42f);

        statusText = CreateText("Status Text", canvas.transform, font, string.Empty, 16, FontStyle.Normal, TextAnchor.UpperLeft);
        statusText.color = new Color(0.94f, 0.82f, 0.58f, 1f);
        AnchorBottom(statusText.rectTransform, 34f, 24f, 430f, 50f);

        generateButton = CreateButton("Primary Action Button", canvas.transform, font, "Generate Scenario", 18);
        AnchorBottom(generateButton.GetComponent<RectTransform>(), 1080f, 24f, 34f, 50f);
        generateButton.onClick.AddListener(HandlePrimaryAction);

        CreateStampOverlay(canvas.transform, font);

        panelGroups = new CanvasGroup[4];
        panelGroups[(int)DeskTab.Briefing] = EnsureCanvasGroup(briefingPanel);
        panelGroups[(int)DeskTab.Intercept] = EnsureCanvasGroup(interceptPanel);
        panelGroups[(int)DeskTab.Decision] = EnsureCanvasGroup(decisionPanel);
        panelGroups[(int)DeskTab.MissionLog] = EnsureCanvasGroup(logPanel);
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
            CreateClueChip(parent, font, 34f),
            CreateClueChip(parent, font, 382f),
            CreateClueChip(parent, font, 730f)
        };
    }

    private Text CreateClueChip(Transform parent, Font font, float left)
    {
        Image chip = CreateImage("Evidence Clue Chip", parent, new Color(0.12f, 0.16f, 0.14f, 0.96f));
        var outline = chip.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0.98f, 0.74f, 0.32f, 0.25f);
        outline.effectDistance = new Vector2(1f, -1f);

        RectTransform rectTransform = chip.rectTransform;
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.zero;
        rectTransform.offsetMin = new Vector2(left, 34f);
        rectTransform.offsetMax = new Vector2(left + 314f, 82f);

        Text label = CreateText("Clue Label", chip.transform, font, "Clue pending", 14, FontStyle.Bold, TextAnchor.MiddleCenter);
        label.color = new Color(0.45f, 0.52f, 0.48f, 1f);
        StretchToParent(label.rectTransform, 12f, 6f, 12f, 6f);
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

    private static void AddPanelLabel(Transform parent, Font font, string label)
    {
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
        Image panel = CreateImage(name, parent, new Color(0.075f, 0.095f, 0.085f, 0.96f));
        var outline = panel.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0.98f, 0.74f, 0.32f, 0.22f);
        outline.effectDistance = new Vector2(2f, -2f);
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
        textComponent.resizeTextForBestFit = false;

        return textComponent;
    }

    private static Button CreateButton(string name, Transform parent, Font font, string label, int fontSize)
    {
        Image buttonImage = CreateImage(name, parent, new Color(0.16f, 0.23f, 0.2f, 1f));
        var outline = buttonImage.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0.98f, 0.74f, 0.32f, 0.2f);
        outline.effectDistance = new Vector2(1f, -1f);
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
        buttonText.color = new Color(0.92f, 0.98f, 0.9f, 1f);
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
