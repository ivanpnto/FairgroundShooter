using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;

// Piezas comunes de las herramientas que montan interfaz con el estilo de la feria (Fase 8): fuentes, sprites,
// colores y ayudantes para crear o reutilizar objetos por nombre. Las usan PauseOptionsBuilder y
// LevelTransitionBuilder. BossUIBuilder (bloque 2) tiene su propia copia y no se toca.
public static class FairgroundUIKit
{
    public const string UIArt = "Assets/UI/";

    public static readonly Color Gold = new Color(0.93f, 0.74f, 0.31f);
    public static readonly Color Cream = new Color(0.96f, 0.91f, 0.79f);
    public static readonly Color Dim = new Color(0.08f, 0.02f, 0.02f, 0.78f);

    public static TMP_FontAsset Rye, Sans;
    public static Material RyeCartel;
    public static Sprite Board, BarFill, Round;
    public static AudioMixerGroup Sfx;

    public static void LoadStyle()
    {
        Rye = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/Rye SDF.asset");
        RyeCartel = AssetDatabase.LoadAssetAtPath<Material>("Assets/Fonts/Rye SDF - Cartel.mat");
        Sans = AssetDatabase.FindAssets("LiberationSans SDF t:TMP_FontAsset").Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<TMP_FontAsset>).FirstOrDefault(f => f != null && f.name == "LiberationSans SDF");
        if (Sans == null) Sans = Rye;
        Board = AssetDatabase.LoadAssetAtPath<Sprite>(UIArt + "Menu/Board.png");
        BarFill = AssetDatabase.LoadAssetAtPath<Sprite>(UIArt + "Boss/BarFill.png");
        Round = AssetDatabase.LoadAssetAtPath<Sprite>(UIArt + "Menu/RoundButton.png");
        Sfx = AssetDatabase.LoadAllAssetsAtPath("Assets/Audio/MainMixer.mixer").OfType<AudioMixerGroup>().FirstOrDefault(g => g.name == "SFX");
    }

    // Fuente 2D para avisos de interfaz, por el grupo SFX del mixer (así la controla el volumen de Efectos)
    public static void ConfigureSource(AudioSource source)
    {
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        source.outputAudioMixerGroup = Sfx;
    }

    public static Button ChildButton(Transform parent, string name, Button template)
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

    public static T GetOrAdd<T>(GameObject go) where T : Component => go.TryGetComponent(out T c) ? c : go.AddComponent<T>();

    public static T Child<T>(Transform parent, string name) where T : Component
    {
        Transform t = parent.Find(name);
        return t != null && t.TryGetComponent(out T c) ? c : null;
    }

    public static GameObject NewRect(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = parent.gameObject.layer;
        go.transform.SetParent(parent, false);
        return go;
    }

    public static Image NewImage(Transform parent, string name, Sprite sprite)
    {
        Image img = NewRect(parent, name).AddComponent<Image>();
        img.sprite = sprite;
        img.raycastTarget = false;
        return img;
    }

    public static TMP_Text NewText(Transform parent, string name, string text, TMP_FontAsset font, float size, Color color, Material material = null)
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

    public static void Place(RectTransform rt, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
    }

    public static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    public static void SetRefs(Object target, params object[] pairs)
    {
        var so = new SerializedObject(target);
        for (int i = 0; i < pairs.Length; i += 2)
        {
            SerializedProperty p = so.FindProperty((string)pairs[i]);
            if (p == null) { Debug.LogWarning($"FairgroundUIKit: {target.GetType().Name} no tiene el campo {pairs[i]}."); continue; }
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
