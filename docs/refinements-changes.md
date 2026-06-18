# Could've Been Worse: Refinements And Changes Log

This document records the main scope changes, design refinements, and AI-assisted decisions made during development.

## 2026-06-18: Feedback-Driven Refinements (Post-Playtest)

Feedback was collected at the Joburg Game Dev Meetup on 11 June 2026 from two playtesters (a hobbyist and an indie developer) and the academic assessor. Three feedback items were selected for implementation based on feasibility, impact, and alignment with the project's design goals.

See `feedback-summary.md` and `critical-feedback.md` for the full feedback record and engagement analysis.

### Softlock Bug: Report Button Locks Player Out of Replies (F11)

**Reported:** Clicking the report/Mission Log button before selecting a reply permanently locks the player out of reply selection.

**Root cause identified:** After `StartNextRound()` incremented the round counter for the fifth and final round, `MissionState.IsComplete` evaluated to `true` before the player had selected a reply. If any code path re-enabled the primary action button or if the visual state drifted, the player could lose reply access. The primary action button text would prematurely show "Generate Final Report" despite a pending reply.

**Fix implemented in `MissionState.cs`:**
- Added `HasPendingReply` boolean property.
- Set `HasPendingReply = true` in `StartNextRound()`.
- Set `HasPendingReply = false` in `ResolveReply()`, `Reset()`, and `SetScenario()`.

**Fix implemented in `SignalInterceptDemoController.cs`:**
- `GenerateIntercept()` now checks `missionState.HasPendingReply` as an additional guard before processing, redirecting to the Decision tab if a reply is still pending.
- `GenerateFinalReport()` now checks `missionState.HasPendingReply` before executing, redirecting to the Decision tab if a reply is pending.
- `HandlePrimaryAction()` adds a top-level guard: if the primary action mode is `GenerateFinalReport` but the visual state is still `AwaitingReply`, the player is redirected to the Decision tab.

**Outcome:** The player can no longer accidentally bypass the reply phase. The primary action button and Mission Log tab can be clicked freely without risk of softlock.

### Report Screen Background Clutter (F2)

**Reported:** The hobbyist playtester had difficulty reading the mission log text because the background behind it was too visually cluttered.

**Fix implemented in `SignalInterceptDemoController.cs`:**
- Added `EnhanceMissionLogReadability()` method, called during scene layout binding to improve the Mission Log panel's text readability regardless of which background sprite is applied.
- The method locates any existing readability plate behind the mission log text (by naming convention) and increases its opacity to 84% with a dark, high-contrast colour (`0.07, 0.09, 0.07`).
- If no plate exists, one is created dynamically behind the text with matching dimensions plus 10px padding, serving as a solid dark backing for improved contrast.
- The plate is inserted as the first sibling so it renders behind the text.

**Outcome:** Mission log text is now readable against busy or patterned panel backgrounds. The fix is non-destructive — it enhances existing plates or adds one, and respects any existing scene hierarchy.

### Text Clipping in Report and Intercept Displays (F3)

**Reported:** Text occasionally clipped or overflowed beyond its container, cutting off content mid-sentence.

**Fix implemented in `SignalInterceptDemoController.cs`:**
- `EnhanceMissionLogReadability()` also reconfigures the mission log text component: `verticalOverflow` is changed from `VerticalWrapMode.Truncate` to `VerticalWrapMode.Overflow`, allowing the full text to render even if it exceeds the container height.
- Font size range tightened to 11–16 (from 13–17) to allow more text to fit within the same area via `resizeTextForBestFit`.

**Limitation acknowledged:** The editable UGUI scene layout provides fixed-height text containers. Switching to `Overflow` mode means text may visually extend beyond the container edge. A full ScrollRect implementation would require scene-level changes and is deferred as a future UX polish item. The current fix prioritises content completeness over visual neatness for assessment purposes.

**Outcome:** No mission log text is truncated. The player can read all logged consequences and outcomes without missing content. For intercept text (which uses the TypewriterTextEffect), the larger transmission text container already provides adequate space.

### Documentation Updates

