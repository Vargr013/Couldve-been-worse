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

public sealed class MissionState
{
    public const int MaxSupervisorPatience = 5;

    public int RoundNumber { get; private set; }
    public int RiskLevel { get; private set; }
    public int CorrectDecisions { get; private set; }
    public int SupervisorPatience { get; private set; } = MaxSupervisorPatience;
    public InterceptClassification CurrentHiddenTruth { get; private set; }
    public string CurrentIntercept { get; private set; } = string.Empty;

    public void StartNextRound(InterceptClassification hiddenTruth)
    {
        RoundNumber++;
        CurrentHiddenTruth = hiddenTruth;
        CurrentIntercept = string.Empty;
    }

    public void SetCurrentIntercept(string intercept)
    {
        CurrentIntercept = intercept;
    }

    public DecisionResult ResolveReply(GeneratedReplyOption option)
    {
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
        }

        return new DecisionResult(
            wasCorrect,
            previousRisk,
            RiskLevel,
            previousPatience,
            SupervisorPatience);
    }

    private static int Clamp(int value, int min, int max)
    {
        return System.Math.Min(max, System.Math.Max(min, value));
    }
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
