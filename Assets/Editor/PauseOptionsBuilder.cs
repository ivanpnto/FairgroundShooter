using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;
using static FairgroundUIKit;

// Monta el menú de pausa, el panel de OPCIONES (volumen) y el botón OPCIONES del inicio en la escena abierta
// (Fase 8). Menú: Tools > Fairground > Build Pause And Options.
//
// Se puede repetir: busca cada pieza por nombre, crea solo lo que falta y vuelve a enlazar las referencias.
// Toca la escena: en Main, avisar antes al compañero (§7). Mismo estilo que BossUIBuilder (tablero, Rye,
// botones copiados del de Récords), con los ayudantes de FairgroundUIKit.
public static class PauseOptionsBuilder
{
    // Estilo y ayudantes compartidos con LevelTransitionBuilder
    private static TMP_FontAsset rye => FairgroundUIKit.Rye;
    private static TMP_FontAsset sans => FairgroundUIKit.Sans;
    private static Material ryeCartel => FairgroundUIKit.RyeCartel;
    private static Sprite board => FairgroundUIKit.Board;
    private static Sprite barFill => FairgroundUIKit.BarFill;
    private static Sprite round => FairgroundUIKit.Round;

    [MenuItem("Tools/Fairground/Build Pause And Options")]
    public static void Build()
    {
        if (!EditorUtility.DisplayDialog("Build Pause And Options",
                "Monta el menú de pausa, el panel de opciones y el botón OPCIONES del inicio en la escena abierta." +
                "\n\nEn Main, avisa antes a tu compañero. ¿Continuar?", "Montar", "Cancelar"))
            return;
        Run();
    }

    public static void Run()
    {
        FairgroundUIKit.LoadStyle();
        GameObject canvasGo = GameObject.Find("UI");
        if (canvasGo == null) { Debug.LogError("PauseOptionsBuilder: no hay un Canvas llamado UI en la escena."); return; }
        Transform canvas = canvasGo.transform;
        Transform startPanel = canvas.Find("StartPanel");
        Button template = startPanel.Find("RecordsButton").GetComponent<Button>();

        GameManager gm = Object.FindFirstObjectByType<GameManager>();
        VolumeSettings volume = GetOrAdd<VolumeSettings>(gm.gameObject);
        SetRefs(volume, "mixer", AssetDatabase.LoadAssetAtPath<AudioMixer>("Assets/Audio/MainMixer.mixer"));

        OptionsPanelUI options = BuildOptions(canvas, startPanel, template);
        BuildPause(canvas, template, options);
        options.transform.SetAsLastSibling();   // las opciones, por encima de la pausa

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        Debug.Log("PauseOptionsBuilder: pausa y opciones montadas.");
    }

    // ── Opciones ────────────────────────────────────────────────────────────────

