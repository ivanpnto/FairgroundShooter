using UnityEngine;
using TMPro;

// Sabe qué niveles hay y en cuál estamos. Al empezar un nivel activa su entorno
// y configura el spawner, el temporizador y la munición. El flujo entre niveles
// (pantalla intermedia, fin de partida) lo decide el GameManager.
public class LevelManager : MonoBehaviour
{
    [System.Serializable]
    public class EnvironmentSet
    {
        public string id;              // El que usa LevelConfig.environmentId
        public GameObject[] objects;   // Decorado e iluminación propios de ese entorno
    }

    [SerializeField] private LevelConfig[] levels;
    [SerializeField] private EnvironmentSet[] environments;
    [Tooltip("Opcional: texto del HUD donde mostrar el nivel actual")]
    [SerializeField] private TMP_Text levelText;

    public int CurrentIndex { get; private set; } = -1;
    public int LevelCount => levels != null ? levels.Length : 0;
    public LevelConfig CurrentLevel => IsValid(CurrentIndex) ? levels[CurrentIndex] : null;
    // I7.6: un nivel con boss solo cuenta como siguiente si el perfil lo ha comprado. Sin comprarlo, la
    // partida acaba en el nivel 3 como siempre (el nivel 4 va el último de la lista)
    public bool HasNextLevel => IsAvailable(CurrentIndex + 1);
    public LevelConfig NextLevel => HasNextLevel ? levels[CurrentIndex + 1] : null;

    // Objetivo de puntos del nivel (24/11), ya ajustado al modo prueba y al arma de la partida (25/11: la Barrett
    // tiene su propio factor). 0 = sin objetivo (boss incluido)
    public int TargetFor(LevelConfig level)
    {
        if (level == null || level.isBossLevel) return 0;
        int target = LevelConfig.ScaledTarget(level.targetScore, DebugMode.LevelDuration(level), level.duration);
        WeaponData weapon = weaponController != null ? weaponController.PrimaryWeapon : null;
        return weapon != null ? LevelConfig.WeaponScaledTarget(target, weapon.objectiveScale) : target;
    }
    public int CurrentTarget => TargetFor(CurrentLevel);

    public event System.Action<LevelConfig, int> LevelStarted;

    private TargetSpawner spawner;
    private GameTimer timer;
    private WeaponAmmo weaponAmmo;
    private WeaponController weaponController;
    private ProfileManager profileManager;

    private void Awake()
    {
        spawner = FindFirstObjectByType<TargetSpawner>();
        timer = FindFirstObjectByType<GameTimer>();
        weaponAmmo = FindFirstObjectByType<WeaponAmmo>();
        weaponController = FindFirstObjectByType<WeaponController>();
        profileManager = FindFirstObjectByType<ProfileManager>();
    }

    private bool IsValid(int index) => levels != null && index >= 0 && index < levels.Length && levels[index] != null;

    // Sin ProfileManager (escenas de prueba) se juega todo
    private bool IsAvailable(int index) =>
        IsValid(index) && (!levels[index].isBossLevel || profileManager == null || profileManager.IsBossLevelUnlocked);

    public void StartLevel(int index)
    {
        if (!IsValid(index))
        {
            Debug.LogError($"LevelManager: no existe el nivel {index}.", this);
            return;
        }

        CurrentIndex = index;
        LevelConfig level = levels[index];

        ActivateEnvironment(level.environmentId);

        if (spawner != null) spawner.Configure(level);
        if (timer != null) timer.StartLevel(DebugMode.LevelDuration(level));   // F1: 10 s en modo prueba
        // Cada nivel empieza con los cargadores llenos (el del arma en mano y el de la pistola de apoyo)
        if (weaponController != null) weaponController.RefillAll();
        else if (weaponAmmo != null) weaponAmmo.ResetAmmo();

        if (levelText != null)
            levelText.text = $"NIVEL {index + 1} · {level.levelName.ToUpper()}";

        LevelStarted?.Invoke(level, index);
        GameLog.Info($"Nivel {index + 1}: {level.levelName}");
    }

    private void ActivateEnvironment(string id)
    {
        if (environments == null)
            return;

        // Primero se apaga todo y luego se enciende el entorno pedido:
        // así un objeto compartido entre entornos acaba encendido
        foreach (EnvironmentSet env in environments)
            SetActive(env, false);

        bool found = false;

        foreach (EnvironmentSet env in environments)
        {
            if (env.id != id)
                continue;

            SetActive(env, true);
            found = true;
        }

        if (!found)
            Debug.LogWarning($"LevelManager: no hay ningún entorno con id '{id}'.", this);
    }

    private static void SetActive(EnvironmentSet env, bool active)
    {
        if (env.objects == null)
            return;

        foreach (GameObject obj in env.objects)
        {
            if (obj != null)
                obj.SetActive(active);
        }
    }
}
