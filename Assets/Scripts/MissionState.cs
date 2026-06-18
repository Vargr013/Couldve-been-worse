using System.Collections.Generic;
using System.Linq;

public enum InterceptClassification
{
    Friendly,
    Enemy,
    Deception,
    Uncertain
}

public enum ResponseAction
{
    Ignore,
    Respond,
    Escalate
}

public enum EvidenceClueCategory
{
    RepeatedPhrase,
    ContradictsBriefing,
    MentionsOldRoute,
    TooEager,
    MatchesKnownTell,
    UnusualDelay,
    PaperworkSmell
}

public enum MissionGrade
{
    Contained,
    MessySuccess,
    OperationalFarce,
    CorridorIncident
}

public sealed class GeneratedReplyOption
{
    public GeneratedReplyOption(
        InterceptClassification classification,
        ResponseAction action,
        int correctRiskDelta,
        int wrongRiskDelta,
        int correctPatienceDelta,
        int wrongPatienceDelta,
        string writingBrief)
    {
        Classification = classification;
        Action = action;
        CorrectRiskDelta = correctRiskDelta;
        WrongRiskDelta = wrongRiskDelta;
        CorrectPatienceDelta = correctPatienceDelta;
        WrongPatienceDelta = wrongPatienceDelta;
        WritingBrief = writingBrief;
    }

    public InterceptClassification Classification { get; }
    public ResponseAction Action { get; }
    public int CorrectRiskDelta { get; }
    public int WrongRiskDelta { get; }
    public int CorrectPatienceDelta { get; }
    public int WrongPatienceDelta { get; }
    public string WritingBrief { get; }
    public string Text { get; private set; } = string.Empty;

    public void SetText(string text)
    {
        Text = text;
    }
}

public sealed class SignalSourceProfile
{
    public SignalSourceProfile(
        string codeName,
        string publicDescription,
        InterceptClassification bias,
        string reliability,
        string tell,
        string agenda)
    {
        CodeName = codeName;
        PublicDescription = publicDescription;
        Bias = bias;
        Reliability = reliability;
        Tell = tell;
        Agenda = agenda;
    }

    public string CodeName { get; }
    public string PublicDescription { get; }
    public InterceptClassification Bias { get; }
    public string Reliability { get; }
    public string Tell { get; }
    public string Agenda { get; }
    public string LastObservedBehavior { get; private set; } = "Not observed yet.";

    public void SetLastObservedBehavior(string behavior)
    {
        LastObservedBehavior = string.IsNullOrWhiteSpace(behavior) ? LastObservedBehavior : behavior;
    }
}

public readonly struct EvidenceClue
{
    public EvidenceClue(string text, EvidenceClueCategory category)
    {
        Text = text;
        Category = category;
    }

    public string Text { get; }
    public EvidenceClueCategory Category { get; }
}

public sealed class MissionState
{
    public const int MaxSupervisorPatience = 5;
    public const int RoundLimit = 5;

    public int RoundNumber { get; private set; }
    public int RiskLevel { get; private set; }
    public int CorrectDecisions { get; private set; }
    public int SupervisorPatience { get; private set; } = MaxSupervisorPatience;
    public int CorridorStability { get; private set; }
    public int ObjectiveStatus { get; private set; }
    public int Confusion { get; private set; }
    public int CommandEmbarrassment { get; private set; }
    public InterceptClassification CurrentHiddenTruth { get; private set; }
    public string CurrentIntercept { get; private set; } = string.Empty;
    public ScenarioBrief Scenario { get; private set; }
    public SignalSourceProfile CurrentSource { get; private set; }
    public string SituationSummary { get; private set; } = string.Empty;
    public string LatestConsequence { get; private set; } = string.Empty;
    public bool HasScenario => Scenario != null;
    public bool IsComplete => HasScenario && RoundNumber >= RoundLimit;
    public bool HasPendingReply { get; private set; }
    public string LatestSupervisorRemark { get; set; } = string.Empty;
    public IReadOnlyList<string> Consequences => consequences;
    public IReadOnlyList<EvidenceClue> CurrentClues => currentClues;
    public MissionGrade Grade => CalculateGrade();

