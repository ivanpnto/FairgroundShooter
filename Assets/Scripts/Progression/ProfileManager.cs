using System.Collections.Generic;
using System.IO;
using UnityEngine;

// Único dueño de los perfiles: los carga de profiles.json (Application.persistentDataPath), es el único
// que los modifica y guarda tras cada cambio. Va en el objeto GameManager de la escena y se busca con
// FindFirstObjectByType, como el resto de sistemas.
//
// Persistencia:
// - Guardado: se escribe profiles.json.tmp entero y luego se cambia por profiles.json, dejando el anterior
//   como profiles.json.bak. Un cierre a mitad de escritura solo puede romper el .tmp.
// - Carga: si profiles.json no se puede leer o no es JSON válido, se copia aparte como
//   profiles.corrupt-<fecha>.json (para no perderlo) y se intenta el .bak; si tampoco vale, se empieza sin
//   perfiles. Nunca se sobrescribe un archivo roto sin haberlo copiado antes.
public class ProfileManager : MonoBehaviour
{
    [Tooltip("Arma que todo perfil tiene desbloqueada desde el principio")]
    [SerializeField] private WeaponData starterWeapon;

    // El catálogo vive aquí y no en la tienda: lo leen WeaponController (estadísticas), ScoreManager
    // (multiplicador de puntos) y la tienda, y así todos usan la misma lista
    [Tooltip("Mejoras que existen en el juego (las que salen en la tienda)")]
    [SerializeField] private UpgradeData[] upgrades;

    // Archivo en Application.persistentDataPath. En Main es profiles.json; en escenas de prueba
    // (BossSandbox) se cambia para no tocar los perfiles reales al ganar o perder una partida
    [Tooltip("Archivo de perfiles en persistentDataPath. Solo se cambia en escenas de prueba")]
    [SerializeField] private string fileName = "profiles.json";
    private string FileName => string.IsNullOrWhiteSpace(fileName) ? "profiles.json" : fileName;

    // Balance (bloque 3): perfil de demostración para la evaluación. Se rehace al arrancar el juego, así que siempre
    // empieza igual aunque alguien lo haya usado antes (sus créditos y compras no se conservan entre sesiones)
    [Tooltip("Crea (o rehace) al arrancar un perfil de demostración con el nivel 4 comprado y media tienda")]
    [SerializeField] private bool demoProfile = true;
    [SerializeField] private ProfilePreset demoPreset = ProfilePreset.DefaultDemo();

    // Créditos = max(0, puntuación final) / ScorePerCredit (división entera)
    public const int ScorePerCredit = 10;

    private ProfileDatabase database = new ProfileDatabase();

    // El perfil activo (null hasta que se elige o se crea uno)
    public PlayerProfile Current { get; private set; }
    public int Credits => Current != null ? Current.credits : 0;
    public IReadOnlyList<PlayerProfile> AllProfiles => database.profiles;
    public IReadOnlyList<UpgradeData> Upgrades => upgrades;
    public WeaponData StarterWeapon => starterWeapon;

    // Cualquier cambio del perfil activo (créditos, mejoras, armas) o cambio de perfil.
    // WeaponController recalcula las estadísticas y la tienda se repinta al recibirlo
    public event System.Action ProfileChanged;

    private string FilePath => Path.Combine(Application.persistentDataPath, FileName);
    private string BackupPath => FilePath + ".bak";
    private string TempPath => FilePath + ".tmp";

    private void Awake()
    {
        Load();
        if (demoProfile) RebuildDemoProfile();

        // El último perfil usado queda elegido al arrancar
        if (!string.IsNullOrEmpty(database.selectedProfile))
            SelectProfile(database.selectedProfile);
    }

    // Sustituye el perfil Demo por uno recién hecho desde demoPreset (o lo crea si no existe). No lo elige:
    // el jugador lo escoge en la pantalla de perfiles como cualquier otro
    private void RebuildDemoProfile()
    {
        if (demoPreset == null || string.IsNullOrWhiteSpace(demoPreset.name)) return;
        int index = database.profiles.FindIndex(p => string.Equals(p.name, demoPreset.name, System.StringComparison.OrdinalIgnoreCase));
        PlayerProfile fresh = demoPreset.Build();
        EnsureStarterWeapon(fresh);
        if (index >= 0) database.profiles[index] = fresh;
        else database.profiles.Add(fresh);
        Save();
    }

