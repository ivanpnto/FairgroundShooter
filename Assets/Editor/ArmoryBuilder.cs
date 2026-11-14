using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using static FairgroundUIKit;

// Monta la armería en 3D (Fase 8): Tools > Fairground > Build Armory.
//  · Prefab Armory (Prefabs/Enviroments): el modelo de Armory.fbx (puerta en la carpa a rayas e interior bajo el
//    suelo), luces, los cinco huecos con su arma, la cámara y el Volume propios. Se rehace entero en cada pasada
//    y se guarda en la misma ruta (conserva el GUID, así que la instancia de Main no se pierde).
//  · En Main: la instancia, la interfaz (fundido, ficha, VOLVER), los botones ARMERÍA del inicio y del resumen,
//    TIENDA → HABILIDADES, y la tienda solo con habilidades (prefab ShopPanel).
//  · Capa Armory y Rendering Layer 2 «Armory»: aíslan el interior de las luces de cada nivel.
// Se puede repetir. Toca Main.unity y ShopPanel.prefab: avisar antes al compañero.
public static class ArmoryBuilder
{
    private const string ModelPath = "Assets/Art/Models/Environments/Armory.fbx";
    private const string PrefabPath = "Assets/Prefabs/Enviroments/Armory.prefab";
    private const string ShopPrefab = "Assets/Prefabs/UI/ShopPanel.prefab";
    private const string AudioFolder = "Assets/Audio/Armory/";
    private const string LayerName = "Armory";
    private const int RenderBit = 2;                 // Rendering Layer «Armory»
    internal const uint InteriorMask = 1u << RenderBit;

    [MenuItem("Tools/Fairground/Build Armory")]
    public static void Build()
    {
        if (!EditorUtility.DisplayDialog("Build Armory",
                "Monta la armería en 3D, sus botones y la tienda de habilidades en la escena abierta, y modifica " +
                "ShopPanel.prefab.\n\nEn Main, avisa antes a tu compañero. ¿Continuar?", "Montar", "Cancelar"))
            return;
        Run();
    }

    public static void Run()
    {
        int layer = EnsureLayers();
        ImportAssets();
        LoadStyle();

        GameObject canvasGo = GameObject.Find("UI");
        if (canvasGo == null) { Debug.LogError("ArmoryBuilder: no hay un Canvas llamado UI en la escena."); return; }
        Transform canvas = canvasGo.transform;

        GameObject prefab = BuildPrefab(layer);
        ArmoryController armory = PlaceInstance(prefab);

        // La cámara del jugador no debe ver el Volume de la armería (es global y tiene más prioridad)
        Camera main = Camera.main;
        if (main != null && layer >= 0)
        {
            var data = main.GetUniversalAdditionalCameraData();
            data.volumeLayerMask &= ~(1 << layer);
            EditorUtility.SetDirty(data);
        }
        ArmoryUI ui = BuildUI(canvas, armory);
        SetRefs(armory, "ui", ui);
        UpdateShopPrefab();

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
        Debug.Log("ArmoryBuilder: armería montada.");
    }

    // ── Capas ───────────────────────────────────────────────────────────────────

    internal static int EnsureLayers()
    {
        var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        SerializedProperty layers = tagManager.FindProperty("layers");
        int index = -1;
        for (int i = 8; i < layers.arraySize; i++)
            if (layers.GetArrayElementAtIndex(i).stringValue == LayerName) { index = i; break; }
        if (index < 0)
            for (int i = 8; i < layers.arraySize; i++)
                if (string.IsNullOrEmpty(layers.GetArrayElementAtIndex(i).stringValue))
                {
                    layers.GetArrayElementAtIndex(i).stringValue = LayerName;
                    index = i;
                    break;
                }
        SerializedProperty rendering = tagManager.FindProperty("m_RenderingLayers");
        if (rendering != null && rendering.arraySize > RenderBit)
            rendering.GetArrayElementAtIndex(RenderBit).stringValue = LayerName;
        tagManager.ApplyModifiedPropertiesWithoutUndo();
        return index;
    }

    // ── Importación ─────────────────────────────────────────────────────────────

    private static void ImportAssets()
    {
        AssetDatabase.Refresh();
        ImportModel(ModelPath);
        ImportAudio(AudioFolder);
    }