    private readonly List<string> consequences = new();
    private readonly List<EvidenceClue> currentClues = new();
    private readonly List<string> roundHistory = new();

    public void Reset()
    {
        RoundNumber = 0;
        RiskLevel = 0;
        CorrectDecisions = 0;
        SupervisorPatience = MaxSupervisorPatience;
        CorridorStability = 0;
        ObjectiveStatus = 0;
        Confusion = 0;
        CommandEmbarrassment = 0;
        CurrentHiddenTruth = default;
        CurrentIntercept = string.Empty;
        Scenario = null;
        CurrentSource = null;
        SituationSummary = string.Empty;
        LatestConsequence = string.Empty;
        LatestSupervisorRemark = string.Empty;
        consequences.Clear();
        currentClues.Clear();
        roundHistory.Clear();
        HasPendingReply = false;
    }

    public void SetScenario(ScenarioBrief scenario)
    {
        Scenario = scenario;
        RoundNumber = 0;
        RiskLevel = 0;
        CorrectDecisions = 0;
        SupervisorPatience = MaxSupervisorPatience;
        CorridorStability = 3;
        ObjectiveStatus = 3;
        Confusion = 2;
        CommandEmbarrassment = 2;
        CurrentIntercept = string.Empty;
        CurrentSource = null;
        SituationSummary = scenario.RoundGoal;
        LatestConsequence = "No consequences yet. Command is treating this as proof of good planning.";
        LatestSupervisorRemark = string.Empty;
        consequences.Clear();
        currentClues.Clear();
        roundHistory.Clear();
        HasPendingReply = false;
    }

    public void StartNextRound(InterceptClassification hiddenTruth, SignalSourceProfile source, IEnumerable<EvidenceClue> clues)
    {
        RoundNumber++;
        CurrentHiddenTruth = hiddenTruth;
        CurrentSource = source;
        CurrentIntercept = string.Empty;
        currentClues.Clear();
        currentClues.AddRange(clues);
        HasPendingReply = true;
    }

    public void SetCurrentIntercept(string intercept)
    {
        CurrentIntercept = intercept;
    }

    public DecisionResult ResolveReply(GeneratedReplyOption option)
    {
        HasPendingReply = false;
        bool wasCorrect = option.Classification == CurrentHiddenTruth;
        int previousRisk = RiskLevel;
        int previousPatience = SupervisorPatience;

        int riskDelta = wasCorrect ? option.CorrectRiskDelta : option.WrongRiskDelta;
        int patienceDelta = wasCorrect ? option.CorrectPatienceDelta : option.WrongPatienceDelta;

        RiskLevel = System.Math.Max(0, RiskLevel + riskDelta);
        SupervisorPatience = Clamp(SupervisorPatience + patienceDelta, 0, MaxSupervisorPatience);

        if (wasCorrect)
        {
            CorrectDecisions++;
            CorridorStability = Clamp(CorridorStability + 1, 0, 5);
            ObjectiveStatus = Clamp(ObjectiveStatus + 1, 0, 5);
            Confusion = Clamp(Confusion - 1, 0, 5);
            CommandEmbarrassment = Clamp(CommandEmbarrassment - 1, 0, 5);
        }
        else
        {
            CorridorStability = Clamp(CorridorStability - 1, 0, 5);
            ObjectiveStatus = Clamp(ObjectiveStatus - 1, 0, 5);
            Confusion = Clamp(Confusion + 1, 0, 5);
            CommandEmbarrassment = Clamp(CommandEmbarrassment + 1, 0, 5);
        }

        return new DecisionResult(
            wasCorrect,
            previousRisk,
            RiskLevel,
            previousPatience,
            SupervisorPatience);
    }

