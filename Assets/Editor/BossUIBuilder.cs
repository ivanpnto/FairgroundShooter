using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;

// Monta el bloque 2 (HUD del boss, efectos de peligro, iconos, récord, «Cómo jugar» y proyectiles) en la escena
// abierta. Menú: Tools > Fairground > Build Boss UI.
//
// Se puede repetir: busca cada pieza por nombre y la reutiliza; solo crea lo que falta y vuelve a enlazar las
// referencias. Toca la escena (avisar en Main antes, §7) y tres prefabs de interfaz (ShopPanel, RecordsPanel)
// y el del proyectil. No sustituye a ProgressionUIBuilder: ese rehace los paneles de la Fase 5 desde cero (y hoy
// ya no se puede ejecutar: busca un RankingBoard que ya no existe en GameOverPanel).
public static class BossUIBuilder
{
    private const string UIArt = "Assets/UI/";
    private const string AudioFolder = "Assets/Audio/Boss/";

    private static readonly Color Gold = new Color(0.93f, 0.74f, 0.31f);
    private static readonly Color Cream = new Color(0.96f, 0.91f, 0.79f);
    private static readonly Color DarkBar = new Color(0.12f, 0.05f, 0.04f, 0.9f);

    private static TMP_FontAsset rye, sans;
    private static Material ryeCartel;
    private static Sprite board, ticket, barFill, heartFull, heartEmpty, vignetteSprite, splatSprite;
    private static AudioMixerGroup sfx;

    [MenuItem("Tools/Fairground/Build Boss UI")]
    public static void Build()
    {
        if (!EditorUtility.DisplayDialog("Build Boss UI",
                "Monta el HUD del boss, los efectos, los iconos, el récord y «Cómo jugar» en la escena abierta, y " +
                "modifica los prefabs ShopPanel, RecordsPanel y el del proyectil.\n\nEn Main, avisa antes a tu compañero. ¿Continuar?",
                "Montar", "Cancelar"))
            return;
        Run();
    }

    // Sin el diálogo: para lanzarlo desde código (scripts de prueba, otra herramienta)
    public static void Run()
    {
        ImportAssets();
        LoadStyle();
        AssignIcons();
        GameObject projectile = BuildProjectilePrefab();
        UpdateShopPrefab();
        UpdateRecordsPrefab();

        GameObject canvasGo = GameObject.Find("UI");
        Transform canvas = canvasGo != null ? canvasGo.transform : null;
        if (canvas == null) { Debug.LogError("BossUIBuilder: no hay un Canvas llamado UI en la escena."); return; }
        Transform hud = canvas.Find("HUD");

        BuildBossBar(hud);
        BuildHearts(hud);
        BuildFeedback(canvas, hud);
        BuildResultBanner(canvas);
        BuildHowToPlay(canvas);
        RemoveDebugHud();

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
        Debug.Log($"BossUIBuilder: bloque 2 montado (proyectil: {AssetDatabase.GetAssetPath(projectile)}).");
    }

    // ── Importación ─────────────────────────────────────────────────────────────

    private static void ImportAssets()
    {
        foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { UIArt + "Boss", UIArt + "Icons" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            bool icon = path.Contains("/Icons/");
            imp.textureType = TextureImporterType.Sprite;
            imp.spriteImportMode = SpriteImportMode.Single;
            imp.alphaIsTransparency = true;
            imp.mipmapEnabled = false;
            imp.maxTextureSize = icon ? 256 : 512;
            imp.textureCompression = TextureImporterCompression.CompressedHQ;
            if (path.EndsWith("BarFill.png")) imp.spriteBorder = new Vector4(12, 12, 12, 12);
            imp.SaveAndReimport();
        }

        foreach (string guid in AssetDatabase.FindAssets("t:AudioClip", new[] { AudioFolder.TrimEnd('/') }))
        {
            var imp = (AudioImporter)AssetImporter.GetAtPath(AssetDatabase.GUIDToAssetPath(guid));
            AudioImporterSampleSettings s = imp.defaultSampleSettings;
            s.loadType = AudioClipLoadType.DecompressOnLoad;   // efectos cortos: sin latencia al sonar
            s.compressionFormat = AudioCompressionFormat.Vorbis;
            s.quality = 0.7f;
            imp.defaultSampleSettings = s;
            imp.forceToMono = true;
            imp.SaveAndReimport();
        }
    }

