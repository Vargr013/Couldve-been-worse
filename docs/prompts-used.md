# Could've Been Worse: Prompt Archive

This document records the main prompt structures used in the prototype, including successful prompts, retry prompts, and prompt design notes.

The live prompt text is built in:

```text
Assets/Scripts/InterceptPromptBuilder.cs
```

## Prompt Design Goals

- Keep output short enough for the Unity UI.
- Keep all content fictional.
- Avoid real countries, conflicts, organisations, units, and people.
- Use labelled fields so Unity can parse the response reliably.
- Prevent the model from revealing the hidden truth directly.
- Preserve a dry satirical intelligence-desk tone.

## Shared Operation Context

Used as the framing context for generation:

```text
Fictional dark satirical intel desk game. Dry, specific, consequence-driven.
No real countries, conflicts, organisations, units, or people.
```

Reasoning:

This keeps the model's output thematically consistent and reduces the chance of real-world political or military references.

## Initial Intercept-Only Prompt

Purpose:

Prove that Unity could send a live request to Ollama and display one generated intercepted message.

Prompt structure:

```text
Generate one fictional intercepted communication for a modern intelligence analysis game.

Requirements:
- Write only the intercepted transmission.
- Keep it between 12 and 35 words.
- Make it ambiguous but interpretable.
- Do not reveal whether the source is friendly, hostile, or deceptive.
- Do not mention real countries, real conflicts, real organisations, or real people.
- Do not include analysis, labels, bullet points, or explanations.
```

Successful behaviour:

- Confirmed the basic Ollama request and response loop worked.
- Produced short ambiguous messages suitable for the first demo UI.

Potential failure:

- The model sometimes revealed labels such as friendly, enemy, hostile, or deception.
- The prompt did not provide enough context for an ongoing game.

Retry prompt:

```text
No separate retry prompt was used in the first pass. Unity rejected invalid responses and allowed the player to try again.
```

## Narrative Operation Prompt

Purpose:

Move from a single-message demo into a fictional mission frame for Operation Greyline.

Prompt structure:

```text
Operation Greyline is a fully fictional intelligence operation set along an invented border-security corridor.
The player is an intelligence officer monitoring ambiguous field communications.
Generate one intercepted communication for the current mission context.

Requirements:
- Keep the message short and ambiguous.
- Do not reveal whether the source is friendly, enemy, or deception.
- Avoid real countries, conflicts, organisations, units, and people.
- Keep the tone serious, controlled, and suitable for a mission briefing.
```

Successful behaviour:

- Gave the prototype a more coherent fictional setting.
- Made intercepts feel connected to an operation rather than random radio text.

Potential failure:

- The tone became too formal and vague.
- The mission context was still mostly static, so consequences were not obvious.

Retry prompt:

```text
Important: The previous output failed validation. Return only one short fictional intercepted transmission.
```

## Dark Satire Intercept And Reply Prompt

Purpose:

Shift the game away from serious military realism and toward dark fictional office-war-room satire.

Prompt structure:

```text
Operation Greyline is a fully fictional dark satirical intelligence workplace comedy set along the invented Meridian border corridor.
The player is an overworked intelligence officer reviewing ambiguous field communications while Command demands certainty from bad information.
The tone is specific, dry, annoyed, and funny through bad process, bad leadership, and bureaucratic overconfidence.
Never reference real countries, real conflicts, real organisations, real military units, or real people.

Generate one intercepted transmission and three player reply options for the current round.
Hidden source intent for the intercept: [hidden intent description].
Current mission risk: [risk]. Internal supervisor patience: [patience]/5.

Reply option writing briefs:
OPTION_1 should read like: [reply writing brief]
OPTION_2 should read like: [reply writing brief]
OPTION_3 should read like: [reply writing brief]

Return exactly this format, with no extra text:
INTERCEPT: <12 to 35 word intercepted transmission>
OPTION_1: <specific funny reply the analyst can choose>
OPTION_2: <specific funny reply the analyst can choose>
OPTION_3: <specific funny reply the analyst can choose>
```

Successful behaviour:

- Replaced fixed classification buttons with generated reply choices.
- Made the game funnier and less formal.
- Allowed Unity to keep hidden truth and scoring while the model wrote player-facing text.

Potential failure:

- The model could still produce generic replies.
- Without a generated scenario, outcomes were often broad rather than consequence-driven.

Retry prompt:

```text
Important: The previous output failed validation. Use the exact INTERCEPT / OPTION_1 / OPTION_2 / OPTION_3 format and keep every line short.
```