    // Para BalanceBot (y herramientas): elige o crea el perfil del preset y lo deja EXACTAMENTE como dice el preset
    public void ApplyPreset(ProfilePreset preset)
    {
        if (preset == null) return;
        int index = database.profiles.FindIndex(p => string.Equals(p.name, preset.name, System.StringComparison.OrdinalIgnoreCase));
        PlayerProfile fresh = preset.Build();
        EnsureStarterWeapon(fresh);
        if (index >= 0) database.profiles[index] = fresh;
        else database.profiles.Add(fresh);
        Current = fresh;
        database.selectedProfile = fresh.name;
        Commit();
    }

    // ── Perfiles ────────────────────────────────────────────────────────────────

    // false (y no crea nada) si el nombre está vacío o ya existe (sin distinguir mayúsculas)
    public bool CreateProfile(string profileName)
    {
        string clean = profileName != null ? profileName.Trim() : "";
        if (clean.Length == 0 || Find(clean) != null)
            return false;

        database.profiles.Add(new PlayerProfile(clean));
        return SelectProfile(clean);   // selecciona, guarda y avisa
    }

    public bool SelectProfile(string profileName)
    {
        PlayerProfile profile = Find(profileName);
        if (profile == null)
            return false;

        Current = profile;
        database.selectedProfile = profile.name;
        EnsureStarterWeapon(profile);

        Commit();
        return true;
    }

    private PlayerProfile Find(string profileName)
    {
        if (string.IsNullOrWhiteSpace(profileName))
            return null;

        string clean = profileName.Trim();
        foreach (PlayerProfile profile in database.profiles)
            if (string.Equals(profile.name, clean, System.StringComparison.OrdinalIgnoreCase))
                return profile;
        return null;
    }

    // También con perfiles antiguos o editados a mano: sin el arma inicial no habría con qué jugar
    private void EnsureStarterWeapon(PlayerProfile profile)
    {
        if (starterWeapon != null && !string.IsNullOrEmpty(starterWeapon.id) && !profile.IsWeaponUnlocked(starterWeapon.id))
            profile.unlockedWeapons.Add(starterWeapon.id);
    }

    // ── Créditos ────────────────────────────────────────────────────────────────

    // Resta los créditos solo si hay suficientes; false si no
    public bool TrySpend(int amount)
    {
        if (Current == null || amount < 0 || Current.credits < amount)
            return false;

        Current.credits -= amount;
        Commit();
        return true;
    }

    public void AddCredits(int amount)
    {
        if (Current == null || amount <= 0)
            return;

        Current.credits += amount;
        Commit();
    }

    // ── Mejoras ─────────────────────────────────────────────────────────────────

    public int GetUpgradeLevel(string id) => Current != null ? Current.GetUpgradeLevel(id) : 0;

    // Cobra el siguiente nivel y lo sube; false si está al máximo o no hay créditos
    public bool BuyUpgradeLevel(UpgradeData upgrade)
    {
        if (Current == null || upgrade == null || string.IsNullOrEmpty(upgrade.id))
            return false;

        int level = Current.GetUpgradeLevel(upgrade.id);
        int cost = upgrade.NextLevelCost(level);
        if (cost < 0 || Current.credits < cost)
            return false;

        Current.credits -= cost;
        SetUpgradeLevel(Current, upgrade.id, level + 1);
        Commit();
        return true;
    }

    private static void SetUpgradeLevel(PlayerProfile profile, string id, int level)
    {
        foreach (UpgradeLevel entry in profile.upgradeLevels)
        {
            if (entry.id != id)
                continue;
            entry.level = level;
            return;
        }
        profile.upgradeLevels.Add(new UpgradeLevel(id, level));
    }

    // ── Armas ───────────────────────────────────────────────────────────────────

    // Sin perfil elegido solo está disponible el arma inicial: así los menús funcionan antes de elegir perfil
    public bool IsWeaponUnlocked(string id)
    {
        if (string.IsNullOrEmpty(id))
            return false;
        if (Current == null)
            return starterWeapon != null && starterWeapon.id == id;
        return Current.IsWeaponUnlocked(id);
    }

    // Desbloquea sin cobrar (arma inicial de un perfil nuevo, menú de depuración)
    public void UnlockWeapon(string id)
    {
        if (Current == null || string.IsNullOrEmpty(id) || Current.IsWeaponUnlocked(id))
            return;

        Current.unlockedWeapons.Add(id);
        Commit();
    }

