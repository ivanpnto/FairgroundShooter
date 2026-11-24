using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using static FairgroundUIKit;

// Monta el selector de arma que sale al darle a jugar (24/11) en la escena abierta.
// Menú: Tools > Fairground > Build Weapon Picker.
//
// - Mueve el WeaponSelector del inicio a una tarjeta nueva («WeaponPicker», encima del resto) con título, pista de la
//   pistola de apoyo y botones ¡A JUGAR! y VOLVER. Recicla el selector: mismos botones ◀ ▶ y textos.
// - Borra el WeaponSelector del Game Over y encoge el tablero del jugador del inicio, que ya no lo lleva.
// - EMPEZAR → WeaponPickerUI.RequestStart y OTRA RONDA → WeaponPickerUI.RequestRestart.
// Se puede repetir: busca cada pieza por nombre y solo crea lo que falta. Toca Main: avisar antes al compañero (§7).
public static class WeaponPickerBuilder
{
    [MenuItem("Tools/Fairground/Build Weapon Picker")]
    public static void Build()
    {
        if (!EditorUtility.DisplayDialog("Build Weapon Picker",
                "Monta el selector de arma al jugar y quita los selectores del inicio y del Game Over en la escena abierta." +
                "\n\nEn Main, avisa antes a tu compañero. ¿Continuar?", "Montar", "Cancelar"))
            return;
        Run();
    }

    public static void Run()
    {
        LoadStyle();
        GameObject canvasGo = GameObject.Find("UI");
        if (canvasGo == null) { Debug.LogError("WeaponPickerBuilder: no hay un Canvas llamado UI en la escena."); return; }
        Transform canvas = canvasGo.transform;
        Transform startPanel = canvas.Find("StartPanel");
        Transform gameOverPanel = canvas.Find("GameOverPanel");
        Button template = startPanel.Find("RecordsButton").GetComponent<Button>();

        // ── Tarjeta ──
        Transform rootT = canvas.Find("WeaponPicker") ?? NewRect(canvas, "WeaponPicker").transform;
        Stretch((RectTransform)rootT);
        // Encima de los menús, pero por debajo de CÓMO JUGAR y las opciones (que pueden abrirse justo después)
        Transform howToPlay = canvas.Find("HowToPlay");
        rootT.SetSiblingIndex(howToPlay != null ? howToPlay.GetSiblingIndex() : canvas.childCount - 1);

        Image panel = Child<Image>(rootT, "Panel") ?? NewImage(rootT, "Panel", null);
        panel.color = new Color(0.08f, 0.02f, 0.02f, 0.82f);   // bloquea los clics al menú de detrás
        panel.raycastTarget = true;
        Stretch(panel.rectTransform);

        Image card = Child<Image>(panel.transform, "Card") ?? NewImage(panel.transform, "Card", Board);
        card.sprite = Board;
        card.type = Image.Type.Sliced;
        Place(card.rectTransform, new Vector2(0f, 0f), new Vector2(980f, 600f));

        TMP_Text title = Child<TMP_Text>(card.transform, "Title") ?? NewText(card.transform, "Title", "ELIGE TU ARMA", Rye, 76, Gold, RyeCartel);
        Place(title.rectTransform, new Vector2(0f, 215f), new Vector2(900f, 100f));

        // ── Selector reciclado del inicio (el del Game Over se borra) ──
        Transform selector = card.transform.Find("WeaponSelector");
        Transform fromStart = startPanel.Find("WeaponSelector");
        if (selector == null && fromStart != null)
        {
            // StartPanel es un prefab: su selector no se puede mover a otro sitio. Se copia (la copia ya no es parte
            // del prefab y los botones ◀ ▶ apuntan a su propio WeaponSelectorUI) y se quita del prefab
            selector = Object.Instantiate(fromStart.gameObject, card.transform).transform;
            selector.name = "WeaponSelector";
            Remove(fromStart);
        }
        if (selector == null) { Debug.LogError("WeaponPickerBuilder: no encuentro el WeaponSelector del inicio."); return; }
        Place((RectTransform)selector, new Vector2(0f, 55f), new Vector2(640f, 110f));

        Transform fromGameOver = gameOverPanel != null ? gameOverPanel.Find("WeaponSelector") : null;
        if (fromGameOver != null) Remove(fromGameOver);

        TMP_Text stats = Child<TMP_Text>(selector, "StatsText");
        TMP_Text sidearm = Child<TMP_Text>(selector, "SidearmText");
        if (sidearm == null)
        {
            sidearm = NewText(selector, "SidearmText", "", Sans, 28, Gold);
            if (stats != null) sidearm.font = stats.font;
        }
        Place(sidearm.rectTransform, new Vector2(0f, -85f), new Vector2(700f, 40f));
        SetRefs(selector.GetComponent<WeaponSelectorUI>(), "sidearmText", sidearm);

        // ── Botones ──
        Button play = ChildButton(card.transform, "PlayButton", template);
        Place((RectTransform)play.transform, new Vector2(170f, -195f), new Vector2(360f, 110f));
        play.GetComponentInChildren<TMP_Text>().text = "¡A JUGAR!";

        Button back = ChildButton(card.transform, "BackButton", template);
        Place((RectTransform)back.transform, new Vector2(-200f, -195f), new Vector2(260f, 96f));
        TMP_Text backText = back.GetComponentInChildren<TMP_Text>();
        backText.text = "VOLVER";
        backText.fontSize = 30;

        WeaponPickerUI ui = GetOrAdd<WeaponPickerUI>(rootT.gameObject);
        SetRefs(ui, "panel", panel.gameObject, "playButton", play, "backButton", back);
        panel.gameObject.SetActive(false);

        // ── Inicio: el tablero del jugador ya solo lleva el nombre y los créditos ──
        RectTransform board = startPanel.Find("PlayerBoard") as RectTransform;
        if (board != null) Place(board, new Vector2(0f, -45f), new Vector2(780f, 190f));

        // ── Enlaces: EMPEZAR y OTRA RONDA pasan por el selector ──
        Rewire(startPanel.Find("StartButton").GetComponent<Button>(), ui.RequestStart);
        if (gameOverPanel != null) Rewire(gameOverPanel.Find("RestartButton").GetComponent<Button>(), ui.RequestRestart);

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        Debug.Log("WeaponPickerBuilder: selector de arma al jugar montado.");
    }

