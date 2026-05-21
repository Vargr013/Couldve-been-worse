# Signal Intercept: Setup Guide

This guide explains how to run the Signal Intercept prototype and reproduce the local Ollama integration.

## 1. System Requirements

Recommended setup:

- Windows 10 or Windows 11
- Unity 6000.3.9f1
- Ollama installed locally
- At least 16 GB RAM for larger local models
- Internet connection for the initial model download

Final tested machine:

- CPU: 12th Gen Intel(R) Core(TM) i5-12450H
- GPU: NVIDIA GeForce RTX 3060 Laptop GPU, with Intel(R) UHD Graphics
- RAM: 32 GB
- Operating system: Microsoft Windows 11 Home Single Language 10.0.26200
- Ollama version: 0.23.2
- Final model used: `llama3.1:8b`

## 2. Install Ollama

1. Download Ollama from `https://ollama.com`.
2. Install it using the Windows installer.
3. Open PowerShell or Command Prompt.
4. Confirm Ollama is available:

   ```powershell
   ollama --version
   ```

## 3. Install The Local Model

The Unity project currently defaults to:

```text
llama3.1:8b
```

Install it with:

```powershell
ollama pull llama3.1:8b
```

If the machine struggles with that model, use a smaller fallback model and update the `modelName` field in `SignalInterceptDemoController`:

```powershell
ollama pull llama3.2:3b
```

or:

```powershell
ollama pull gemma2:2b
```

## 4. Start Ollama

Ollama normally runs as a local service after installation. If needed, start it manually:

```powershell
ollama serve
```

The Unity project sends requests to:

```text
http://localhost:11434/api/generate
```

To confirm the model responds locally, run:

```powershell
ollama run llama3.1:8b
```

Then type a short test prompt and confirm the model answers.

## 5. Open The Unity Project

1. Open Unity Hub.
2. Add the project folder.
3. Open the project using Unity 6000.3.9f1.
4. Open the splash entry scene for the normal playable flow:

   ```text
   Assets/Scenes/SplashScene.unity
   ```

5. Open the editable presentation scene directly when adjusting the UI:

   ```text
   Assets/Scenes/OperationGreylineVisualScene.unity
   ```

6. Press Play.

## 6. Run The Prototype

When the splash scene starts, it plays `Assets/Video/Splash - Trim.mp4` while additively loading `OperationGreylineVisualScene`. This lets the main scene begin its Ollama scenario request while the splash video is still visible.

When the main scene is ready, the prototype automatically asks Ollama to generate a five-round scenario. If that succeeds, continue through the loop:

1. Review the generated Operation Greyline briefing.
2. Read the situation board and source notes.
3. Generate an intercept.
4. Read the transmission and clue chips.
5. Select one generated reply.
6. Review the consequence and updated source note.
7. Repeat until the final report is available after round 5.

The visual scene contains a real editable UGUI hierarchy named `Signal Intercept UI`. The panels, tab buttons, reply buttons, text fields, status line, and primary action button can be adjusted visually in Unity.

The `SignalInterceptDemoController` now binds to that hierarchy at runtime. It no longer silently builds a fallback GUI if required objects are missing. If the editable UI is deleted or badly renamed, use:

```text
Tools > Signal Intercept > Rebuild Editable Scene UI
```

Decorative overlays such as scanlines, stamps, glow layers, clue labels, and supervisor-note decoration can be removed or disabled for readability. The core panels, buttons, text fields, and controller object should remain in place unless the rebuild tool is used.

The visual-direction scene also exposes inspector controls for generated art, procedural labels, scanlines, outlines, stamp flashes, signal pings, supervisor accent opacity, and sprite overrides. These controls are intended for final readability tuning without code edits.

## 7. Common Issues

### Ollama is not running

Symptom:

```text
Ollama request failed
```

Fix:

```powershell
ollama serve
```

### Model is not installed

Symptom:

```text
model not found
```

Fix:

```powershell
ollama pull llama3.1:8b
```

### Responses are too slow

Fix options:

- Use a smaller model such as `llama3.2:3b` or `gemma2:2b`.
- Increase `requestTimeoutSeconds` in the Unity inspector.
- Close other heavy applications before running the prototype.
- Keep Ollama warm by running a short `ollama run llama3.1:8b` test before recording.

### Output fails validation

The game rejects responses that are empty, reveal hidden classification labels, mention real-world conflicts, countries, organisations, or people, or are too malformed to recover. Scenario parsing now handles common local-model formatting drift such as bullets, numbering, loose labels, and source bias descriptions before it retries.

If validation still fails, press the generation button again so the retry prompt can request a cleaner response.

### Editable UI hierarchy is missing or incomplete

Symptom:

```text
Editable Signal Intercept UI is incomplete.
```

Fix:

Open `Assets/Scenes/OperationGreylineVisualScene.unity`, then run:

```text
Tools > Signal Intercept > Rebuild Editable Scene UI
```

After rebuilding, save the scene. You can then remove or adjust optional decorative overlays again, but keep the core bound UI objects.

### Generated art and old effects clash

Open `Assets/Scenes/OperationGreylineVisualScene.unity` and adjust the `SignalInterceptDemoController` inspector fields:

- Turn off procedural scanlines, outlines, labels, stamp flashes, or signal pings.
- Reduce supervisor accent opacity.
- Override any sprite slot manually.
- Keep the generated art asset loading enabled unless testing a pure procedural UI.

## 8. Building The Final Playable Version

1. In Unity, open `File > Build Profiles`.
2. Select the Windows build target.
3. Confirm `Assets/Scenes/SplashScene.unity` is first in the scene list and `Assets/Scenes/OperationGreylineVisualScene.unity` is also included.
4. Build the project into a folder named:

   ```text
   SignalIntercept_FinalBuild
   ```

5. Zip the final build folder for submission.
6. Before recording or submitting, run the executable while Ollama is active and confirm it can generate a scenario and at least one intercept.

## 9. Submission Notes

The final submission includes:

- Zipped Unity project folder
- GitHub repository link
- Final playable Windows build
- Technical demonstration video
- Final showcase video
- All documentation in the `docs` folder
- LLM integration report
