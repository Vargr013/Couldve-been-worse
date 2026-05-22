# Signal Intercept

Signal Intercept is a Unity prototype for GADS7331 POE Part 2. The project is a fictional intelligence desk simulation where the player reads AI-generated intercepted messages, studies clue chips, chooses a reply, and manages the consequences across a short five-round scenario.

The main assessment focus is the local Large Language Model integration. Signal Intercept uses Ollama through a local HTTP endpoint so the prototype can generate scenario briefs, intercepts, reply options, consequences, and a final report while the game is running.

## Project Details

- Module: Game Design 3A, GADS7331
- Assessment: POE Part 2, LLM-Integrated Game
- Engine: Unity 6000.3.9f1
- LLM runtime: Ollama
- Default endpoint: `http://localhost:11434/api/generate`
- Default model in Unity: `llama3.1:8b`
- Repository: `https://github.com/Vargr013/Couldve-been-worse.git`

## Core Gameplay

The player acts as an intelligence officer working through Operation Greyline. Each scenario contains fictional sources, ambiguous communication, and a situation that becomes better or worse based on player decisions.

The gameplay loop is:

1. Generate a fictional scenario through Ollama.
2. Generate a new intercepted transmission and three reply options.
3. Review the source clues and situation board.
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

The game requests structured output from Ollama using labelled fields, then validates the response before showing it to the player. Validation checks include empty output, unrecoverable missing structure, revealed classification labels, and real-world references that should not appear in the fictional setting. Scenario parsing also handles common local-model formatting drift such as bullets, loose labels, and source bias descriptions.

## Requirements

- Unity 6000.3.9f1 or compatible Unity 6 version
- Ollama installed locally
- A local Ollama model installed, preferably `llama3.1:8b`
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

## Submission Evidence

The repository contains the prototype project, final refined Unity scenes, source code, art assets, and written documentation. The playable Windows build and assessment videos are submitted as external packaged files:

- Prototype evidence: the live Ollama-driven Unity project in this repository.
- Final build: Windows build packaged from `Assets/Scenes/SplashScene.unity` and `Assets/Scenes/OperationGreylineVisualScene.unity`.
- Technical demonstration video: 3-6 minute recording showing Ollama running locally and explaining the Unity integration.
- Final showcase video: 3-6 minute recording showing the gameplay flow, visual improvements, and design intent.

## AI Tools Used

AI tools were used to support planning, code drafting, debugging, prompt design, and documentation. The submitted prototype is still manually reviewed, adapted, and integrated into Unity. Ollama and `llama3.1:8b` are the required local language model setup, with runtime output used directly by the gameplay system.

## Credits

Created for GADS7331 POE Part 2.

Student name: Xander Jacobs

Student number: ST10443085