- `feedback-summary.md` created: structured record of all feedback with aspect mapping, recurring themes, and initial reactions.
- `critical-feedback.md` created: analytical engagement with feedback including expectations, surprises, declined items with feasibility justification, and final judgement.
- `evidence-attendance.md` created: event description, photo reference, attendee notes, and identification of feedback providers.
- `final-reflection.md` created: 777-word reflection on professional engagement, feedback integration, AI collaboration, and ethical considerations.
- This file updated to record feedback-driven code changes.
- `README.md` updated to reference the new feedback documentation.

### LLM Context Retention: Narrative Recap Injection (F12)

**Reported:** The academic assessor noted that during play, the LLM "would struggle to keep context of the scenario," weakening narrative coherence across rounds.

**Root cause:** While the existing prompt builder already passed scenario details, situation summaries, and the last four consequences to each LLM call, the information was compressed into dense single-line summaries. The model had no structured record of what happened in each previous round — which player choices were made, which sources were involved, or how consequences connected to specific decisions. This made it difficult for the LLM to maintain a coherent narrative arc across five rounds.

**Fix implemented in `MissionState.cs`:**
- Added a `roundHistory` list to store a one-line summary of every completed round (round number, correct/incorrect, source, chosen reply text, outcome).
- Added `RecordRoundSummary(selectedReply, result, outcome)` to capture each round's resolution after outcome generation.
- Added `BuildNarrativeRecap()` to return the round history as a pipe-delimited narrative string for prompt injection.
- The history is capped at 5 entries (one full scenario) and cleared on `Reset()` and `SetScenario()`.

**Fix implemented in `InterceptPromptBuilder.cs`:**
- `BuildInterceptAndRepliesPrompt()` now accepts and includes a `narrativeRecap` parameter, placing round history directly before the current state.
- `BuildInterceptAndRepliesRetryPrompt()` signature updated to match.
- `BuildOutcomePrompt()` now also accepts a `narrativeRecap` parameter, giving the outcome generator access to the full story arc when writing consequences.

**Fix implemented in `SignalInterceptDemoController.cs`:**
- `GenerateIntercept()` now passes `missionState.BuildNarrativeRecap()` to both the primary and retry intercept prompts.
- `SelectReply()` calls `missionState.RecordRoundSummary()` after successful outcome generation, and also in the validation-failure and exception paths so context remains consistent even when outcome generation fails.
- `SelectReply()` passes `missionState.BuildNarrativeRecap()` to the outcome prompt.

**Design rationale (F13 declined, F12 addressed):** The academic assessor also suggested using multiple AI models for better control. This was declined because running concurrent Ollama models on consumer hardware would multiply latency and memory requirements beyond the project's "runs on a laptop" goal. Instead, the narrative recap approach addresses the underlying concern (context retention) through improved single-model prompt engineering — keeping the toolset unchanged while making the LLM's output more coherent across rounds.

**Outcome:** Each LLM call now receives a structured history of everything that has happened in the scenario so far, expressed as narrative events rather than just compressed value summaries. This should improve the model's ability to write intercepts, replies, and consequences that feel connected to prior rounds rather than isolated per round.

### Multi-Model Architecture: Per-Task Model Selection (F13)

**Reported:** The academic assessor recommended using more AI models to give better control over the final result.

**Root cause:** The prototype used a single `llama3.1:8b` model for all four LLM tasks (scenario generation, intercept and reply generation, outcome narration, and final report generation). While functional, this treated a general-purpose model as a one-size-fits-all solution. Different tasks require different strengths — scenario generation benefits from creative long-form capability, while intercept generation benefits from structured brevity and speed. A single model cannot be optimised for all tasks simultaneously.

**Fix implemented in `SignalInterceptDemoController.cs`:**
- Replaced the single `modelName` field with four task-specific model fields in the inspector:
  - `scenarioModelName` (default: empty, falls back to `modelName`)
  - `interceptModelName` (default: empty, falls back to `modelName`)
  - `outcomeModelName` (default: empty, falls back to `modelName`)
  - `reportModelName` (default: empty, falls back to `modelName`)
