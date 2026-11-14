using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;

// Monta el menú de pausa, el panel de OPCIONES (volumen) y el botón OPCIONES del inicio en la escena abierta
// (Fase 8). Menú: Tools > Fairground > Build Pause And Options.
//
// Se puede repetir: busca cada pieza por nombre, crea solo lo que falta y vuelve a enlazar las referencias.
// Toca la escena: en Main, avisar antes al compañero (§7). Mismo estilo que BossUIBuilder (tablero, Rye,
// botones copiados del de Récords); los ayudantes están duplicados a propósito para no acoplar las dos
// herramientas.
public static class PauseOptionsBuilder
{
    private const string UIArt = "Assets/UI/";

    private static readonly Color Gold = new Color(0.93f, 0.74f, 0.31f);
    private static readonly Color Cream = new Color(0.96f, 0.91f, 0.79f);
    private static readonly Color Dim = new Color(0.08f, 0.02f, 0.02f, 0.78f);

    private static TMP_FontAsset rye, sans;
    private static Material ryeCartel;
    private static Sprite board, barFill, round;

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
        LoadStyle();
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

    private static void LoadStyle()
    {
        rye = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/Rye SDF.asset");
        ryeCartel = AssetDatabase.LoadAssetAtPath<Material>("Assets/Fonts/Rye SDF - Cartel.mat");
        sans = AssetDatabase.FindAssets("LiberationSans SDF t:TMP_FontAsset").Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<TMP_FontAsset>).FirstOrDefault(f => f != null && f.name == "LiberationSans SDF");
        if (sans == null) sans = rye;
        board = AssetDatabase.LoadAssetAtPath<Sprite>(UIArt + "Menu/Board.png");
        barFill = AssetDatabase.LoadAssetAtPath<Sprite>(UIArt + "Boss/BarFill.png");
        round = AssetDatabase.LoadAssetAtPath<Sprite>(UIArt + "Menu/RoundButton.png");
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

        Image card = Card(panel.transform, new Vector2(1100f, 760f));
        Title(card.transform, "OPCIONES", 300f);
        TMP_Text sub = Child<TMP_Text>(card.transform, "Subtitle") ?? NewText(card.transform, "Subtitle", "", rye, 36, Gold);
        sub.text = "VOLUMEN";
        Place(sub.rectTransform, new Vector2(0f, 210f), new Vector2(800f, 50f));

        string[] names = { "GENERAL", "MÚSICA", "EFECTOS", "AMBIENTE" };   // orden de VolumeChannel
        var sliders = new Slider[names.Length];
        var values = new TMP_Text[names.Length];
        for (int i = 0; i < names.Length; i++)
        {
            float y = 120f - i * 95f;
            Transform row = card.transform.Find("Row" + i) ?? NewRect(card.transform, "Row" + i).transform;
            Place((RectTransform)row, new Vector2(0f, y), new Vector2(900f, 80f));

            TMP_Text label = Child<TMP_Text>(row, "Label") ?? NewText(row, "Label", "", rye, 34, Cream);
            label.text = names[i];
            label.alignment = TextAlignmentOptions.Left;
            Place(label.rectTransform, new Vector2(-330f, 0f), new Vector2(240f, 70f));

            sliders[i] = BuildSlider(row);
            Place((RectTransform)sliders[i].transform, new Vector2(40f, 0f), new Vector2(460f, 36f));

            values[i] = Child<TMP_Text>(row, "Value") ?? NewText(row, "Value", "100 %", sans, 32, Cream);
            values[i].alignment = TextAlignmentOptions.Right;
            Place(values[i].rectTransform, new Vector2(370f, 0f), new Vector2(140f, 70f));
        }

        Button close = ChildButton(card.transform, "CloseButton", template);
        Place((RectTransform)close.transform, new Vector2(0f, -290f), new Vector2(340f, 105f));
        close.GetComponentInChildren<TMP_Text>().text = "VOLVER";

        // OPCIONES en el inicio, debajo de CÓMO JUGAR (BossUIBuilder lo puso en (760, 220) con 300 × 100)
        Button open = ChildButton(startPanel, "OptionsButton", template);
        Place((RectTransform)open.transform, new Vector2(760f, 105f), new Vector2(300f, 100f));
        TMP_Text openText = open.GetComponentInChildren<TMP_Text>();
        openText.text = "OPCIONES";
        openText.fontSize = 28;

        OptionsPanelUI ui = GetOrAdd<OptionsPanelUI>(rootT.gameObject);
        SetRefs(ui, "panel", panel.gameObject, "sliders", sliders, "valueTexts", values, "closeButton", close, "openButton", open);
        panel.gameObject.SetActive(false);
        return ui;
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

