# Could've Been Worse: Ollama Integration Plan

## 1. Purpose

Could've Been Worse uses a locally hosted Large Language Model through Ollama as a live gameplay system. The model generates fictional operation content during play, while Unity controls the rules and state.

The current implementation uses Ollama for:

- Scenario generation.
- Recurring source profile generation.
- Intercept and reply generation.
- Outcome narration.
- Situation/source note updates.
- Final report generation.

The LLM is not a scoring system. It supplies short-form fiction and ambiguity. Unity decides hidden truth, correctness, risk, mission values, and final grade.

## 2. Platform

The project uses:

- Ollama as the local LLM runtime.
- Unity HTTP requests to Ollama's local API.
- The local endpoint `http://localhost:11434/api/generate`.
- `stream: false` so Unity receives one complete response.

Ollama keeps the prototype independent from cloud services during demonstration after the model has been installed.

## 3. Model

Default model:

```text
llama3.1:8b
```

Fallback options for slower machines:

```text
llama3.2:3b
gemma2:2b
mistral
```

The model name is inspector-editable in `SignalInterceptDemoController`.

## 4. Runtime Flow

The scenario now generates automatically when the scene starts.

```text
Scene starts
  |
  v
Unity requests generated scenario + sources
  |
  v
Briefing and situation board unlock
  |
  v
Player generates round intercept
  |
  v
Unity chooses hidden truth, source, clues, and reply profiles
  |
  v
Ollama generates intercept + three replies
  |
  v
Player chooses a reply
  |
  v
Unity evaluates correctness and updates values
  |
  v
Ollama narrates outcome, situation update, consequence, and source note
  |
  v
After round 5, Unity calculates grade and Ollama writes final report
```

## 5. Prompt Types

The live prompt text is built in:

```text
Assets/Scripts/InterceptPromptBuilder.cs
```

Current prompt types:

- `BuildScenarioPrompt`
  - Returns labelled scenario fields and three source profiles.
  - Requests no markdown, bullets, numbering, code fences, or extra bias wording.
- `BuildInterceptAndRepliesPrompt`
  - Uses scenario state, current values, recent consequences, selected source, clues, and hidden intent.
- `BuildOutcomePrompt`
  - Uses the player's selected reply, correctness, source behaviour, clues, and visible values.
- `BuildFinalReportPrompt`
  - Uses mission grade, correct decisions, risk, final situation, and consequence history.
- Retry prompts
  - Request exact labelled output if tolerant parsing or validation fails.

The prompts were condensed after timeout testing. They now use compact context lines instead of long explanatory instructions.

## 6. Structured Output

Scenario output uses exact labelled lines such as:

```text
SCENARIO_TITLE:
LOCATION:
PLAYER_TASK:
SOURCE_1_CODE:
SOURCE_1_DESC:
```

Exact labelled output is still the preferred format. The Unity parser also normalises common local-model formatting drift, including bullets, numbering, markdown emphasis, extra spaces, and alternate label separators. If a scenario response contains the expected values in order but does not keep the labels, Unity can recover from a loose ordered scenario format and source blocks before failing validation.

Intercept output uses:

```text
INTERCEPT:
OPTION_1:
OPTION_2:
OPTION_3:
```

Outcome output uses:

```text
OUTCOME:
SITUATION:
CONSEQUENCE:
SOURCE_NOTE:
```

Labelled output keeps parsing simple and makes validation easier. The tolerant scenario parser is a stability layer for local model inconsistency, not a replacement for the structured prompt contract.

## 7. Validation

Unity validates responses before displaying them.

The game rejects:

- Empty responses.
- Missing required scenario content after exact and tolerant parsing.
- Intercepts that reveal blocked labels such as `friendly`, `enemy`, `hostile`, `deception`, or `deceptive`.
- Output that includes real-world countries, conflicts, organisations, units, or people where detectable.
- Responses that are too malformed for the current flow.

On failure, the UI shows a blocking error and allows retry. The prototype does not silently use fallback content.

## 8. State Ownership

Unity owns:

