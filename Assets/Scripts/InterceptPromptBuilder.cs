using System.Collections.Generic;

public static class InterceptPromptBuilder
{
    private const string OperationContext =
        "Fictional dark satirical intel desk game. Dry, specific, consequence-driven. No real countries, conflicts, organisations, units, or people.";

    public static string BuildMissionBriefingText()
    {
        return "OPERATION GREYLINE\n\n" +
               "No scenario is loaded yet. Ask the local model to generate today's operational mess, then manage five rounds of bad information with visible consequences.\n\n" +
               "Command still wants confident answers by lunch. This is not a promise that lunch exists.";
    }

    public static string BuildScenarioPrompt()
    {
        return OperationContext + "\n\n" +
               "Create one 5-round fictional scenario. Return only these labelled lines. Keep values under 14 words. Source biases must be one Friendly, one Enemy, one Deception.\n" +
               "SCENARIO_TITLE:\nLOCATION:\nPLAYER_TASK:\nCIVILIAN_OR_OPERATIONAL_STAKE:\nCOMPLICATION:\nCOMMAND_BAD_IDEA:\nTONE_DETAIL:\nROUND_GOAL:\n" +
               "SOURCE_1_CODE:\nSOURCE_1_PUBLIC:\nSOURCE_1_BIAS:\nSOURCE_1_RELIABILITY:\nSOURCE_1_TELL:\nSOURCE_1_AGENDA:\n" +
               "SOURCE_2_CODE:\nSOURCE_2_PUBLIC:\nSOURCE_2_BIAS:\nSOURCE_2_RELIABILITY:\nSOURCE_2_TELL:\nSOURCE_2_AGENDA:\n" +
               "SOURCE_3_CODE:\nSOURCE_3_PUBLIC:\nSOURCE_3_BIAS:\nSOURCE_3_RELIABILITY:\nSOURCE_3_TELL:\nSOURCE_3_AGENDA:";
    }

    public static string BuildScenarioRetryPrompt()
    {
        return BuildScenarioPrompt() + "\n\n" +
               "Retry: exact labels only. Fill every line.";
    }

    public static string BuildInterceptAndRepliesPrompt(
        ScenarioBrief scenario,
        string situationSummary,
        string consequenceSummary,
        InterceptClassification hiddenTruth,
        SignalSourceProfile activeSource,
        string clueSummary,
        GeneratedReplyOption[] replyOptions,
        int roundNumber,
        int riskLevel,
        int supervisorPatience,
        int corridorStability,
        int objectiveStatus,
        int confusion,
        int commandEmbarrassment)
    {
        return OperationContext + "\n" +
               $"Scenario: {scenario.Title}; {scenario.Location}; task {scenario.PlayerTask}; stake {scenario.Stake}; problem {scenario.Complication}; bad idea {scenario.CommandBadIdea}.\n" +
               $"State: {situationSummary}. Consequences: {consequenceSummary}. Values S{corridorStability} O{objectiveStatus} C{confusion} E{commandEmbarrassment}. Round {roundNumber}/{MissionState.RoundLimit}. Risk {riskLevel}. Patience {supervisorPatience}.\n" +
               $"Source: {activeSource.CodeName}; {activeSource.PublicDescription}; reliable {activeSource.Reliability}; tell {activeSource.Tell}; agenda {activeSource.Agenda}; last {activeSource.LastObservedBehavior}.\n" +
               $"Clues: {clueSummary}. Hidden intent: {DescribeHiddenTruth(hiddenTruth)}.\n" +
               $"Write OPTION_1 as {replyOptions[0].WritingBrief}. OPTION_2 as {replyOptions[1].WritingBrief}. OPTION_3 as {replyOptions[2].WritingBrief}.\n" +
               "Return only:\nINTERCEPT: <12-35 words, source voice, scenario detail, no labels friendly/enemy/hostile/deception/deceptive>\nOPTION_1: <under 20 words>\nOPTION_2: <under 20 words>\nOPTION_3: <under 20 words>";
    }

