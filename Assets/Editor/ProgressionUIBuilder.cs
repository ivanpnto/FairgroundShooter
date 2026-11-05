using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// Construye la interfaz de la Fase 5 con la estética de feria de StartPanel/GameOverPanel (fondo, toldo,
// cartel con bombillas, tickets, fuente Rye). Menú: Tools > Fairground > Build Progression UI.
//
// - Crea (o rehace) los prefabs ProfilePanel, ShopPanel y RecordsPanel en Assets/Prefabs/UI.
// - Adapta los prefabs StartPanel (fuera el campo de nombre; perfil, créditos, tienda, récords) y
//   GameOverPanel (créditos, resumen, tienda, récords) si aún no están adaptados.
// - En Main.unity: instancia los paneles nuevos bajo el Canvas UI, añade MenuController al Canvas, el
//   multiplicador de racha al HUD y el texto del bonus a LevelTransitionPanel, y guarda la escena.
//
// Se guarda en el repo para poder regenerar los paneles si cambia el diseño; los retoques a mano en los
// prefabs se perderían al volver a ejecutarlo en los paneles nuevos (no en Inicio ni Game Over, que solo
// se tocan la primera vez).
public static class ProgressionUIBuilder
{
    private const string UIFolder = "Assets/Prefabs/UI/";

    private static readonly Color Gold = new Color(0.93f, 0.74f, 0.31f);
    private static readonly Color Cream = new Color(0.96f, 0.91f, 0.79f);
    private static readonly Color Brown = new Color(0.35f, 0.10f, 0.06f);
    private static readonly Color RowColor = new Color(0.22f, 0.10f, 0.05f, 0.85f);
    private static readonly Color MessageColor = new Color(1f, 0.55f, 0.45f);

    private static TMP_FontAsset rye;
    private static TMP_FontAsset sans;
    private static Material ryeCartel;
    private static Sprite background;
    private static Sprite board;
    private static Sprite ticket;
    private static GameObject gameOverAsset;
    private static GameObject startAsset;

    [MenuItem("Tools/Fairground/Build Progression UI")]
    public static void Build()
    {
        LoadStyle();

        // Antes de tocar StartPanel: ProfilePanel copia su campo de nombre
        GameObject profilePanel = SavePrefab(BuildProfilePanel(), "ProfilePanel");
        GameObject shopPanel = SavePrefab(BuildShopPanel(), "ShopPanel");
        GameObject recordsPanel = SavePrefab(BuildRecordsPanel(), "RecordsPanel");

        UpdateStartPanel();
        UpdateGameOverPanel();
        WireScene(profilePanel, shopPanel, recordsPanel);

        Debug.Log("ProgressionUIBuilder: interfaz de progresión construida.");
    }

    // ── Estilo ──────────────────────────────────────────────────────────────────

    private static void LoadStyle()
    {
        gameOverAsset = AssetDatabase.LoadAssetAtPath<GameObject>(UIFolder + "GameOverPanel.prefab");
        startAsset = AssetDatabase.LoadAssetAtPath<GameObject>(UIFolder + "StartPanel.prefab");

        rye = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/Rye SDF.asset");
        ryeCartel = AssetDatabase.LoadAssetAtPath<Material>("Assets/Fonts/Rye SDF - Cartel.mat");
        sans = gameOverAsset.transform.Find("RankingBoard/RankingColumns/RankingNamesText").GetComponent<TMP_Text>().font;

        background = gameOverAsset.GetComponent<Image>().sprite;
        board = gameOverAsset.transform.Find("Sign").GetComponent<Image>().sprite;
        ticket = gameOverAsset.transform.Find("ScoreTicket").GetComponent<Image>().sprite;
    }