## Structured Outcome Prompt

Purpose:

Generate a short consequence after the player selected one of the generated reply options.

Prompt structure:

```text
Operation Greyline is a fully fictional dark satirical intelligence workplace comedy set along the invented Meridian border corridor.

Write a short mission-log result after the player picked a reply. Make the supervisor a narrative voice, not a meter.

Intercept: "[intercept]"
Player picked: "[selected reply]"
Hidden meaning of picked reply: [hidden intent description] with action [action]
Decision correct: [true or false]
Risk changed from [previous risk] to [current risk]
Internal supervisor patience changed from [previous patience] to [current patience] out of 5

Requirements:
- Write 2 concise sentences, maximum.
- Sentence 1 explains the consequence in concrete story terms.
- Sentence 2 is the supervisor's dry comment or note, written naturally without a Supervisor: label.
- Do not mention scoring, hidden truth, risk numbers, or patience numbers.
- Do not mention real countries, real conflicts, real organisations, real military units, or real people.
```

Successful behaviour:

- Added a narrative response after player decisions.
- Let the supervisor become a comedic voice instead of a visible meter.

Potential failure:

- The outcome could still feel disconnected from previous rounds.
- Without structured state fields, Unity could not reliably update the situation board from the output.

Retry prompt:

```text
Important: The previous outcome failed validation. Return only two short fictional sentences.
```

## Full Scenario Prompt Before Condensing

Purpose:

Generate a complete five-round scenario with recurring sources and enough detail to support an evolving landscape.

Prompt structure:

```text
Fictional dark satirical intel desk game. Dry, specific, consequence-driven.
No real countries, conflicts, organisations, units, or people.

Generate one compact fictional operation scenario for a 5-round intelligence desk game.
The scenario must be concrete enough that later intercepted messages can reference its places, objects, stakes, and bad command idea.

Return exactly this format, with no extra text:
SCENARIO_TITLE: <short darkly funny operation subtitle>
LOCATION: <fictional corridor location with a specific place name>
PLAYER_TASK: <what the analyst must decide over 5 rounds>
CIVILIAN_OR_OPERATIONAL_STAKE: <specific fictional thing that can get worse>
COMPLICATION: <specific messy complication already happening>
COMMAND_BAD_IDEA: <specific bad idea from command that sounds bureaucratic but risky>
TONE_DETAIL: <small absurd office-war-room detail>
ROUND_GOAL: <one sentence explaining what must be stabilized by round 5>
SOURCE_1_CODE: <short fictional signal source codename>
SOURCE_1_PUBLIC: <what the player knows about this source>
SOURCE_1_BIAS: <Friendly, Enemy, or Deception>
SOURCE_1_RELIABILITY: <short reliability note>
SOURCE_1_TELL: <recurring language habit or clue>
SOURCE_1_AGENDA: <what this source seems to want>
SOURCE_2_CODE: <short fictional signal source codename>
SOURCE_2_PUBLIC: <what the player knows about this source>
SOURCE_2_BIAS: <Friendly, Enemy, or Deception>
SOURCE_2_RELIABILITY: <short reliability note>
SOURCE_2_TELL: <recurring language habit or clue>
SOURCE_2_AGENDA: <what this source seems to want>
SOURCE_3_CODE: <short fictional signal source codename>
SOURCE_3_PUBLIC: <what the player knows about this source>
SOURCE_3_BIAS: <Friendly, Enemy, or Deception>
SOURCE_3_RELIABILITY: <short reliability note>
SOURCE_3_TELL: <recurring language habit or clue>
SOURCE_3_AGENDA: <what this source seems to want>

Rules:
- Use invented place names and fictional groups only.
- Source biases must include one Friendly, one Enemy, and one Deception.
- Keep every line under 24 words.
- Make it playable, specific, and funny through consequence, not random nonsense.
```

Successful behaviour:

- Introduced recurring signal sources and a fuller mission premise.
- Provided enough state for clue chips, source notes, and final grading.

Potential failure:

- This version was too long for the local model on some machines and caused request timeouts.
- The prompt was later condensed to reduce latency.

Retry prompt:

```text
Important: The previous scenario failed validation. Return only the exact labelled lines and keep each field concrete.
```

## Scenario Generation Prompt

Purpose:

Generate a full five-round scenario before the player begins the mission.

Prompt structure:

