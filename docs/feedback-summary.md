# Feedback Summary

## Complete List of Feedback Received

The following table captures every distinct piece of feedback collected during the Joburg Game Dev Meetup playtest session and the academic assessment. Each item is numbered for cross-referencing with the critical engagement document.

| # | Source | Feedback |
|---|---|---|
| F1 | Hobbyist | There is a lot of reading — the text volume felt high for a casual play session. |
| F2 | Hobbyist | The report screen background is too cluttered, making text hard to read. |
| F3 | Hobbyist | Text clips or overflows at times, causing visual glitches. |
| F4 | Hobbyist | The overall structure and flow of the game is good. |
| F5 | Hobbyist | The concept is intriguing and has potential. |
| F6 | Indie Dev | The game needs a unique, consistent pull — something that makes it feel special and gives the player a reason to keep going. |
| F7 | Indie Dev | A minigame during LLM generation wait time would keep the player engaged. |
| F8 | Indie Dev | Look at *Stories Untold* for inspiration on narrative delivery and atmosphere. |
| F9 | Indie Dev | Consider a consistent handwritten narrative and only generate the reply options procedurally, rather than generating the entire scenario and intercept each round. |
| F10 | Indie Dev | The humorous tone works well; laughed at the reply options. |
| F11 | Observed | Clicking the Report button before selecting a reply locks the player out of the reply selection permanently (softlock bug). |
| F12 | Assessor | The LLM struggles to keep context of the scenario across rounds, weakening narrative coherence. |
| F13 | Assessor | Using multiple AI models (rather than a single LLM) would give better control over the final result. |

---

## Feedback Mapped by Project Aspect

| Aspect | Related Feedback Items | Summary |
|---|---|---|
| **LLM Integration** | F12, F13 | Context retention across rounds is inconsistent; a multi-model architecture could improve coherence. |
| **Gameplay / Engagement** | F1, F6, F7, F11 | Reading volume is high; the game lacks a unique hook; a minigame during generation could help; the report-button softlock blocks progress. |
| **UI / Visual Clarity** | F2, F3 | Report screen background is cluttered; text clipping occurs. |
| **Narrative / Writing** | F8, F9, F10 | The humour is well-received; the suggestion is to anchor the narrative (Stories Untold style) and only generate certain elements procedurally. |
| **Overall Impressions** | F4, F5 | Structure and flow are working; the concept is considered strong. |

---

## Recurring Themes

Three themes appeared across multiple feedback sources:

### 1. UI Readability and Polish (F1, F2, F3)
Both the hobbyist and the observed bug point to issues with how information is displayed. The report screen's cluttered background, occasional text clipping, and high reading volume collectively suggest the UI needs a clarity pass — particularly in the post-round reporting phase where the player is processing mission outcomes.

### 2. LLM as a Main Feature Is Not Compelling Enough (F6, F7, F12, F13)
Multiple sources — the indie developer, the assessor, and even my own observations — suggest that the LLM integration, while functional, does not carry the experience on its own. The indie developer wanted a stronger hook and more to do during generation. The assessor wanted deeper architectural control. The implication across sources is that the LLM should support the game rather than being positioned as the headline draw.

### 3. Narrative Structure Over Procedural Breadth (F6, F8, F9)
The indie developer's suggestion to look at *Stories Untold* and his preference for a consistent narrative with generated responses points to a desire for a tighter, more authored experience. This connects with the academic concern about context retention — a fully procedural narrative struggles to maintain coherence in a way that a scripted backbone would not.

---

## Initial Reactions to Feedback (Unfiltered)

| Reaction | Triggered By |
|---|---|
| **Surprise** | The LLM integration — which I considered the defining technical achievement — was met with indifference. No attendee asked about the model, the prompt engineering, or the validation pipeline. The AI was accepted as background plumbing, not a feature. |
| **Agreement** | The report screen clutter and text clipping were issues I was already aware of but had deprioritised. Hearing them flagged by a first-time player validated that they need attention. |
| **Concern** | The softlock bug (F11) is a critical failure state — it blocks progress entirely with no recovery path. This needs an immediate fix. |
| **Validation** | The positive reception of the humour (F10) confirmed that the satirical tone, which I worried might fall flat, actually landed. The structural praise (F4) also reassured me that the five-round loop works. |
| **Discomfort** | Presenting a heavily AI-assisted game at a community event built around craftsmanship and the love of making games felt morally awkward. I sensed that the concept itself, an AI generating narrative content, may have been less exciting to this audience precisely because they value human authorship. |
| **Recalibration** | The feedback collectively suggests that I should reposition the LLM in my own framing of the project: less "look what the AI does," more "here's a game where AI helps the pacing." The indie developer's reaction in particular made me reconsider how I pitch the project in future. |


## Post-Playtest Actions (June 2026)

| Feedback Item | Status | Summary |
|---|---|---|
| F1 (too much reading) | **Partially addressed** | Layout and typography improved; content volume retained as it is the core mechanic |
| F2 (report clutter) | **Fixed** | Added readability plate behind mission log text with high-contrast backing |
| F3 (text clipping) | **Fixed** | Changed vertical overflow from Truncate to Overflow; tightened font size range |
| F4 (good structure/presentation) | **Retained** | Five-round loop preserved as designed |
| F5 (minimalist rooms/desks) | **Noted** | Aspirational; visual scene direction already established |
| F6 (narrative pull / unique hook) | **Partially addressed** | Narrative recap injection added; full authored narrative declined as scope-breaking |
| F7 (minigame during generation) | **Implemented** | Bug Squash minigame runs during intercept generation with session high score |
| F8 (Stories Untold reference) | **Declined** | Curated narrative backbone conflicts with procedural design goals |
| F9 (consistent narrative) | **Partially addressed** | Recap injection maintains context across rounds; procedural variety retained |
| F10 (dark humour received well) | **Retained** | Satirical tone confirmed as a strength |
| F11 (report button softlock) | **Fixed** | Added HasPendingReply guard in GenerateIntercept, GenerateFinalReport, and HandlePrimaryAction |
| F12 (LLM context retention) | **Addressed** | Narrative recap injection via BuildNarrativeRecap; round history passed to all prompts |
| F13 (multiple AI models) | **Implemented** | Per-task model routing via inspector; quality overseer with refusal/chatty detection hardening |