    // Fondo + toldo + cartel con bombillas, copiados del Game Over
    private static GameObject NewPanel(string name, string title, float titleSize)
    {
        GameObject panel = new GameObject(name, typeof(RectTransform), typeof(Image));
        Stretch(panel.GetComponent<RectTransform>());
        panel.GetComponent<Image>().sprite = background;

        Object.Instantiate(gameOverAsset.transform.Find("Valance").gameObject, panel.transform).name = "Valance";
        GameObject sign = Object.Instantiate(gameOverAsset.transform.Find("Sign").gameObject, panel.transform);
        sign.name = "Sign";

        TMP_Text titleText = sign.transform.Find("TitleText").GetComponent<TMP_Text>();
        titleText.text = title;
        titleText.fontSize = titleSize;
        return panel;
    }

    // Cartel: Rye con contorno (material Cartel), para textos sobre el fondo
    private static TextMeshProUGUI CartelText(Transform parent, string name, string text, float size, Color color,
        Vector2 pos, Vector2 box)
    {
        TextMeshProUGUI t = NewText(parent, name, text, rye, size, color, pos, box);
        t.fontSharedMaterial = ryeCartel;
        return t;
    }

    private static TextMeshProUGUI NewText(Transform parent, string name, string text, TMP_FontAsset font, float size,
        Color color, Vector2 pos, Vector2 box)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        Place(go.GetComponent<RectTransform>(), pos, box);

