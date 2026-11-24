using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using static FairgroundUIKit;

// Retoques del 25/11 en la escena abierta (Main). Menú: Tools > Fairground > Build Polish 25-11.
//
// - HUD legible y con el estilo de la feria: puntos, tiempo, balas y objetivo en Rye crema con sombra (material
//   «Rye SDF - HUD»); la racha sigue en LiberationSans (Rye no tiene «×»), también con sombra.
// - Pista del cambio de arma encima de las balas («Q · PISTOLA», SidearmHintUI) y sonido del cambio (se genera
//   aquí: Assets/Audio/Weapons/Weapon_Switch.wav).
// - El resumen de fin de partida dice HABILIDADES, como el inicio (seguía diciendo TIENDA).
// - Lista de perfiles (prefab ProfilePanel): filas más bajas y lista más alta, para que quepan cuatro sin cortar;
//   rueda del ratón más rápida y botón BORRAR en cada fila.
// Se puede repetir: busca cada pieza por nombre. Toca Main y el prefab ProfilePanel: avisar antes al compañero (§7).
public static class PolishBuilder
{
    private const string RyeHudPath = "Assets/Fonts/Rye SDF - HUD.mat";
    private const string SansHudPath = "Assets/Fonts/LiberationSans SDF - HUD.mat";
    private const string SwitchClipPath = "Assets/Audio/Weapons/Weapon_Switch.wav";
    private const string ProfilePanelPath = "Assets/Prefabs/UI/ProfilePanel.prefab";

    [MenuItem("Tools/Fairground/Build Polish 25-11")]
    public static void Build()
    {
        if (!EditorUtility.DisplayDialog("Build Polish 25-11",
                "Retoca el HUD, el resumen y la lista de perfiles, y genera el sonido del cambio de arma." +
                "\n\nToca Main y el prefab ProfilePanel: avisa antes a tu compañero. ¿Continuar?", "Montar", "Cancelar"))
            return;
        Run();
    }

    public static void Run()
    {
        LoadStyle();
        GameObject canvasGo = GameObject.Find("UI");
        if (canvasGo == null) { Debug.LogError("PolishBuilder: no hay un Canvas llamado UI en la escena."); return; }
        Transform canvas = canvasGo.transform;

        StyleHud(canvas);
        BuildSidearmHint(canvas);
        AssignSwitchSound();
        FixSummaryShopLabel(canvas);
        FixProfilePanel();

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
        Debug.Log("PolishBuilder: retoques del 25/11 montados.");
    }

    // ── HUD ─────────────────────────────────────────────────────────────────────

