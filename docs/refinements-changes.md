# Signal Intercept: Refinements And Changes Log

This document records the main scope changes, design refinements, and AI-assisted decisions made during development.

## 2026-05-07: Initial Prototype Direction

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

## Current Remaining Refinements

- Record exact final Ollama model and version.
- Record final test machine specifications.
- Build the final Windows executable.
- Record the technical demonstration video.
- Record the final showcase video.
- Confirm the GitHub repository is accessible to the marker.
- Package documentation and final build into the Drive submission folder.
- Improve the generated text quality in the next sprint so scenario consequences and satire feel less generic.

## Known Limitations

- The prototype depends on Ollama being installed and running locally.
- Runtime generation speed depends on the selected model and hardware.
- Some LLM outputs may still fail validation and require retrying.
- The current project is focused on a single polished gameplay loop rather than multiple levels or broad content variety.
- The visual scene is intentionally inspector-editable because final image placement and text polish may still need manual tuning.