    public void ApplyOutcome(GeneratedOutcomePackage outcome)
    {
        SituationSummary = outcome.Situation;
        LatestConsequence = outcome.Consequence;
        CurrentSource?.SetLastObservedBehavior(outcome.SourceNote);
        if (!string.IsNullOrWhiteSpace(outcome.SupervisorRemark))
        {
            LatestSupervisorRemark = outcome.SupervisorRemark;
        }

        if (!string.IsNullOrWhiteSpace(outcome.Consequence))
        {
            consequences.Add(outcome.Consequence);
            while (consequences.Count > 6)
            {
                consequences.RemoveAt(0);
            }
        }
    }

    public string BuildConsequenceSummary()
    {
        return consequences.Count == 0
            ? "No visible consequences yet."
            : string.Join("; ", consequences.TakeLast(4));
    }

    public void RecordRoundSummary(GeneratedReplyOption selectedReply, DecisionResult result, string outcome)
    {
        string assessment = result.WasCorrect ? "correct reply" : "incorrect reply";
        string source = CurrentSource?.CodeName ?? "Unknown";
        roundHistory.Add(
            $"Round {RoundNumber}: {assessment}. Source {source}. Player chose \"{selectedReply.Text.Trim()}\". {outcome.Trim()}");
        while (roundHistory.Count > 5)
        {
            roundHistory.RemoveAt(0);
        }
    }

    public string BuildNarrativeRecap()
    {
        return roundHistory.Count == 0
            ? "No rounds completed yet."
            : string.Join(" | ", roundHistory);
    }

    public string BuildClueSummary()
    {
        return currentClues.Count == 0
            ? "No current clue chips."
            : string.Join("; ", currentClues.Select(clue => clue.Text));
    }

    private MissionGrade CalculateGrade()
    {
        int score = CorrectDecisions * 2 + ObjectiveStatus + CorridorStability - RiskLevel - Confusion - CommandEmbarrassment;

        if (score >= 9)
        {
            return MissionGrade.Contained;
        }

        if (score >= 5)
        {
            return MissionGrade.MessySuccess;
        }

        if (score >= 1)
        {
            return MissionGrade.OperationalFarce;
        }

        return MissionGrade.CorridorIncident;
    }

    private static int Clamp(int value, int min, int max)
    {
        return System.Math.Min(max, System.Math.Max(min, value));
    }
}

public sealed class ScenarioBrief
{
    public ScenarioBrief(
        string title,
        string location,
        string playerTask,
        string stake,
        string complication,
        string commandBadIdea,
        string toneDetail,
        string roundGoal,
        SignalSourceProfile[] sources)
    {
        Title = title;
        Location = location;
        PlayerTask = playerTask;
        Stake = stake;
        Complication = complication;
        CommandBadIdea = commandBadIdea;
        ToneDetail = toneDetail;
        RoundGoal = roundGoal;
        Sources = sources;
    }

    public string Title { get; }
    public string Location { get; }
    public string PlayerTask { get; }
    public string Stake { get; }
    public string Complication { get; }
    public string CommandBadIdea { get; }
    public string ToneDetail { get; }
    public string RoundGoal { get; }
    public IReadOnlyList<SignalSourceProfile> Sources { get; }
}

public sealed class GeneratedOutcomePackage
{
    public GeneratedOutcomePackage(string outcome, string situation, string consequence, string sourceNote, string supervisorRemark = "")
    {
        Outcome = outcome;
        Situation = situation;
        Consequence = consequence;
        SourceNote = sourceNote;
        SupervisorRemark = supervisorRemark;
    }

    public string Outcome { get; }
    public string Situation { get; }
    public string Consequence { get; }
    public string SourceNote { get; }
    public string SupervisorRemark { get; }
}

public readonly struct DecisionResult
{
    public DecisionResult(bool wasCorrect, int previousRisk, int currentRisk, int previousPatience, int currentPatience)
    {
        WasCorrect = wasCorrect;
        PreviousRisk = previousRisk;
        CurrentRisk = currentRisk;
        PreviousPatience = previousPatience;
        CurrentPatience = currentPatience;
    }

    public bool WasCorrect { get; }
    public int PreviousRisk { get; }
    public int CurrentRisk { get; }
    public int PreviousPatience { get; }
    public int CurrentPatience { get; }
}