        TextMeshProUGUI t = go.GetComponent<TextMeshProUGUI>();
        t.font = font;
        t.text = text;
        t.fontSize = size;
        t.color = color;
        t.alignment = TextAlignmentOptions.Center;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.raycastTarget = false;
        return t;
    }

    private static Image NewBoard(Transform parent, string name, Vector2 pos, Vector2 box)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        Place(go.GetComponent<RectTransform>(), pos, box);
        Image image = go.GetComponent<Image>();
        image.sprite = board;
        image.type = Image.Type.Sliced;
        return image;
    }

    // Botón ticket, como ¡A JUGAR! y OTRA RONDA
    private static Button TicketButton(Transform parent, string name, string label, Vector2 pos, Vector2 box, float fontSize)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        Place(go.GetComponent<RectTransform>(), pos, box);

        Image image = go.GetComponent<Image>();
        image.sprite = ticket;
        image.type = Image.Type.Sliced;

        Button button = go.GetComponent<Button>();
        ColorBlock colors = button.colors;
        colors.highlightedColor = new Color(1f, 0.95f, 0.8f);
        colors.pressedColor = new Color(0.85f, 0.75f, 0.6f);
        colors.selectedColor = Color.white;
        colors.disabledColor = new Color(0.55f, 0.5f, 0.45f, 0.6f);
        button.colors = colors;

        TextMeshProUGUI text = NewText(go.transform, "Text", label, rye, fontSize, Brown, Vector2.zero, Vector2.zero);
        Stretch(text.rectTransform);
        return button;
    }

    private static void Place(RectTransform rt, Vector2 pos, Vector2 box)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = box;
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    // Columna vertical que crece hacia abajo (lista de perfiles, filas de la tienda)
    private static RectTransform NewList(Transform parent, string name, Vector2 pos, Vector2 box, float spacing)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(VerticalLayoutGroup));
        go.transform.SetParent(parent, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        Place(rt, pos, box);

        VerticalLayoutGroup layout = go.GetComponent<VerticalLayoutGroup>();
        layout.spacing = spacing;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        return rt;
    }

    private static void SetRefs(Object target, params object[] pairs)
    {
        SerializedObject so = new SerializedObject(target);
        for (int i = 0; i < pairs.Length; i += 2)
        {
            SerializedProperty p = so.FindProperty((string)pairs[i]);
            if (pairs[i + 1] is int number) p.intValue = number;
            else p.objectReferenceValue = (Object)pairs[i + 1];
        }
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static GameObject SavePrefab(GameObject root, string name)
    {
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, UIFolder + name + ".prefab");
        Object.DestroyImmediate(root);
        return prefab;
    }

    // ── Perfiles ────────────────────────────────────────────────────────────────

    private static GameObject BuildProfilePanel()
    {
        GameObject panel = NewPanel("ProfilePanel", "¿QUIÉN DISPARA?", 72);
        Transform root = panel.transform;

        NewBoard(root, "ProfileBoard", new Vector2(0f, -110f), new Vector2(1000f, 600f));
        CartelText(root, "ChooseLabel", "ELIGE TU PERFIL", 40, Gold, new Vector2(0f, 135f), new Vector2(800f, 56f));

        // Lista con scroll (rueda del ratón) por si hay muchos perfiles
        GameObject scroll = new GameObject("ProfileList", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
        scroll.transform.SetParent(root, false);
        Place(scroll.GetComponent<RectTransform>(), new Vector2(0f, -20f), new Vector2(820f, 230f));
        scroll.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.25f);

        GameObject viewport = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D));
        viewport.transform.SetParent(scroll.transform, false);
        Stretch(viewport.GetComponent<RectTransform>());

        RectTransform content = NewList(viewport.transform, "Content", Vector2.zero, Vector2.zero, 8f);
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.anchoredPosition = Vector2.zero;
        content.sizeDelta = Vector2.zero;
        content.GetComponent<VerticalLayoutGroup>().padding = new RectOffset(10, 10, 10, 10);
        content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        ScrollRect scrollRect = scroll.GetComponent<ScrollRect>();
        scrollRect.content = content;
        scrollRect.viewport = viewport.GetComponent<RectTransform>();
        scrollRect.horizontal = false;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.scrollSensitivity = 30f;

        // Plantilla de cada perfil (desactivada; ProfilePanelUI la clona)
        GameObject entry = new GameObject("EntryTemplate", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        entry.transform.SetParent(content, false);
        entry.GetComponent<Image>().color = RowColor;
        entry.GetComponent<LayoutElement>().preferredHeight = 56f;
        ColorBlock colors = entry.GetComponent<Button>().colors;
        colors.highlightedColor = new Color(1.4f, 1.3f, 1.1f);
        colors.pressedColor = new Color(0.8f, 0.7f, 0.6f);
        entry.GetComponent<Button>().colors = colors;
        TextMeshProUGUI entryText = NewText(entry.transform, "Text", "Nombre", rye, 30, Cream, Vector2.zero, Vector2.zero);
        Stretch(entryText.rectTransform);

        TextMeshProUGUI empty = NewText(root, "EmptyText", "Aún no hay perfiles: crea el primero", sans, 28, Cream,
            new Vector2(0f, -20f), new Vector2(800f, 50f));

        CartelText(root, "CreateLabel", "O CREA UNO NUEVO", 34, Gold, new Vector2(0f, -185f), new Vector2(800f, 50f));

        GameObject input = Object.Instantiate(startAsset.transform.Find("NameInputField").gameObject, root);
        input.name = "NameInputField";
        Place(input.GetComponent<RectTransform>(), new Vector2(-120f, -255f), new Vector2(520f, 64f));
        TMP_InputField inputField = input.GetComponent<TMP_InputField>();
        ((TMP_Text)inputField.placeholder).text = "Nombre del nuevo tirador…";
        inputField.text = "";

        Button create = TicketButton(root, "CreateButton", "CREAR", new Vector2(290f, -255f), new Vector2(220f, 90f), 36);

        TextMeshProUGUI message = CartelText(root, "MessageText", "", 28, MessageColor, new Vector2(0f, -335f), new Vector2(900f, 44f));

        ProfilePanelUI ui = panel.AddComponent<ProfilePanelUI>();
        SetRefs(ui, "listContent", content, "entryTemplate", entry.GetComponent<Button>(), "nameInput", inputField,
            "createButton", create, "messageText", message, "emptyText", empty);
        return panel;
    }

    // ── Tienda ──────────────────────────────────────────────────────────────────

    private static GameObject BuildShopPanel()
    {
        GameObject panel = NewPanel("ShopPanel", "TIENDA", 100);
        Transform root = panel.transform;

        TextMeshProUGUI credits = CartelText(root, "CreditsText", "TUS CRÉDITOS: 0", 44, Gold, new Vector2(0f, 160f), new Vector2(900f, 60f));

        NewBoard(root, "UpgradesBoard", new Vector2(-475f, -160f), new Vector2(900f, 560f));
        CartelText(root, "UpgradesLabel", "MEJORAS", 38, Gold, new Vector2(-475f, 75f), new Vector2(800f, 50f));
        RectTransform upgrades = NewList(root, "UpgradesList", new Vector2(-475f, -195f), new Vector2(820f, 440f), 6f);

        NewBoard(root, "WeaponsBoard", new Vector2(475f, -160f), new Vector2(900f, 560f));
        CartelText(root, "WeaponsLabel", "ARMAS", 38, Gold, new Vector2(475f, 75f), new Vector2(800f, 50f));
        RectTransform weapons = NewList(root, "WeaponsList", new Vector2(475f, -195f), new Vector2(820f, 440f), 6f);

        // Plantilla de fila (desactivada; ShopPanelUI la clona en una u otra lista)
        GameObject row = new GameObject("RowTemplate", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        row.transform.SetParent(upgrades, false);
        row.GetComponent<Image>().color = RowColor;
        row.GetComponent<LayoutElement>().preferredHeight = 84f;

        TextMeshProUGUI title = NewText(row.transform, "Title", "Mejora 0/3", rye, 28, Cream, new Vector2(20f, 18f), new Vector2(560f, 36f));
        LeftAnchor(title.rectTransform);
        title.alignment = TextAlignmentOptions.Left;

        TextMeshProUGUI info = NewText(row.transform, "Info", "Descripción", sans, 19, new Color(Cream.r, Cream.g, Cream.b, 0.85f),
            new Vector2(20f, -18f), new Vector2(570f, 44f));
        LeftAnchor(info.rectTransform);
        info.alignment = TextAlignmentOptions.TopLeft;
        info.textWrappingMode = TextWrappingModes.Normal;

        Button buy = TicketButton(row.transform, "BuyButton", "100 CR.", Vector2.zero, new Vector2(200f, 70f), 28);
        RectTransform buyRt = buy.GetComponent<RectTransform>();
        buyRt.anchorMin = buyRt.anchorMax = buyRt.pivot = new Vector2(1f, 0.5f);
        buyRt.anchoredPosition = new Vector2(-12f, 0f);

        ShopRowUI rowUI = row.AddComponent<ShopRowUI>();
        SetRefs(rowUI, "titleText", title, "infoText", info, "buyButton", buy, "buyText", buy.GetComponentInChildren<TMP_Text>());

        Button back = TicketButton(root, "BackButton", "VOLVER", new Vector2(0f, -490f), new Vector2(300f, 90f), 36);

        ShopPanelUI ui = panel.AddComponent<ShopPanelUI>();
        SetRefs(ui, "creditsText", credits, "upgradesContent", upgrades, "weaponsContent", weapons,
            "rowTemplate", rowUI, "backButton", back);
        return panel;
    }

    private static void LeftAnchor(RectTransform rt)
    {
        Vector2 pos = rt.anchoredPosition;
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f);
        rt.pivot = new Vector2(0f, 0.5f);
        rt.anchoredPosition = pos;
    }

    // ── Récords ─────────────────────────────────────────────────────────────────

    private static GameObject BuildRecordsPanel()
    {
        GameObject panel = NewPanel("RecordsPanel", "RÉCORDS", 100);
        Transform root = panel.transform;

        NewBoard(root, "RecordsBoard", new Vector2(0f, -110f), new Vector2(900f, 600f));
        CartelText(root, "RecordsLabel", "MEJOR PUNTUACIÓN DE CADA TIRADOR", 34, Gold, new Vector2(0f, 135f), new Vector2(820f, 50f));

        // Las mismas columnas que tenía el ranking del Game Over
        GameObject columns = Object.Instantiate(gameOverAsset.transform.Find("RankingBoard/RankingColumns").gameObject, root);
        columns.name = "RecordsColumns";
        Place(columns.GetComponent<RectTransform>(), new Vector2(0f, -140f), new Vector2(700f, 440f));

        TicketButton(root, "BackButton", "VOLVER", new Vector2(0f, -470f), new Vector2(300f, 90f), 36)
            .gameObject.AddComponent<MenuBackButton>();

        RecordsUI ui = panel.AddComponent<RecordsUI>();
        SetRefs(ui, "rankingNamesText", columns.transform.Find("RankingNamesText").GetComponent<TMP_Text>(),
            "rankingScoresText", columns.transform.Find("RankingScoresText").GetComponent<TMP_Text>(),
            "entriesToShow", 10);
        return panel;
    }

    // ── Inicio ──────────────────────────────────────────────────────────────────

    private static void UpdateStartPanel()
    {
        string path = UIFolder + "StartPanel.prefab";
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            if (root.GetComponent<StartPanelUI>() != null)
                return;   // ya adaptado

            Object.DestroyImmediate(root.transform.Find("NameLabel").gameObject);
            Object.DestroyImmediate(root.transform.Find("NameInputField").gameObject);

            // Justo detrás de PlayerBoard, para dibujarse encima de él
            int index = root.transform.Find("PlayerBoard").GetSiblingIndex() + 1;

            TextMeshProUGUI profile = CartelText(root.transform, "ProfileText", "TIRADOR: -", 40, Gold, new Vector2(-95f, -15f), new Vector2(560f, 56f));
            Button change = TicketButton(root.transform, "ChangeProfileButton", "CAMBIAR", new Vector2(285f, -15f), new Vector2(190f, 70f), 22);
            TextMeshProUGUI credits = CartelText(root.transform, "CreditsText", "0 CRÉDITOS", 30, Cream, new Vector2(0f, -75f), new Vector2(700f, 44f));
            profile.transform.SetSiblingIndex(index);
            change.transform.SetSiblingIndex(index + 1);
            credits.transform.SetSiblingIndex(index + 2);

            Button shop = TicketButton(root.transform, "ShopButton", "TIENDA", new Vector2(-500f, -410f), new Vector2(320f, 120f), 44);
            Button records = TicketButton(root.transform, "RecordsButton", "RÉCORDS", new Vector2(500f, -410f), new Vector2(320f, 120f), 44);

            StartPanelUI ui = root.AddComponent<StartPanelUI>();
            SetRefs(ui, "profileText", profile, "creditsText", credits, "shopButton", shop,
                "recordsButton", records, "changeProfileButton", change);

            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // ── Fin de partida ──────────────────────────────────────────────────────────

    private static void UpdateGameOverPanel()
    {
        string path = UIFolder + "GameOverPanel.prefab";
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            if (root.GetComponent<GameOverSummaryUI>() != null)
                return;   // ya adaptado

            // Los récords tienen ahora su propia pantalla (botón RÉCORDS); el tablero pasa a ser el resumen
            RecordsUI records = root.GetComponent<RecordsUI>();
            if (records != null)
                Object.DestroyImmediate(records);

            Transform boardT = root.transform.Find("RankingBoard");
            boardT.name = "SummaryBoard";
            Place((RectTransform)boardT, new Vector2(0f, -130f), new Vector2(820f, 290f));
            Object.DestroyImmediate(boardT.Find("RankingColumns").gameObject);
            Transform title = boardT.Find("RankingTitleText");
            title.name = "SummaryTitleText";
            title.GetComponent<TMP_Text>().text = "RESUMEN DE LA PARTIDA";
            ((RectTransform)title).anchoredPosition = new Vector2(0f, 100f);

            TextMeshProUGUI summary = NewText(boardT, "SummaryText", "", sans, 28, Cream, new Vector2(0f, -25f), new Vector2(740f, 190f));
            summary.lineSpacing = 10f;

            TextMeshProUGUI credits = CartelText(root.transform, "CreditsText", "+0 CRÉDITOS", 36, Gold, new Vector2(0f, 42f), new Vector2(900f, 46f));
            credits.transform.SetSiblingIndex(root.transform.Find("ScoreTicket").GetSiblingIndex() + 1);

            Button shop = TicketButton(root.transform, "ShopButton", "TIENDA", new Vector2(-500f, -462f), new Vector2(320f, 110f), 40);
            Button recordsButton = TicketButton(root.transform, "RecordsButton", "RÉCORDS", new Vector2(500f, -462f), new Vector2(320f, 110f), 40);

            GameOverSummaryUI ui = root.AddComponent<GameOverSummaryUI>();
            SetRefs(ui, "creditsText", credits, "summaryText", summary, "shopButton", shop, "recordsButton", recordsButton);

            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // ── Escena ──────────────────────────────────────────────────────────────────

    private static void WireScene(GameObject profilePrefab, GameObject shopPrefab, GameObject recordsPrefab)
    {
        GameManager gameManager = Object.FindFirstObjectByType<GameManager>(FindObjectsInactive.Include);
        SerializedObject gm = new SerializedObject(gameManager);
        GameObject startPanel = (GameObject)gm.FindProperty("startPanel").objectReferenceValue;
        GameObject gameOverPanel = (GameObject)gm.FindProperty("gameOverPanel").objectReferenceValue;
        GameObject hud = (GameObject)gm.FindProperty("hud").objectReferenceValue;
        GameObject transition = (GameObject)gm.FindProperty("levelTransitionPanel").objectReferenceValue;
        Transform canvas = startPanel.transform.parent;

        GameObject profilePanel = EnsureInstance(canvas, profilePrefab, gameOverPanel.transform.GetSiblingIndex() + 1);
        GameObject shopPanel = EnsureInstance(canvas, shopPrefab, profilePanel.transform.GetSiblingIndex() + 1);
        GameObject recordsPanel = EnsureInstance(canvas, recordsPrefab, shopPanel.transform.GetSiblingIndex() + 1);

        MenuController menu = canvas.GetComponent<MenuController>();
        if (menu == null) menu = canvas.gameObject.AddComponent<MenuController>();
        SetRefs(menu, "profilePanel", profilePanel, "startPanel", startPanel, "shopPanel", shopPanel,
            "recordsPanel", recordsPanel, "gameOverPanel", gameOverPanel);

        // Racha en el HUD, bajo la puntuación y con su misma fuente
        if (hud.transform.Find("StreakText") == null)
        {
            TMP_Text score = hud.transform.Find("ScoreText").GetComponent<TMP_Text>();
            GameObject streak = Object.Instantiate(score.gameObject, hud.transform);
            streak.name = "StreakText";
            RectTransform rt = streak.GetComponent<RectTransform>();
            rt.anchoredPosition = ((RectTransform)score.transform).anchoredPosition + new Vector2(0f, -60f);
            TMP_Text text = streak.GetComponent<TMP_Text>();
            text.text = "×1";
            text.fontSize = 48;
            text.fontStyle = FontStyles.Bold;
            streak.AddComponent<StreakUI>();
        }

        // Bonus de precisión en la transición entre niveles
        Transform bonus = transition.transform.Find("BonusText");
        if (bonus == null)
            bonus = CartelText(transition.transform, "BonusText", "BONUS DE PRECISIÓN: +0", 40, Cream,
                new Vector2(0f, -170f), new Vector2(1000f, 60f)).transform;
        gm.FindProperty("levelTransitionBonus").objectReferenceValue = bonus.GetComponent<TMP_Text>();
        gm.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(canvas.gameObject.scene);
        EditorSceneManager.SaveScene(canvas.gameObject.scene);
    }

    private static GameObject EnsureInstance(Transform canvas, GameObject prefab, int siblingIndex)
    {
        Transform existing = canvas.Find(prefab.name);
        GameObject instance = existing != null
            ? existing.gameObject
            : (GameObject)PrefabUtility.InstantiatePrefab(prefab, canvas);
        instance.transform.SetSiblingIndex(siblingIndex);
        instance.SetActive(false);
        return instance;
    }
}
