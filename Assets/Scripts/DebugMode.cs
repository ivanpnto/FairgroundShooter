// Modo prueba rápida (menú F1). No cambia ningún asset: los sistemas preguntan aquí al empezar un nivel o
// un combate y, si está activo, usan estos valores en vez de los del LevelConfig o del prefab.
//
// Por qué una clase estática y no un componente: lo leen LevelManager, TargetSpawner, PlayerHealth,
// BossReveal, WeaponAmmo y ProfileManager, que no se conocen entre sí. Un componente obligaría a todos a
// buscarlo en la escena (y a ponerlo en Main.unity); un bool estático no tiene dependencias.
//
// En una build normal Enabled es la constante false: el compilador elimina las ramas de depuración y no
// hay forma de activarlo (el menú F1 tampoco existe ahí).
public static class DebugMode
{
    public const float LevelSeconds = 10f;     // en vez de los 60 s de cada nivel
    public const int BossHearts = 10;          // en vez de 3
    public const float MaxRevealDelay = 2f;    // la revelación del boss llega a los 2 s, no a los 12

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    public static bool Enabled { get; set; }
    public static bool InfiniteAmmo { get; set; }   // aparte: la recarga es parte de lo que se prueba

    // Con el dominio sin recargar (Enter Play Mode Options) los estáticos sobreviven entre ejecuciones:
    // se apaga al darle a Play para no medir el balance con el modo puesto sin darse cuenta
    [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnPlay()
    {
        Enabled = false;
        InfiniteAmmo = false;
    }
#else
    public static bool Enabled => false;
    public static bool InfiniteAmmo => false;
#endif

    public static float LevelDuration(LevelConfig level) =>
        Enabled ? LevelSeconds : (level != null ? level.duration : 60f);

    public static float RevealDelay(float configured) =>
        Enabled ? UnityEngine.Mathf.Min(configured, MaxRevealDelay) : configured;
}