    // Borra un objeto de la escena. Si pertenece a un prefab (no es un añadido de esta escena), se borra del prefab:
    // Unity no deja quitar piezas de una instancia
    private static void Remove(Transform t)
    {
        GameObject go = t.gameObject;
        if (!PrefabUtility.IsPartOfPrefabInstance(go) || PrefabUtility.IsAddedGameObjectOverride(go))
        {
            Object.DestroyImmediate(go);
            return;
        }

        string path = AssetDatabase.GetAssetPath(PrefabUtility.GetCorrespondingObjectFromSource(go));
        string childName = t.name;
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        Transform inPrefab = root.transform.Find(childName);
        if (inPrefab != null) Object.DestroyImmediate(inPrefab.gameObject);
        PrefabUtility.SaveAsPrefabAsset(root, path);
        PrefabUtility.UnloadPrefabContents(root);
        Debug.Log($"WeaponPickerBuilder: quitado {childName} de {path}.");
    }

    // Deja una sola llamada persistente en el botón: la del selector
    private static void Rewire(Button button, UnityEngine.Events.UnityAction action)
    {
        for (int i = button.onClick.GetPersistentEventCount() - 1; i >= 0; i--)
            UnityEventTools.RemovePersistentListener(button.onClick, i);
        UnityEventTools.AddVoidPersistentListener(button.onClick, action);
        EditorUtility.SetDirty(button);
    }
}
