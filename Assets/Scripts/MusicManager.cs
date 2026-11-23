using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// Música de fondo: la pista de menú fuera de la partida y la del nivel (LevelConfig.music)
/// mientras se juega, con fundido cruzado al cambiar.
/// Solo presentación: cada frame LEE el estado de GameManager y el nivel de LevelManager y
/// decide qué debería sonar, como hacen los demás sistemas con CurrentState. Así no hace
/// falta ningún evento nuevo ni un Reset*(): reiniciar la partida simplemente vuelve a
/// pedir la pista del nivel 1. Va en el objeto GameManager.
/// En el nivel del boss (LevelConfig.isBossLevel) hay tres momentos: el circo falso con la
/// pista del nivel, la revelación en silencio y el combate con bossMusic. También se LEEN
/// (BossReveal.IsPlaying y BossController.State), sin engancharse a sus eventos.
/// </summary>
public class MusicManager : MonoBehaviour
{
    [Tooltip("Pista de los menús (Perfiles, Inicio, Tienda, Récords y Game Over)")]
    [SerializeField] private AudioClip menuMusic;
    // Aquí y no en LevelConfig: es del combate, no del nivel (el nivel 4 empieza como un circo
    // normal con su propia pista), y así no se añade otro campo a un script de Iván
    [Tooltip("Pista del combate contra el boss, desde que empieza hasta el Game Over. Vacío = sigue la del nivel")]
    [SerializeField] private AudioClip bossMusic;
    [Tooltip("Grupo Music del MainMixer. Vacío = sale directo al AudioListener")]
    [SerializeField] private AudioMixerGroup output;

    [Range(0f, 1f)] [SerializeField] private float volume = 0.8f;
    [Tooltip("Segundos del fundido cruzado entre pistas")]
    [Min(0.01f)] [SerializeField] private float fadeTime = 1.5f;
    // Entre niveles sigue sonando la pista del nivel terminado, pero más baja: se nota la
    // pausa y el panel de la cuenta atrás, sin un silencio que parezca un corte
    [Tooltip("Factor de volumen durante la pantalla entre niveles")]
    [Range(0f, 1f)] [SerializeField] private float transitionVolume = 0.5f;
    [Tooltip("Factor de volumen con el juego en pausa")]
    [Range(0f, 1f)] [SerializeField] private float pausedVolume = 0.4f;

    private GameManager gameManager;
    private LevelManager levelManager;

    // Dos fuentes para poder fundir: la activa sube hacia el volumen objetivo y la otra baja
    // a 0 (y se para). Al cambiar de pista se intercambian los papeles
    private AudioSource[] sources;
    private int active;

    private void Awake()
    {
        gameManager = FindFirstObjectByType<GameManager>();
        levelManager = FindFirstObjectByType<LevelManager>();

        sources = new AudioSource[2];
        for (int i = 0; i < sources.Length; i++)
        {
            // Por código, como en WeaponEffects: son un detalle interno y así no hay nada
            // que asignar en Main.unity salvo el clip de menú y el grupo del mixer
            AudioSource source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = true;
            source.spatialBlend = 0f;   // música de fondo: 2D
            source.volume = 0f;
            source.outputAudioMixerGroup = output;
            // La pausa calla todo con AudioListener.pause; la música sigue (más baja) para que no parezca colgado
            source.ignoreListenerPause = true;
            sources[i] = source;
        }
    }

    private void Update()
    {
        LevelConfig level = CurrentRunLevel();
        AudioClip wanted = WantedClip(level);

        if (wanted != sources[active].clip)
            SwitchTo(wanted);

        float target = volume;
        // La revelación va antes que LevelTransition: GameManager la pausa con ese mismo estado
        // (PauseForCutscene) y, si no, sonaría a la mitad como entre niveles. En silencio se oyen
        // los golpes, el estallido y el rugido, y el combate entra con su pista desde cero
        if (IsBossRevealPlaying(level))
            target = 0f;
        else if (gameManager != null && gameManager.CurrentState == GameManager.GameState.LevelTransition)
            target *= transitionVolume;
        else if (gameManager != null && gameManager.IsPaused)
            target *= pausedVolume;

        // unscaledDeltaTime: si algún día se pausa con Time.timeScale = 0 (Fase 8), el
        // fundido no se quedaría congelado a medias
        float step = Time.unscaledDeltaTime / fadeTime;

        for (int i = 0; i < sources.Length; i++)
        {
            AudioSource source = sources[i];
            float goal = i == active && source.clip != null ? target : 0f;
            // Lineal y no exponencial: llega al objetivo exacto en fadeTime y no se queda
            // sonando a un volumen inaudible para siempre. Con volume * step el cambio de
            // transitionVolume también tarda lo mismo que un fundido
            source.volume = Mathf.MoveTowards(source.volume, goal, step * volume);

            if (i != active && source.isPlaying && source.volume <= 0f)
                source.Stop();
        }
    }

    // Menús: pista de menú. Jugando o entre niveles: la del nivel actual (en la transición
    // CurrentLevel aún es el terminado, así que su pista sigue hasta que empieza el siguiente).
    // Un nivel sin música da silencio en vez de la de menú: la de menú en partida desorienta
    private AudioClip WantedClip(LevelConfig level)
    {
        if (gameManager == null || !gameManager.IsInRun)   // en pausa sigue la pista del nivel
            return menuMusic;

        if (level == null)
            return null;

        // Con el boss muerto (Dead) sigue la del combate durante el cartel de victoria, hasta el Game Over
        if (bossMusic != null && level.isBossLevel)
        {
            BossController boss = SceneCache<BossController>.Get();
            if (boss != null && boss.State != BossController.BossState.Inactive)
                return bossMusic;
        }
        return level.music;
    }

    // Nivel de la partida en curso, o null en los menús
    private LevelConfig CurrentRunLevel()
    {
        if (gameManager == null || !gameManager.IsInRun || levelManager == null)
            return null;
        return levelManager.CurrentLevel;
    }

    // Solo se busca BossReveal en el nivel del boss: SceneCache repite la búsqueda mientras no lo
    // encuentra, y así no se recorre la escena en cada frame de los demás niveles ni de los menús
    private static bool IsBossRevealPlaying(LevelConfig level)
    {
        if (level == null || !level.isBossLevel)
            return false;
        BossReveal reveal = SceneCache<BossReveal>.Get();
        return reveal != null && reveal.IsPlaying;
    }

    private void SwitchTo(AudioClip clip)
    {
        // Si la pista pedida es la que se está apagando (p. ej. Game Over y OTRA RONDA muy
        // seguidos), se recupera desde donde iba en vez de empezarla de cero
        int other = 1 - active;
        active = other;

        AudioSource source = sources[active];
        if (source.clip == clip && source.isPlaying)
            return;

        source.Stop();
        source.clip = clip;
        source.volume = 0f;
        if (clip != null)
            source.Play();
    }
}