    private static void LoadStyle()
    {
        rye = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/Rye SDF.asset");
        ryeCartel = AssetDatabase.LoadAssetAtPath<Material>("Assets/Fonts/Rye SDF - Cartel.mat");
        sans = AssetDatabase.FindAssets("LiberationSans SDF t:TMP_FontAsset").Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<TMP_FontAsset>).FirstOrDefault(f => f != null && f.name == "LiberationSans SDF");
        if (sans == null) sans = rye;
        board = LoadSprite(UIArt + "Menu/Board.png");
        ticket = LoadSprite(UIArt + "Menu/Ticket.png");
        barFill = LoadSprite(UIArt + "Boss/BarFill.png");
        heartFull = LoadSprite(UIArt + "Boss/Heart_Full.png");
        heartEmpty = LoadSprite(UIArt + "Boss/Heart_Empty.png");
        vignetteSprite = LoadSprite(UIArt + "Boss/Vignette.png");
        splatSprite = LoadSprite(UIArt + "Boss/PieSplat.png");
        sfx = AssetDatabase.LoadAllAssetsAtPath("Assets/Audio/MainMixer.mixer").OfType<AudioMixerGroup>().FirstOrDefault(g => g.name == "SFX");
    }

    private static Sprite LoadSprite(string path) => AssetDatabase.LoadAssetAtPath<Sprite>(path);
    private static AudioClip Clip(string name) => AssetDatabase.LoadAssetAtPath<AudioClip>(AudioFolder + name + ".wav");

    // ── Iconos de la tienda ─────────────────────────────────────────────────────

    private static void AssignIcons()
    {
        (string asset, string icon)[] map =
        {
            ("Ammo", "Icon_Ammo"), ("FireRate", "Icon_FireRate"), ("ScoreMultiplier", "Icon_ScoreMultiplier"),
            ("Damage", "Icon_Damage"), ("Penetration", "Icon_Penetration"), ("DoubleShot", "Icon_DoubleShot"),
            ("Explosive", "Icon_Explosive"), ("BossLevel", "Icon_BossLevel")
        };
        foreach (var (asset, icon) in map)
        {
            var data = AssetDatabase.LoadAssetAtPath<UpgradeData>($"Assets/Data/Upgrades/{asset}.asset");
            Sprite sprite = LoadSprite($"{UIArt}Icons/{icon}.png");
            if (data == null || sprite == null) { Debug.LogWarning($"BossUIBuilder: falta {asset} o {icon}."); continue; }
            data.icon = sprite;
            EditorUtility.SetDirty(data);
        }
    }

    // ── Proyectil con tres modelos ──────────────────────────────────────────────

    private static GameObject BuildProjectilePrefab()
    {
        const string path = "Assets/Prefabs/Boss/BossProjectile.prefab";
        GameObject root;
        bool exists = AssetDatabase.LoadAssetAtPath<GameObject>(path) != null;
        if (exists) root = PrefabUtility.LoadPrefabContents(path);
        else
        {
            // Se parte del de prueba (Rigidbody, Target, BossProjectile, capa BossHazard ya configurados)
            root = PrefabUtility.LoadPrefabContents("Assets/Prefabs/Boss/BossProjectile_Test.prefab");
            Object.DestroyImmediate(root.GetComponent<MeshRenderer>());
            Object.DestroyImmediate(root.GetComponent<MeshFilter>());
            root.name = "BossProjectile";
        }
        root.transform.localScale = Vector3.one;
        var sphere = root.GetComponent<SphereCollider>();
        sphere.radius = 0.22f;   // una esfera que vale para los tres (antes 0,2 m con la esfera de prueba)

        string[] models = { "Projectile_Ball", "Projectile_Club", "Projectile_Pie" };   // orden de ProjectileKind
        var children = new GameObject[models.Length];
        for (int i = 0; i < models.Length; i++)
        {
            Transform existing = root.transform.Find(models[i]);
            GameObject child = existing != null ? existing.gameObject
                : (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Art/Models/Projectiles/{models[i]}.fbx"), root.transform);
            child.name = models[i];
            child.transform.localPosition = Vector3.zero;
            child.transform.localRotation = Quaternion.identity;   // regla del FBX (§6): rotación local a 0
            child.layer = root.layer;
            foreach (Renderer r in child.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            children[i] = child;
        }
        SetRefs(root.GetComponent<BossProjectile>(), "models", children);

        GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, path);
        PrefabUtility.UnloadPrefabContents(root);

        var config = AssetDatabase.LoadAssetAtPath<BossConfig>("Assets/Data/Boss/BossConfig.asset");
        if (config != null) { config.projectilePrefab = saved; EditorUtility.SetDirty(config); }
        return saved;
    }

    // ── Tienda: icono en la fila ────────────────────────────────────────────────

    private static void UpdateShopPrefab()
    {
        const string path = "Assets/Prefabs/UI/ShopPanel.prefab";
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        ShopRowUI row = root.GetComponentInChildren<ShopRowUI>(true);
        Transform iconT = row.transform.Find("Icon");
        Image icon = iconT != null ? iconT.GetComponent<Image>() : NewImage(row.transform, "Icon", null);
        RectTransform rt = icon.rectTransform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 0.5f);
        rt.anchoredPosition = new Vector2(12f, 0f);
        rt.sizeDelta = new Vector2(68f, 68f);
        icon.preserveAspect = true;
        icon.raycastTarget = false;
        SetRefs(row, "iconImage", icon, "iconSpace", 84f);
        MakeScrollable(root.transform, "UpgradesList", "UpgradesScroll");
        PrefabUtility.SaveAsPrefabAsset(root, path);
        PrefabUtility.UnloadPrefabContents(root);
    }

    // Con 8 mejoras (84 px cada fila) la lista no cabe en el tablero y las filas se solapaban. La lista pasa a ser
    // el CONTENIDO de un ScrollRect: el ScrollRect ocupa el sitio que tenía la lista y hace de ventana (RectMask2D
    // recorta lo que sobresale), y la lista crece hacia abajo lo que necesiten sus filas (ContentSizeFitter).
    // ShopPanelUI sigue añadiendo las filas a la misma lista: no cambia nada en su código
    private static void MakeScrollable(Transform panel, string listName, string scrollName)
    {
        Transform scrollT = panel.Find(scrollName);
        RectTransform list;
        if (scrollT == null)
        {
            list = (RectTransform)panel.Find(listName);
            scrollT = NewRect(panel, scrollName).transform;
            scrollT.SetSiblingIndex(list.GetSiblingIndex());
            var view = (RectTransform)scrollT;
            view.anchorMin = list.anchorMin; view.anchorMax = list.anchorMax; view.pivot = list.pivot;
            view.anchoredPosition = list.anchoredPosition; view.sizeDelta = list.sizeDelta;
            list.SetParent(scrollT, false);
        }
        else list = (RectTransform)scrollT.Find(listName);

        // La lista, pegada arriba y a lo ancho de la ventana (menos la barra)
        list.anchorMin = new Vector2(0f, 1f); list.anchorMax = new Vector2(1f, 1f); list.pivot = new Vector2(0.5f, 1f);
        list.offsetMin = new Vector2(0f, list.offsetMin.y); list.offsetMax = new Vector2(-18f, 0f);   // 18 px para la barra
        ContentSizeFitter fitter = GetOrAdd<ContentSizeFitter>(list.gameObject);
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

        GetOrAdd<RectMask2D>(scrollT.gameObject);
        Image hit = GetOrAdd<Image>(scrollT.gameObject);   // transparente: recibe la rueda del ratón en toda la zona
        hit.color = new Color(0f, 0f, 0f, 0f);
        ScrollRect scroll = GetOrAdd<ScrollRect>(scrollT.gameObject);
        scroll.horizontal = false; scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 35f;
        scroll.viewport = (RectTransform)scrollT;
        scroll.content = list;

        // Barra fina a la derecha (se oculta sola si todo cabe)
        Transform barT = scrollT.Find("Scrollbar");
        Scrollbar bar;
        if (barT == null)
        {
            Image track = NewImage(scrollT, "Scrollbar", barFill);
            track.type = Image.Type.Sliced; track.color = new Color(0f, 0f, 0f, 0.35f); track.raycastTarget = true;
            RectTransform tr = track.rectTransform;
            tr.anchorMin = new Vector2(1f, 0f); tr.anchorMax = Vector2.one; tr.pivot = new Vector2(1f, 0.5f);
            tr.offsetMin = new Vector2(-10f, 0f); tr.offsetMax = Vector2.zero;
            Image handle = NewImage(track.transform, "Handle", barFill);
            handle.type = Image.Type.Sliced; handle.color = Gold; handle.raycastTarget = true;
            Stretch(handle.rectTransform, 0f, 0f);
            bar = track.gameObject.AddComponent<Scrollbar>();
            bar.direction = Scrollbar.Direction.BottomToTop;
            bar.handleRect = handle.rectTransform;
            bar.targetGraphic = handle;
        }
        else bar = barT.GetComponent<Scrollbar>();
        scroll.verticalScrollbar = bar;
        scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
    }

    // ── Récords: columna «El Gran Feriante» ─────────────────────────────────────

    private static void UpdateRecordsPrefab()
    {
        const string path = "Assets/Prefabs/UI/RecordsPanel.prefab";
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        Transform t = root.transform;

        Size((RectTransform)t.Find("RecordsBoard"), new Vector2(0f, -110f), new Vector2(1300f, 600f));
        t.Find("RecordsLabel").GetComponent<TMP_Text>().text = "RÉCORDS DE CADA TIRADOR";

        var columns = (RectTransform)t.Find("RecordsColumns");
        Size(columns, new Vector2(0f, -150f), new Vector2(1120f, 420f));
        var names = (RectTransform)columns.Find("RankingNamesText");
        var scores = (RectTransform)columns.Find("RankingScoresText");
        Anchor(names, 0f, 0.46f);
        Anchor(scores, 0.46f, 0.63f);

        Transform bossT = columns.Find("RankingBossText");
        TMP_Text boss = bossT != null ? bossT.GetComponent<TMP_Text>()
            : Object.Instantiate(scores.gameObject, columns).GetComponent<TMP_Text>();
        boss.name = "RankingBossText";
        Anchor(boss.rectTransform, 0.68f, 1f);
        boss.alignment = TextAlignmentOptions.Top;
        boss.text = "";

        // Cabecera de las tres columnas, con la fuente del cartel
        Transform header = t.Find("RecordsHeader");
        if (header == null)
        {
            header = new GameObject("RecordsHeader", typeof(RectTransform)).transform;
            header.SetParent(t, false);
            header.SetSiblingIndex(columns.GetSiblingIndex());
        }
        Size((RectTransform)header, new Vector2(0f, 82f), new Vector2(1120f, 40f));
        HeaderLabel(header, "Tirador", "TIRADOR", 0f, 0.46f, TextAlignmentOptions.Left);
        HeaderLabel(header, "Puntos", "PUNTOS", 0.46f, 0.63f, TextAlignmentOptions.Right);
        HeaderLabel(header, "Boss", "EL GRAN FERIANTE", 0.68f, 1f, TextAlignmentOptions.Center);

        SetRefs(root.GetComponent<RecordsUI>(), "bossText", boss);
        PrefabUtility.SaveAsPrefabAsset(root, path);
        PrefabUtility.UnloadPrefabContents(root);
    }

    private static void HeaderLabel(Transform parent, string name, string text, float x0, float x1, TextAlignmentOptions align)
    {
        Transform existing = parent.Find(name);
        TMP_Text label = existing != null ? existing.GetComponent<TMP_Text>() : NewText(parent, name, text, rye, 26, Gold);
        label.text = text;
        label.alignment = align;
        Anchor(label.rectTransform, x0, x1);
    }

    // ── HUD: barra del boss ─────────────────────────────────────────────────────

    private static void BuildBossBar(Transform hud)
    {
        Transform rootT = hud.Find("BossHealthBar") ?? NewRect(hud, "BossHealthBar").transform;
        var root = (RectTransform)rootT;
        root.anchorMin = root.anchorMax = new Vector2(0.5f, 0.5f);
        root.pivot = new Vector2(0.5f, 1f);
        root.anchoredPosition = new Vector2(0f, 525f);
        root.sizeDelta = new Vector2(960f, 150f);
        CanvasGroup group = GetOrAdd<CanvasGroup>(root.gameObject);
        group.blocksRaycasts = false; group.interactable = false;

        TMP_Text title = Child<TMP_Text>(root, "Name") ?? NewText(root, "Name", "EL GRAN FERIANTE", rye, 44, Gold, ryeCartel);
        Place(title.rectTransform, new Vector2(0f, -28f), new Vector2(900f, 56f));

        // Marco de chapa con la barra dentro
        Image frame = Child<Image>(root, "Frame") ?? NewImage(root, "Frame", board);
        frame.sprite = board;
        frame.type = Image.Type.Sliced; frame.pixelsPerUnitMultiplier = 3f;
        Place(frame.rectTransform, new Vector2(0f, -86f), new Vector2(940f, 62f));

        Image track = Child<Image>(frame.transform, "Track") ?? NewImage(frame.transform, "Track", barFill);
        track.type = Image.Type.Sliced; track.color = DarkBar;
        Stretch(track.rectTransform, 16f, 14f);

        Image ghost = Child<Image>(track.transform, "Ghost") ?? NewImage(track.transform, "Ghost", barFill);
        ghost.type = Image.Type.Sliced; ghost.color = new Color(1f, 0.93f, 0.75f, 0.9f);
        Fill(ghost.rectTransform);

        Image fill = Child<Image>(track.transform, "Fill") ?? NewImage(track.transform, "Fill", barFill);
        fill.type = Image.Type.Sliced; fill.color = new Color(0.86f, 0.16f, 0.16f);
        Fill(fill.rectTransform);

        Image mark = Child<Image>(track.transform, "PhaseMark") ?? NewImage(track.transform, "PhaseMark", null);
        mark.color = new Color(0.05f, 0.02f, 0.02f, 0.95f);
        mark.rectTransform.sizeDelta = new Vector2(6f, 40f);
        mark.transform.SetAsLastSibling();

        TMP_Text phase = Child<TMP_Text>(root, "Phase") ?? NewText(root, "Phase", "FASE 1/3", rye, 24, Cream);
        phase.alignment = TextAlignmentOptions.Right;
        Place(phase.rectTransform, new Vector2(380f, -28f), new Vector2(180f, 40f));

        // Aviso del bocinazo, debajo de la barra
        Transform honkT = root.Find("HonkPrompt") ?? NewRect(root, "HonkPrompt").transform;
        Place((RectTransform)honkT, new Vector2(0f, -142f), new Vector2(420f, 44f));
        CanvasGroup honkGroup = GetOrAdd<CanvasGroup>(honkT.gameObject);
        honkGroup.blocksRaycasts = false;
        TMP_Text honkText = Child<TMP_Text>(honkT, "Text") ?? NewText(honkT, "Text", "¡A LA BOCA!", rye, 30, new Color(1f, 0.35f, 0.3f), ryeCartel);
        Place(honkText.rectTransform, new Vector2(0f, 6f), new Vector2(420f, 40f));
        Image honkTrack = Child<Image>(honkT, "Track") ?? NewImage(honkT, "Track", barFill);
        honkTrack.type = Image.Type.Sliced; honkTrack.color = DarkBar;
        Place(honkTrack.rectTransform, new Vector2(0f, -22f), new Vector2(260f, 14f));
        Image honkFill = Child<Image>(honkTrack.transform, "Fill") ?? NewImage(honkTrack.transform, "Fill", barFill);
        honkFill.type = Image.Type.Sliced; honkFill.color = new Color(0.30f, 0.85f, 0.95f);
        Fill(honkFill.rectTransform);
        honkFill.rectTransform.anchorMax = new Vector2(0f, 1f);

        BossHealthBarUI ui = GetOrAdd<BossHealthBarUI>(root.gameObject);
        SetRefs(ui, "group", group, "fill", fill.rectTransform, "ghost", ghost.rectTransform, "fillImage", fill,
            "phaseMarkTemplate", mark.rectTransform, "nameText", title, "phaseText", phase,
            "honkGroup", honkGroup, "honkFill", honkFill.rectTransform);
    }

    // ── HUD: corazones ──────────────────────────────────────────────────────────

    private static void BuildHearts(Transform hud)
    {
        Transform rootT = hud.Find("Hearts") ?? NewRect(hud, "Hearts").transform;
        var root = (RectTransform)rootT;
        root.anchorMin = root.anchorMax = new Vector2(0.5f, 0.5f);
        root.pivot = new Vector2(0f, 0f);
        root.anchoredPosition = new Vector2(-920f, -455f);   // encima del texto de munición
        root.sizeDelta = new Vector2(300f, 80f);
        CanvasGroup group = GetOrAdd<CanvasGroup>(root.gameObject);
        group.blocksRaycasts = false;
        HorizontalLayoutGroup layout = GetOrAdd<HorizontalLayoutGroup>(root.gameObject);
        layout.spacing = 8f; layout.childAlignment = TextAnchor.LowerLeft;
        layout.childControlWidth = layout.childControlHeight = false;
        layout.childForceExpandWidth = layout.childForceExpandHeight = false;

        Image template = Child<Image>(root, "HeartTemplate") ?? NewImage(root, "HeartTemplate", heartFull);
        template.rectTransform.sizeDelta = new Vector2(72f, 72f);
        template.preserveAspect = true;

        HeartsUI ui = GetOrAdd<HeartsUI>(root.gameObject);
        SetRefs(ui, "group", group, "heartTemplate", template, "fullSprite", heartFull, "emptySprite", heartEmpty);
    }

    // ── Viñeta, tartazo, sacudida y sonidos ─────────────────────────────────────

    private static void BuildFeedback(Transform canvas, Transform hud)
    {
        // Fuera del HUD y DETRÁS de él (se dibuja antes): la viñeta tiñe la escena, no los textos
        Transform rootT = canvas.Find("BossFeedbackOverlay") ?? NewRect(canvas, "BossFeedbackOverlay").transform;
        Stretch((RectTransform)rootT, 0f, 0f);
        rootT.SetSiblingIndex(hud.GetSiblingIndex());

        Image vignette = Child<Image>(rootT, "Vignette") ?? NewImage(rootT, "Vignette", vignetteSprite);
        Stretch(vignette.rectTransform, 0f, 0f);
        Image splat = Child<Image>(rootT, "PieSplat") ?? NewImage(rootT, "PieSplat", splatSprite);
        Place(splat.rectTransform, Vector2.zero, new Vector2(560f, 560f));
        splat.preserveAspect = true;

        AudioSource source = GetOrAdd<AudioSource>(rootT.gameObject);
        ConfigureSource(source);

        Camera cam = Camera.main;
        CameraShake shake = cam != null ? GetOrAdd<CameraShake>(cam.gameObject) : null;

        BossFeedback fb = GetOrAdd<BossFeedback>(rootT.gameObject);
        SetRefs(fb, "cameraShake", shake, "vignette", vignette, "pieSplat", splat, "audioSource", source,
            "inhale", Clip("Boss_Inhale"), "honk", Clip("Boss_Honk"), "stun", Clip("Boss_Stun"),
            "minionWarning", Clip("Minion_Warning"), "playerHit", Clip("Player_Hit"), "pieSplatSound", Clip("Pie_Splat"),
            "heartGain", Clip("Heart_Gain"), "phaseCrash", Clip("Boss_PhaseCrash"));
    }

    // ── Cartel de victoria / derrota ────────────────────────────────────────────

    private static void BuildResultBanner(Transform canvas)
    {
        // La raíz siempre activa (escucha GameEnded); el panel, que se enciende y apaga, es hijo
        Transform rootT = canvas.Find("ResultBanner") ?? NewRect(canvas, "ResultBanner").transform;
        Stretch((RectTransform)rootT, 0f, 0f);
        rootT.SetAsLastSibling();

        Image panel = Child<Image>(rootT, "Panel") ?? NewImage(rootT, "Panel", null);
        panel.color = new Color(0f, 0f, 0f, 0.35f);
        Stretch(panel.rectTransform, 0f, 0f);
        CanvasGroup group = GetOrAdd<CanvasGroup>(panel.gameObject);

        Image card = Child<Image>(panel.transform, "Card") ?? NewImage(panel.transform, "Card", board);
        card.sprite = board;
        card.type = Image.Type.Sliced;
        Place(card.rectTransform, new Vector2(0f, 40f), new Vector2(1200f, 420f));

        TMP_Text title = Child<TMP_Text>(card.transform, "Title") ?? NewText(card.transform, "Title", "¡VICTORIA!", rye, 150, Gold, ryeCartel);
        Place(title.rectTransform, new Vector2(0f, 60f), new Vector2(1100f, 190f));
        TMP_Text subtitle = Child<TMP_Text>(card.transform, "Subtitle") ?? NewText(card.transform, "Subtitle", "", rye, 46, Cream);
        subtitle.fontSize = 46;
        Place(subtitle.rectTransform, new Vector2(0f, -110f), new Vector2(1100f, 120f));

        AudioSource source = GetOrAdd<AudioSource>(rootT.gameObject);
        ConfigureSource(source);

        ResultBannerUI ui = GetOrAdd<ResultBannerUI>(rootT.gameObject);
        SetRefs(ui, "group", group, "card", card.rectTransform, "titleText", title, "subtitleText", subtitle,
            "audioSource", source, "victoryClip", Clip("Result_Victory"), "defeatClip", Clip("Result_Defeat"));
        panel.gameObject.SetActive(false);
    }

    // ── Cómo jugar ──────────────────────────────────────────────────────────────

    private static void BuildHowToPlay(Transform canvas)
    {
        Transform startPanel = canvas.Find("StartPanel");
        Button template = startPanel.Find("RecordsButton").GetComponent<Button>();

        Transform rootT = canvas.Find("HowToPlay") ?? NewRect(canvas, "HowToPlay").transform;
        Stretch((RectTransform)rootT, 0f, 0f);
        rootT.SetAsLastSibling();

        Image panel = Child<Image>(rootT, "Panel") ?? NewImage(rootT, "Panel", null);
        panel.color = new Color(0.08f, 0.02f, 0.02f, 0.82f);   // bloquea los clics al menú de detrás
        Stretch(panel.rectTransform, 0f, 0f);

        Image card = Child<Image>(panel.transform, "Card") ?? NewImage(panel.transform, "Card", board);
        card.sprite = board;
        card.type = Image.Type.Sliced;
        Place(card.rectTransform, new Vector2(0f, 10f), new Vector2(1500f, 860f));

        TMP_Text title = Child<TMP_Text>(card.transform, "Title") ?? NewText(card.transform, "Title", "CÓMO JUGAR", rye, 84, Gold, ryeCartel);
        Place(title.rectTransform, new Vector2(0f, 345f), new Vector2(1300f, 110f));

        Column(card.transform, "ControlsLabel", "CONTROLES", new Vector2(-370f, 255f));
        Column(card.transform, "TargetsLabel", "DIANAS", new Vector2(370f, 255f));

        string[] controls =
        {
            "<b>Ratón</b>: apuntar",
            "<b>Clic izquierdo</b>: disparar",
            "<b>Clic derecho</b>: mirar por la mira del arma",
            "<b>R</b>: recargar (también se recarga sola al vaciar el cargador)",
            "<b>Racha</b>: cada 5 aciertos seguidos el multiplicador sube (hasta x3). Fallar o dar a una diana prohibida la reinicia",
            "Al acabar la partida, gasta tus créditos en la <b>tienda</b>: mejoras y armas nuevas",
        };
        TMP_Text controlsText = Child<TMP_Text>(card.transform, "ControlsText") ?? NewText(card.transform, "ControlsText", "", sans, 28, Cream);
        controlsText.text = string.Join("\n\n", controls);
        controlsText.alignment = TextAlignmentOptions.TopLeft;
        controlsText.textWrappingMode = TextWrappingModes.Normal;
        Place(controlsText.rectTransform, new Vector2(-370f, -20f), new Vector2(640f, 480f));

        GameObject pinkRow = null;
        (string name, Color color, string text)[] rows =
        {
            ("GoldRow", new Color(1f, 0.80f, 0.20f), "<b>Contorno dorado</b>: +3 segundos"),
            ("RedRow", new Color(0.95f, 0.15f, 0.15f), "<b>Contorno rojo</b>: cargador lleno"),
            ("ForbiddenRow", new Color(0.85f, 0.10f, 0.10f), "<b>Señal de prohibido</b>: resta puntos y rompe la racha"),
            ("PinkRow", new Color(1f, 0.45f, 0.75f), "<b>Contorno rosa</b>: +1 corazón"),
        };
        for (int i = 0; i < rows.Length; i++)
        {
            GameObject row = LegendRow(card.transform, rows[i].name, rows[i].color, rows[i].text, rows[i].name == "ForbiddenRow",
                new Vector2(370f, 170f - i * 95f));
            if (rows[i].name == "PinkRow") pinkRow = row;
        }

        // Botón de cerrar (ENTENDIDO / ¡A JUGAR!): copia del botón de Récords, sin sus acciones
        Button close = ChildButton(card.transform, "CloseButton", template);
        Place((RectTransform)close.transform, new Vector2(0f, -360f), new Vector2(360f, 110f));
        TMP_Text closeText = close.GetComponentInChildren<TMP_Text>();
        closeText.text = "ENTENDIDO";

        // Botón CÓMO JUGAR en el inicio
        Button open = ChildButton(startPanel, "HowToPlayButton", template);
        Place((RectTransform)open.transform, new Vector2(760f, 220f), new Vector2(300f, 100f));
        TMP_Text openText = open.GetComponentInChildren<TMP_Text>();
        openText.text = "CÓMO JUGAR";
        openText.fontSize = 28;

        HowToPlayUI ui = GetOrAdd<HowToPlayUI>(rootT.gameObject);
        SetRefs(ui, "panel", panel.gameObject, "pinkRow", pinkRow, "closeButton", close, "closeButtonText", closeText,
            "openButton", open);
        panel.gameObject.SetActive(false);

        // EMPEZAR pasa por HowToPlayUI (primera partida del perfil). Fuera también la llamada a
        // PlayerAim.EnableAiming, un método que ya no existe (se borró en la Fase 2)
        Button start = startPanel.Find("StartButton").GetComponent<Button>();
        for (int i = start.onClick.GetPersistentEventCount() - 1; i >= 1; i--)
            UnityEventTools.RemovePersistentListener(start.onClick, i);
        if (start.onClick.GetPersistentEventCount() == 0)
            UnityEventTools.AddVoidPersistentListener(start.onClick, ui.RequestStart);
        else
            UnityEventTools.RegisterVoidPersistentListener(start.onClick, 0, ui.RequestStart);
        EditorUtility.SetDirty(start);
    }

    private static void Column(Transform parent, string name, string text, Vector2 pos)
    {
        TMP_Text label = Child<TMP_Text>(parent, name) ?? NewText(parent, name, text, rye, 38, Gold);
        Place(label.rectTransform, pos, new Vector2(640f, 50f));
    }

    private static GameObject LegendRow(Transform parent, string name, Color color, string text, bool forbidden, Vector2 pos)
    {
        Transform rowT = parent.Find(name) ?? NewRect(parent, name).transform;
        Place((RectTransform)rowT, pos, new Vector2(640f, 80f));

        // Muestra del color: un círculo con aro (o la señal de prohibido: círculo blanco con aro y barra rojos)
        Image chip = Child<Image>(rowT, "Chip") ?? NewImage(rowT, "Chip", null);
        chip.sprite = LoadSprite(UIArt + "Menu/RoundButton.png");
        chip.color = forbidden ? Color.white : color;
        chip.rectTransform.anchorMin = chip.rectTransform.anchorMax = chip.rectTransform.pivot = new Vector2(0f, 0.5f);
        chip.rectTransform.anchoredPosition = new Vector2(0f, 0f);
        chip.rectTransform.sizeDelta = new Vector2(64f, 64f);
        Outline outline = GetOrAdd<Outline>(chip.gameObject);
        outline.effectColor = forbidden ? color : new Color(0.08f, 0.05f, 0.05f);
        outline.effectDistance = new Vector2(4f, -4f);
        Image bar = Child<Image>(chip.transform, "Bar");   // null de verdad (Child usa TryGetComponent)
        if (forbidden)
        {
            if (bar == null) bar = NewImage(chip.transform, "Bar", null);
            bar.color = color;
            Place(bar.rectTransform, Vector2.zero, new Vector2(62f, 12f));
            bar.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
        }

        TMP_Text label = Child<TMP_Text>(rowT, "Text") ?? NewText(rowT, "Text", "", sans, 30, Cream);
        label.text = text;
        label.alignment = TextAlignmentOptions.Left;
        label.rectTransform.anchorMin = label.rectTransform.anchorMax = label.rectTransform.pivot = new Vector2(0f, 0.5f);
        label.rectTransform.anchoredPosition = new Vector2(90f, 0f);
        label.rectTransform.sizeDelta = new Vector2(550f, 80f);
        label.textWrappingMode = TextWrappingModes.Normal;
        return rowT.gameObject;
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

    private static void RemoveDebugHud()
    {
        foreach (BossDebugHUD hud in Object.FindObjectsByType<BossDebugHUD>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            Object.DestroyImmediate(hud);
    }

    // ── Utilidades ──────────────────────────────────────────────────────────────

    private static void ConfigureSource(AudioSource source)
    {
        source.playOnAwake = false;
        source.spatialBlend = 0f;   // 2D: avisos de interfaz, se oyen igual por los dos lados
        source.outputAudioMixerGroup = sfx;
    }

    // TryGetComponent y no GetComponent() ?? AddComponent(): en el editor, GetComponent de algo que no existe puede
    // devolver un «null falso» de Unity (compara igual a null con ==, pero no para el operador ?? de C#)
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

    private static void Size(RectTransform rt, Vector2 pos, Vector2 size) => Place(rt, pos, size);

    private static void Stretch(RectTransform rt, float padX, float padY)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = new Vector2(padX, padY); rt.offsetMax = new Vector2(-padX, -padY);
    }

    // Relleno que crece desde la izquierda: el ancho lo da anchorMax.x (lo mueve BossHealthBarUI)
    private static void Fill(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.pivot = new Vector2(0f, 0.5f);
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    private static void Anchor(RectTransform rt, float x0, float x1)
    {
        rt.anchorMin = new Vector2(x0, 0f); rt.anchorMax = new Vector2(x1, 1f);
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    private static void SetRefs(Object target, params object[] pairs)
    {
        var so = new SerializedObject(target);
        for (int i = 0; i < pairs.Length; i += 2)
        {
            SerializedProperty p = so.FindProperty((string)pairs[i]);
            if (p == null) { Debug.LogWarning($"BossUIBuilder: {target.GetType().Name} no tiene el campo {pairs[i]}."); continue; }
            object v = pairs[i + 1];
            switch (v)
            {
                case Object o: p.objectReferenceValue = o; break;
                case float f: p.floatValue = f; break;
                case GameObject[] arr:
                    p.arraySize = arr.Length;
                    for (int k = 0; k < arr.Length; k++) p.GetArrayElementAtIndex(k).objectReferenceValue = arr[k];
                    break;
                case null: p.objectReferenceValue = null; break;
            }
        }
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}