    private static OptionsPanelUI BuildOptions(Transform canvas, Transform startPanel, Button template)
    {
        Transform rootT = canvas.Find("Options") ?? NewRect(canvas, "Options").transform;
        Stretch((RectTransform)rootT);

        Image panel = Child<Image>(rootT, "Panel") ?? NewImage(rootT, "Panel", null);
        panel.color = Dim;
        panel.raycastTarget = true;   // bloquea los clics a lo que hay detrás
        Stretch(panel.rectTransform);

        // 1000 de alto para que quepa la sección CONTROLES debajo del volumen (el Canvas mide 1080)
        Image card = Card(panel.transform, new Vector2(1100f, 1000f));
        // 25/11: filas a 75 px (antes 85) para que quepa la fila PANTALLA sin agrandar el tablero
        Title(card.transform, "OPCIONES", 410f);
        TMP_Text sub = Child<TMP_Text>(card.transform, "Subtitle") ?? NewText(card.transform, "Subtitle", "", rye, 36, Gold);
        sub.text = "VOLUMEN";
        Place(sub.rectTransform, new Vector2(0f, 330f), new Vector2(800f, 50f));

        string[] names = { "GENERAL", "MÚSICA", "EFECTOS", "AMBIENTE" };   // orden de VolumeChannel
        var sliders = new Slider[names.Length];
        var values = new TMP_Text[names.Length];
        for (int i = 0; i < names.Length; i++)
        {
            SliderRow(card.transform, "Row" + i, names[i], 255f - i * 75f, out sliders[i], out values[i]);
            sliders[i].minValue = 0f; sliders[i].maxValue = 1f;
        }

        // ── Controles: sensibilidad del ratón e invertir el eje vertical (ControlSettings)
        TMP_Text controls = Child<TMP_Text>(card.transform, "ControlsSubtitle") ?? NewText(card.transform, "ControlsSubtitle", "", rye, 36, Gold);
        controls.text = "CONTROLES";
        Place(controls.rectTransform, new Vector2(0f, -40f), new Vector2(800f, 50f));

        SliderRow(card.transform, "SensitivityRow", "SENSIBILIDAD", -110f, out Slider sensitivity, out TMP_Text sensitivityValue);
        sensitivity.minValue = ControlSettings.MinSensitivity;
        sensitivity.maxValue = ControlSettings.MaxSensitivity;
        sensitivity.value = ControlSettings.DefaultSensitivity;
        sensitivityValue.text = "×1,00";

        Transform invertRow = card.transform.Find("InvertRow") ?? NewRect(card.transform, "InvertRow").transform;
        Place((RectTransform)invertRow, new Vector2(0f, -185f), new Vector2(900f, 80f));
        TMP_Text invertLabel = Child<TMP_Text>(invertRow, "Label") ?? NewText(invertRow, "Label", "", rye, 34, Cream);
        invertLabel.text = "INVERTIR EJE Y";
        invertLabel.alignment = TextAlignmentOptions.Left;
        Place(invertLabel.rectTransform, new Vector2(-250f, 0f), new Vector2(400f, 70f));
        Button invert = ChildButton(invertRow, "InvertButton", template);
        Place((RectTransform)invert.transform, new Vector2(300f, 0f), new Vector2(200f, 80f));
        TMP_Text invertText = invert.GetComponentInChildren<TMP_Text>();
        invertText.text = "NO";

        // ── Pantalla (25/11): completa o ventana, y resolución. Dos tiques que cambian al pulsarlos
        Transform displayRow = card.transform.Find("DisplayRow") ?? NewRect(card.transform, "DisplayRow").transform;
        Place((RectTransform)displayRow, new Vector2(0f, -260f), new Vector2(900f, 80f));
        TMP_Text displayLabel = Child<TMP_Text>(displayRow, "Label") ?? NewText(displayRow, "Label", "", rye, 34, Cream);
        displayLabel.text = "PANTALLA";
        displayLabel.alignment = TextAlignmentOptions.Left;
        Place(displayLabel.rectTransform, new Vector2(-250f, 0f), new Vector2(400f, 70f));
        Button screenMode = ChildButton(displayRow, "ScreenModeButton", template);
        Place((RectTransform)screenMode.transform, new Vector2(110f, 0f), new Vector2(230f, 80f));
        TMP_Text screenModeText = screenMode.GetComponentInChildren<TMP_Text>();
        screenModeText.text = "COMPLETA";
        Button resolution = ChildButton(displayRow, "ResolutionButton", template);
        Place((RectTransform)resolution.transform, new Vector2(350f, 0f), new Vector2(230f, 80f));
        TMP_Text resolutionText = resolution.GetComponentInChildren<TMP_Text>();
        resolutionText.text = "1920 x 1080";
        foreach (TMP_Text t in new[] { screenModeText, resolutionText })
        {
            t.enableAutoSizing = true; t.fontSizeMin = 16; t.fontSizeMax = 28;
            t.margin = new Vector4(30f, 0f, 30f, 0f);
        }

        Button close = ChildButton(card.transform, "CloseButton", template);
        Place((RectTransform)close.transform, new Vector2(0f, -380f), new Vector2(340f, 105f));
        close.GetComponentInChildren<TMP_Text>().text = "VOLVER";

        // OPCIONES en el inicio, debajo de CÓMO JUGAR (BossUIBuilder lo puso en (760, 220) con 300 × 100)
        Button open = ChildButton(startPanel, "OptionsButton", template);
        Place((RectTransform)open.transform, new Vector2(760f, 105f), new Vector2(300f, 100f));
        TMP_Text openText = open.GetComponentInChildren<TMP_Text>();
        openText.text = "OPCIONES";
        openText.fontSize = 28;

        OptionsPanelUI ui = GetOrAdd<OptionsPanelUI>(rootT.gameObject);
        SetRefs(ui, "panel", panel.gameObject, "sliders", sliders, "valueTexts", values, "closeButton", close, "openButton", open,
            "sensitivitySlider", sensitivity, "sensitivityText", sensitivityValue, "invertButton", invert, "invertText", invertText,
            "screenModeButton", screenMode, "screenModeText", screenModeText, "resolutionButton", resolution, "resolutionText", resolutionText);

        // SALIR en el inicio, debajo de OPCIONES (25/11): antes solo se salía de la build con Alt+F4
        Button exitStart = ChildButton(startPanel, "ExitButton", template);
        Place((RectTransform)exitStart.transform, new Vector2(760f, -10f), new Vector2(300f, 100f));
        TMP_Text exitStartText = exitStart.GetComponentInChildren<TMP_Text>();
        exitStartText.text = "SALIR";
        exitStartText.fontSize = 28;
        GetOrAdd<QuitButtonUI>(exitStart.gameObject);
        panel.gameObject.SetActive(false);
        return ui;
    }

