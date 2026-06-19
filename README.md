# Could've Been Worse

Could've Been Worse is a Unity prototype for GADS7331 POE Part 2. The project is a fictional intelligence desk simulation where the player reads AI-generated intercepted messages, studies clue chips, chooses a reply, and manages the consequences across a short five-round scenario.

The main assessment focus is the local Large Language Model integration. Could've Been Worse uses Ollama through a local HTTP endpoint so the prototype can generate scenario briefs, intercepts, reply options, consequences, and a final report while the game is running.

## Project Details

- Module: Game Design 3A, GADS7331
- Assessment: POE Part 3, LLM-Integrated Game
- Engine: Unity 6000.3.9f1
- LLM runtime: Ollama
- Default endpoint: `http://localhost:11434/api/generate`
- Default model in Unity: `llama3.1:8b`
- Repository: `https://github.com/Vargr013/Couldve-been-worse.git`

## Core Gameplay

The player acts as an intelligence officer working through Operation Greyline. Each scenario contains fictional sources, ambiguous communication, and a situation that becomes better or worse based on player decisions.

The gameplay loop is:

1. Generate a fictional scenario through Ollama.
2. Generate a new intercepted transmission while squashing bugs in a terminal minigame.
3. Review three AI-generated reply options, source clues, and the situation board.
4. Choose a reply.
5. Resolve the consequence through another Ollama call.
6. Repeat for five rounds.
7. Generate a final mission report.

## Ollama Integration

The Unity project sends runtime HTTP requests to the local Ollama API. The main integration is implemented in:

- `Assets/Scripts/OllamaClient.cs`
- `Assets/Scripts/InterceptPromptBuilder.cs`
- `Assets/Scripts/SignalInterceptDemoController.cs`
- `Assets/Scripts/MissionState.cs`
- `Assets/Scripts/BugSquashMinigame.cs`

The game requests structured output from Ollama using labelled fields, then validates the response before showing it to the player. Validation checks include empty output, unrecoverable missing structure, revealed classification labels, real-world references, LLM refusal detection, and chatty meta-commentary from the quality overseer. Scenario parsing handles common local-model formatting drift such as bullets, loose labels, and source bias descriptions.

A configurable per-task model architecture is supported: scenario, intercept, outcome, and report generation can each target a different Ollama model via the Unity inspector, with the default `llama3.1:8b` used as fallback. An optional quality overseer model can review and refine every LLM output before display, toggled on or off for speed.

## Requirements

- Unity 6000.3.9f1 or compatible Unity 6 version
- Ollama installed locally
- A local Ollama model installed, preferably `llama3.1:8b`
- Optional: additional models for per-task routing (`llama3.2:3b`, `gemma2:2b`, etc.) and quality overseer
- Windows build target for the final playable build

## Setup

1. Install Ollama from `https://ollama.com`.
2. Open a terminal and run:

   ```powershell
   ollama pull llama3.1:8b
   ollama serve
   ```

3. Open the project in Unity.
4. Open `Assets/Scenes/SplashScene.unity` for the full splash-to-game flow, or `Assets/Scenes/OperationGreylineVisualScene.unity` for direct UI editing.
5. Press Play.
6. Confirm the game can generate a scenario and intercept through the local model.

Full setup instructions are in `docs/setup.md`.

## Documentation

Submission documentation is stored in `docs/`:

- `high-concept.md`
- `ollama-plan.md`
- `setup.md`
- `refinements-changes.md`
- `prompts-used.md`
- `llm-integration-report.md`
- `feedback-summary.md`
- `critical-feedback.md`
- `evidence-attendance.md`
- `final-reflection.md`

## Submission Evidence

The repository contains the prototype project, final refined Unity scenes, source code, art assets, and written documentation. The playable Windows build and assessment videos are submitted as external packaged files:

- Prototype evidence: the live Ollama-driven Unity project in this repository.
- Final build: Windows build packaged from `Assets/Scenes/SplashScene.unity` and `Assets/Scenes/OperationGreylineVisualScene.unity`.
- Final showcase video: 3-6 minute recording showing the gameplay flow, visual improvements, and design intent.

## AI Tools Used

AI tools were used to support planning, code drafting, debugging, prompt design, and documentation. The submitted prototype is still manually reviewed, adapted, and integrated into Unity. Ollama and `llama3.1:8b` are the required local language model setup, with runtime output used directly by the gameplay system.

## Credits

Created for GADS7331 POE Part 2.

Student name: Xander Jacobs

Student number: ST10443085