- Added `ResolveModel(string taskModel)` helper that returns the task-specific model if set, otherwise the default `modelName`.
- Each `GenerateScenario()`, `GenerateIntercept()`, `SelectReply()`, and `GenerateFinalReport()` call now passes its resolved model to `new OllamaClient()`.
- `RefreshStats()` updated to display the multi-model configuration when custom models are set (e.g., `Scn:llama3.1:8b Int:llama3.2:3b Out:llama3.2:3b Rpt:llama3.1:8b`), or the default single-model label when not.
- `BuildOllamaFailureMessage()` updated to accept a model name parameter so error messages identify which specific model failed.
- The start-screen status text now displays the full multi-model assignment.

**Design rationale:** The assessor's initial suggestion — more AI models for better control — was sound. The concern was that running multiple models concurrently would multiply latency and memory. The solution routes tasks to different models without concurrency: only one model runs at any time. The developer can install `llama3.2:3b` or `gemma2:2b` for faster, lighter intercept and outcome tasks while keeping `llama3.1:8b` for complex scenario and report generation. If a task-specific model is not installed, the default model handles everything — the system degrades gracefully.

**Feasibility:** All models run through the same Ollama endpoint and API. No architectural changes to `OllamaClient.cs` were required. The implementation is fully backward-compatible — leaving all task-specific fields empty preserves the original single-model behaviour.

**Outcome:** The project now supports a configurable multi-model pipeline. Each stage of the LLM workflow can be assigned a different model optimised for its specific task. This gives the developer precise control over quality, speed, and hardware requirements at each stage of the game loop.

### Quality Overseer: Secondary Overwatch Model (F13 Extension)

**Rationale:** Extending the multi-model architecture further, a quality overseer model was added to review and refine every LLM output before it reaches the player. This directly addresses the assessor's concern about "more AIs for better control" — the primary model generates raw content, and a second model acts as a quality gate, polishing coherence, tone, and consistency.

**Fix implemented in `SignalInterceptDemoController.cs`:**
- Added `enableQualityOverseer` boolean toggle in the inspector (default: off).
- Added `qualityModelName` field (default: `llama3.2:3b`) for selecting the overseer model.
- Added `QualityRefineAsync()` method:
  - Takes raw LLM output, a context summary describing the task, a task label for logging, and a cancellation token.
  - Builds a review prompt instructing the quality model to fix incoherence, maintain satirical tone, remove real-world references, and improve clarity — while preserving the exact labelled field structure.
  - Returns the refined text. On failure, returns the original raw output unchanged (graceful degradation).
  - Updates the signal state text to show "Quality overseer checking..." during the review.
- Wired into all four `SendAndParse*` / `SendAndValidate` methods:
  - **Scenario** (line ~806): reviews the raw labelled response before parsing.
  - **Intercept + replies** (line ~950): reviews the entire package with round history context.
  - **Outcome** (line ~927): reviews the outcome narration with decision context.
  - **Final report** (line ~989): reviews the debrief with scenario summary context.
- `RefreshStats()` updated to append `Q:modelName` to the HUD when the quality overseer is active.

**Performance consideration:** The quality overseer adds one extra Ollama call per game step (scenario, each intercept, each outcome, final report). This approximately doubles the LLM wait time per step when enabled. For a 5-round playthrough, this adds roughly 6 additional model calls. The trade-off is intentional: the overseer is off by default for speed, and can be enabled when output quality is prioritised over response time. On fast hardware with smaller models (e.g. `llama3.2:3b` as overseer), the review pass adds 3-5 seconds per step.

**Design:** The quality overseer operates as a sequential polish pass — it receives the primary model's output and context, reviews it, and returns a refined version. It does not generate content from scratch. This gives the developer a two-tier control system: the primary model handles creative generation, and the overseer handles editorial consistency.

**Outcome:** When enabled, every piece of LLM-generated text passes through a second model for quality review before display. This provides an additional layer of coherence control without requiring the developer to hand-edit prompt outputs, and directly satisfies the assessor's request for multiple AI models working together to improve the final result.