    public static string BuildInterceptAndRepliesRetryPrompt(
        ScenarioBrief scenario,
        string situationSummary,
        string consequenceSummary,
        InterceptClassification hiddenTruth,
        SignalSourceProfile activeSource,
        string clueSummary,
        GeneratedReplyOption[] replyOptions,
        int roundNumber,
        int riskLevel,
        int supervisorPatience,
        int corridorStability,
        int objectiveStatus,
        int confusion,
        int commandEmbarrassment)
    {
        return BuildInterceptAndRepliesPrompt(
                   scenario,
                   situationSummary,
                   consequenceSummary,
                   hiddenTruth,
                   activeSource,
                   clueSummary,
                   replyOptions,
                   roundNumber,
                   riskLevel,
                   supervisorPatience,
                   corridorStability,
                   objectiveStatus,
                   confusion,
                   commandEmbarrassment) + "\n\n" +
               "Important: The previous output failed validation. Use the exact INTERCEPT / OPTION_1 / OPTION_2 / OPTION_3 format and keep every line short.";
    }

    public static string BuildOutcomePrompt(
        ScenarioBrief scenario,
        string situationSummary,
        string consequenceSummary,
        string intercept,
        SignalSourceProfile activeSource,
        string clueSummary,
        GeneratedReplyOption selectedReply,
        DecisionResult decisionResult,
        int corridorStability,
        int objectiveStatus,
        int confusion,
        int commandEmbarrassment)
    {
        return OperationContext + "\n" +
               $"Scenario: {scenario.Title}; {scenario.Location}; stake {scenario.Stake}; problem {scenario.Complication}; bad idea {scenario.CommandBadIdea}.\n" +
               $"Before: {situationSummary}. Consequences: {consequenceSummary}. Source {activeSource.CodeName}, tell {activeSource.Tell}, last {activeSource.LastObservedBehavior}. Clues {clueSummary}.\n" +
               $"Intercept \"{intercept}\". Player \"{selectedReply.Text}\". Choice meaning {DescribeHiddenTruth(selectedReply.Classification)} / {selectedReply.Action}. Correct {decisionResult.WasCorrect}. Values S{corridorStability} O{objectiveStatus} C{confusion} E{commandEmbarrassment}.\n" +
               "Return only:\nOUTCOME: <2 short sentences, concrete consequence plus dry supervisor comment>\nSITUATION: <one sentence updated landscape>\nCONSEQUENCE: <short board consequence>\nSOURCE_NOTE: <short source note>";
    }

    public static string BuildOutcomeRetryPrompt(string previousPrompt)
    {
        return previousPrompt + "\n\n" +
               "Retry: exact OUTCOME, SITUATION, CONSEQUENCE, SOURCE_NOTE labels only.";
    }

    public static string BuildFinalReportPrompt(
        ScenarioBrief scenario,
        string situationSummary,
        string consequenceSummary,
        MissionGrade grade,
        int correctDecisions,
        int riskLevel,
        int objectiveStatus,
        int confusion,
        int commandEmbarrassment)
    {
        return OperationContext + "\n" +
               $"Final debrief. Scenario {scenario.Title} at {scenario.Location}. Task {scenario.PlayerTask}. Stake {scenario.Stake}. Final {situationSummary}. Consequences {consequenceSummary}. Grade {FormatMissionGrade(grade)}. Correct {correctDecisions}/{MissionState.RoundLimit}. Risk {riskLevel}. O{objectiveStatus} C{confusion} E{commandEmbarrassment}.\n" +
               "Write max 3 short sentences: what survived, what worsened, what Command pretends was intentional. Dry supervisor voice. No real-world references.";
    }

    public static string BuildFinalReportRetryPrompt(string previousPrompt)
    {
        return previousPrompt + "\n\n" +
               "Important: The previous final report failed validation. Return only 3 concise fictional sentences with no labels or real-world references.";
    }

    private static string DescribeHiddenTruth(InterceptClassification classification)
    {
        return classification switch
        {
            InterceptClassification.Friendly => "it supports a legitimate extraction or safe movement, without naming that directly",
            InterceptClassification.Enemy => "it suggests an ambush setup or dangerous movement, without naming that directly",
            InterceptClassification.Deception => "it suggests bait, contradiction, or deliberate confusion, without naming that directly",
            _ => "it remains unclear enough that caution is justified"
        };
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
}