- Round number.
- Hidden truth.
- Source selection.
- Evidence clue categories.
- Reply profiles.
- Correctness.
- Risk.
- Corridor stability.
- Objective status.
- Confusion.
- Command embarrassment.
- Mission grade.

Ollama owns:

- Fictional wording.
- Satirical tone.
- Scenario detail.
- Intercept phrasing.
- Reply wording.
- Outcome prose.
- Final report prose.

This split makes the game playable and reproducible while still proving live AI generation.

## 9. Failure Handling

Common failure states:

- Ollama is not running.
- The configured model is not installed.
- The model request times out.
- The model omits all recoverable scenario structure.
- The model reveals hidden classification labels.

The UI reports these failures directly. The default timeout is inspector-editable and is clamped in code to support the larger scenario prompt.

## 10. Current Limitations

- Local inference speed depends on the machine and model.
- Text quality can still feel generic, so sharper generated satire is a future polish area.
- Validation cannot catch every possible real-world reference.
- The current build focuses on one five-round loop, not multiple campaign branches.

## 11. Reproducibility Notes

Current recorded setup:

- Ollama version: 0.23.2.
- Installed model name: `llama3.1:8b`.
- Unity version: 6000.3.9f1.
- Test machine: 12th Gen Intel(R) Core(TM) i5-12450H, NVIDIA GeForce RTX 3060 Laptop GPU, 32 GB RAM, Windows 11 Home Single Language 10.0.26200.
- Main playable flow: `SplashScene.unity` loads `OperationGreylineVisualScene.unity`.
- Any later model or timeout changes made in the inspector should be recorded before final submission.


## 12. Amendments (Post-Playtest, June 2026)

The following changes were made after the original plan was written:

**Multi-model per-task routing (F13):** The architecture was expanded from a single default model to per-task model selection. Scenario, intercept, outcome, and report generation each target a dedicated model configured in the Unity inspector. Only one model runs at a time — concurrency was avoided. If a task-specific model field is empty, the default `llama3.1:8b` handles that task as a fallback.

**Intercept and reply pipeline split:** The combined intercept-and-replies prompt was split into two independent calls. `BuildInterceptOnlyPrompt` generates the in-world radio text, which is validated before `BuildRepliesOnlyPrompt` generates the three reply options with the confirmed intercept injected as context. This prevents a failed reply generation from forcing a full intercept regeneration.

**Quality overseer with refusal and chatty-output hardening:** An optional second model (default `llama3.2:3b`) can review and refine every LLM output before display. The overseer prompt was strengthened to forbid greetings, explanations, headers, footers, and closing commentary. Two detection methods were added: `LooksLikeRefusal` (expanded from 8 to 19 refusal patterns including "I can't fulfill this request") and `LooksLikeChattyOverseerOutput` (detects conversational wrapper text like "I can assist you with refining the text"). When the overseer returns a refusal or chatty commentary, the system falls back to the valid raw output. A further guard in `ValidateIntercept` rejects any parsed intercept that still reads as a refusal.

**Narrative recap injection (F12):** `MissionState` now maintains a round-by-round history list. `BuildNarrativeRecap` returns the history as a pipe-delimited string injected into intercept, reply, and outcome prompts, giving the LLM a structured record of everything that happened in prior rounds.

**Bug Squash minigame (F7):** A terminal-themed click-to-squash minigame runs during intercept generation. Bugs spawn on screen during the LLM call and the player clicks to squash them. The minigame starts before the Ollama request and stops cleanly on all exit paths. A session high score appears on the HUD.

**Scenario parsing resilience:** Scenario fields now use `ReadFieldOrDefault` with sensible fallback values instead of `ReadRequiredField`. Source bias parsing uses `TryParseClassification` with slot-based fallbacks (Friendly/Enemy/Deception). A scenario that is mostly well-formed but has one missing field parses successfully rather than triggering a full retry.

**Per-model inspection:** Four inspector fields (`scenarioModelName`, `interceptModelName`, `outcomeModelName`, `reportModelName`) plus `qualityModelName` and an `enableQualityOverseer` toggle control the multi-model pipeline from the Unity editor.
