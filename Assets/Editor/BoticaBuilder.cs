using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using static FairgroundUIKit;

// Monta la botica en 3D (Fase 8): Tools > Fairground > Build Botica.
//  · Prefab Botica (Prefabs/Enviroments): el modelo de Botica.fbx (puerta al fondo del puesto azul e interior bajo
//    el suelo, en x = 40), luces, los siete frascos y el billete con su hueco, el charlatán, la cámara y el Volume.
//    Se rehace entero en cada pasada y se guarda en la misma ruta (conserva el GUID).
//  · En Main: la instancia y su interfaz (fundido, ficha, VOLVER). Los botones HABILIDADES no cambian: llaman a
//    MenuController.ShowShop, que abre la botica si está en la escena.
//  · Reutiliza de ArmoryBuilder las capas (física y Rendering Layer «Armory»), las luces, la cámara, el Volume y
//    la etiqueta flotante: las dos tiendas se aíslan de los niveles de la misma forma.
// Se puede repetir. Toca Main.unity: avisar antes al compañero.
public static class BoticaBuilder
{
    private const string ModelPath = "Assets/Art/Models/Environments/Botica.fbx";
    private const string PrefabPath = "Assets/Prefabs/Enviroments/Botica.prefab";
    private const string AudioFolder = "Assets/Audio/Botica/";
    private const string MatFolder = "Assets/Art/Materials/Fairground/";
    private const uint InteriorMask = ArmoryBuilder.InteriorMask;

    // Qué mejora va en cada hueco del modelo (Botica_Slot_0…7): frascos altos, matraces y el billete
    private static readonly UpgradeEffect[] SlotEffects =
    {
        UpgradeEffect.Ammo, UpgradeEffect.FireRate, UpgradeEffect.ScoreMultiplier, UpgradeEffect.Damage,
        UpgradeEffect.Penetration, UpgradeEffect.ExtraRays, UpgradeEffect.Explosive,
        UpgradeEffect.UnlockBossLevel,
    };

    // Color del tónico de cada hueco
    private static readonly Color[] LiquidColors =
    {
        new Color(1f, 0.62f, 0.12f),   // munición: ámbar
        new Color(0.3f, 0.9f, 0.3f),   // cadencia: verde
        new Color(0.68f, 0.3f, 1f),    // multiplicador: morado
        new Color(0.95f, 0.15f, 0.12f),// daño: rojo
        new Color(0.2f, 0.45f, 1f),    // penetración: azul
        new Color(0.2f, 0.95f, 0.95f), // disparo doble: cian
        new Color(1f, 0.42f, 0.05f),   // bala explosiva: naranja
    };

    // Interior (coordenadas de mundo de Unity)
    private static readonly Vector3 Center = new Vector3(40f, -60f, 0f);

    [MenuItem("Tools/Fairground/Build Botica")]
    public static void Build()
    {
        if (!EditorUtility.DisplayDialog("Build Botica",
                "Monta la botica en 3D y su interfaz en la escena abierta.\n\nEn Main, avisa antes a tu compañero. ¿Continuar?",
                "Montar", "Cancelar"))
            return;
        Run();
    }

    public static void Run()
    {
        int layer = ArmoryBuilder.EnsureLayers();
        AssetDatabase.Refresh();
        ArmoryBuilder.ImportModel(ModelPath);
        ArmoryBuilder.ImportAudio(AudioFolder);
        LoadStyle();

        GameObject canvasGo = GameObject.Find("UI");
        if (canvasGo == null) { Debug.LogError("BoticaBuilder: no hay un Canvas llamado UI en la escena."); return; }
        ProfileManager pm = Object.FindFirstObjectByType<ProfileManager>();
        if (pm == null || pm.Upgrades == null) { Debug.LogError("BoticaBuilder: no hay ProfileManager con mejoras en la escena."); return; }

        GameObject prefab = BuildPrefab(layer, pm.Upgrades);
        BoticaController botica = PlaceInstance(prefab);
        BoticaUI ui = BuildUI(canvasGo.transform, botica);
        SetRefs(botica, "ui", ui);

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
        Debug.Log("BoticaBuilder: botica montada.");
    }