    // Cobra weapon.price y desbloquea; false si ya está desbloqueada o no hay créditos
    public bool BuyWeapon(WeaponData weapon)
    {
        if (Current == null || weapon == null || string.IsNullOrEmpty(weapon.id)
            || Current.IsWeaponUnlocked(weapon.id) || Current.credits < weapon.price)
            return false;

        Current.credits -= weapon.price;
        Current.unlockedWeapons.Add(weapon.id);
        Commit();
        return true;
    }

    // Al acabar la partida: suma max(0, puntuación) / 10 créditos, actualiza bestScore y gamesPlayed,
    // guarda y devuelve los créditos ganados (para el GameOverPanel). Sin perfil no se gana nada
    public int RecordGameResult(int finalScore)
    {
        if (Current == null)
            return 0;

        int earned = Mathf.Max(0, finalScore) / ScorePerCredit;
        Current.credits += earned;
        // Una partida en modo prueba (F1) da créditos, pero no cuenta para los récords ni para las partidas:
        // con niveles de 10 s su puntuación no es comparable con las demás
        if (!DebugMode.Enabled)
        {
            Current.gamesPlayed++;
            Current.bestScore = Mathf.Max(Current.bestScore, finalScore);
        }

        Commit();
        return earned;
    }

    // ¿Ha comprado el nivel secreto? Basta con cualquier mejora con ese efecto a nivel 1 o más
    public bool IsBossLevelUnlocked
    {
        get
        {
            if (Current == null || upgrades == null)
                return false;
            foreach (UpgradeData upgrade in upgrades)
                if (upgrade != null && upgrade.effect == UpgradeEffect.UnlockBossLevel && Current.GetUpgradeLevel(upgrade.id) > 0)
                    return true;
            return false;
        }
    }

    // Victoria contra el boss: cuenta y mejor tiempo. Lo llama BossController antes de acabar la partida
    public void MarkHowToPlaySeen()
    {
        if (Current == null || Current.seenHowToPlay) return;
        Current.seenHowToPlay = true;
        Commit();
    }

    public void MarkBossEncountered()
    {
        if (Current == null || Current.bossEncountered) return;
        Current.bossEncountered = true;
        Commit();
    }

    public void RecordBossVictory(float fightSeconds)
    {
        if (Current == null || DebugMode.Enabled)   // con 10 corazones el tiempo no vale como récord
            return;

        Current.bossVictories++;
        if (Current.bestBossTime <= 0f || fightSeconds < Current.bestBossTime)
            Current.bestBossTime = fightSeconds;
        Commit();
    }

    // Perfiles con al menos una partida, de mayor a menor récord (pantalla de Récords)
    public List<PlayerProfile> GetRecords()
    {
        List<PlayerProfile> records = database.profiles.FindAll(p => p.gamesPlayed > 0);
        records.Sort((a, b) => b.bestScore.CompareTo(a.bestScore));
        return records;
    }

    // ── Depuración (menú de ProgressionDebugMenu y del Inspector) ────────────────

    // Vuelve el perfil actual a como recién creado (conserva el nombre)
    public void ResetCurrentProfile()
    {
        if (Current == null)
            return;

        int index = database.profiles.IndexOf(Current);
        PlayerProfile fresh = new PlayerProfile(Current.name);
        database.profiles[index] = fresh;
        Current = fresh;
        EnsureStarterWeapon(fresh);
        Commit();
    }

    // Todas las armas desbloqueadas y todas las mejoras al máximo
    public void UnlockEverything(IEnumerable<WeaponData> weapons)
    {
        if (Current == null)
            return;

        if (weapons != null)
            foreach (WeaponData weapon in weapons)
                if (weapon != null && !string.IsNullOrEmpty(weapon.id) && !Current.IsWeaponUnlocked(weapon.id))
                    Current.unlockedWeapons.Add(weapon.id);

        if (upgrades != null)
            foreach (UpgradeData upgrade in upgrades)
                if (upgrade != null && !string.IsNullOrEmpty(upgrade.id))
                    SetUpgradeLevel(Current, upgrade.id, upgrade.maxLevel);

        Commit();
    }

    // ── Persistencia ────────────────────────────────────────────────────────────