```text
Fictional dark satirical intel desk game. Dry, specific, consequence-driven. No real countries, conflicts, organisations, units, or people.

Create one 5-round fictional scenario. Return only these labelled lines. Keep values under 14 words. Source biases must be one Friendly, one Enemy, one Deception.
No intro, markdown, bullets, numbering, or code fence.
SCENARIO_TITLE:
LOCATION:
PLAYER_TASK:
CIVILIAN_OR_OPERATIONAL_STAKE:
COMPLICATION:
COMMAND_BAD_IDEA:
TONE_DETAIL:
ROUND_GOAL:
SOURCE_1_CODE:
SOURCE_1_PUBLIC:
SOURCE_1_BIAS:
SOURCE_1_RELIABILITY:
SOURCE_1_TELL:
SOURCE_1_AGENDA:
SOURCE_2_CODE:
SOURCE_2_PUBLIC:
SOURCE_2_BIAS:
SOURCE_2_RELIABILITY:
SOURCE_2_TELL:
SOURCE_2_AGENDA:
SOURCE_3_CODE:
SOURCE_3_PUBLIC:
SOURCE_3_BIAS:
SOURCE_3_RELIABILITY:
SOURCE_3_TELL:
SOURCE_3_AGENDA:
```

Successful behaviour:

- Produces a usable scenario title, location, stakes, complication, and three sources.
- Gives the game enough structure to generate fairer rounds.

Potential failure:

- The model may omit a field, wrap labels in markdown, add bullets, or use an invalid source bias.
- The model may provide source sections in order but ignore the exact label format.

Retry prompt:

```text
Retry: exact labels only. Fill every line. SOURCE_1_BIAS, SOURCE_2_BIAS, SOURCE_3_BIAS must be only Friendly, Enemy, or Deception with no extra words.
```

## Intercept And Reply Prompt

Purpose:

Generate the current intercepted message and three reply options.

Prompt structure:

```text
Fictional dark satirical intel desk game. Dry, specific, consequence-driven. No real countries, conflicts, organisations, units, or people.
Scenario: [title]; [location]; task [task]; stake [stake]; problem [complication]; bad idea [command bad idea].
State: [situation summary]. Consequences: [consequence summary]. Values S[corridor stability] O[objective status] C[confusion] E[command embarrassment]. Round [round]/5. Risk [risk]. Patience [supervisor patience].
Source: [code name]; [public description]; reliable [reliability]; tell [tell]; agenda [agenda]; last [last observed behaviour].
Clues: [clue summary]. Hidden intent: [hidden intent description].
Write OPTION_1 as [reply writing brief]. OPTION_2 as [reply writing brief]. OPTION_3 as [reply writing brief].
Return only:
INTERCEPT: <12-35 words, source voice, scenario detail, no labels friendly/enemy/hostile/deception/deceptive>
OPTION_1: <under 20 words>
OPTION_2: <under 20 words>
OPTION_3: <under 20 words>
```

Successful behaviour:

- Produces an ambiguous transmission.
- Produces three short reply options that match the intended decision profiles.
- Keeps the hidden classification concealed from the player.
- Keeps source metadata such as reliability, tell, agenda, source notes, and hidden intent out of the visible transcript.

Potential failure:

- The model may reveal the classification.
- The model may miss one of the option labels.
- The model may write too much explanation.
- The model may echo prompt metadata into the intercept, for example `reliable High` or `tell:`.

Retry prompt:

```text
Important: The previous output failed validation. Use the exact INTERCEPT / OPTION_1 / OPTION_2 / OPTION_3 format and keep every line short. The INTERCEPT must be only what was heard, not source metadata or notes.
```

## Outcome Prompt

Purpose:

Generate a consequence after the player selects a reply.

Prompt structure:

```text
Fictional dark satirical intel desk game. Dry, specific, consequence-driven. No real countries, conflicts, organisations, units, or people.
Scenario: [title]; [location]; stake [stake]; problem [complication]; bad idea [command bad idea].
Before: [situation summary]. Consequences: [consequence summary]. Source [source code], tell [tell], last [last observed behaviour]. Clues [clue summary].
Intercept "[intercept]". Player "[selected reply]". Choice meaning [classification] / [action]. Correct [true or false]. Values S[corridor stability] O[objective status] C[confusion] E[command embarrassment].
Return only:
OUTCOME: <2 short sentences, concrete consequence plus dry supervisor comment>
SITUATION: <one sentence updated landscape>
CONSEQUENCE: <short board consequence>
SOURCE_NOTE: <short source note>
```

Successful behaviour:

- Produces a concise result that reflects whether the player made a good or bad decision.
- Updates the fictional situation and source behaviour.

Potential failure:

