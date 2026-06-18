# Critical Engagement With Feedback

## What I Expected

Before attending the Joburg Game Dev Meetup, I anticipated that the LLM integration would be the standout talking point. I had spent significant development effort on the Ollama pipeline — structured prompts, labelled-field parsing, validation with blocking rules, retry logic for malformed output, and a strict separation of concerns where Unity owns all game state and the LLM only handles fictional text. I assumed attendees would ask about the model choice, how the prompts were engineered, whether local inference was fast enough, and how I handled the unpredictability of generative output.

In terms of gameplay feedback, I expected comments about the reading volume — the game is text-heavy by design — and possibly remarks about the visual polish, since the art assets were generated and the UI was assembled quickly. I assumed the humour would be divisive; satirical tone often is.

## What Surprised Me

The biggest surprise was the near-total indifference to the LLM integration. Not a single attendee asked about the AI pipeline — not the model, not the prompts, not the validation system, not the local vs. cloud trade-off. The LLM was treated as invisible infrastructure. This was simultaneously humbling and instructive: what felt like the core technical achievement of the project was, to a player, just part of the furniture. They cared about what they could see and do — the narrative pull, the visual clarity, the pacing.

A second surprise: the indie developer's suggestion to look at *Stories Untold* and his preference for a consistent authored narrative over procedural generation. I had assumed the per-round procedural variety would be the selling point. Instead, he wanted a tighter, more hand-crafted experience where only the responses were generated. This directly challenges the project's original design thesis — that full procedural generation of scenarios and intercepts creates replayability. It turns out that players may prefer one coherent story over five procedurally-variable ones.

Attendees also completely ignored aspects I thought would draw focus. No one commented on the mission values system (Corridor Stability, Objective Status, Confusion, etc.), the clue chips, the source profiles, or the hidden-truth deduction mechanic. These systems — which I considered the real gameplay depth — went unremarked. The feedback instead centred on surface-level experience: readability, hook, and pacing during generation waits.

## What I Chose Not to Implement (With Justification)

### Minigame During LLM Generation (F7)

**Declined.** While technically feasible in Unity (a simple asynchronous minigame could run on a coroutine while the Ollama request is in flight), this would add scope without addressing the root problem. On adequate hardware with `llama3.1:8b`, generation typically completes within 10-20 seconds. Adding a minigame risks making that wait feel *longer* by demanding active attention during what is otherwise a brief pause. The better solution is to reduce perceived wait time through UI feedback (a progress indicator, lore snippets, or a "transmission incoming" animation) rather than a full minigame.

### Multiple AI Models for Context Control (F13)

**Declined.** The assessor's suggestion of using multiple AI models to improve context retention is architecturally interesting but impractical within this project's constraints. Running multiple Ollama models simultaneously on consumer hardware would multiply latency and memory requirements beyond what is reasonable for a local-only prototype. More importantly, the original design philosophy was to solve context retention through prompt engineering — each prompt carries the full scenario context as labelled fields — rather than through a multi-model pipeline. If context is being lost, the fix should be stronger prompt structure or a context-summary injection step, not an architectural overhaul that introduces new failure modes and hardware dependencies.

### Full Narrative Restructure (F8, F9)

**Declined.** Re-architecting the game around a pre-authored narrative backbone with procedural response generation only (*Stories Untold*-style) would require a fundamental rewrite of the mission state machine, the prompt builder, and the controller flow. It also conflicts with the project's stated scope: a five-round procedural loop where each playthrough is unique. This was the design goal approved at the start of the project. Reducing procedural scope would make the project more narratively polished but less technically demonstrative of the LLM integration brief.

### Reducing Reading Volume (F1)

**Partially declined.** The game is a deduction-first experience. Reading is the core mechanic — the intercept text, source profiles, mission values, and clue chips are what the player engages with to make decisions. Drastically cutting text would hollow out the gameplay. However, I acknowledge that presentation can be improved: shorter paragraphs, more scannable layouts, and better typographic hierarchy can reduce the *perceived* reading load without removing content.

## Subjective and Contradictory Feedback

Some feedback items are inherently subjective or in direct tension with each other, which affects how they can be actioned.

The most notable contradiction is between F1 (the hobbyist's concern about too much reading) and F6 plus F9 (the indie developer's desire for a stronger narrative pull and a consistent authored story). Reducing text volume and deepening narrative engagement pull in opposite directions — richer storytelling requires more text, not less. This tension likely reflects different player types: one wants a quick, scannable experience while the other wants atmospheric immersion. Both are valid preferences, but they cannot be fully satisfied simultaneously within a deduction-first game. My resolution is to lean towards the game's intended audience — players who engage with text as a mechanic — while improving typographic presentation to reduce perceived reading fatigue.

The assessor's suggestion of a multi-model architecture (F13) is a subjective technical opinion, not an established best practice for local LLM game integration. There are valid arguments both for (specialised models per task) and against (doubled latency, doubled memory, inter-model inconsistency). It reflects a particular philosophy about AI architecture rather than an objectively necessary improvement. Dismissing it does not mean ignoring the underlying concern — context retention (F12) is a real problem — but the prescribed solution is one of several possible approaches.

The *Stories Untold* reference (F8) also highlights a subjective taste divide. That game is a curated, linear narrative experience. Could've Been Worse is designed for procedural replay. The feedback is useful as an aspirational direction but cannot be implemented literally without changing the project's genre.

## Evaluation of Feasibility

All suggested changes are *technically* possible within the Unity + Ollama toolset, but feasibility must be weighed against the project's scope, timeline, and design intent:

- **UI fixes (F2, F3, F11):** High feasibility, low risk. These are straightforward UGUI adjustments and bug fixes that do not touch the core systems.
- **Minigame (F7):** Feasible but low return on effort. The generation wait is already short and shrinking as hardware improves.
- **Multi-model pipeline (F13):** Feasible but impractical. Local inference on consumer GPUs would struggle with concurrent models; the project's "runs on a laptop" goal would be compromised.
- **Narrative restructuring (F8, F9):** Feasible but out of scope. This would be a different game.
- **Reducing reading (F1):** Feasible through layout and presentation improvements but not through content removal without undermining the deduction mechanic.

Performance considerations are real: Ollama inference is already the bottleneck. Anything that adds latency or memory pressure — especially running multiple models — would degrade the player experience rather than improve it.

## Final Judgement

The feedback that ultimately shaped my refinements falls into two categories: **immediate fixes** and **philosophical recalibrations**.

The softlock bug (F11), report screen clutter (F2), and text clipping (F3) will be fixed. These are low-cost, high-impact changes that improve the first-time player experience without altering the game's identity.

Feedback I declined — the minigame, the multi-model architecture, the narrative restructure — was declined not because it was bad advice, but because it would either compromise the core design, exceed the project's technical constraints, or demand effort disproportionate to the benefit.

What I learned from this experience is that critique of an AI-driven game is fundamentally critique of the *game*, not the AI. Players do not separate the technology from the experience. If the LLM adds friction — long waits, incoherent context, walls of text — it is a liability regardless of how clever the pipeline is. If it adds something players genuinely enjoy — in this case, darkly funny reply options — it earns its place. The bar for AI in games is not "does it work?" but "does it make the game better than it would be without it?"

This experience also forced me to confront the discomfort of presenting AI-assisted work to a community built on human craftsmanship. The lesson is not to hide the AI but to lead with the game. In future, I will pitch the premise, the tone, and the player experience first — and let the LLM integration be discovered as a supporting detail, not a headline.
