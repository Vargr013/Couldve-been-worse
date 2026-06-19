# Could've Been Worse: High Concept

## 1. High Concept

Could've Been Worse is a darkly satirical intelligence desk game about trying to make serious decisions from unreliable AI-generated information.

The player works inside Operation Greyline, a fictional border-corridor monitoring desk where Command demands confident answers from vague intercepted transmissions, recurring signal sources, and messy consequences. A locally hosted Large Language Model through Ollama generates the scenario, intercepts, reply options, outcomes, and final report during play.

The game is not about military realism. It is about deduction under pressure, workplace absurdity, and the consequences of pretending bad information is clear.

## 2. Core Idea

Each mission is a five-round scenario generated at runtime. The player receives a fictional briefing, studies recurring signal sources, reads intercepted communications, reviews clue chips, and chooses one of three generated replies.

Unlike a fixed dialogue tree, Could've Been Worse uses local AI to:

- Generate a concrete fictional operation scenario.
- Create recurring signal sources with tells, agendas, and reliability patterns.
- Produce ambiguous intercepts that reference the current situation.
- Generate reply options and narrative consequences.
- Build a final debrief from the visible mission state.

The LLM supplies the language and variation. Unity owns the rules, hidden truth, risk, values, source state, and mission grade.

## 3. Player Role

The player acts as an overworked intelligence officer responsible for:

- Reading ambiguous intercepted communications.
- Comparing transmissions against source notes and clue chips.
- Choosing scenario-specific replies.
- Managing risk, confusion, objective status, and Command embarrassment.
- Interpreting recurring source behaviour across five rounds.

## 4. Core Gameplay Loop

1. Scenario Generation
   - Ollama generates the Operation Greyline briefing, location, stake, complication, bad Command idea, and three recurring signal sources.
2. Situation Review
   - The player reads the briefing, source notes, visible values, and latest consequence.
3. Intercept Generation
   - Unity chooses a hidden truth and source, then Ollama generates a short intercepted message and three replies.
4. Deduction Phase
   - The player reads the intercept, clue chips, and source behaviour.
5. Reply Selection
   - The player chooses one generated reply.
6. Outcome Resolution
   - Unity evaluates correctness and updates mission values.
   - Ollama narrates the visible consequence and source note update.
7. Final Report
   - After five rounds, Unity calculates the mission grade and Ollama writes the final debrief.

## 5. Role Of The LLM

The local LLM is central to the playable experience.

It generates:

- Scenario briefings.
- Fictional source profiles.
- Intercept text.
- Three reply options.
- Outcome narration.
- Situation summaries.
- Source note updates.
- Final mission reports.

It does not control:

- Hidden truth.
- Correctness.
- Risk calculation.
- Mission values.
- Round count.
- Final grade.

This keeps the game markable and reproducible while still making the AI output meaningful.

## 6. Why A Local LLM

Using Ollama supports:

- Visible local AI integration.
- Offline demonstration after model installation.
- Direct comparison between prompt design and generated output.
- Controlled short-form generation without a cloud API.
- Assessment-friendly reproducibility on a configured machine.

The current default model is `llama3.1:8b`, with smaller models such as `llama3.2:3b` or `gemma2:2b` available if latency is too high.

## 7. Visual Direction

The current visual target is a cartoon office war-room rather than a realistic command centre.

The prototype includes:

- A tabbed intelligence desk interface.
- Generated art panels and cleaned UI sprites.
- Typewriter intercept reveal.
- Button motion, panel pulse, shake, and flash effects.
- A full editable UGUI visual scene for manual art, text, and layout adjustment.

The older procedural scanlines, labels, outlines, and stamp effects can be toggled or removed so they do not clash with the generated art assets. The runtime controller binds to the editable hierarchy instead of replacing it with a generated fallback UI.

## 8. Design Goals

- Make local AI visibly central to the gameplay loop.
- Reward deduction through repeated source patterns and clue chips.
- Keep the mission short, readable, and demonstrable.
- Make consequences concrete across the five-round scenario.
- Maintain a fictional and ethically safer setting.
- Keep the UI editable in Unity for final presentation polish.

## 9. Scope

The current prototype focuses on one strong loop:

- One generated five-round scenario.
- Three recurring signal sources.
- One intercept and reply choice per round.
- Visible situation values and source notes.
- One generated final report.
- Editable UGUI scene interface with inspector-editable visual controls.

Future polish could focus on sharper generated consequences and more characterful writing, but the submitted scope is the complete five-round loop described above.

## 10. Ethical Considerations

The project avoids real countries, real conflicts, real organisations, real units, and real people. The military-intelligence framing is fictional and satirical.

The system also:

- Uses validation to reject real-world references where practical.
- Clearly documents that AI-generated text appears during gameplay.
- Treats model output as fictional creative content, not factual intelligence.

## 11. Expected Outcome

The expected outcome is a functional Unity prototype demonstrating:

- Real-time Ollama integration.
- AI-generated scenarios, intercepts, replies, consequences, and reports.
- A controlled deduction-first gameplay loop.
- Visible state changes and consequence tracking.
- A documented, reproducible local LLM workflow.


## Post-Playtest Amendments (June 2026)

The following features were added after the Joburg Game Dev Meetup playtest:

### Bug Squash Minigame

A terminal-themed click-to-squash minigame runs during intercept generation. Bugs appear on screen while the LLM generates — the player clicks them for a session high score. This addresses the dead-time concern raised by playtesters and reinforces the "janky intelligence terminal" aesthetic.

### Multi-Model Per-Task Routing

The game now supports assigning different Ollama models to each task (scenario, intercept, outcome, report) via the Unity inspector. Only one model runs at a time, preserving the local-hardware constraint. An optional quality overseer model can review and refine every LLM output before display.

### Quality Overseer With Refusal Detection

The overseer was hardened against two failure modes discovered in testing: outright refusals ("I can't fulfill this request") and chatty commentary ("I can assist you with refining the text..."). Expanded detection methods reject these outputs and fall back to the valid raw text.

### Narrative Context Retention

A round-by-round narrative recap is injected into all LLM prompts, giving the model structured memory of everything that happened in prior rounds. This addresses the assessor's observation that the model "would struggle to keep context of the scenario."

### Intercept/Reply Pipeline Split

Intercept text and reply options are now generated in two independent LLM calls. This prevents a failed reply generation from forcing a complete intercept regeneration.

### Scenario Parsing Resilience

Scenario parsing was hardened with fallback values. If one field is missing or malformed, the scenario still parses successfully rather than triggering a full retry.
