using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using static FairgroundUIKit;

// Monta la transición escenificada entre niveles (Fase 8) en la escena abierta: cartel y cuenta atrás en la
// interfaz, LevelTransitionSequence y StageDimmer en GameManager, y el sonido del telón en Booth.prefab.
// Menú: Tools > Fairground > Build Level Transition.
//
// Se puede repetir (reutiliza por nombre y vuelve a enlazar). Toca la escena y Booth.prefab: en Main, avisar antes.
// El panel antiguo (LevelTransitionPanel) se queda en la escena sin usar: GameManager vuelve a él si falta la
// secuencia nueva.
public static class LevelTransitionBuilder
{
    private const string AudioFolder = "Assets/Audio/Transition/";
    private const string BoothPrefab = "Assets/Prefabs/Enviroments/Booth.prefab";

    [MenuItem("Tools/Fairground/Build Level Transition")]
    public static void Build()
    {
        if (!EditorUtility.DisplayDialog("Build Level Transition",
                "Monta la transición entre niveles (telón, cartel y 3-2-1) en la escena abierta y añade el sonido del " +
                "telón a Booth.prefab.\n\nEn Main, avisa antes a tu compañero. ¿Continuar?", "Montar", "Cancelar"))
            return;
        Run();
    }

    public static void Run()
    {
        ImportAudio();
        LoadStyle();
        GameObject canvasGo = GameObject.Find("UI");
        if (canvasGo == null) { Debug.LogError("LevelTransitionBuilder: no hay un Canvas llamado UI en la escena."); return; }

        LevelTransitionUI ui = BuildUI(canvasGo.transform);
        SetupCurtainPrefab();

        GameManager gm = Object.FindFirstObjectByType<GameManager>();
        StageDimmer dimmer = GetOrAdd<StageDimmer>(gm.gameObject);
        SetRefs(dimmer, "lights", StageLights());

        LevelTransitionSequence sequence = GetOrAdd<LevelTransitionSequence>(gm.gameObject);
        SetRefs(sequence, "ui", ui, "dimmer", dimmer, "curtain", Object.FindFirstObjectByType<CurtainController>());
        SetRefs(gm, "transitionSequence", sequence);

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        Debug.Log("LevelTransitionBuilder: transición entre niveles montada.");
    }

    private static void ImportAudio()
    {
        AssetDatabase.Refresh();
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

    private static AudioClip Clip(string name) => AssetDatabase.LoadAssetAtPath<AudioClip>(AudioFolder + name + ".wav");

    // Todas las luces de los montajes de iluminación por nivel (también los apagados: se encienden al cambiar)
    private static Light[] StageLights()
    {
        var lights = new List<Light>();
        foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (t.name.StartsWith("Lighting_")) lights.AddRange(t.GetComponentsInChildren<Light>(true));
        return lights.ToArray();
    }

    // ── Interfaz ────────────────────────────────────────────────────────────────

    private static LevelTransitionUI BuildUI(Transform canvas)
    {
        // La raíz siempre activa (anima en Update); el cartel y el número se encienden y apagan
        Transform rootT = canvas.Find("LevelTransition") ?? NewRect(canvas, "LevelTransition").transform;
        Stretch((RectTransform)rootT);
        Transform pause = canvas.Find("PauseMenu");
        rootT.SetSiblingIndex(pause != null ? pause.GetSiblingIndex() : canvas.childCount - 1);   // bajo la pausa

        Transform groupT = rootT.Find("CardGroup") ?? NewRect(rootT, "CardGroup").transform;
        Stretch((RectTransform)groupT);
        CanvasGroup group = GetOrAdd<CanvasGroup>(groupT.gameObject);
        group.blocksRaycasts = false;
        group.interactable = false;

        var card = Child<UnityEngine.UI.Image>(groupT, "Card") ?? NewImage(groupT, "Card", Board);
        card.sprite = Board;
        card.type = UnityEngine.UI.Image.Type.Sliced;
        Place(card.rectTransform, new Vector2(0f, 60f), new Vector2(1250f, 430f));

        TMP_Text title = Child<TMP_Text>(card.transform, "Title") ?? NewText(card.transform, "Title", "¡BOSQUE SUPERADO!", Rye, 110, Gold, RyeCartel);
        title.enableAutoSizing = true;   // «¡FONDO MARINO SUPERADO!» es mucho más largo que «¡CIELO SUPERADO!»
        title.fontSizeMin = 60; title.fontSizeMax = 110;
        title.textWrappingMode = TextWrappingModes.NoWrap;
        Place(title.rectTransform, new Vector2(0f, 75f), new Vector2(1130f, 160f));

        TMP_Text next = Child<TMP_Text>(card.transform, "Next") ?? NewText(card.transform, "Next", "SIGUIENTE: FONDO MARINO", Rye, 50, Cream);
        Place(next.rectTransform, new Vector2(0f, -55f), new Vector2(1130f, 70f));

        TMP_Text bonus = Child<TMP_Text>(card.transform, "Bonus") ?? NewText(card.transform, "Bonus", "+87 por precisión (87 %)", Sans, 32, Cream);
        bonus.color = new Color(Cream.r, Cream.g, Cream.b, 0.8f);
        Place(bonus.rectTransform, new Vector2(0f, -130f), new Vector2(1130f, 50f));

        TMP_Text count = Child<TMP_Text>(rootT, "Count") ?? NewText(rootT, "Count", "3", Rye, 400, Cream, RyeCartel);
        Place(count.rectTransform, new Vector2(0f, 40f), new Vector2(1400f, 520f));
        count.textWrappingMode = TextWrappingModes.NoWrap;

        AudioSource source = GetOrAdd<AudioSource>(rootT.gameObject);
        ConfigureSource(source);

        LevelTransitionUI ui = GetOrAdd<LevelTransitionUI>(rootT.gameObject);
        SetRefs(ui, "cardGroup", group, "card", card.rectTransform, "titleText", title, "nextText", next, "bonusText", bonus,
            "countText", count, "audioSource", source,
            "popClip", Clip("Card_Pop"), "tickClip", Clip("Count_Tick"), "goClip", Clip("Count_Go"));
        groupT.gameObject.SetActive(false);
        count.gameObject.SetActive(false);
        return ui;
    }

    // ── Telón: sonido en Booth.prefab ───────────────────────────────────────────

    private static void SetupCurtainPrefab()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(BoothPrefab);
        CurtainController curtain = root.GetComponentInChildren<CurtainController>(true);
        if (curtain == null)
        {
            Debug.LogWarning("LevelTransitionBuilder: Booth.prefab no tiene CurtainController.");
            PrefabUtility.UnloadPrefabContents(root);
            return;
        }
        AudioSource source = GetOrAdd<AudioSource>(curtain.gameObject);
        ConfigureSource(source);
        SetRefs(curtain, "audioSource", source, "swooshClip", Clip("Curtain_Swoosh"), "thumpClip", Clip("Curtain_Thump"));
        PrefabUtility.SaveAsPrefabAsset(root, BoothPrefab);
        PrefabUtility.UnloadPrefabContents(root);
    }
}
