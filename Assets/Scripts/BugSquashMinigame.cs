using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public sealed class BugSquashMinigame : MonoBehaviour
{
    [Header("Runtime References (auto-wired if empty)")]
    [SerializeField] private RectTransform titleTextRect;
    [SerializeField] private RectTransform scoreTextRect;
    [SerializeField] private RectTransform holesContainerRect;

    [Header("Grid")]
    [SerializeField] private int gridColumns = 4;
    [SerializeField] private int gridRows = 2;
    [SerializeField] private Vector2 cellSize = new(100f, 100f);
    [SerializeField] private Vector2 cellSpacing = new(10f, 10f);

    [Header("Timing")]
    [SerializeField] private float bugLifetime = 0.8f;
    [SerializeField] private float minSpawnInterval = 0.3f;
    [SerializeField] private float maxSpawnInterval = 0.95f;
    [SerializeField] private float squashDisplayTime = 0.35f;
    [SerializeField] private float glitchInterval = 0.05f;
    [SerializeField] private int glitchFrames = 4;

    [Header("Visuals")]
    [SerializeField] private Color titleColor = new(0.6f, 0.9f, 0.5f, 0.85f);
    [SerializeField] private Color scoreColor = new(0.98f, 0.73f, 0.34f, 1f);
    [SerializeField] private Color bugColor = new(0.98f, 0.73f, 0.34f, 1f);
    [SerializeField] private Color glitchColor = new(1f, 0.2f, 0.16f, 1f);
    [SerializeField] private Color squashColor = new(0.6f, 0.1f, 0.1f, 0.8f);
    [SerializeField] private Color holeHoverColor = new(0.12f, 0.3f, 0.2f, 0.35f);
    [SerializeField] private int bugFontSize = 50;
    [SerializeField] private int titleFontSize = 16;
    [SerializeField] private int scoreFontSize = 20;
    [SerializeField] private int instructionFontSize = 12;

    private static readonly string[] BugCharacters = { "~", "&", "*", "@", "%" };
    private static readonly string[] GlitchCharacters = { "@", "#", "$", "%", "&", "*", "!", "?", "/", "\\", "|", "~", "+", "=", "<", ">" };
    private const string SquashCharacter = "#";
    private const string TitleString = "BUG SQUASH";
    private const string InstructionString = "click bugs to squash";

    public static int SessionHighScore { get; private set; }

    private Transform miniGameContainer;
    private Text titleText;
    private Text scoreText;
    private Text instructionText;
    private GridLayoutGroup holesGrid;
    private BugHole[] holes;

    private int score;
    private bool isRunning;
    private Coroutine spawnRoutine;
    private Font font;

    private readonly List<int> activeHoleIndices = new();
    private readonly List<int> freeHoleIndices = new();

    private sealed class BugHole
    {
        public GameObject Root;
        public Image Background;
        public Button Button;
        public Text BugChar;
        public bool IsActive;
        public Coroutine LifetimeRoutine;
        public Color NormalColor;
    }

    public void BuildUI(Transform container, Font uiFont)
    {
        miniGameContainer = container;
        font = uiFont;

        if (miniGameContainer == null)
        {
            Debug.LogError("BugSquashMinigame.BuildUI: container is null.");
            return;
        }

        RectTransform containerRect = miniGameContainer as RectTransform;
        if (containerRect == null)
        {
            Debug.LogError("BugSquashMinigame.BuildUI: MiniGame container must be a UI object with a RectTransform (place it under the Canvas).");
            miniGameContainer = null;
            return;
        }

        if (containerRect.sizeDelta.magnitude < 1f)
        {
            containerRect.anchorMin = new Vector2(0.5f, 0.5f);
            containerRect.anchorMax = new Vector2(0.5f, 0.5f);
            containerRect.pivot = new Vector2(0.5f, 0.5f);
            containerRect.anchoredPosition = Vector2.zero;
            containerRect.sizeDelta = new Vector2(720f, 320f);
        }

        EnsureCanvasGroup();
        ClearContainer();
        BuildTitleAndScore();
        BuildInstruction();
        BuildHolesGrid();

        miniGameContainer.gameObject.SetActive(false);
    }

    private void EnsureCanvasGroup()
    {
        CanvasGroup group = miniGameContainer.GetComponent<CanvasGroup>();
        if (group == null)
        {
            group = miniGameContainer.gameObject.AddComponent<CanvasGroup>();
        }
    }

    private void ClearContainer()
    {
        for (int i = miniGameContainer.childCount - 1; i >= 0; i--)
        {
            Transform child = miniGameContainer.GetChild(i);
            if (child.name.StartsWith("Bug Squash"))
            {
                DestroyImmediate(child.gameObject);
            }
        }
    }

    private void BuildTitleAndScore()
    {
        GameObject titleObj = new("Bug Squash Title", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        titleObj.transform.SetParent(miniGameContainer, false);
        titleTextRect = titleObj.GetComponent<RectTransform>();
        AnchorTop(titleTextRect, 0f, 0f, 0f, 28f);

        titleText = titleObj.GetComponent<Text>();
        titleText.font = font;
        titleText.text = TitleString;
        titleText.fontSize = titleFontSize;
        titleText.fontStyle = FontStyle.Bold;
        titleText.alignment = TextAnchor.UpperLeft;
        titleText.color = titleColor;
        titleText.horizontalOverflow = HorizontalWrapMode.Overflow;
        titleText.verticalOverflow = VerticalWrapMode.Overflow;
        titleText.raycastTarget = false;

        GameObject scoreObj = new("Bug Squash Score", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        scoreObj.transform.SetParent(miniGameContainer, false);
        scoreTextRect = scoreObj.GetComponent<RectTransform>();
        AnchorTop(scoreTextRect, 0f, 0f, 0f, 28f);

        scoreText = scoreObj.GetComponent<Text>();
        scoreText.font = font;
        scoreText.text = "0";
        scoreText.fontSize = scoreFontSize;
        scoreText.fontStyle = FontStyle.Bold;
        scoreText.alignment = TextAnchor.UpperRight;
        scoreText.color = scoreColor;
        scoreText.horizontalOverflow = HorizontalWrapMode.Overflow;
        scoreText.verticalOverflow = VerticalWrapMode.Overflow;
        scoreText.raycastTarget = false;
    }

    private void BuildInstruction()
    {
        GameObject instructionObj = new("Bug Squash Instruction", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        instructionObj.transform.SetParent(miniGameContainer, false);
        RectTransform rect = instructionObj.GetComponent<RectTransform>();
        AnchorBottom(rect, 0f, 0f, 0f, 20f);

        instructionText = instructionObj.GetComponent<Text>();
        instructionText.font = font;
        instructionText.text = InstructionString;
        instructionText.fontSize = instructionFontSize;
        instructionText.fontStyle = FontStyle.Normal;
        instructionText.alignment = TextAnchor.LowerCenter;
        instructionText.color = new Color(0.5f, 0.7f, 0.45f, 0.7f);
        instructionText.horizontalOverflow = HorizontalWrapMode.Overflow;
        instructionText.verticalOverflow = VerticalWrapMode.Overflow;
        instructionText.raycastTarget = false;
    }

    private void BuildHolesGrid()
    {
        GameObject holesObj = new("Bug Squash Holes", typeof(RectTransform), typeof(GridLayoutGroup));
        holesObj.transform.SetParent(miniGameContainer, false);
        holesContainerRect = holesObj.GetComponent<RectTransform>();

        float totalWidth = gridColumns * cellSize.x + (gridColumns + 1) * cellSpacing.x;
        float totalHeight = gridRows * cellSize.y + (gridRows + 1) * cellSpacing.y;
        holesContainerRect.anchorMin = new Vector2(0.5f, 0.5f);
        holesContainerRect.anchorMax = new Vector2(0.5f, 0.5f);
        holesContainerRect.pivot = new Vector2(0.5f, 0.5f);
        holesContainerRect.anchoredPosition = Vector2.zero;
        holesContainerRect.sizeDelta = new Vector2(totalWidth, totalHeight);

        holesGrid = holesObj.GetComponent<GridLayoutGroup>();
        holesGrid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        holesGrid.constraintCount = gridColumns;
        holesGrid.cellSize = cellSize;
        holesGrid.spacing = cellSpacing;
        holesGrid.startCorner = GridLayoutGroup.Corner.UpperLeft;
        holesGrid.startAxis = GridLayoutGroup.Axis.Horizontal;
        holesGrid.childAlignment = TextAnchor.MiddleCenter;

        int totalHoles = gridColumns * gridRows;
        holes = new BugHole[totalHoles];

        for (int i = 0; i < totalHoles; i++)
        {
            BugHole hole = CreateHole(holesObj.transform, i);
            holes[i] = hole;
        }
    }

    private BugHole CreateHole(Transform parent, int index)
    {
        var holeObj = new GameObject($"Bug Hole {index}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        holeObj.transform.SetParent(parent, false);

        Image bg = holeObj.GetComponent<Image>();
        bg.color = Color.clear;
        bg.raycastTarget = true;

        RectTransform rect = holeObj.GetComponent<RectTransform>();
        rect.sizeDelta = cellSize;

        Button button = holeObj.GetComponent<Button>();
        button.targetGraphic = bg;
        ColorBlock colors = button.colors;
        colors.normalColor = Color.clear;
        colors.highlightedColor = holeHoverColor;
        colors.pressedColor = new Color(holeHoverColor.r, holeHoverColor.g, holeHoverColor.b, holeHoverColor.a * 1.4f);
        colors.selectedColor = Color.clear;
        colors.disabledColor = Color.clear;
        colors.fadeDuration = 0.08f;
        button.colors = colors;

        var textObj = new GameObject("Bug Char", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        textObj.transform.SetParent(holeObj.transform, false);

        Text charText = textObj.GetComponent<Text>();
        charText.font = font;
        charText.text = string.Empty;
        charText.fontSize = bugFontSize;
        charText.fontStyle = FontStyle.Bold;
        charText.alignment = TextAnchor.MiddleCenter;
        charText.color = bugColor;
        charText.raycastTarget = false;
        charText.horizontalOverflow = HorizontalWrapMode.Overflow;
        charText.verticalOverflow = VerticalWrapMode.Overflow;
        charText.resizeTextForBestFit = false;

        RectTransform textRect = textObj.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        var hole = new BugHole
        {
            Root = holeObj,
            Background = bg,
            Button = button,
            BugChar = charText,
            IsActive = false,
            NormalColor = Color.clear
        };

        int capturedIndex = index;
        button.onClick.AddListener(() => OnHoleClicked(capturedIndex));

        return hole;
    }

    private void OnHoleClicked(int index)
    {
        if (!isRunning || index < 0 || index >= holes.Length)
        {
            return;
        }

        BugHole hole = holes[index];
        if (!hole.IsActive)
        {
            return;
        }

        SquashBug(index);
    }

    public void StartMinigame()
    {
        if (isRunning)
        {
            return;
        }

        if (miniGameContainer == null)
        {
            return;
        }

        score = 0;
        isRunning = true;
        activeHoleIndices.Clear();
        freeHoleIndices.Clear();
        for (int i = 0; i < holes.Length; i++)
        {
            freeHoleIndices.Add(i);
        }

        UpdateScoreDisplay();
        miniGameContainer.gameObject.SetActive(true);
        LayoutRebuilder.ForceRebuildLayoutImmediate(holesContainerRect);
        spawnRoutine = StartCoroutine(SpawnLoop());
    }

    public int StopMinigame()
    {
        if (!isRunning)
        {
            return score;
        }

        isRunning = false;

        if (spawnRoutine != null)
        {
            StopCoroutine(spawnRoutine);
            spawnRoutine = null;
        }

        for (int i = 0; i < holes.Length; i++)
        {
            if (holes[i] != null && holes[i].IsActive)
            {
                HideBug(i);
            }
        }

        activeHoleIndices.Clear();
        freeHoleIndices.Clear();

        if (miniGameContainer != null)
        {
            miniGameContainer.gameObject.SetActive(false);
        }

        if (score > SessionHighScore)
        {
            SessionHighScore = score;
        }

        return score;
    }

    private IEnumerator SpawnLoop()
    {
        yield return new WaitForSeconds(0.25f);

        float elapsed = 0f;
        while (isRunning)
        {
            float interval = Mathf.Lerp(maxSpawnInterval, minSpawnInterval, Mathf.Clamp01(elapsed / 25f));
            yield return new WaitForSeconds(Random.Range(interval * 0.7f, interval));

            if (!isRunning)
            {
                yield break;
            }

            if (freeHoleIndices.Count > 0)
            {
                SpawnBug();
            }

            elapsed += interval;
        }
    }

    private void SpawnBug()
    {
        if (freeHoleIndices.Count == 0)
        {
            return;
        }

        int pickIndex = Random.Range(0, freeHoleIndices.Count);
        int holeIndex = freeHoleIndices[pickIndex];
        freeHoleIndices.RemoveAt(pickIndex);
        activeHoleIndices.Add(holeIndex);

        BugHole hole = holes[holeIndex];
        hole.IsActive = true;

        string bugChar = BugCharacters[Random.Range(0, BugCharacters.Length)];
        hole.BugChar.text = bugChar;
        hole.BugChar.color = bugColor;

        StartCoroutine(GlitchIntro(hole, bugChar));
        hole.LifetimeRoutine = StartCoroutine(BugLifetimeRoutine(holeIndex));
    }

    private IEnumerator GlitchIntro(BugHole hole, string finalChar)
    {
        for (int i = 0; i < glitchFrames; i++)
        {
            hole.BugChar.text = GlitchCharacters[Random.Range(0, GlitchCharacters.Length)];
            hole.BugChar.color = glitchColor;
            yield return new WaitForSeconds(glitchInterval);
        }

        hole.BugChar.text = finalChar;
        hole.BugChar.color = bugColor;
    }

    private IEnumerator BugLifetimeRoutine(int holeIndex)
    {
        yield return new WaitForSeconds(bugLifetime);

        if (isRunning && holeIndex >= 0 && holeIndex < holes.Length && holes[holeIndex].IsActive)
        {
            HideBug(holeIndex);
        }
    }

    private void SquashBug(int holeIndex)
    {
        BugHole hole = holes[holeIndex];
        hole.IsActive = false;

        if (hole.LifetimeRoutine != null)
        {
            StopCoroutine(hole.LifetimeRoutine);
            hole.LifetimeRoutine = null;
        }

        hole.BugChar.text = SquashCharacter;
        hole.BugChar.color = squashColor;
        score++;
        UpdateScoreDisplay();

        StartCoroutine(SquashDisplayRoutine(holeIndex));
    }

    private IEnumerator SquashDisplayRoutine(int holeIndex)
    {
        yield return new WaitForSeconds(squashDisplayTime);

        if (holeIndex >= 0 && holeIndex < holes.Length)
        {
            BugHole hole = holes[holeIndex];
            hole.BugChar.text = string.Empty;

            activeHoleIndices.Remove(holeIndex);
            freeHoleIndices.Add(holeIndex);
        }
    }

    private void HideBug(int holeIndex)
    {
        BugHole hole = holes[holeIndex];
        hole.IsActive = false;

        if (hole.LifetimeRoutine != null)
        {
            StopCoroutine(hole.LifetimeRoutine);
            hole.LifetimeRoutine = null;
        }

        hole.BugChar.text = string.Empty;

        activeHoleIndices.Remove(holeIndex);
        freeHoleIndices.Add(holeIndex);
    }

    private void UpdateScoreDisplay()
    {
        if (scoreText != null)
        {
            scoreText.text = score.ToString();
        }
    }

    private static void StretchToParent(RectTransform rect, float left, float top, float right, float bottom)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(left, bottom);
        rect.offsetMax = new Vector2(-right, -top);
    }

    private static void AnchorTop(RectTransform rect, float left, float top, float right, float height)
    {
        rect.anchorMin = Vector2.up;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(left, -top - height);
        rect.offsetMax = new Vector2(-right, -top);
    }

    private static void AnchorBottom(RectTransform rect, float left, float bottom, float right, float height)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.right;
        rect.offsetMin = new Vector2(left, bottom);
        rect.offsetMax = new Vector2(-right, bottom + height);
    }
}