    // FBX de interior: materiales por descripción (remapeados a los .mat del proyecto), sin animación
    internal static void ImportModel(string path)
    {
        var model = (ModelImporter)AssetImporter.GetAtPath(path);
        model.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
        model.materialLocation = ModelImporterMaterialLocation.InPrefab;
        model.importAnimation = false;
        model.bakeAxisConversion = false;
        model.SearchAndRemapMaterials(ModelImporterMaterialName.BasedOnMaterialName, ModelImporterMaterialSearch.Everywhere);
        model.SaveAndReimport();
    }

    // Efectos cortos: descomprimidos al cargar (suenan sin retardo), Vorbis y mono
    internal static void ImportAudio(string folder)
    {
        foreach (string guid in AssetDatabase.FindAssets("t:AudioClip", new[] { folder.TrimEnd('/') }))
        {
            var imp = (AudioImporter)AssetImporter.GetAtPath(AssetDatabase.GUIDToAssetPath(guid));
            AudioImporterSampleSettings s = imp.defaultSampleSettings;
            s.loadType = AudioClipLoadType.DecompressOnLoad;
            s.compressionFormat = AudioCompressionFormat.Vorbis;
            s.quality = 0.7f;
            imp.defaultSampleSettings = s;
            imp.forceToMono = true;
            imp.SaveAndReimport();
        }
    }

    private static AudioClip Clip(string name) => AssetDatabase.LoadAssetAtPath<AudioClip>(AudioFolder + name + ".wav");