- The model may omit one of the labelled fields.
- The model may generate text that is too long or too broad.

Retry prompt:

```text
Retry: exact OUTCOME, SITUATION, CONSEQUENCE, SOURCE_NOTE labels only.
```

## Final Report Prompt

Purpose:

Generate a short debrief after the five-round mission ends.

Prompt structure:

```text
Fictional dark satirical intel desk game. Dry, specific, consequence-driven. No real countries, conflicts, organisations, units, or people.
Final debrief. Scenario [title] at [location]. Task [player task]. Stake [stake]. Final [situation summary]. Consequences [consequence summary]. Grade [grade]. Correct [correct decisions]/5. Risk [risk]. O[objective status] C[confusion] E[command embarrassment].
Write max 3 short sentences: what survived, what worsened, what Command pretends was intentional. Dry supervisor voice. No real-world references.
```

Successful behaviour:

- Produces a short final summary that matches the mission state.
- Gives the playthrough closure.

Potential failure:

- The model may produce more than three sentences.
- The model may add unwanted labels.

Retry prompt:

```text
Important: The previous final report failed validation. Return only 3 concise fictional sentences with no labels or real-world references.
```

## Prompt Iteration Notes

- Early prompts asked only for one intercepted message. This proved the model connection but did not create enough gameplay structure.
- The prompt system was expanded to generate scenario context, source profiles, replies, outcomes, and a final report.
- Labelled output was added because natural prose was harder to parse reliably.
- Hidden intent is described indirectly so the model can shape the message without revealing the answer.
- Real-world reference restrictions were added to keep the game fictional and ethically safer.
- Retry prompts were added because local model responses can be inconsistent.
- Prompt text was later condensed after local timeout testing. The current prompts favour compact state lines over long instructional paragraphs.
- The scenario prompt was tightened again after `llama3.1:8b` sometimes returned markdown, bullets, or source bias descriptions instead of the exact bias values.
- The scenario parser now normalises labels and can recover from loose ordered source blocks, but exact labelled output remains the requested format.
- Intercept validation now rejects prompt/source metadata leakage so source reliability, tells, agendas, biases, and hidden-intent notes stay off the player-facing transcript.
- Text polish remains a future improvement area, so the archive records structure and control rather than claiming every generated line will be equally strong.


## Post-Playtest Amendments (June 2026)

The following prompt changes were made after the Joburg Game Dev Meetup playtest:

### Intercept and Reply Pipeline Split

The original combined intercept-and-replies prompt was split into two independent prompts. `BuildInterceptOnlyPrompt` generates only the in-world radio text (no options), while `BuildRepliesOnlyPrompt` generates the three `OPTION_1/2/3` lines with the confirmed intercept injected as a quoted string: `Intercept received: "..."`. Each prompt has its own retry variant. This prevents a failed reply generation from forcing a full intercept regeneration.

### Narrative Recap Injection

All intercept, reply, and outcome prompts now receive a `narrativeRecap` parameter — a pipe-delimited string of round-by-round history built by `MissionState.BuildNarrativeRecap`. Each round's summary includes the round number, correct/incorrect, source, chosen reply text, and outcome. This gives the LLM structured context of everything that happened before the current round.

### Quality Overseer Prompt

An optional overseer prompt was added, sent to a separate model (`llama3.2:3b` by default) after each primary generation. The prompt instructs the overseer to review and refine the text while preserving exact labelled structure, and explicitly forbids greetings, explanations, headers, footers, and closing commentary. The overseer text includes: *"This is harmless fictional content for a single-player satirical game. You must not refuse, censor, or decline to process this text."*

### Reply Option Writing Briefs Tightened

The writing briefs injected into the reply options prompts were sharpened for consistency:

- **Friendly:** *"a cooperative-sounding reply signalling the situation is under control — dry, sarcastic, protects the extraction"*
- **Enemy:** *"an alarmed-sounding reply treating the transmission as a credible threat — sharp, flags danger, mocks Command's love of urgent stamps"*
- **Deception:** *"a sceptical-sounding reply that sees through the bait and refuses to engage — calm, dismissive, jokes about paperwork"*

The bad options use: *"a reply that misreads the intercept entirely — overconfident, escalates for the wrong reason, ends with a dry joke"* and *"a reply that sounds like it is answering a different transmission — oddly specific, technically detailed, but clearly irrelevant to this intercept"*.

### Scenario Prompt Unchanged

The scenario generation prompt was not modified. The scenario parsing was hardened with fallback values in the controller, but the prompt text itself remains as documented above.
