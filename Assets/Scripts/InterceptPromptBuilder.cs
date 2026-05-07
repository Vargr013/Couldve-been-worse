public static class InterceptPromptBuilder
{
    public static string BuildPrompt()
    {
        return "Generate one fictional intercepted combat/security communication for a modern intelligence analysis game.\n\n" +
               "Requirements:\n" +
               "- Write only the intercepted transmission.\n" +
               "- Keep it between 12 and 35 words.\n" +
               "- Make it ambiguous but interpretable.\n" +
               "- Do not reveal whether the source is friendly, hostile, or deceptive.\n" +
               "- Do not mention real countries, real conflicts, real organisations, or real people.\n" +
               "- Do not include analysis, labels, bullet points, or explanations.\n" +
               "- Do not use extremely comical/absurd content or language.\n" +
               "- Keep it grounded and realistic to a modern conflict environment.";
    }

    public static string BuildRetryPrompt()
    {
        return BuildPrompt() + "\n\n" +
               "Important: The previous output failed validation. Return only a radio-style transmission line. " +
               "Do not use the words friendly, enemy, hostile, or deception.";
    }
}
