# Could've Been Worse: LLM Integration Report

Could've Been Worse is a Unity prototype built around local Large Language Model integration through Ollama. The project uses an LLM as a direct gameplay system rather than as a background writing tool. The player works as an intelligence officer in a fictional satirical operation desk, reading intercepted transmissions, reviewing clue chips and source notes, choosing replies, and watching the mission state change across five rounds. The LLM generates the scenario briefing, source profiles, intercepted messages, reply options, outcome text, source note updates, and final report during play.

The project uses Ollama because the assessment requires a local LLM workflow, and local inference also fits the design well. The prototype does not need long-form responses or cloud-scale reasoning. It needs short, ambiguous, replayable text that can be generated during play. Ollama allows this to happen through a local API endpoint, `http://localhost:11434/api/generate`, without requiring an online service during the demonstration. The current Unity configuration uses `llama3.1:8b` as the default model, with smaller fallback options such as `llama3.2:3b` or `gemma2:2b` if performance is too slow on another machine.

The current build uses a short splash video entry scene while the main Operation Greyline scene loads additively. This gives the first scenario request time to start before the player reaches the desk UI.

The technical flow is simple but controlled. Unity builds a structured prompt in `InterceptPromptBuilder`, sends it through `OllamaClient`, receives the model response, cleans the text, validates it, and then displays it through the game interface. The gameplay state is managed separately in `MissionState`. This separation is important because the LLM creates flavour, ambiguity, and consequence text, but the game still controls the hidden truth, scoring, risk, source behaviour, situation values, and mission outcome. This prevents the model from becoming an unreliable rules engine while still allowing it to meaningfully shape the player's experience.

Prompt engineering became one of the most important parts of the implementation. Early prompts could generate a single intercept, but that was not enough to support a full gameplay loop. The prompt system was expanded into four main prompt types: scenario generation, intercept and reply generation, outcome generation, and final report generation. Most prompts use labelled fields such as `INTERCEPT`, `OPTION_1`, `OUTCOME`, `SITUATION`, and `SOURCE_NOTE`. This makes the output easier to parse inside Unity and reduces the chance that extra explanation breaks the prototype. Retry prompts and tolerant scenario parsing handle common local-model formatting drift, while intercept validation rejects prompt metadata such as source reliability, tells, agendas, biases, and hidden intent if it leaks into the visible transcript. The prompts were later condensed because the richer scenario system could time out on local hardware.

Performance and reproducibility are the main limitations of the local approach. Local inference depends heavily on the machine, the selected model, and what else is running at the same time. Larger models can produce stronger writing, but they may respond too slowly for a smooth game flow. The current prototype uses request timeouts and visible status messages so the player understands when the game is waiting for Ollama. The design also keeps outputs short, which reduces latency and helps the UI remain readable.

The LLM improves the design by making each playthrough less predictable. Instead of memorising fixed transmissions, the player must interpret changing source behaviour, ambiguous wording, and evolving consequences. The prototype also includes an editable UGUI visual scene using generated art assets, cleaned UI sprites, and optional scripted effects so the AI-generated content is presented as a readable cartoon office war-room. The game is still small in scope, but the AI integration is central to the player experience because the main content is generated live.

There are also ethical and practical concerns. The prototype deliberately avoids real countries, real conflicts, real organisations, and real people. This keeps the intelligence theme fictional and reduces the risk of generating sensitive or misleading political content. The game also validates responses and rejects real-world references where possible. Throughout my documentation and videos I clearly state that AI-generated text is used during gameplay and that AI tools supported development. The local model is not presented as factual or authoritative; it is used as a creative system for fictional interactive content. Ollama and the selected model are credited as external runtime dependencies rather than original project assets. Any final distribution should keep the model name visible and respect the licence terms attached to the installed Ollama model package.

Overall, Could've Been Worse demonstrates a functional local LLM integration that supports gameplay, replayability, and reproducibility. The project shows the strengths of local AI for controlled short-form generation, while also acknowledging its limits around speed, formatting consistency, character, and hardware requirements.


## Post-Playtest Amendments (June 2026)

The following architectural changes were made after the Joburg Game Dev Meetup playtest:

### Multi-Model Per-Task Routing (F13)

The project was expanded from a single-model architecture to per-task model selection. Four inspector fields (`scenarioModelName`, `interceptModelName`, `outcomeModelName`, `reportModelName`) allow each LLM task to target a different Ollama model. Only one model runs at a time — concurrency was avoided to preserve the "runs on a laptop" constraint. An optional quality overseer model (`qualityModelName`, default `llama3.2:3b`) can review and refine every output before display, toggled via `enableQualityOverseer`. If any task-specific field is empty, the default `llama3.1:8b` handles that task.

### Intercept and Reply Pipeline Split

The combined intercept-and-replies generation was split into two independent Ollama calls. `RequestValidIntercept` generates and validates the in-world radio text first. Only after a valid intercept is confirmed does `RequestValidReplies` generate the three reply options with the intercept injected as context. This prevents cascading failures — a malformed reply set no longer forces a full regeneration of both intercept and replies. Each stage has its own retry path.

### Quality Overseer With Refusal and Chatty-Output Hardening

`LooksLikeRefusal` was expanded from 8 to 19 detection patterns covering phrasings such as "I can't fulfill this request" and "I will not." A new `LooksLikeChattyOverseerOutput` method detects conversational wrapper text ("I can assist you," "Here's the refined," "Let me know"). The quality overseer prompt was strengthened to forbid all meta-commentary. When the overseer returns a refusal or chatty output, the system falls back to the valid raw text. A further guard in `ValidateIntercept` rejects any parsed intercept that still presents as a refusal.

### Narrative Recap Injection (F12)

`MissionState.BuildNarrativeRecap` now builds a pipe-delimited string of round-by-round history — round number, correct/incorrect, source, chosen reply, and outcome. This recap is injected into all intercept, reply, and outcome prompts, giving the LLM a structured narrative record rather than compressed value summaries.

### Bug Squash Minigame (F7)

A terminal-themed minigame (`BugSquashMinigame.cs`, 511 lines) runs during intercept generation. Bugs spawn on screen during the LLM call and the player clicks to squash them. The minigame starts before the Ollama request and stops cleanly on success, cancellation, validation failure, or exception. A session high score appears on the HUD. This addresses the dead-time concern raised during playtesting.

### Scenario Parsing Resilience

Scenario fields now use `ReadFieldOrDefault` with sensible fallback values instead of `ReadRequiredField`. Source bias parsing uses `TryParseClassification` with slot-based fallbacks (Friendly/Enemy/Deception). A mostly well-formed scenario with one missing field now parses successfully rather than triggering a full retry.
