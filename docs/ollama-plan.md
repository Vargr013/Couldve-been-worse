# Signal Intercept: Ollama Integration Plan

## 1. Purpose of the Ollama Integration

Signal Intercept uses a locally hosted Large Language Model through Ollama to generate dynamic intercepted communications during gameplay.

The LLM is not used as decoration or background text. It directly supports the main gameplay loop by creating ambiguous messages that the player must analyse, classify, and respond to.

The goal is to use local AI to create uncertainty, variation, and replayability while still keeping the system controlled and reproducible.

## 2. Chosen Local LLM Platform

The project will use:

- Ollama as the local LLM runtime
- A locally installed model accessed through Ollama's local API
- HTTP requests from the Unity game to the local Ollama server

Ollama is suitable because it allows the model to run locally on the development machine, making the project independent from cloud-based AI services during demonstration.

## 3. Proposed Model Choice

The preferred starting model is:

- `llama3.1:8b` or a similar available Ollama model

Fallback options:

- `mistral`
- `gemma2:2b`
- `llama3.2:3b`

The final model choice will depend on local hardware performance, response speed, and output quality.

A smaller model may be used if latency becomes too high during gameplay.

## 4. Why a Local Model is Appropriate

A local model is appropriate for this project because:

- The game focuses on short text generation rather than long-form writing.
- The system only needs small intercepted messages and short response options.
- The assessment requires visible, functioning Ollama integration.
- Local inference allows the project to demonstrate offline AI behaviour.
- The system can be tested without depending on internet access or external APIs.

## 5. Inference Timing

The LLM will be used during runtime.

### Runtime Inference

The model will generate intercepted communications while the game is running.

This means:

1. The player starts or continues a round.
2. The game sends a structured prompt to Ollama.
3. Ollama generates an intercepted message.
4. The response is displayed in the game UI.
5. The player analyses and responds to the message.

### Optional Future Fallback

If runtime latency becomes too slow in a later version, the game could include a small fallback list of prewritten messages.

This is not the main submitted behaviour. The submitted version is intended to demonstrate live Ollama generation. A fallback would only be appropriate if:

- Ollama is not running
- The model times out
- A response fails validation

For the current prototype, failures are surfaced through the UI and the player can retry generation.

## 6. Data Flow

```text
Game UI
  |
  v
Player starts round / requests next intercept
  |
  v
Game builds structured prompt
  |
  v
HTTP request sent to Ollama local API
  |
  v
Ollama model generates response
  |
  v
Game receives text response
  |
  v
Response is cleaned / validated
  |
  v
Intercept message appears in UI
  |
  v
Player chooses one of three generated replies
  |
  v
Game evaluates decision
  |
  v
Score, risk, and mission state update
```

## 7. Ollama API Approach

Unity will communicate with Ollama over HTTP.

Default local endpoint:

```text
http://localhost:11434/api/generate
```

Expected request fields:

- `model`: the selected local Ollama model
- `prompt`: the structured prompt for the next intercepted message
- `stream`: `false`, so Unity receives one complete response

The implementation keeps the request readable and uses labelled prompt output so Unity can parse scenario, intercept, reply, outcome, and final report responses.

## 8. Prompt System

The prompt must keep AI output controlled enough for gameplay.

The system should ask the model to:

- Generate one short intercepted communication
- Avoid naming the source directly
- Keep the message ambiguous but interpretable
- Avoid real-world political, military, or sensitive references
- Return only the intercepted message text unless structured JSON is required later

Example prompt direction:

```text
Generate one fictional intercepted communication for a modern intelligence analysis game.
The message must be short, ambiguous, and safe for a fictional setting.
Do not reveal whether the source is friendly, hostile, or deceptive.
Do not reference real countries, real conflicts, or real organisations.
Return only the transmission text.
```

## 9. Output Validation

Generated responses should be cleaned before being shown to the player.

Validation should check:

- The response is not empty.
- The response is short enough for the UI.
- The response does not contain obvious labels such as "friendly", "enemy", or "deception".
- The response does not include unwanted explanation around the message.

If validation fails, the game retries with a stricter retry prompt or asks the player to generate again.

## 10. Gameplay Evaluation

The LLM generates the message, but the game should still control scoring and state.

For the prototype, each generated intercept can be paired with an internal hidden classification chosen by the game before the prompt is sent.

Example hidden classifications:

- Friendly
- Enemy
- Deception

The prompt can be adjusted based on the hidden classification while still preventing the model from revealing it directly.

This keeps gameplay fair because the game knows the intended answer while the player only sees the ambiguous transmission.

## 11. Failure Handling

The prototype should handle common Ollama issues clearly:

- Ollama server not running
- Selected model not installed
- Request timeout
- Invalid or empty model response

If a failure occurs, the UI shows a clear message so the player can resolve the Ollama issue or retry generation.

## 12. Reproducibility Notes

The documentation should record:

- The model used during final testing
- The Ollama version if available
- The local endpoint used
- Any fallback model used for lower-spec machines
- Any known latency or performance limits

This supports assessment because another person can reproduce the integration setup.
