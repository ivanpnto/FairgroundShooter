using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using static FairgroundUIKit;

// Monta el contador y el rótulo del objetivo de puntos por nivel (24/11) en la escena abierta.
// Menú: Tools > Fairground > Build Objective HUD.
//
// Crea «ObjectiveHUD» justo después del HUD (por debajo de los menús): el contador va bajo la racha, con el mismo
// estilo que la puntuación, y el rótulo en el centro, en Rye dorado. Se puede repetir: busca las piezas por nombre.
// Los objetivos de cada nivel están en su LevelConfig (targetScore). Toca Main: avisar antes al compañero (§7).
public static class ObjectiveHUDBuilder
{
    [MenuItem("Tools/Fairground/Build Objective HUD")]
    public static void Build()
    {
        if (!EditorUtility.DisplayDialog("Build Objective HUD",
                "Monta el contador y el rótulo del objetivo de puntos en la escena abierta." +
                "\n\nEn Main, avisa antes a tu compañero. ¿Continuar?", "Montar", "Cancelar"))
            return;
        Run();
    }

    public static void Run()
    {
        LoadStyle();
        GameObject canvasGo = GameObject.Find("UI");
        if (canvasGo == null) { Debug.LogError("ObjectiveHUDBuilder: no hay un Canvas llamado UI en la escena."); return; }
        Transform canvas = canvasGo.transform;
        Transform hud = canvas.Find("HUD");
        TMP_Text scoreText = Child<TMP_Text>(hud, "ScoreText");
        RectTransform streak = hud.Find("StreakText") as RectTransform;

        Transform rootT = canvas.Find("ObjectiveHUD") ?? NewRect(canvas, "ObjectiveHUD").transform;
        // El mismo rect que el HUD (100×100 en el centro): así el contador usa las coordenadas de ScoreText tal cual
        RectTransform hudRect = (RectTransform)hud, rootRect = (RectTransform)rootT;
        rootRect.anchorMin = hudRect.anchorMin; rootRect.anchorMax = hudRect.anchorMax; rootRect.pivot = hudRect.pivot;
        rootRect.sizeDelta = hudRect.sizeDelta; rootRect.anchoredPosition = hudRect.anchoredPosition;
        rootT.SetSiblingIndex(hud.GetSiblingIndex() + 1);

        // ── Contador: mismo anclaje y fuente que la puntuación, una fila por debajo de la racha ──
        TMP_Text counter = Child<TMP_Text>(rootT, "Counter") ?? NewText(rootT, "Counter", "OBJETIVO 0 / 650", scoreText.font, 32, Cream);
        RectTransform src = scoreText.rectTransform, rt = counter.rectTransform;
        rt.anchorMin = src.anchorMin; rt.anchorMax = src.anchorMax; rt.pivot = src.pivot;
        float y = (streak != null ? streak.anchoredPosition.y : src.anchoredPosition.y - 60f) - 62f;
        rt.anchoredPosition = new Vector2(src.anchoredPosition.x, y);
        rt.sizeDelta = new Vector2(520f, 50f);
        counter.font = scoreText.font;
        counter.fontSize = 32;
        counter.alignment = TextAlignmentOptions.TopLeft;
        counter.textWrappingMode = TextWrappingModes.NoWrap;

        Image check = Child<Image>(counter.transform, "Check") ?? NewImage(counter.transform, "Check",
            AssetDatabase.LoadAssetAtPath<Sprite>(UIArt + "Icons/Check.png"));
        RectTransform crt = check.rectTransform;
        crt.anchorMin = crt.anchorMax = new Vector2(0f, 1f);
        crt.pivot = new Vector2(0f, 1f);
        crt.anchoredPosition = new Vector2(200f, 0f);
        crt.sizeDelta = new Vector2(38f, 38f);
        check.enabled = false;

        // ── Rótulo central ──
        TMP_Text banner = Child<TMP_Text>(rootT, "Banner") ?? NewText(rootT, "Banner", "¡OBJETIVO SUPERADO!", Rye, 64, Gold, RyeCartel);
        Place(banner.rectTransform, new Vector2(0f, 250f), new Vector2(1500f, 120f));
        banner.textWrappingMode = TextWrappingModes.NoWrap;
        banner.gameObject.SetActive(false);

        ObjectiveUI ui = GetOrAdd<ObjectiveUI>(rootT.gameObject);
        SetRefs(ui, "hud", hud.gameObject, "counterText", counter, "checkIcon", check, "bannerText", banner);
        counter.gameObject.SetActive(false);

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        Debug.Log("ObjectiveHUDBuilder: contador y rótulo del objetivo montados.");
    }
}