    private static AudioClip Clip(string folder, string name) => AssetDatabase.LoadAssetAtPath<AudioClip>(folder + name + ".wav");

    // ── Materiales ──────────────────────────────────────────────────────────────

    private static Material LoadOrCreate(string name, Shader shader)
    {
        string path = MatFolder + name + ".mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null) { mat = new Material(shader); AssetDatabase.CreateAsset(mat, path); }
        mat.shader = shader;
        return mat;
    }

    // Transparente con mezcla alfa normal. Lo que haría el inspector de URP al poner Surface Type = Transparent
    private static void MakeTransparent(Material mat, int queue)
    {
        mat.SetFloat("_Surface", 1f);
        mat.SetFloat("_Blend", 0f);
        mat.SetOverrideTag("RenderType", "Transparent");
        mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        mat.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
        mat.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
        mat.SetFloat("_ZWrite", 0f);
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.SetShaderPassEnabled("DepthOnly", false);
        mat.SetShaderPassEnabled("ShadowCaster", false);
        // URP recalcula la cola al validar el material (3000 + _QueueOffset): poner solo renderQueue no dura
        mat.SetFloat("_QueueOffset", queue - 3000);
        mat.renderQueue = queue;
    }

    // Cristal: casi invisible, con brillo. Cola 3001: siempre después del líquido (opaco) que tiene dentro
    private static Material GlassMaterial()
    {
        Material mat = LoadOrCreate("M_B_Glass", Shader.Find("Universal Render Pipeline/Lit"));
        // Claro y con poca opacidad: con un tono oscuro, la parte vacía del frasco parecía llena de líquido negro
        mat.SetColor("_BaseColor", new Color(0.9f, 1f, 0.97f, 0.16f));
        mat.SetFloat("_Smoothness", 0.85f);
        mat.SetFloat("_Metallic", 0f);
        MakeTransparent(mat, 3001);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    // Tónico: opaco y con algo de emisión, para que brille un poco en la penumbra de la trastienda
    private static Material LiquidMaterial(int index, Color c)
    {
        Material mat = LoadOrCreate($"M_B_Liquid_{index}", Shader.Find("Universal Render Pipeline/Lit"));
        mat.SetColor("_BaseColor", c);
        mat.SetFloat("_Smoothness", 0.85f);
        mat.EnableKeyword("_EMISSION");
        mat.SetColor("_EmissionColor", c * 0.45f);
        mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        EditorUtility.SetDirty(mat);
        return mat;
    }

    // Burbujas y nubecilla: partículas sin luz, redondas, por encima del cristal (cola 3002)
    private static Material BubbleMaterial()
    {
        Material mat = LoadOrCreate("M_B_Bubble", Shader.Find("Universal Render Pipeline/Particles/Unlit"));
        mat.SetTexture("_BaseMap", AssetDatabase.GetBuiltinExtraResource<Texture2D>("Default-Particle.psd"));
        mat.SetColor("_BaseColor", Color.white);
        MakeTransparent(mat, 3002);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    // ── Prefab ──────────────────────────────────────────────────────────────────

    private static GameObject BuildPrefab(int layer, IReadOnlyList<UpgradeData> upgrades)
    {
        Material glass = GlassMaterial();
        Material bubble = BubbleMaterial();
        Material bulbOn = ArmoryBuilder.BulbMaterial("M_A_BulbOn", new Color(1f, 0.92f, 0.78f), new Color(1f, 0.78f, 0.5f) * 2.2f);
        Material ticketOff = ArmoryBuilder.BulbMaterial("M_B_TicketOff", new Color(0.5f, 0.42f, 0.2f), Color.black);
        Material ticketOn = ArmoryBuilder.BulbMaterial("M_B_TicketOn", new Color(1f, 0.86f, 0.4f), new Color(1f, 0.68f, 0.15f) * 2.4f);

        var root = new GameObject("Botica");
        root.layer = layer;
        var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath), root.transform);
        PrefabUtility.UnpackPrefabInstance(model, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        // Este FBX tiene empty raíz (Botica_Root), como Env_Promenade: entra bien sin girarlo (el de la armería no)
        model.transform.localPosition = Vector3.zero;
        model.transform.localRotation = Quaternion.identity;

        // El interior solo lo iluminan sus propias luces; la puerta del puesto, las de la feria
        foreach (Renderer r in model.GetComponentsInChildren<Renderer>(true))
        {
            bool outside = r.name.StartsWith("Botica_Entrance");
            r.renderingLayerMask = outside ? 1u : InteriorMask;
            r.shadowCastingMode = outside ? ShadowCastingMode.On : ShadowCastingMode.Off;
            // El cristal (frascos y vitrina) con el material transparente; en Blender era un gris opaco
            Material[] mats = r.sharedMaterials;
            bool changed = false;
            for (int i = 0; i < mats.Length; i++)
                if (mats[i] != null && mats[i].name.StartsWith("M_Glass")) { mats[i] = glass; changed = true; }
            if (changed) r.sharedMaterials = mats;
        }

        Transform signBulbs = ArmoryBuilder.FindDeep(model.transform, "Botica_SignBulbs");
        if (signBulbs != null) signBulbs.GetComponent<Renderer>().sharedMaterial = bulbOn;

        // Hueco de la puerta, negro de verdad (con M_C_Dark y las luces de la noche se veía morado)
        Transform door = ArmoryBuilder.FindDeep(model.transform, "Botica_Entrance");
        if (door != null)
        {
            var dr = door.GetComponent<Renderer>();
            Material[] dm = dr.sharedMaterials;
            dm[0] = ArmoryBuilder.DoorDarkMaterial();
            dr.sharedMaterials = dm;
            // Farol dentro del puesto, junto a la puerta: de noche se ve desde la caseta (luz de la feria)
            Bounds db = dr.bounds;
            Light lantern = ArmoryBuilder.NewLight(root.transform, "Light_Door", LightType.Point, new Vector3(db.center.x, db.max.y + 0.55f, db.center.z - 0.9f),
                Quaternion.identity, new Color(1f, 0.74f, 0.45f), 2.5f, 4.5f);
            lantern.renderingLayerMask = 1;
        }

        // Luces del interior: como en la armería, más un toque verdoso de botica en la estantería
        Vector3 c = Center;
        ArmoryBuilder.NewLight(root.transform, "Light_Main", LightType.Point, c + new Vector3(0f, 3.6f, 0.6f), Quaternion.identity, new Color(1f, 0.84f, 0.66f), 7f, 10f);
        Vector3 wallFrom = c + new Vector3(0f, 4.0f, -1.6f);
        ArmoryBuilder.NewLight(root.transform, "Light_Wall", LightType.Spot, wallFrom,
            Quaternion.LookRotation(c + new Vector3(0f, 2.0f, 3.2f) - wallFrom), new Color(1f, 0.9f, 0.76f), 18f, 11f).spotAngle = 80f;
        ArmoryBuilder.NewLight(root.transform, "Light_Sign", LightType.Point, c + new Vector3(0f, 3.5f, 2.5f), Quaternion.identity, new Color(1f, 0.8f, 0.5f), 1.5f, 2.6f);
        ArmoryBuilder.NewLight(root.transform, "Light_Tonic", LightType.Point, c + new Vector3(-0.4f, 2.2f, 2.2f), Quaternion.identity, new Color(0.45f, 1f, 0.75f), 0.9f, 3.2f);

        // Huecos
        var slots = new List<BoticaSlot>();
        for (int i = 0; i < SlotEffects.Length; i++)
        {
            UpgradeData upgrade = FindUpgrade(upgrades, SlotEffects[i]);
            Transform anchor = ArmoryBuilder.FindDeep(model.transform, $"Botica_Slot_{i}");
            if (anchor == null || upgrade == null) { Debug.LogWarning($"BoticaBuilder: falta el hueco {i} o su mejora ({SlotEffects[i]})."); continue; }
            BoticaSlot slot = i == 7
                ? BuildTicketSlot(root.transform, model.transform, anchor, upgrade, layer, ticketOff, ticketOn)
                : BuildBottleSlot(root.transform, model.transform, anchor, i, upgrade, layer, LiquidMaterial(i, LiquidColors[i]), bubble);
            slots.Add(slot);
        }

        TextMeshPro label = ArmoryBuilder.HoverLabel(root.transform);

        // Atrezo: cajón de la caja y charlatán (se mece en su poste, pero no celebra la venta)
        Transform drawer = ArmoryBuilder.FindDeep(model.transform, "Botica_RegisterDrawer");
        Transform charlatan = ArmoryBuilder.FindDeep(model.transform, "Botica_Charlatan");
        Transform pivot = null;
        if (charlatan != null)
        {
            Bounds cb = charlatan.GetComponent<Renderer>().bounds;
            pivot = new GameObject("CharlatanPivot").transform;
            pivot.SetParent(root.transform, false);
            pivot.position = new Vector3(cb.center.x, cb.min.y, cb.max.z - 0.05f);
            charlatan.SetParent(pivot, true);
        }
        var audio = root.AddComponent<AudioSource>();
        ConfigureSource(audio);
        ArmoryProps props = root.AddComponent<ArmoryProps>();
        SetRefs(props, "drawer", drawer, "clownPivot", pivot, "audioSource", audio,
            "registerClip", Clip("Assets/Audio/Armory/", "Register_ChaChing"));
        SetValue(props, "reactOnSale", false);

        Camera cam = ArmoryBuilder.ShopCamera(root.transform, "BoticaCamera");
        Volume vol = ArmoryBuilder.InteriorVolume(root.transform, layer, "BoticaVolume");

        BoticaController controller = root.AddComponent<BoticaController>();
        SetRefs(controller, "shopCamera", cam, "interiorVolume", vol, "slots", slots.ToArray(), "props", props, "hoverLabel", label,
            "audioSource", audio, "hoverClip", Clip("Assets/Audio/Armory/", "Armory_Hover"),
            "glugClip", Clip(AudioFolder, "Tonic_Glug"), "corkClip", Clip(AudioFolder, "Cork_Pop"), "ticketClip", Clip(AudioFolder, "Ticket_Shine"));

        // Viaje: se aparta de la caseta hacia la derecha, pasa por delante del puesto azul, se mete bajo el toldo,
        // cruza por encima del mostrador y entra por la cortina del fondo
        SetValue(controller, "flightPath", new[]
        {
            new Vector3(3.5f, 2.4f, -8.2f),
            new Vector3(8.5f, 2.3f, -8.6f),
            new Vector3(11f, 2.0f, -6.6f),
            new Vector3(11f, 1.9f, -4.7f),
            new Vector3(11f, 1.6f, -1.6f),
            new Vector3(11f, 1.5f, -0.6f),
        });
        SetValue(controller, "flightTime", 3.0f);
        SetValue(controller, "doorLook", Vector3.forward);
        SetValue(controller, "insidePosition", c + new Vector3(0f, 2.3f, -0.5f));
        SetValue(controller, "insideEuler", new Vector3(4f, 0f, 0f));
        SetValue(controller, "insideAmbient", new Color(0.4f, 0.37f, 0.34f));

        GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
        return saved;
    }

    private static UpgradeData FindUpgrade(IReadOnlyList<UpgradeData> upgrades, UpgradeEffect effect)
    {
        foreach (UpgradeData u in upgrades)
            if (u != null && u.effect == effect) return u;
        return null;
    }

    private static GameObject NewSlotObject(Transform root, string name, Vector3 pos, int layer, out Transform holder)
    {
        var go = new GameObject(name);
        go.layer = layer;
        go.transform.SetParent(root, false);
        go.transform.position = pos;
        holder = new GameObject("Holder").transform;
        holder.SetParent(go.transform, false);
        return go;
    }

    // Frasco: cristal, corcho, marcas y lacre cuelgan del pivote (se levantan juntos al pasar el ratón); el líquido,
    // de un pivote propio en su base, que es lo que se escala al llenar
    private static BoticaSlot BuildBottleSlot(Transform root, Transform model, Transform anchor, int i, UpgradeData upgrade, int layer, Material liquidMat, Material bubbleMat)
    {
        GameObject go = NewSlotObject(root, $"Slot_{i}", anchor.position, layer, out Transform holder);
        Transform glassT = ArmoryBuilder.FindDeep(model, $"Botica_Glass_{i}");
        Transform liquidT = ArmoryBuilder.FindDeep(model, $"Botica_Liquid_{i}");
        Transform seal = ArmoryBuilder.FindDeep(model, $"Botica_Seal_{i}");
        foreach (string part in new[] { "Glass", "Cork", "Seal", "Marks" })
        {
            Transform t = ArmoryBuilder.FindDeep(model, $"Botica_{part}_{i}");
            if (t != null) t.SetParent(holder, true);
        }

        Transform liquidPivot = null;
        Bounds lb = new Bounds(anchor.position + Vector3.up * 0.15f, new Vector3(0.2f, 0.3f, 0.2f));
        if (liquidT != null)
        {
            var lr = liquidT.GetComponent<Renderer>();
            lr.sharedMaterial = liquidMat;
            lb = lr.bounds;
            liquidPivot = new GameObject("LiquidPivot").transform;
            liquidPivot.SetParent(holder, false);
            liquidPivot.position = new Vector3(lb.center.x, lb.min.y, lb.center.z);
            liquidT.SetParent(liquidPivot, true);
        }

        // La zona que se puede señalar: el frasco entero, un poco holgada
        Bounds gb = glassT != null ? glassT.GetComponent<Renderer>().bounds : new Bounds(anchor.position + Vector3.up * 0.3f, new Vector3(0.3f, 0.6f, 0.3f));
        var box = go.AddComponent<BoxCollider>();
        box.center = go.transform.InverseTransformPoint(gb.center);
        box.size = gb.size + new Vector3(0.12f, 0.12f, 0.12f);

        // Las burbujas cuelgan del pivote del frasco, no del líquido: el líquido se escala en Y y las aplastaría.
        // Y suben pegadas a la cara del líquido que da a la cámara (entre líquido y cristal): el líquido es opaco
        // y escribe profundidad, así que las de dentro quedaban tapadas por su propia cara frontal
        bool big = i < 4;
        Vector3 bubblesAt = new Vector3(lb.center.x, lb.min.y + (big ? 0.01f : 0.09f), lb.min.z - 0.004f);
        ParticleSystem bubbles = Bubbles(holder, bubblesAt, lb.extents.x * 0.7f, lb.size.y, bubbleMat);
        ParticleSystem puff = Puff(holder, new Vector3(gb.center.x, gb.max.y + 0.02f, gb.center.z), bubbleMat);

        // Etiqueta de papel de la balda, delante y debajo del frasco
        var plate = new GameObject("Plate", typeof(RectTransform)).AddComponent<TextMeshPro>();
        plate.transform.SetParent(go.transform, false);
        plate.transform.position = new Vector3(anchor.position.x, big ? -58.415f : -57.665f, 2.862f);
        plate.rectTransform.sizeDelta = new Vector2(big ? 0.52f : 0.46f, 0.09f);
        plate.font = Rye;
        plate.fontSize = 0.6f;
        // Una sola línea: en dos, la segunda quedaba por debajo del canto y la tapaba la balda (24/11). Los nombres
        // largos se acortan en la etiqueta («Multiplicador de puntos» → MULTIPLICADOR); el cartel al pasar el ratón
        // y la ficha siguen diciendo el nombre entero
        plate.textWrappingMode = TextWrappingModes.NoWrap;
        plate.enableAutoSizing = true; plate.fontSizeMin = 0.25f; plate.fontSizeMax = 0.6f;
        plate.alignment = TextAlignmentOptions.Center;
        plate.text = BoticaSlot.PlateName(upgrade.displayName);
        plate.GetComponent<MeshRenderer>().renderingLayerMask = InteriorMask;

        BoticaSlot slot = go.AddComponent<BoticaSlot>();
        SetRefs(slot, "upgrade", upgrade, "holder", holder, "liquid", liquidPivot, "seal", seal != null ? seal.gameObject : null,
            "plate", plate, "bubbles", bubbles, "puff", puff);
        SetValue(slot, "liftHeight", 0.05f);
        SetValue(slot, "liftTurn", 14f);
        SetValue(slot, "labelHeight", gb.size.y + 0.32f);
        return slot;
    }

    // Billete: dentro de la vitrina, en su atril; al comprarlo se enciende y sale flotando por encima
    private static BoticaSlot BuildTicketSlot(Transform root, Transform model, Transform anchor, UpgradeData upgrade, int layer, Material off, Material on)
    {
        GameObject go = NewSlotObject(root, "Slot_Ticket", anchor.position, layer, out Transform holder);
        Transform ticket = ArmoryBuilder.FindDeep(model, "Botica_Ticket");
        if (ticket != null) ticket.SetParent(holder, true);

        Transform caseT = ArmoryBuilder.FindDeep(model, "Botica_Case");
        Bounds cb = caseT != null ? caseT.GetComponent<Renderer>().bounds : new Bounds(anchor.position, new Vector3(0.45f, 0.35f, 0.35f));
        var box = go.AddComponent<BoxCollider>();
        box.center = go.transform.InverseTransformPoint(cb.center + Vector3.up * 0.12f);
        box.size = cb.size + new Vector3(0.08f, 0.3f, 0.08f);   // también el hueco de encima, donde flota

        Light glow = ArmoryBuilder.NewLight(holder, "TicketLight", LightType.Point, anchor.position + new Vector3(0f, 0.05f, -0.2f), Quaternion.identity,
            new Color(1f, 0.78f, 0.35f), 1.2f, 1.6f);
        glow.enabled = false;

        BoticaSlot slot = go.AddComponent<BoticaSlot>();
        SetRefs(slot, "upgrade", upgrade, "holder", holder, "ticket", ticket != null ? ticket.GetComponent<Renderer>() : null,
            "ticketOff", off, "ticketOn", on, "ticketLight", glow);
        SetValue(slot, "kind", (int)BoticaSlot.Kind.Ticket);
        SetValue(slot, "liftHeight", 0.04f);
        SetValue(slot, "liftTurn", 18f);
        SetValue(slot, "labelHeight", 0.62f);
        return slot;
    }

    // Burbujas que suben por el líquido mientras se llena (en local: se levantan con el frasco)
    private static ParticleSystem Bubbles(Transform parent, Vector3 worldPos, float halfWidth, float height, Material mat)
    {
        var go = new GameObject("Bubbles");
        go.transform.SetParent(parent, false);
        go.transform.position = worldPos + Vector3.up * 0.01f;
        go.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);   // la forma emite hacia +Z: así, hacia arriba
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.playOnAwake = false;
        main.loop = true;
        main.duration = 1f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.7f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(height * 0.6f, height * 1.1f);
        // Más grandes de lo real: a casi 4 m de la cámara, burbujas de 1-2 cm eran dos o tres píxeles
        main.startSize = new ParticleSystem.MinMaxCurve(0.022f, 0.045f);
        main.startColor = new Color(1f, 1f, 1f, 0.9f);
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        main.maxParticles = 80;
        var emission = ps.emission;
        emission.rateOverTime = 60f;
        // Una franja horizontal (caja plana) en vez de un cono: así todas salen en la cara frontal del líquido
        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(halfWidth * 2f, 0.004f, 0.004f);
        var col = ps.colorOverLifetime;
        col.enabled = true;
        col.color = FadeOut();
        ConfigureRenderer(ps, mat);
        return ps;
    }