    // Fila de etiqueta + deslizador + valor (la de cada volumen y la de la sensibilidad)
    private static Transform SliderRow(Transform card, string name, string labelText, float y, out Slider slider, out TMP_Text value)
    {
        Transform row = card.Find(name) ?? NewRect(card, name).transform;
        Place((RectTransform)row, new Vector2(0f, y), new Vector2(900f, 80f));

        TMP_Text label = Child<TMP_Text>(row, "Label") ?? NewText(row, "Label", "", rye, 34, Cream);
        label.text = labelText;
        label.alignment = TextAlignmentOptions.Left;
        // SENSIBILIDAD es más larga que los nombres de los volúmenes: se encoge si no cabe
        label.enableAutoSizing = true;
        label.fontSizeMin = 24; label.fontSizeMax = 34;
        Place(label.rectTransform, new Vector2(-330f, 0f), new Vector2(240f, 70f));

        slider = BuildSlider(row);
        Place((RectTransform)slider.transform, new Vector2(40f, 0f), new Vector2(460f, 36f));

        value = Child<TMP_Text>(row, "Value") ?? NewText(row, "Value", "100 %", sans, 32, Cream);
        value.alignment = TextAlignmentOptions.Right;
        Place(value.rectTransform, new Vector2(370f, 0f), new Vector2(140f, 70f));
        return row;
    }

    // Estructura estándar de un Slider de UGUI: fondo, zona de relleno con el relleno, y zona del tirador
    private static Slider BuildSlider(Transform row)
    {
        Transform existing = row.Find("Slider");
        if (existing != null) return existing.GetComponent<Slider>();

        GameObject go = NewRect(row, "Slider");
        Image background = NewImage(go.transform, "Background", barFill);
        background.type = Image.Type.Sliced;
        background.color = new Color(0f, 0f, 0f, 0.55f);
        Stretch(background.rectTransform);

        RectTransform fillArea = NewRect(go.transform, "Fill Area").GetComponent<RectTransform>();
        Stretch(fillArea);
        fillArea.offsetMin = new Vector2(4f, 4f); fillArea.offsetMax = new Vector2(-4f, -4f);
        Image fill = NewImage(fillArea, "Fill", barFill);
        fill.type = Image.Type.Sliced;
        fill.color = Gold;
        fill.rectTransform.anchorMin = Vector2.zero; fill.rectTransform.anchorMax = new Vector2(1f, 1f);
        fill.rectTransform.offsetMin = fill.rectTransform.offsetMax = Vector2.zero;

        RectTransform handleArea = NewRect(go.transform, "Handle Slide Area").GetComponent<RectTransform>();
        Stretch(handleArea);
        handleArea.offsetMin = new Vector2(18f, 0f); handleArea.offsetMax = new Vector2(-18f, 0f);
        Image handle = NewImage(handleArea, "Handle", round);
        handle.color = Cream;
        handle.raycastTarget = true;
        handle.rectTransform.sizeDelta = new Vector2(46f, 0f);
        handle.rectTransform.offsetMin = new Vector2(handle.rectTransform.offsetMin.x, -8f);
        handle.rectTransform.offsetMax = new Vector2(handle.rectTransform.offsetMax.x, 8f);
        Outline outline = handle.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0.08f, 0.05f, 0.05f);
        outline.effectDistance = new Vector2(3f, -3f);

