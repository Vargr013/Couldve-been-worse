public static class InterceptPromptBuilder
{
    private const string OperationContext =
        "Operation Greyline is a fully fictional dark satirical intelligence workplace comedy set along the invented Meridian border corridor. " +
        "The player is an overworked intelligence officer reviewing ambiguous field communications while Command demands certainty from bad information. " +
        "The tone is specific, dry, annoyed, and funny through bad process, bad leadership, and bureaucratic overconfidence. " +
        "Never reference real countries, real conflicts, real organisations, real military units, or real people.";

    public static string BuildMissionBriefingText()
    {
        return "OPERATION GREYLINE\n\n" +
               "Command wants confident answers by lunch. The transmissions are short, contradictory, and occasionally sound like they were dictated into a filing cabinet.\n\n" +
               "Read each intercept, pick one reply, and let the supervisor explain how your choice has somehow become a process issue.";
    }

    public static string BuildInterceptAndRepliesPrompt(
        InterceptClassification hiddenTruth,
        GeneratedReplyOption[] replyOptions,
        int roundNumber,
        int riskLevel,
        int supervisorPatience)
    {
        return OperationContext + "\n\n" +
               $"Generate one intercepted transmission and three player reply options for round {roundNumber}.\n" +
               $"Hidden source intent for the intercept: {DescribeHiddenTruth(hiddenTruth)}.\n" +
               $"Current mission risk: {riskLevel}. Internal supervisor patience: {supervisorPatience}/{MissionState.MaxSupervisorPatience}.\n\n" +
               "Reply option writing briefs:\n" +
               $"OPTION_1 should read like: {replyOptions[0].WritingBrief}\n" +
               $"OPTION_2 should read like: {replyOptions[1].WritingBrief}\n" +
               $"OPTION_3 should read like: {replyOptions[2].WritingBrief}\n\n" +
               "Return exactly this format, with no extra text:\n" +
               "INTERCEPT: <12 to 35 word intercepted transmission>\n" +
               "OPTION_1: <specific funny reply the analyst can choose>\n" +
               "OPTION_2: <specific funny reply the analyst can choose>\n" +
               "OPTION_3: <specific funny reply the analyst can choose>\n\n" +
               "Rules:\n" +
               "- Do not reveal hidden source intent or classification labels.\n" +
               "- Do not use the words friendly, enemy, hostile, deception, or deceptive in the intercept.\n" +
               "- Do not mention real countries, real conflicts, real organisations, real military units, or real people.\n" +
               "- Do not write science-fiction signals, magic energy, inspirational slogans, or all-caps shouting.\n" +
               "- Make the replies concrete, playable, and funny rather than formal or vague.\n" +
               "- Keep each reply option under 24 words.\n" +
               "- Avoid generic replies like escalate, ignore, respond, classify, investigate, or monitor unless they are part of a more specific joke.";
    }

    public static string BuildInterceptAndRepliesRetryPrompt(
        InterceptClassification hiddenTruth,
        GeneratedReplyOption[] replyOptions,
        int roundNumber,
        int riskLevel,
        int supervisorPatience)
    {
        return BuildInterceptAndRepliesPrompt(hiddenTruth, replyOptions, roundNumber, riskLevel, supervisorPatience) + "\n\n" +
               "Important: The previous output failed validation. Use the exact INTERCEPT / OPTION_1 / OPTION_2 / OPTION_3 format and keep every line short.";
    }

    public static string BuildOutcomePrompt(
        string intercept,
        GeneratedReplyOption selectedReply,
        DecisionResult decisionResult)
    {
        return OperationContext + "\n\n" +
               "Write a short mission-log result after the player picked a reply. Make the supervisor a narrative voice, not a meter.\n\n" +
               $"Intercept: \"{intercept}\"\n" +
               $"Player picked: \"{selectedReply.Text}\"\n" +
               $"Hidden meaning of picked reply: {DescribeHiddenTruth(selectedReply.Classification)} with action {selectedReply.Action}\n" +
               $"Decision correct: {decisionResult.WasCorrect}\n" +
               $"Risk changed from {decisionResult.PreviousRisk} to {decisionResult.CurrentRisk}\n" +
               $"Internal supervisor patience changed from {decisionResult.PreviousPatience} to {decisionResult.CurrentPatience} out of {MissionState.MaxSupervisorPatience}\n\n" +
               "Requirements:\n" +
               "- Write 2 concise sentences, maximum.\n" +
               "- Sentence 1 explains the consequence in concrete story terms.\n" +
               "- Sentence 2 is the supervisor's dry comment or note, written naturally without a Supervisor: label.\n" +
               "- Do not give the supervisor a personal name.\n" +
               "- Do not mention scoring, hidden truth, risk numbers, or patience numbers.\n" +
               "- Do not mention real countries, real conflicts, real organisations, real military units, or real people.\n" +
               "- Do not use bullet points or headings.";
    }

    public static string BuildOutcomeRetryPrompt(string previousPrompt)
    {
        return previousPrompt + "\n\n" +
               "Important: The previous outcome failed validation. Return only two short fictional sentences.";
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
}