    // Nubecilla que sale por la boca del frasco al terminar de llenarse
    private static ParticleSystem Puff(Transform parent, Vector3 worldPos, Material mat)
    {
        var go = new GameObject("Puff");
        go.transform.SetParent(parent, false);
        go.transform.position = worldPos;
        go.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.playOnAwake = false;
        main.loop = false;
        main.duration = 0.5f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 0.95f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.08f, 0.22f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.1f);
        main.startColor = new Color(1f, 1f, 1f, 0.45f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        var emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 10) });
        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 28f;
        shape.radius = 0.03f;
        var size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 1.6f));
        var col = ps.colorOverLifetime;
        col.enabled = true;
        col.color = FadeOut();
        ConfigureRenderer(ps, mat);
        return ps;
    }

    private static ParticleSystem.MinMaxGradient FadeOut()
    {
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.6f), new GradientAlphaKey(0f, 1f) });
        return new ParticleSystem.MinMaxGradient(g);
    }

    private static void ConfigureRenderer(ParticleSystem ps, Material mat)
    {
        var r = ps.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = mat;
        r.renderingLayerMask = InteriorMask;
        r.shadowCastingMode = ShadowCastingMode.Off;
        r.receiveShadows = false;
    }

    private static BoticaController PlaceInstance(GameObject prefab)
    {
        BoticaController existing = Object.FindFirstObjectByType<BoticaController>(FindObjectsInactive.Include);
        if (existing != null) return existing;
        var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        inst.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        return inst.GetComponent<BoticaController>();
    }

    // ── Valores serializados que no son referencias ─────────────────────────────

    private static void SetValue(Object target, string field, object value)
    {
        var so = new SerializedObject(target);
        SerializedProperty p = so.FindProperty(field);
        if (p == null) { Debug.LogWarning($"BoticaBuilder: {target.GetType().Name} no tiene el campo {field}."); return; }
        switch (value)
        {
            case bool b: p.boolValue = b; break;
            case int n when p.propertyType == SerializedPropertyType.Enum: p.enumValueIndex = n; break;
            case int n: p.intValue = n; break;
            case float f: p.floatValue = f; break;
            case Vector3 v: p.vector3Value = v; break;
            case Color col: p.colorValue = col; break;
            case Vector3[] arr:
                p.arraySize = arr.Length;
                for (int k = 0; k < arr.Length; k++) p.GetArrayElementAtIndex(k).vector3Value = arr[k];
                break;
        }
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // ── Interfaz ────────────────────────────────────────────────────────────────

    private static BoticaUI BuildUI(Transform canvas, BoticaController botica)
    {
        Button template = canvas.Find("StartPanel/RecordsButton").GetComponent<Button>();

        Transform rootT = canvas.Find("BoticaUI") ?? NewRect(canvas, "BoticaUI").transform;
        Stretch((RectTransform)rootT);
        rootT.SetAsLastSibling();

        Transform interior = rootT.Find("Interior") ?? NewRect(rootT, "Interior").transform;
        Stretch((RectTransform)interior);

        TMP_Text credits = Child<TMP_Text>(interior, "Credits") ?? NewText(interior, "Credits", "TUS CRÉDITOS: 0", Rye, 44, Gold, RyeCartel);
        credits.alignment = TextAlignmentOptions.Left;
        Place(credits.rectTransform, new Vector2(-560f, 480f), new Vector2(760f, 70f));

        TMP_Text hint = Child<TMP_Text>(interior, "Hint") ?? NewText(interior, "Hint", "", Sans, 28, Cream);
        hint.text = "Pulsa un tónico para ver su ficha  ·  Esc o VOLVER para salir";
        Place(hint.rectTransform, new Vector2(0f, -490f), new Vector2(1100f, 50f));

        Button back = ChildButton(interior, "BackButton", template);
        Place((RectTransform)back.transform, new Vector2(-760f, -460f), new Vector2(300f, 100f));
        back.GetComponentInChildren<TMP_Text>().text = "VOLVER";

        // Ficha: la misma franja de abajo que en la armería, sobre el mostrador
        var card = Child<Image>(interior, "Card") ?? NewImage(interior, "Card", Board);
        card.sprite = Board;
        card.type = Image.Type.Sliced;
        card.raycastTarget = true;
        Place(card.rectTransform, new Vector2(60f, -375f), new Vector2(1180f, 280f));
        TMP_Text name = Child<TMP_Text>(card.transform, "Name") ?? NewText(card.transform, "Name", "", Rye, 44, Gold, RyeCartel);
        // Los nombres largos («Multiplicador de puntos») ocupan dos líneas: el nombre tiene sitio para ellas
        name.enableAutoSizing = true; name.fontSizeMin = 24; name.fontSizeMax = 40;
        Place(name.rectTransform, new Vector2(-370f, 64f), new Vector2(380f, 84f));
        TMP_Text level = Child<TMP_Text>(card.transform, "Level") ?? NewText(card.transform, "Level", "", Rye, 26, Cream);
        Place(level.rectTransform, new Vector2(-370f, -6f), new Vector2(380f, 40f));
        TMP_Text status = Child<TMP_Text>(card.transform, "Status") ?? NewText(card.transform, "Status", "", Rye, 30, Cream);
        status.enableAutoSizing = true; status.fontSizeMin = 18; status.fontSizeMax = 30;
        Place(status.rectTransform, new Vector2(-370f, -62f), new Vector2(380f, 50f));
        TMP_Text description = Child<TMP_Text>(card.transform, "Description") ?? NewText(card.transform, "Description", "", Sans, 27, Cream);
        description.alignment = TextAlignmentOptions.MidlineLeft;
        description.enableAutoSizing = true; description.fontSizeMin = 18; description.fontSizeMax = 27;
        Place(description.rectTransform, new Vector2(40f, 0f), new Vector2(380f, 220f));
        Button action = ChildButton(card.transform, "ActionButton", template);
        Place((RectTransform)action.transform, new Vector2(400f, 0f), new Vector2(300f, 100f));
        TMP_Text actionText = action.GetComponentInChildren<TMP_Text>();
        actionText.text = "COMPRAR";

        var fadeImg = Child<Image>(rootT, "Fade") ?? NewImage(rootT, "Fade", null);
        fadeImg.color = Color.black;
        fadeImg.raycastTarget = true;
        Stretch(fadeImg.rectTransform);
        fadeImg.transform.SetAsLastSibling();
        CanvasGroup fade = GetOrAdd<CanvasGroup>(fadeImg.gameObject);

        BoticaUI ui = GetOrAdd<BoticaUI>(rootT.gameObject);
        SetRefs(ui, "shop", botica, "fade", fade, "interiorRoot", interior.gameObject, "creditsText", credits, "hintText", hint,
            "backButton", back, "openButtons", new Object[0], "card", card.gameObject, "nameText", name, "descriptionText", description,
            "levelText", level, "statusText", status, "actionButton", action, "actionText", actionText);
        return ui;
    }
}