        background.raycastTarget = true;   // clic en cualquier punto de la barra mueve el tirador
        Slider slider = go.AddComponent<Slider>();
        slider.fillRect = fill.rectTransform;
        slider.handleRect = handle.rectTransform;
        slider.targetGraphic = handle;
        slider.direction = Slider.Direction.LeftToRight;
        slider.minValue = 0f; slider.maxValue = 1f; slider.value = 1f;
        Navigation nav = slider.navigation; nav.mode = Navigation.Mode.None; slider.navigation = nav;
        return slider;
    }

    // ── Pausa ───────────────────────────────────────────────────────────────────

    private static void BuildPause(Transform canvas, Button template, OptionsPanelUI options)
    {
        Transform rootT = canvas.Find("PauseMenu") ?? NewRect(canvas, "PauseMenu").transform;
        Stretch((RectTransform)rootT);
        rootT.SetAsLastSibling();

        Image panel = Child<Image>(rootT, "Panel") ?? NewImage(rootT, "Panel", null);
        panel.color = Dim;
        panel.raycastTarget = true;
        Stretch(panel.rectTransform);

        // 25/11: 840 de alto para el cuarto botón, SALIR DEL JUEGO
        Image card = Card(panel.transform, new Vector2(760f, 840f));
        Title(card.transform, "PAUSA", 330f);

        Button resume = ChildButton(card.transform, "ContinueButton", template);
        Place((RectTransform)resume.transform, new Vector2(0f, 180f), new Vector2(460f, 110f));
        resume.GetComponentInChildren<TMP_Text>().text = "CONTINUAR";

        Button opts = ChildButton(card.transform, "OptionsButton", template);
        Place((RectTransform)opts.transform, new Vector2(0f, 55f), new Vector2(460f, 110f));
        opts.GetComponentInChildren<TMP_Text>().text = "OPCIONES";

        Button quit = ChildButton(card.transform, "QuitButton", template);
        Place((RectTransform)quit.transform, new Vector2(0f, -70f), new Vector2(460f, 110f));
        TMP_Text quitText = quit.GetComponentInChildren<TMP_Text>();
        quitText.text = "TERMINAR PARTIDA";

        Button exit = ChildButton(card.transform, "ExitButton", template);
        Place((RectTransform)exit.transform, new Vector2(0f, -195f), new Vector2(460f, 110f));
        TMP_Text exitText = exit.GetComponentInChildren<TMP_Text>();
        exitText.text = "SALIR DEL JUEGO";

        // Textos largos (y más aún «¿SEGURO? PULSA OTRA VEZ»): se encogen para caber entre las muescas del tique
        foreach (TMP_Text t in new[] { quitText, exitText })
        {
            t.enableAutoSizing = true;
            t.fontSizeMin = 16; t.fontSizeMax = 34;
            t.margin = new Vector4(48f, 0f, 48f, 0f);
        }

        TMP_Text hint = Child<TMP_Text>(card.transform, "Hint") ?? NewText(card.transform, "Hint", "", sans, 26, Cream);
        hint.text = "Esc: continuar  ·  Terminar y Salir cobran los créditos de lo conseguido";
        Place(hint.rectTransform, new Vector2(0f, -310f), new Vector2(680f, 60f));
        hint.textWrappingMode = TextWrappingModes.Normal;

        PauseMenuUI ui = GetOrAdd<PauseMenuUI>(rootT.gameObject);
        SetRefs(ui, "panel", panel.gameObject, "card", card.gameObject, "continueButton", resume, "optionsButton", opts,
            "quitButton", quit, "quitText", quitText, "exitButton", exit, "exitText", exitText, "options", options);
        panel.gameObject.SetActive(false);
    }

    // ── Piezas comunes ──────────────────────────────────────────────────────────

    private static Image Card(Transform parent, Vector2 size)
    {
        Image card = Child<Image>(parent, "Card") ?? NewImage(parent, "Card", board);
        card.sprite = board;
        card.type = Image.Type.Sliced;
        card.raycastTarget = true;
        Place(card.rectTransform, Vector2.zero, size);
        return card;
    }

    private static void Title(Transform card, string text, float y)
    {
        TMP_Text title = Child<TMP_Text>(card, "Title") ?? NewText(card, "Title", "", rye, 84, Gold, ryeCartel);
        title.text = text;
        Place(title.rectTransform, new Vector2(0f, y), new Vector2(900f, 110f));
    }
}
