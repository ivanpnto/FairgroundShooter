using System.Collections.Generic;

// Nivel comprado de una mejora. JsonUtility no serializa Dictionary, así que los niveles
// se guardan como una lista de pares {id, nivel}
[System.Serializable]
public class UpgradeLevel
{
    public string id;
    public int level;

    public UpgradeLevel(string id, int level)
    {
        this.id = id;
        this.level = level;
    }

    public UpgradeLevel() { }   // para JsonUtility
}

// Progreso de un jugador entre partidas. Solo datos: quien lo modifica es ProfileManager,
// que además se encarga de guardarlo. El resto del juego solo debería leerlo
[System.Serializable]
public class PlayerProfile
{
    public string name;
    public int credits;
    public int bestScore;
    public int gamesPlayed;
    public List<UpgradeLevel> upgradeLevels = new List<UpgradeLevel>();
    public List<string> unlockedWeapons = new List<string>();   // ids de WeaponData

    // Nivel 4 (I7.6). Campos nuevos con valor por defecto: los perfiles guardados antes se leen igual
    public int bossVictories;
    public float bestBossTime;   // segundos del combate más rápido; 0 = nunca lo ha vencido
    public bool BossDefeated => bossVictories > 0;

    // Bloque 2: la pantalla «Cómo jugar» sale sola la primera partida; el contorno rosa solo se explica
    // cuando el jugador ya ha visto al boss (si no, destriparía la sorpresa del nivel 4)
    public bool seenHowToPlay;
    public bool bossEncountered;

    // 24/11: CÓMO JUGAR tiene versión. Cuando cambian las reglas (objetivo por nivel, pistola de apoyo) se sube
    // CurrentHowToPlayVersion y cada perfil la vuelve a ver UNA vez al pulsar EMPEZAR. Los perfiles anteriores
    // leen 0 y la ven otra vez; seenHowToPlay se mantiene para no romper los que ya existen
    public const int CurrentHowToPlayVersion = 2;
    public int howToPlayVersion;
    public bool NeedsHowToPlay => howToPlayVersion < CurrentHowToPlayVersion;

    // Arma principal elegida (selector al jugar o EQUIPAR de la armería), para preseleccionarla la próxima vez.
    // Vacío en los perfiles antiguos: se usa la inicial
    public string equippedWeapon;

    // Lo creó ProfileManager desde demoPreset: solo ese se rehace en cada arranque. Un perfil de jugador que
    // se llame igual (creado antes de que existiera el Demo) no lleva la marca y no se toca
    public bool isDemo;

    public PlayerProfile(string name)
    {
        this.name = name;
    }

    // Para JsonUtility, que crea los objetos al leer profiles.json
    public PlayerProfile() { }

    // Lectura sin efectos secundarios: la necesita WeaponStatsCalculator, que recibe el perfil
    // y no el ProfileManager. Una mejora nunca comprada está a nivel 0
    public int GetUpgradeLevel(string id)
    {
        foreach (UpgradeLevel entry in upgradeLevels)
            if (entry.id == id)
                return entry.level;
        return 0;
    }

    public bool IsWeaponUnlocked(string id) => unlockedWeapons.Contains(id);
}

// Raíz de profiles.json. JsonUtility necesita una clase como raíz (no acepta una lista suelta).
// version permite migrar el formato en el futuro sin perder los perfiles guardados
[System.Serializable]
public class ProfileDatabase
{
    public const int CurrentVersion = 1;

    public int version = CurrentVersion;
    public string selectedProfile;   // nombre del último perfil usado, para seleccionarlo al arrancar
    public List<PlayerProfile> profiles = new List<PlayerProfile>();
}