---

- Created the Unity project for a standalone GADS7331 POE Part 2 prototype.
- Chose a text-focused intelligence desk simulation instead of a larger action game.
- Defined the core player role as an intelligence officer interpreting intercepted communication.
- Selected Ollama as the required local LLM runtime.
- Planned the first gameplay loop around generating short ambiguous messages that the player must classify or respond to.

Reasoning:

The assessment requires meaningful Ollama integration, so the project needed a design where generated text directly affects the play experience. A focused desk simulation is technically achievable and makes the LLM central to the game rather than decorative.

## 2026-05-07: High Concept And Ollama Plan

- Drafted `high-concept.md`.
- Drafted `ollama-plan.md`.
- Defined the original core loop:
  - generate intercept
  - analyse message
  - decide response
  - resolve outcome
- Identified local inference, latency, reproducibility, and output control as key technical risks.

AI-assisted decision:

AI support was used to structure the design documentation and clarify how local model inference should be explained for assessment.

## 2026-05-07: Basic Ollama Connection

- Added a basic Ollama API path.
- Used Unity HTTP requests to communicate with the local Ollama server.
- Confirmed the intended endpoint:

  ```text
  http://localhost:11434/api/generate
  ```

- Started with simple prompt and response handling.

Refinement:

The integration needed to prove actual model output, not simulated text. The design therefore moved toward runtime generation inside the Unity scene.

## 2026-05-10: Expanded Gameplay System

- Expanded the prototype from simple intercept generation into a fuller five-round scenario system.
- Added generated scenario briefs so each playthrough begins with a concrete fictional situation.
- Added source profiles with reliability, tells, and agendas.
- Added hidden truth categories:
  - Friendly
  - Enemy
  - Deception
- Added evidence clues so the player has context instead of guessing blindly.

Reasoning:

The rubric rewards design value and technical execution. Scenario generation, source profiles, and clue chips make the LLM output more meaningful to gameplay.

## 2026-05-10: Reply And Consequence Generation

- Added three AI-generated reply options per intercept.
- Connected reply choices to internal scoring and state changes.
- Added generated outcome text after each decision.
- Added a mission log to preserve consequence history.
- Added final report generation after five rounds.

Refinement:

The LLM now contributes to several parts of the loop, while the game still controls scoring and hidden truth. This keeps the experience fair and prevents the model from becoming the only source of rules.

## 2026-05-10: Validation And Safety Controls

- Added response validation for empty outputs.
- Added required labelled fields for structured responses.
- Blocked responses that reveal labels such as `friendly`, `enemy`, `hostile`, `deception`, or `deceptive` inside intercept text.
- Blocked real-world references including real countries, conflicts, organisations, and people.
- Added retry prompts for responses that fail formatting or validation.

Reasoning:

Local LLM output can be inconsistent. Validation improves stability, protects the fictional tone, and supports reproducibility for marking.

## 2026-05-10: UI And Presentation Improvements

- Added a complete runtime UI built from Unity UI components.
- Added tabs for:
  - Briefing
  - Intercept
  - Decision
  - Mission Log
- Added status text, clue chips, reply buttons, visual states, and mission stats.
- Added typewriter-style text reveal and UI motion polish.

Refinement:

The first version proved the integration. The later version made the prototype easier to demonstrate in video evidence and easier for a marker to understand.

## 2026-05-17: Visual Direction Scene And Art Integration

- Added generated art assets for the cartoon office war-room direction.
- Cleaned the generated images and added sliced UI-friendly variants.
- Added `OperationGreylineVisualScene.unity` as an inspector-editable scene for manual polish.
- Hooked the gameplay controller into the new scene so it uses the same Ollama-driven mission loop.
- Added inspector switches for generated art, procedural labels, scanlines, outlines, stamp flashes, signal pings, and supervisor accent opacity.
- Reduced default procedural effects in the visual scene so the new art remains readable.

Reasoning:

The first visual polish pass added motion and screen effects, but those effects competed with the generated image style. The revised scene keeps the gameplay functional while giving the final presentation a clearer art direction that can be adjusted directly in Unity.