        Image card = Card(panel.transform, new Vector2(760f, 720f));
        Title(card.transform, "PAUSA", 270f);

        Button resume = ChildButton(card.transform, "ContinueButton", template);
        Place((RectTransform)resume.transform, new Vector2(0f, 120f), new Vector2(460f, 110f));
        resume.GetComponentInChildren<TMP_Text>().text = "CONTINUAR";

        Button opts = ChildButton(card.transform, "OptionsButton", template);
        Place((RectTransform)opts.transform, new Vector2(0f, -10f), new Vector2(460f, 110f));
        opts.GetComponentInChildren<TMP_Text>().text = "OPCIONES";

        Button quit = ChildButton(card.transform, "QuitButton", template);
        Place((RectTransform)quit.transform, new Vector2(0f, -140f), new Vector2(460f, 110f));
        TMP_Text quitText = quit.GetComponentInChildren<TMP_Text>();
        quitText.text = "TERMINAR PARTIDA";
        // Texto largo (y más aún «¿SEGURO? PULSA OTRA VEZ»): se encoge para caber entre las muescas del tique
        quitText.enableAutoSizing = true;
        quitText.fontSizeMin = 16; quitText.fontSizeMax = 34;
        quitText.margin = new Vector4(48f, 0f, 48f, 0f);

        TMP_Text hint = Child<TMP_Text>(card.transform, "Hint") ?? NewText(card.transform, "Hint", "", sans, 26, Cream);
        hint.text = "Esc: continuar  ·  Terminar cobra los créditos de lo conseguido";
        Place(hint.rectTransform, new Vector2(0f, -265f), new Vector2(680f, 60f));
        hint.textWrappingMode = TextWrappingModes.Normal;

        PauseMenuUI ui = GetOrAdd<PauseMenuUI>(rootT.gameObject);
        SetRefs(ui, "panel", panel.gameObject, "card", card.gameObject, "continueButton", resume, "optionsButton", opts,
            "quitButton", quit, "quitText", quitText, "options", options);
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

    private static Button ChildButton(Transform parent, string name, Button template)
    {
        Transform existing = parent.Find(name);
        if (existing != null) return existing.GetComponent<Button>();
        GameObject copy = Object.Instantiate(template.gameObject, parent);
        copy.name = name;
        Button button = copy.GetComponent<Button>();
        button.onClick = new Button.ButtonClickedEvent();
        foreach (var extra in copy.GetComponents<MonoBehaviour>())
            if (!(extra is Button) && !(extra is Image)) Object.DestroyImmediate(extra);
        return button;
    }

    private static T GetOrAdd<T>(GameObject go) where T : Component => go.TryGetComponent(out T c) ? c : go.AddComponent<T>();

    private static T Child<T>(Transform parent, string name) where T : Component
    {
        Transform t = parent.Find(name);
        return t != null && t.TryGetComponent(out T c) ? c : null;
    }

    private static GameObject NewRect(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = parent.gameObject.layer;
        go.transform.SetParent(parent, false);
        return go;
    }

    private static Image NewImage(Transform parent, string name, Sprite sprite)
    {
        Image img = NewRect(parent, name).AddComponent<Image>();
        img.sprite = sprite;
        img.raycastTarget = false;
        return img;
    }

    private static TMP_Text NewText(Transform parent, string name, string text, TMP_FontAsset font, float size, Color color, Material material = null)
    {
        var t = NewRect(parent, name).AddComponent<TextMeshProUGUI>();
        t.font = font;
        if (material != null) t.fontSharedMaterial = material;
        t.fontSize = size;
        t.color = color;
        t.text = text;
        t.alignment = TextAlignmentOptions.Center;
        t.raycastTarget = false;
        return t;
    }

    private static void Place(RectTransform rt, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    private static void SetRefs(Object target, params object[] pairs)
    {
        var so = new SerializedObject(target);
        for (int i = 0; i < pairs.Length; i += 2)
        {
            SerializedProperty p = so.FindProperty((string)pairs[i]);
            if (p == null) { Debug.LogWarning($"PauseOptionsBuilder: {target.GetType().Name} no tiene el campo {pairs[i]}."); continue; }
            switch (pairs[i + 1])
            {
                case Object o: p.objectReferenceValue = o; break;
                case Object[] arr:
                    p.arraySize = arr.Length;
                    for (int k = 0; k < arr.Length; k++) p.GetArrayElementAtIndex(k).objectReferenceValue = arr[k];
                    break;
                case null: p.objectReferenceValue = null; break;
            }
        }
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}
