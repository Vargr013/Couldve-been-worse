# Critical Engagement With Feedback

## What I Expected

I anticipated the LLM integration would be a standout talking point. I had built structured prompts, labelled-field parsing, validation with blocking rules, and retry logic for malformed output. I assumed attendees would ask about model choice, prompt engineering, inference speed, and handling generative unpredictability.

In terms of gameplay, I expected comments about reading volume (the game is text-heavy by design), remarks about visual polish since the art was generated, and that the dark satirical tone would be divisive.

My expectations did not align with the actual feedback at all.

## What Surprised Me

The biggest surprise was near-total indifference to the LLM integration. Not a single attendee asked about the AI pipeline — not the model, prompts, validation, or local vs. cloud trade-off. What felt like the core technical achievement was, to players, just furniture. They cared about narrative pull, visual clarity, and pacing.

A second surprise: the indie developer's preference for an authored narrative over procedural generation. I had assumed procedural variety would be the selling point — players may prefer one coherent story over five procedurally-variable ones.

Attendees also ignored the aspects I considered strongest. No one commented on the mission values system, source profiles, or hidden-truth deduction mechanic. Feedback centred entirely on surface-level experience: readability, hook, and pacing. The deepest design work went unremarked.

## What I Chose Not to Implement (With Justification)

The minigame (F7) and multi-model pipeline (F13) were initially declined but later implemented. The Bug Squash minigame lets players squash bugs during LLM calls — built after quality overseer latency made passive waiting feel longer. Per-task model routing runs without concurrency: each prompt type targets a dedicated model, with an optional overseer for second-pass polish, later hardened against refusal and chatty-commentary outputs.

The narrative restructure (F8, F9) was declined — re-architecting around a pre-authored backbone would rewrite core systems and conflicts with the procedural scope. Reducing reading volume (F1) was partially declined: reading is the deduction mechanic, but scannable layouts reduce perceived load.

Some feedback was subjective or contradictory. The clearest contradiction is between F1 and F6+F9 — richer storytelling demands more text, reflecting player types irreconcilable in a deduction-first game. The multi-model suggestion was initially viewed as subjective but proved actionable. The *Stories Untold* reference highlights an aspirational taste divide between curated and procedural experiences.


## Evaluation of Feasibility

All suggestions were technically possible within the Unity + Ollama toolset under local inference constraints. UI fixes were straightforward UGUI adjustments. Performance considerations initially made the multi-model suggestion seem unrealistic (concurrent models would double memory and latency), but sequential routing resolved this. The minigame is a lightweight coroutine with negligible overhead. The narrative restructure is feasible but would compromise the core experience by replacing procedural variety with a fixed narrative. Reading reduction through content removal would hollow out the deduction mechanic; layout improvements preserve it.


## Final Judgement

The feedback that shaped my refinements includes the three priority fixes — softlock bug, report clutter, and text clipping — plus playtesting-driven improvements: splitting intercept and reply generation, hardening scenario parsing with fallback values, and strengthening quality overseer refusal detection. The minigame, initially declined, was implemented when testing validated its value.

The narrative restructure was declined because it would require rewriting core systems and replacing procedural scope with a fixed narrative — a different game. Reading reduction was partially declined; content cuts would hollow out the deduction mechanic, but layout improvements were embraced.

This experience reshaped my understanding of critique in AI-driven development. If the LLM adds friction it is a liability; if it adds enjoyment it earns its place. The bar is not "does it work?" but "does it make the game better?" Presenting AI-assisted work to a craftsmanship-focused community taught me to lead with premise, tone, and player experience — the game must be the headline. 
