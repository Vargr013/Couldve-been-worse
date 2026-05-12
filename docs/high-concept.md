# Signal Intercept: High Concept

## 1. High Concept

Signal Intercept is a modern intelligence analysis simulation in which the player assumes the role of an intelligence officer monitoring intercepted communications.

Using a locally hosted Large Language Model (LLM) through Ollama, the system dynamically generates ambiguous communication messages that may originate from friendly units, hostile actors, or deceptive sources.

The player must interpret these transmissions, assess their origin and intent, and make critical decisions under uncertainty. Success depends on analytical reasoning, pattern recognition, and effective use of AI-assisted interpretation.

## 2. Core Idea

The central experience revolves around uncertainty and interpretation.

Unlike traditional games with predefined outcomes, Signal Intercept uses AI to:

- Generate unpredictable communication chains
- Introduce ambiguity and deception
- Force the player to reason rather than memorise

The LLM is not decorative. It is the primary driver of gameplay.

## 3. Player Role

The player acts as an Intelligence Officer responsible for:

- Monitoring intercepted communications
- Identifying whether transmissions are:
  - Friendly
  - Enemy
  - Misinformation / Deception
- Selecting appropriate responses or actions
- Managing risk under incomplete information

## 4. Core Gameplay Loop

1. Intercept Message (LLM-generated)
   - The system generates a short, ambiguous communication.
2. Analysis Phase
   - The player interprets tone, language, and context clues.
3. Decision Phase
   - The player selects one of three generated reply options.
   - Each reply represents a different interpretation and response action.
4. Outcome Resolution
   - The system evaluates the decision and provides feedback.
   - Correct interpretation rewards progression.
   - Incorrect interpretation creates penalties or escalation.
5. Mission Log
   - The consequence is recorded and affects the next round.
6. Next Transmission / Final Report
   - The loop repeats with new AI-generated content.
   - After five rounds, the system generates a final mission report.

## 5. Role of the LLM

The locally hosted LLM is central to the system and is responsible for:

### 5.1 Communication Generation

- Producing short intercepted messages
- Ensuring ambiguity and realism
- Avoiding explicit identification of the source

### 5.2 Variation and Replayability

- Generating different phrasing, tone, and structure
- Preventing predictable gameplay patterns

### 5.3 Controlled Output Design

Prompts are structured to ensure:

- Consistent message length
- Controlled ambiguity
- Thematic alignment with modern operations

## 6. Why a Local LLM

Using a locally hosted model supports:

- Low-latency interaction during gameplay
- Offline functionality
- Greater control over outputs and prompt structure
- Reproducibility across machines, which is important for assessment

The project will demonstrate understanding of:

- Local inference vs cloud-based AI
- Performance constraints
- Prompt engineering for controlled outputs

## 7. Design Goals

- Create a system where AI meaningfully affects gameplay
- Encourage critical thinking and interpretation
- Maintain clarity and usability despite AI variability
- Ensure the system is technically stable and reproducible

## 8. Unique Selling Point

Signal Intercept transforms AI from a content generator into a decision-making partner under uncertainty.

Rather than providing answers, the AI introduces ambiguity, requiring the player to interpret, decide, and accept the consequences.

## 9. Scope

To maintain a focused and achievable project, the prototype will use:

- Text-based interface as the primary interaction style
- Single core gameplay loop
- One LLM model through Ollama
- Five-round scenario structure
- Generated replies, outcomes, and final report

This keeps the project stable, technically focused, and achievable within the assessment scope.

## 10. Ethical Considerations

The project acknowledges:

- AI-generated misinformation risks
- The importance of transparency in AI-assisted systems
- The potential for bias or misleading outputs

The system will:

- Avoid sensitive real-world references
- Clearly communicate AI involvement to the player

## 11. Expected Outcome

The expected outcome is a functional prototype demonstrating:

- Real-time LLM integration using Ollama
- AI-generated interactive gameplay elements
- A complete, reproducible system
- Clear documentation and reflection