    // Material con sombra (underlay) a partir del de la fuente: comparte su atlas, así que vale para esa fuente
    private static Material HudMaterial(TMP_FontAsset font, string path)
    {
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(font.material) { name = Path.GetFileNameWithoutExtension(path) };
            AssetDatabase.CreateAsset(mat, path);
        }
        mat.EnableKeyword("UNDERLAY_ON");
        mat.SetColor("_UnderlayColor", new Color(0f, 0f, 0f, 0.85f));
        mat.SetFloat("_UnderlayOffsetX", 0.5f);
        mat.SetFloat("_UnderlayOffsetY", -0.5f);
        mat.SetFloat("_UnderlayDilate", 0.4f);
        mat.SetFloat("_UnderlaySoftness", 0.35f);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    private static void StyleHud(Transform canvas)
    {
        Material ryeHud = HudMaterial(Rye, RyeHudPath);
        Transform hud = canvas.Find("HUD");

        Style(Child<TMP_Text>(hud, "ScoreText"), Rye, ryeHud, 34, 460f, TextAlignmentOptions.TopLeft);
        // El tiempo, alineado a la derecha y algo más hacia dentro: con el rect original (borde en x = 990) el texto
        // alineado a la derecha se salía de la pantalla en 16:9
        TMP_Text timer = Child<TMP_Text>(hud, "TimerText");
        Style(timer, Rye, ryeHud, 34, 460f, TextAlignmentOptions.TopRight);
        if (timer != null) timer.rectTransform.anchoredPosition = new Vector2(890f, timer.rectTransform.anchoredPosition.y);
        // Las balas, arriba de su rect (como estaban): abajo del todo el texto quedaba cortado por el borde
        Style(Child<TMP_Text>(hud, "AmmoText"), Rye, ryeHud, 34, 460f, TextAlignmentOptions.TopLeft);

        // La racha cambia de color y tamaño sola (StreakUI): solo se le pone la sombra
        TMP_Text streak = Child<TMP_Text>(hud, "StreakText");
        if (streak != null)
        {
            streak.fontSharedMaterial = HudMaterial(streak.font, SansHudPath);
            EditorUtility.SetDirty(streak);
        }

        TMP_Text counter = Child<TMP_Text>(canvas.Find("ObjectiveHUD"), "Counter");
        if (counter != null) Style(counter, Rye, ryeHud, 30, 560f, TextAlignmentOptions.TopLeft);

        ObjectiveUI objective = Object.FindFirstObjectByType<ObjectiveUI>();
        if (objective != null)
        {
            var so = new SerializedObject(objective);
            so.FindProperty("pendingColor").colorValue = Cream;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    private static void Style(TMP_Text text, TMP_FontAsset font, Material mat, float size, float width, TextAlignmentOptions align)
    {
        if (text == null) return;
        text.font = font;
        text.fontSharedMaterial = mat;
        text.fontSize = size;
        text.color = Cream;
        text.alignment = align;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        RectTransform rt = text.rectTransform;
        rt.sizeDelta = new Vector2(width, rt.sizeDelta.y);
        EditorUtility.SetDirty(text);
    }

    // ── Cambio de arma: pista y sonido ──────────────────────────────────────────

    private static void BuildSidearmHint(Transform canvas)
    {
        Transform root = canvas.Find("ObjectiveHUD");
        if (root == null) { Debug.LogError("PolishBuilder: falta ObjectiveHUD (Tools > Fairground > Build Objective HUD)."); return; }
        Transform hud = canvas.Find("HUD");
        RectTransform ammo = hud.Find("AmmoText") as RectTransform;

        TMP_Text hint = Child<TMP_Text>(root, "SidearmHint") ?? NewText(root, "SidearmHint", "Q · PISTOLA", Rye, 26, Gold);
        hint.font = Rye;
        hint.fontSharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(RyeHudPath);
        hint.fontSize = 26;
        hint.color = Gold;
        hint.alignment = TextAlignmentOptions.BottomLeft;
        hint.textWrappingMode = TextWrappingModes.NoWrap;
        RectTransform rt = hint.rectTransform;
        // Mismo anclaje que las balas, justo encima de su rect (las balas van arriba de un rect de 80 de alto)
        rt.anchorMin = ammo.anchorMin; rt.anchorMax = ammo.anchorMax; rt.pivot = ammo.pivot;
        rt.anchoredPosition = ammo.anchoredPosition + new Vector2(0f, ammo.sizeDelta.y + 2f);
        rt.sizeDelta = new Vector2(460f, 40f);

        SidearmHintUI ui = GetOrAdd<SidearmHintUI>(root.gameObject);
        SetRefs(ui, "hud", hud.gameObject, "hintText", hint);
        hint.gameObject.SetActive(false);
    }

    private static void AssignSwitchSound()
    {
        if (!File.Exists(SwitchClipPath))
        {
            WriteSwitchClip(SwitchClipPath);
            AssetDatabase.ImportAsset(SwitchClipPath);
        }
        AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(SwitchClipPath);
        WeaponEffects effects = Object.FindFirstObjectByType<WeaponEffects>();
        if (effects != null && clip != null) SetRefs(effects, "switchSound", clip);
    }

    // «Clac-clac» metálico de 0,24 s: un golpe seco (ruido filtrado con caída rápida) al bajar el arma y otro algo más
    // agudo al subir la nueva, con un tono corto de chapa. 44,1 kHz, 16 bits, mono. Determinista (semilla fija)
    private static void WriteSwitchClip(string path)
    {
        const int rate = 44100;
        int n = (int)(rate * 0.24f);
        var samples = new float[n];
        var rng = new System.Random(2511);
        float lp = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)rate;
            float s = 0f;
            s += Click(t, 0.000f, 0.030f, 0.9f, 1650f, rng, ref lp);
            s += Click(t, 0.120f, 0.025f, 0.7f, 2400f, rng, ref lp);
            samples[i] = Mathf.Clamp(s, -1f, 1f) * 0.8f;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(path));
        using (var w = new BinaryWriter(File.Create(path)))
        {
            int bytes = n * 2;
            w.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); w.Write(36 + bytes);
            w.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt ")); w.Write(16); w.Write((short)1); w.Write((short)1);
            w.Write(rate); w.Write(rate * 2); w.Write((short)2); w.Write((short)16);
            w.Write(System.Text.Encoding.ASCII.GetBytes("data")); w.Write(bytes);
            foreach (float s in samples) w.Write((short)(s * short.MaxValue));
        }
    }

    // Un golpe: ruido paso bajo + tono de chapa, los dos con caída exponencial desde «start»
    private static float Click(float t, float start, float decay, float gain, float ring, System.Random rng, ref float lp)
    {
        float dt = t - start;
        if (dt < 0f) return 0f;
        float env = Mathf.Exp(-dt / decay);
        float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
        lp += 0.35f * (noise - lp);
        float tone = Mathf.Sin(2f * Mathf.PI * ring * dt) * Mathf.Exp(-dt / (decay * 0.6f));
        return gain * env * (0.75f * lp + 0.45f * tone);
    }

    // ── Resumen: TIENDA → HABILIDADES ───────────────────────────────────────────

    private static void FixSummaryShopLabel(Transform canvas)
    {
        GameOverSummaryUI summary = canvas.GetComponentInChildren<GameOverSummaryUI>(true);
        if (summary == null) return;
        var button = new SerializedObject(summary).FindProperty("shopButton").objectReferenceValue as Button;
        if (button == null) return;
        TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
        label.text = "HABILIDADES";
        label.enableAutoSizing = true; label.fontSizeMin = 18; label.fontSizeMax = 36;
        label.margin = new Vector4(30f, 0f, 30f, 0f);
        EditorUtility.SetDirty(label);
    }

    // ── Perfiles ────────────────────────────────────────────────────────────────

    private static void FixProfilePanel()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(ProfilePanelPath);
        try
        {
            Transform t = root.transform;
            RectTransform choose = t.Find("ChooseLabel") as RectTransform;
            RectTransform list = t.Find("ProfileList") as RectTransform;
            // Filas de 46 px con 6 de separación: caben cuatro (10 + 4 × 46 + 3 × 6 + 10 = 222 px) y la quinta asoma,
            // que avisa de que hay más. La lista pasa de 230 a 250 px
            if (choose != null) choose.anchoredPosition = new Vector2(0f, 145f);
            if (list != null)
            {
                list.anchoredPosition = new Vector2(0f, -25f);
                list.sizeDelta = new Vector2(860f, 250f);
                ScrollRect scroll = list.GetComponent<ScrollRect>();
                if (scroll != null) { scroll.scrollSensitivity = 30f; scroll.horizontal = false; }
            }
            VerticalLayoutGroup layout = root.GetComponentInChildren<VerticalLayoutGroup>(true);
            if (layout != null) layout.spacing = 6f;

            Transform template = list != null ? list.Find("Viewport/Content/EntryTemplate") : null;
            if (template == null) { Debug.LogError("PolishBuilder: no encuentro EntryTemplate en ProfilePanel."); return; }
            LayoutElement element = template.GetComponent<LayoutElement>();
            if (element != null) element.preferredHeight = 46f;

            // El nombre deja sitio al botón BORRAR a la derecha
            TMP_Text label = template.GetComponentInChildren<TMP_Text>(true);
            // Los nombres largos («Demo antiguo · récord 7126 · 2212 créditos») se encogen en vez de salirse
            label.enableAutoSizing = true; label.fontSizeMin = 16; label.fontSizeMax = 30;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            RectTransform lrt = label.rectTransform;
            lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one;
            lrt.offsetMin = new Vector2(12f, 0f); lrt.offsetMax = new Vector2(-170f, 0f);

            Button create = t.Find("CreateButton").GetComponent<Button>();
            Transform deleteT = template.Find("DeleteButton");
            if (deleteT == null)
            {
                GameObject copy = Object.Instantiate(create.gameObject, template);
                copy.name = "DeleteButton";
                deleteT = copy.transform;
            }
            Button delete = deleteT.GetComponent<Button>();
            delete.onClick = new Button.ButtonClickedEvent();
            RectTransform drt = (RectTransform)deleteT;
            drt.anchorMin = drt.anchorMax = drt.pivot = new Vector2(1f, 0.5f);
            drt.anchoredPosition = new Vector2(-6f, 0f);
            drt.sizeDelta = new Vector2(150f, 40f);
            TMP_Text deleteText = deleteT.GetComponentInChildren<TMP_Text>(true);
            deleteText.text = "BORRAR";
            deleteText.enableAutoSizing = true; deleteText.fontSizeMin = 14; deleteText.fontSizeMax = 22;
            deleteText.margin = new Vector4(12f, 0f, 12f, 0f);

            PrefabUtility.SaveAsPrefabAsset(root, ProfilePanelPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }
}
