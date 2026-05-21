# Signal Intercept: Ollama Integration Plan

## 1. Purpose

Signal Intercept uses a locally hosted Large Language Model through Ollama as a live gameplay system. The model generates fictional operation content during play, while Unity controls the rules and state.

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
- Text quality can still feel generic; text refinement is planned for the next sprint.
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