    // Bombillas de la armería: materiales Lit propios. M_Bulb no sirve: usa el shader de las bombillas en cadena de
    // la caseta, que se enciende según los colores de vértice de su modelo
    internal static Material BulbMaterial(string name, Color baseColor, Color emission)
    {
        string path = $"Assets/Art/Materials/Fairground/{name}.mat";
        Shader lit = Shader.Find("Universal Render Pipeline/Lit");
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null) { mat = new Material(lit); AssetDatabase.CreateAsset(mat, path); }
        mat.shader = lit;
        mat.SetColor("_BaseColor", baseColor);
        mat.SetFloat("_Smoothness", 0.6f);
        if (emission.maxColorComponent > 0f)
        {
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", emission);
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        }
        else
        {
            mat.DisableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", Color.black);
        }
        EditorUtility.SetDirty(mat);
        return mat;
    }

    internal static Material DoorDarkMaterial()
    {
        const string path = "Assets/Art/Materials/Fairground/M_A_DoorDark.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null) { mat = new Material(Shader.Find("Universal Render Pipeline/Unlit")); AssetDatabase.CreateAsset(mat, path); }
        mat.SetColor("_BaseColor", new Color(0.03f, 0.015f, 0.02f));
        EditorUtility.SetDirty(mat);
        return mat;
    }

    // ── Prefab ──────────────────────────────────────────────────────────────────

    private static GameObject BuildPrefab(int layer)
    {
        Material bulbOff = BulbMaterial("M_A_BulbOff", new Color(0.3f, 0.28f, 0.26f), Color.black);
        Material bulbOn = BulbMaterial("M_A_BulbOn", new Color(1f, 0.92f, 0.78f), new Color(1f, 0.78f, 0.5f) * 2.2f);
        Material bulbGold = BulbMaterial("M_A_BulbGold", new Color(1f, 0.82f, 0.35f), new Color(1f, 0.62f, 0.12f) * 3f);

        var root = new GameObject("Armory");
        root.layer = layer;
        var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath), root.transform);
        PrefabUtility.UnpackPrefabInstance(model, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        model.transform.localPosition = Vector3.zero;
        // Con la raíz a 0 (la regla del §6) este FBX entra girado 180° en Y respecto a Env_Fairground: la puerta
        // salía en (19, 5,5) en vez de en la carpa (−19, −5,5). Comprobado contra la posición de Fair_Tents
        model.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);

        // El interior solo lo iluminan sus propias luces; la puerta, las de la feria
        foreach (Renderer r in model.GetComponentsInChildren<Renderer>(true))
        {
            bool outside = r.name.StartsWith("Armory_Entrance");
            r.renderingLayerMask = outside ? 1u : InteriorMask;
            r.shadowCastingMode = outside ? ShadowCastingMode.On : ShadowCastingMode.Off;
        }

        // Las bombillas del cartel, encendidas siempre
        Transform signBulbs = FindDeep(model.transform, "Armory_SignBulbs");
        if (signBulbs != null) signBulbs.GetComponent<Renderer>().sharedMaterial = bulbOn;

        // Luces del interior
        NewLight(root.transform, "Light_Main", LightType.Point, new Vector3(0f, -56.4f, 0.6f), Quaternion.identity, new Color(1f, 0.84f, 0.66f), 8f, 10f);
        NewLight(root.transform, "Light_Wall", LightType.Spot, new Vector3(0f, -56.0f, -1.6f),
            Quaternion.LookRotation(new Vector3(0f, -58.1f, 3.3f) - new Vector3(0f, -56.0f, -1.6f)), new Color(1f, 0.9f, 0.75f), 20f, 11f).spotAngle = 80f;
        NewLight(root.transform, "Light_Sign", LightType.Point, new Vector3(0f, -56.5f, 2.5f), Quaternion.identity, new Color(1f, 0.8f, 0.5f), 1.5f, 2.6f);

        // Farol sobre la puerta, por fuera: la entrada se ve de noche desde lejos (luz normal, la de la feria)
        Transform door = FindDeep(model.transform, "Armory_Entrance");
        if (door != null)
        {
            // El hueco de la puerta, negro de verdad: con M_C_Dark y las luces de la noche se veía morado
            var doorRenderer = door.GetComponent<Renderer>();
            Material[] dm = doorRenderer.sharedMaterials;
            dm[0] = DoorDarkMaterial();
            doorRenderer.sharedMaterials = dm;

            Bounds db = door.GetComponent<Renderer>().bounds;
            Light lantern = NewLight(root.transform, "Light_Door", LightType.Point, new Vector3(db.center.x, db.max.y + 0.25f, db.center.z) + new Vector3(0.556f, 0f, -0.831f) * 0.8f,
                Quaternion.identity, new Color(1f, 0.72f, 0.4f), 4f, 5f);
            lantern.renderingLayerMask = 1;
        }

        // Huecos con su arma
        WeaponController wc = Object.FindFirstObjectByType<WeaponController>();
        var weapons = new List<WeaponData>(wc != null ? wc.AvailableWeapons : new List<WeaponData>());
        var slots = new List<ArmorySlot>();
        for (int i = 0; i < 5 && i < weapons.Count; i++)
        {
            Transform anchor = FindDeep(model.transform, $"Armory_Slot_{i}");
            Transform bulbT = FindDeep(model.transform, $"Armory_Bulb_{i}");
            if (anchor == null) { Debug.LogWarning($"ArmoryBuilder: falta Armory_Slot_{i} en el modelo."); continue; }

            var slotGo = new GameObject($"Slot_{i}");
            slotGo.layer = layer;
            slotGo.transform.SetParent(root.transform, false);
            slotGo.transform.position = anchor.position;
            var holder = new GameObject("Holder").transform;
            holder.SetParent(slotGo.transform, false);
            GameObject gun = PlaceWeapon(weapons[i], holder);

            // La zona que se puede señalar, a la medida del arma (los huecos ya no son iguales)
            var box = slotGo.AddComponent<BoxCollider>();
            float gunLength = gun != null ? RendererBounds(gun).size.x : 0.8f;
            box.size = new Vector3(Mathf.Max(0.6f, gunLength + 0.15f), 0.5f, 0.35f);

            Light bulbLight = bulbT != null
                ? NewLight(root.transform, $"BulbLight_{i}", LightType.Point, bulbT.GetComponent<Renderer>().bounds.center + new Vector3(0f, -0.05f, -0.15f),
                    Quaternion.identity, new Color(1f, 0.78f, 0.45f), 0.9f, 1.4f)
                : null;

            // Placa de latón (en el modelo, bajo cada arma): el texto dice el precio, TUYA o EQUIPADA
            var plate = new GameObject("Plate", typeof(RectTransform)).AddComponent<TextMeshPro>();
            plate.transform.SetParent(slotGo.transform, false);
            plate.transform.position = new Vector3(anchor.position.x, -58.64f, 3.255f);
            plate.rectTransform.sizeDelta = new Vector2(0.42f, 0.11f);
            plate.font = Rye;
            plate.fontSize = 0.75f;
            plate.enableAutoSizing = true; plate.fontSizeMin = 0.3f; plate.fontSizeMax = 0.75f;
            plate.alignment = TextAlignmentOptions.Center;
            plate.color = new Color(0.25f, 0.12f, 0.05f);
            plate.text = $"{weapons[i].price} CR.";
            plate.GetComponent<MeshRenderer>().renderingLayerMask = InteriorMask;

            ArmorySlot slot = slotGo.AddComponent<ArmorySlot>();
            SetRefs(slot, "weapon", weapons[i], "holder", holder, "bulb", bulbT != null ? bulbT.GetComponent<Renderer>() : null,
                "bulbLight", bulbLight, "plate", plate, "bulbOff", bulbOff, "bulbOn", bulbOn, "bulbGold", bulbGold);
            slots.Add(slot);
        }

        // Etiqueta flotante (nombre y precio del arma señalada)
        TextMeshPro label = HoverLabel(root.transform);

        // Atrezo animado: cajón (se desliza) y payaso (gira sobre la base del poste)
        Transform drawer = FindDeep(model.transform, "Armory_RegisterDrawer");
        Transform clown = FindDeep(model.transform, "Armory_Clown");
        Transform clownPivot = null;
        if (clown != null)
        {
            Bounds cb = clown.GetComponent<Renderer>().bounds;
            clownPivot = new GameObject("ClownPivot").transform;
            clownPivot.SetParent(root.transform, false);
            clownPivot.position = new Vector3(cb.center.x, cb.min.y, cb.max.z - 0.05f);
            clown.SetParent(clownPivot, true);
        }
        var audio = root.AddComponent<AudioSource>();
        ConfigureSource(audio);
        ArmoryProps props = root.AddComponent<ArmoryProps>();
        SetRefs(props, "drawer", drawer, "clownPivot", clownPivot, "audioSource", audio, "registerClip", Clip("Register_ChaChing"));

        // Cámara y post-procesado propios
        Camera cam = ShopCamera(root.transform, "ArmoryCamera");
        Volume vol = InteriorVolume(root.transform, layer, "ArmoryVolume");

        ArmoryController controller = root.AddComponent<ArmoryController>();
        SetRefs(controller, "shopCamera", cam, "interiorVolume", vol, "slots", slots.ToArray(), "props", props, "hoverLabel", label,
            "audioSource", audio, "hoverClip", Clip("Armory_Hover"), "bulbClip", Clip("Bulb_On"));

        GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
        return saved;
    }

    // Etiqueta flotante: nombre y precio del objeto señalado, en dorado sobre una placa oscura
    internal static TextMeshPro HoverLabel(Transform root)
    {
        var label = new GameObject("HoverLabel", typeof(RectTransform)).AddComponent<TextMeshPro>();
        label.transform.SetParent(root, false);
        label.rectTransform.sizeDelta = new Vector2(2f, 0.5f);
        label.font = Rye;
        label.fontSize = 1.5f;
        label.alignment = TextAlignmentOptions.Center;
        // Material por defecto de la fuente: RyeCartel es de interfaz y recorta con la máscara del Canvas, así que en
        // el mundo no se veía. Para que se lea sobre la madera, dorado con una placa oscura detrás
        label.color = Gold;
        var back = GameObject.CreatePrimitive(PrimitiveType.Quad);
        back.name = "LabelBack";
        Object.DestroyImmediate(back.GetComponent<Collider>());
        back.transform.SetParent(label.transform, false);
        back.transform.localPosition = new Vector3(0f, 0f, 0.01f);
        back.transform.localScale = new Vector3(0.62f, 0.3f, 1f);
        var backRenderer = back.GetComponent<MeshRenderer>();
        backRenderer.sharedMaterial = DoorDarkMaterial();
        backRenderer.renderingLayerMask = InteriorMask;
        backRenderer.shadowCastingMode = ShadowCastingMode.Off;
        label.GetComponent<MeshRenderer>().renderingLayerMask = InteriorMask;
        return label;
    }

    // Cámara propia de la tienda (no la del jugador, que lleva el arma pegada delante), apagada hasta entrar
    internal static Camera ShopCamera(Transform root, string name)
    {
        var camGo = new GameObject(name);
        camGo.transform.SetParent(root, false);
        Camera cam = camGo.AddComponent<Camera>();
        cam.enabled = false;
        cam.nearClipPlane = 0.05f;
        cam.farClipPlane = 300f;
        cam.cullingMask = ~((1 << LayerMask.NameToLayer("Weapon")) | (1 << LayerMask.NameToLayer("UI")));
        var camData = camGo.AddComponent<UniversalAdditionalCameraData>();
        camData.renderPostProcessing = true;
        Camera main = Camera.main;
        if (main != null)
        {
            cam.clearFlags = main.clearFlags;
            cam.backgroundColor = main.backgroundColor;
            cam.fieldOfView = main.fieldOfView;
            var mainData = main.GetUniversalAdditionalCameraData();
            camData.antialiasing = mainData.antialiasing;
        }
        return cam;
    }

    // Post-procesado del interior, en la capa Armory. Apagado: el controlador lo enciende solo con su tienda abierta
    internal static Volume InteriorVolume(Transform root, int layer, string name)
    {
        var volGo = new GameObject(name);
        volGo.layer = layer;
        volGo.transform.SetParent(root, false);
        Volume vol = volGo.AddComponent<Volume>();
        vol.isGlobal = true;
        vol.priority = 10f;
        vol.sharedProfile = ArmoryProfile();
        vol.enabled = false;
        return vol;
    }

    // Copia de VP_Night con el filtro de color neutro: el interior es cálido por sus luces, no por el etalonaje
    internal static VolumeProfile ArmoryProfile()
    {
        const string path = "Assets/Settings/VP_Armory.asset";
        var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
        if (profile == null)
        {
            AssetDatabase.CopyAsset("Assets/Settings/VP_Night.asset", path);
            profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
        }
        if (profile.TryGet(out ColorAdjustments color))
        {
            color.colorFilter.Override(Color.white);
            color.postExposure.Override(0.2f);
        }
        if (profile.TryGet(out Vignette vignette)) vignette.intensity.Override(0.3f);
        EditorUtility.SetDirty(profile);
        return profile;
    }

    // El arma del expositor: una copia «muda» del modelo (sin scripts) con la longitud en horizontal, centrada en
    // su soporte y en la capa por defecto (la capa Weapon solo la ve la cámara del jugador)
    private static GameObject PlaceWeapon(WeaponData data, Transform holder)
    {
        if (data == null || data.modelPrefab == null) return null;
        var gun = (GameObject)PrefabUtility.InstantiatePrefab(data.modelPrefab, holder);
        PrefabUtility.UnpackPrefabInstance(gun, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        foreach (MonoBehaviour mb in gun.GetComponentsInChildren<MonoBehaviour>(true)) Object.DestroyImmediate(mb);
        foreach (AudioSource a in gun.GetComponentsInChildren<AudioSource>(true)) Object.DestroyImmediate(a);
        foreach (Light l in gun.GetComponentsInChildren<Light>(true)) Object.DestroyImmediate(l.gameObject);
        // Piezas sueltas que el juego coloca o esconde por su cuenta (la bala de la Five-seven está a 60 cm)
        foreach (Transform t in gun.GetComponentsInChildren<Transform>(true))
            if (t != null && t != gun.transform && t.name.Contains("Bullet")) Object.DestroyImmediate(t.gameObject);
        foreach (Transform t in gun.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 0;
        foreach (Renderer r in gun.GetComponentsInChildren<Renderer>(true))
        {
            r.renderingLayerMask = InteriorMask;
            r.shadowCastingMode = ShadowCastingMode.Off;
        }
        gun.transform.localPosition = Vector3.zero;
        gun.transform.localRotation = Quaternion.identity;
        gun.transform.localScale = Vector3.one;

        Bounds b = RendererBounds(gun);
        if (b.size.z > b.size.x) gun.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
        b = RendererBounds(gun);
        float length = Mathf.Max(b.size.x, 0.01f);
        if (length < 0.5f) gun.transform.localScale = Vector3.one * (0.5f / length);   // la pistola, un poco más grande
        b = RendererBounds(gun);
        gun.transform.position += holder.position - b.center;
        return gun;
    }

    internal static Bounds RendererBounds(GameObject go)
    {
        Renderer[] rs = go.GetComponentsInChildren<Renderer>(true);
        if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
        Bounds b = rs[0].bounds;
        for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
        return b;
    }

    internal static Light NewLight(Transform parent, string name, LightType type, Vector3 pos, Quaternion rot, Color color, float intensity, float range)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.SetPositionAndRotation(pos, rot);
        Light l = go.AddComponent<Light>();
        l.type = type;
        l.color = color;
        l.intensity = intensity;
        l.range = range;
        l.shadows = LightShadows.None;
        l.renderingLayerMask = (int)InteriorMask;
        return l;
    }

    internal static Transform FindDeep(Transform root, string name)
    {
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            if (t.name == name) return t;
        return null;
    }

    private static ArmoryController PlaceInstance(GameObject prefab)
    {
        ArmoryController existing = Object.FindFirstObjectByType<ArmoryController>(FindObjectsInactive.Include);
        if (existing != null) return existing;
        var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        inst.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        return inst.GetComponent<ArmoryController>();
    }

    // ── Interfaz ────────────────────────────────────────────────────────────────

    private static ArmoryUI BuildUI(Transform canvas, ArmoryController armory)
    {
        Transform startPanel = canvas.Find("StartPanel");
        Button template = startPanel.Find("RecordsButton").GetComponent<Button>();

        Transform rootT = canvas.Find("ArmoryUI") ?? NewRect(canvas, "ArmoryUI").transform;
        Stretch((RectTransform)rootT);
        rootT.SetAsLastSibling();

        Transform interior = rootT.Find("Interior") ?? NewRect(rootT, "Interior").transform;
        Stretch((RectTransform)interior);

        // Arriba a la izquierda: en el centro taparía el cartel luminoso de la carpa
        TMP_Text credits = Child<TMP_Text>(interior, "Credits") ?? NewText(interior, "Credits", "TUS CRÉDITOS: 0", Rye, 44, Gold, RyeCartel);
        credits.alignment = TextAlignmentOptions.Left;
        Place(credits.rectTransform, new Vector2(-560f, 480f), new Vector2(760f, 70f));

        TMP_Text hint = Child<TMP_Text>(interior, "Hint") ?? NewText(interior, "Hint", "", Sans, 28, Cream);
        hint.text = "Pulsa un arma para ver su ficha  ·  Esc o VOLVER para salir";
        Place(hint.rectTransform, new Vector2(0f, -490f), new Vector2(1100f, 50f));

        Button back = ChildButton(interior, "BackButton", template);
        Place((RectTransform)back.transform, new Vector2(-760f, -460f), new Vector2(300f, 100f));
        back.GetComponentInChildren<TMP_Text>().text = "VOLVER";

        // Ficha lateral
        var card = Child<Image>(interior, "Card") ?? NewImage(interior, "Card", Board);
        card.sprite = Board;
        card.type = Image.Type.Sliced;
        card.raycastTarget = true;
        // Franja ancha abajo, sobre el mostrador: a un lado taparía las armas de ese extremo de la pared
        Place(card.rectTransform, new Vector2(60f, -375f), new Vector2(1180f, 280f));
        TMP_Text name = Child<TMP_Text>(card.transform, "Name") ?? NewText(card.transform, "Name", "", Rye, 46, Gold, RyeCartel);
        name.enableAutoSizing = true; name.fontSizeMin = 26; name.fontSizeMax = 46;
        Place(name.rectTransform, new Vector2(-370f, 45f), new Vector2(380f, 70f));
        TMP_Text stats = Child<TMP_Text>(card.transform, "Stats") ?? NewText(card.transform, "Stats", "", Sans, 26, Cream);
        stats.alignment = TextAlignmentOptions.TopLeft;
        stats.lineSpacing = 4f;
        Place(stats.rectTransform, new Vector2(40f, 0f), new Vector2(360f, 220f));
        TMP_Text status = Child<TMP_Text>(card.transform, "Status") ?? NewText(card.transform, "Status", "", Rye, 30, Cream);
        status.enableAutoSizing = true; status.fontSizeMin = 18; status.fontSizeMax = 30;
        Place(status.rectTransform, new Vector2(-370f, -35f), new Vector2(380f, 50f));
        Button action = ChildButton(card.transform, "ActionButton", template);
        Place((RectTransform)action.transform, new Vector2(400f, 0f), new Vector2(300f, 100f));
        TMP_Text actionText = action.GetComponentInChildren<TMP_Text>();
        actionText.text = "COMPRAR";

        // Fundido: lo último, por encima de todo
        var fadeImg = Child<Image>(rootT, "Fade") ?? NewImage(rootT, "Fade", null);
        fadeImg.color = Color.black;
        fadeImg.raycastTarget = true;
        Stretch(fadeImg.rectTransform);
        fadeImg.transform.SetAsLastSibling();
        CanvasGroup fade = GetOrAdd<CanvasGroup>(fadeImg.gameObject);

        // Botones: TIENDA pasa a HABILIDADES y debajo sale ARMERÍA, en el inicio y en el resumen
        var openButtons = new List<Button>();
        openButtons.Add(SplitShopButton(startPanel, -355f, -470f, new Vector2(320f, 100f)));
        GameOverSummaryUI summary = canvas.GetComponentInChildren<GameOverSummaryUI>(true);
        if (summary != null) openButtons.Add(SplitShopButton(summary.transform, -405f, -510f, new Vector2(300f, 92f)));

        ArmoryUI ui = GetOrAdd<ArmoryUI>(rootT.gameObject);
        SetRefs(ui, "armory", armory, "fade", fade, "interiorRoot", interior.gameObject, "creditsText", credits, "hintText", hint,
            "backButton", back, "openButtons", openButtons.ToArray(), "card", card.gameObject, "nameText", name,
            "statsText", stats, "statusText", status, "actionButton", action, "actionText", actionText);
        return ui;
    }

    private static Button SplitShopButton(Transform panel, float shopY, float armoryY, Vector2 size)
    {
        Transform shop = panel.Find("ShopButton");
        if (shop == null) return null;
        Vector2 pos = ((RectTransform)shop).anchoredPosition;
        Place((RectTransform)shop, new Vector2(pos.x, shopY), size);
        TMP_Text shopText = shop.GetComponentInChildren<TMP_Text>();
        shopText.text = "HABILIDADES";
        shopText.enableAutoSizing = true; shopText.fontSizeMin = 18; shopText.fontSizeMax = 36;
        shopText.margin = new Vector4(30f, 0f, 30f, 0f);

        Button armory = ChildButton(panel, "ArmoryButton", shop.GetComponent<Button>());
        Place((RectTransform)armory.transform, new Vector2(pos.x, armoryY), size);
        TMP_Text t = armory.GetComponentInChildren<TMP_Text>();
        t.text = "ARMERÍA";
        return armory;
    }

    // La tienda de siempre, solo con habilidades: sin la columna de armas y con la de mejoras centrada
    private static void UpdateShopPrefab()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(ShopPrefab);
        foreach (string n in new[] { "WeaponsBoard", "WeaponsLabel", "WeaponsList" })
        {
            Transform t = root.transform.Find(n);
            if (t != null) t.gameObject.SetActive(false);
        }
        foreach (string n in new[] { "UpgradesBoard", "UpgradesLabel", "UpgradesScroll" })
        {
            var rt = root.transform.Find(n) as RectTransform;
            if (rt != null) rt.anchoredPosition = new Vector2(0f, rt.anchoredPosition.y);
        }
        Transform title = root.transform.Find("Sign/TitleText");
        if (title != null) title.GetComponent<TMP_Text>().text = "HABILIDADES";
        ShopPanelUI shop = root.GetComponent<ShopPanelUI>();
        if (shop != null)
        {
            var so = new SerializedObject(shop);
            so.FindProperty("includeWeapons").boolValue = false;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        PrefabUtility.SaveAsPrefabAsset(root, ShopPrefab);
        PrefabUtility.UnloadPrefabContents(root);
    }
}