## 2026-05-17: Documentation Alignment

- Updated the high concept to match the current scenario-driven deduction loop.
- Updated the Ollama plan to describe scenario generation, source profiles, compact prompts, validation, and state ownership.
- Updated the setup guide with the visual scene, inspector controls, and current run flow.
- Updated the LLM report to mention source notes, condensed prompts, visual polish, and the next text-quality sprint.
- Kept the prompt archive as the record of earlier and current prompt structures.

Reasoning:

The repository documentation needs to describe the actual shipped prototype, not the earliest one-button Ollama demo.

## 2026-05-21: Editable UI Scene And Runtime Binding

- Converted the visual scene from a mostly runtime-generated interface into a full editable UGUI hierarchy.
- Added the scene-level `Could've Been Worse UI` layout with editable panels, tab buttons, reply buttons, status text, action button, and content text fields.
- Updated `SignalInterceptDemoController` to bind to the editable hierarchy at runtime instead of silently rebuilding a fallback GUI.
- Added `Tools > Could've Been Worse > Rebuild Editable Scene UI` so the hierarchy can be regenerated if required.
- Made removed decorative overlays safe by treating background glow, scanlines, stamps, supervisor note, and clue-label decoration as optional scene objects.
- Kept the existing gameplay flow, tab behaviour, prompt calls, scoring, and mission state unchanged.

Reasoning:

The art-heavy runtime UI was difficult to manually correct in Unity and could become unreadable when generated art was used as a text surface. The updated scene gives the project a visible component layout that can be edited directly while still letting code own the game state.

## 2026-05-21: Ollama Scenario Parsing Hardening

- Tightened the scenario prompt so Ollama is asked for no markdown, bullets, numbering, code fences, or extra source-bias wording.
- Updated retry wording to require source bias values of exactly `Friendly`, `Enemy`, and `Deception`.
- Hardened scenario parsing so labels can include common formatting drift such as bullets, numbering, spacing, markdown emphasis, or alternate separators.
- Added a tolerant scenario parser for ordered responses where the model provides the fields and source blocks without exact labels.
- Allowed source classification parsing to recover from values such as `Friendly - loyal to ...` while still storing the controlled classification value.

Reasoning:

The local `llama3.1:8b` model sometimes followed the requested content but ignored the exact label format. The new parsing keeps exact labelled output as the preferred path, but recovers from common local-model formatting drift before treating a scenario as failed.

## 2026-05-21: Splash Video Entry Scene

- Added `SplashScene.unity` as the first build scene.
- Connected `Assets/Video/Splash - Trim.mp4` to a dedicated splash controller.
- The splash controller additively loads `OperationGreylineVisualScene` under the video so the main scene can begin its Ollama scenario request before the splash clears.
- Added `Tools > Could've Been Worse > Rebuild Splash Scene` for regenerating the editable splash scene from the editor.
- Kept `OperationGreylineVisualScene.unity` available for direct UI editing and testing.

Reasoning:

Scenario generation can take several seconds on a local model. Playing the splash video while the main scene loads gives the generation process a head start and makes the opening flow feel intentional instead of waiting on a blank or half-ready UI.

## 2026-05-21: Intercept Metadata Leak Guard

- Updated the intercept prompt so source notes are explicitly context only.
- Added validation that rejects prompt metadata leaking into the visible transcript, including reliability, tell, agenda, source-note, bias, and hidden-intent labels.
- Strengthened the retry prompt so the intercept must be only what was heard, not a summary of source fields.

Reasoning:

The model can sometimes echo prompt context into the intercept, which exposes information the player should infer from source notes and clues. The validator now treats that as failed generation and asks Ollama for a cleaner intercept.

## Known Limitations

- The prototype depends on Ollama being installed and running locally.
- Runtime generation speed depends on the selected model and hardware.
- Severely malformed LLM outputs may still fail validation after tolerant parsing and retry.
- The current project is focused on a single polished gameplay loop rather than multiple levels or broad content variety.
- The visual scene is intentionally editable because final image placement and text polish may still need manual tuning.