    // Guardar y avisar van siempre juntos: cualquier cambio del perfil queda en disco al momento
    private void Commit()
    {
        Save();
        ProfileChanged?.Invoke();
    }

    public void Save()
    {
        database.version = ProfileDatabase.CurrentVersion;

        try
        {
            File.WriteAllText(TempPath, JsonUtility.ToJson(database, true));

            // El anterior pasa a ser la copia de seguridad. Si el juego se cierra entre los dos Move,
            // no hay profiles.json pero sí .bak, y Load lo recupera
            if (File.Exists(FilePath))
            {
                if (File.Exists(BackupPath))
                    File.Delete(BackupPath);
                File.Move(FilePath, BackupPath);
            }
            File.Move(TempPath, FilePath);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"ProfileManager: no se han podido guardar los perfiles en {FilePath}: {e.Message}", this);
        }
    }

    private void Load()
    {
        if (TryRead(FilePath, out ProfileDatabase loaded, out bool corrupt))
        {
            database = loaded;
            return;
        }

        if (corrupt)
        {
            // Se guarda aparte antes de que el próximo Save lo sobrescriba
            string copy = Path.Combine(Application.persistentDataPath,
                $"profiles.corrupt-{System.DateTime.Now:yyyyMMdd-HHmmss}.json");
            // Una vez copiado se borra: si no, el siguiente Save lo rotaría a .bak y pisaría la copia
            // de seguridad buena con el archivo roto
            try
            {
                File.Copy(FilePath, copy, true);
                File.Delete(FilePath);
            }
            catch (System.Exception e) { Debug.LogError($"ProfileManager: no se ha podido apartar el archivo dañado: {e.Message}", this); }
            Debug.LogWarning($"ProfileManager: {FileName} está dañado; copiado en {copy}. Se intenta la copia de seguridad.", this);
        }

        if (TryRead(BackupPath, out loaded, out _))
        {
            database = loaded;
            Debug.LogWarning("ProfileManager: perfiles recuperados de la copia de seguridad.", this);
            return;
        }

        database = new ProfileDatabase();
    }

    // false si el archivo no existe o no se puede usar; corrupt = existe pero no es válido
    private static bool TryRead(string path, out ProfileDatabase result, out bool corrupt)
    {
        result = null;
        corrupt = false;

        if (!File.Exists(path))
            return false;

        try
        {
            ProfileDatabase db = JsonUtility.FromJson<ProfileDatabase>(File.ReadAllText(path));
            if (db == null || db.profiles == null)
                throw new System.FormatException("sin lista de perfiles");

            if (db.version > ProfileDatabase.CurrentVersion)
                Debug.LogWarning($"ProfileManager: {path} es de una versión más nueva ({db.version}); se lee lo que se entiende.");

            Sanitize(db);
            result = db;
            return true;
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"ProfileManager: no se puede leer {path}: {e.Message}");
            corrupt = true;
            return false;
        }
    }

    // Un JSON editado a mano (o de otra versión) puede traer huecos: se rellenan para no tener nulls
    private static void Sanitize(ProfileDatabase db)
    {
        db.profiles.RemoveAll(p => p == null || string.IsNullOrWhiteSpace(p.name));
        foreach (PlayerProfile p in db.profiles)
        {
            if (p.upgradeLevels == null) p.upgradeLevels = new List<UpgradeLevel>();
            if (p.unlockedWeapons == null) p.unlockedWeapons = new List<string>();
            p.upgradeLevels.RemoveAll(u => u == null || string.IsNullOrEmpty(u.id));
            p.credits = Mathf.Max(0, p.credits);
        }
    }

#if UNITY_EDITOR
    // Ayudas para probar desde el Inspector (menú ⋮ del componente) hasta que exista la pantalla de
    // perfiles (Etapa 5) y el menú de depuración (Etapa 4). Solo en el editor
    [ContextMenu("Depuración/Crear o elegir perfil 'Prueba'")]
    private void DebugTestProfile()
    {
        if (!SelectProfile("Prueba"))
            CreateProfile("Prueba");
    }

    [ContextMenu("Depuración/Desbloquear todo")]
    private void DebugUnlockEverything()
    {
        WeaponController controller = FindFirstObjectByType<WeaponController>();
        UnlockEverything(controller != null ? controller.AvailableWeapons : null);
    }
#endif
}
