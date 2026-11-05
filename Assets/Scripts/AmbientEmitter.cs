using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// Un punto de sonido ambiente de la feria: un bucle continuo, sonidos sueltos al azar, o ambos.
/// Solo presentación y sin estado de partida: suena siempre, también en los menús (estar
/// "en la feria" no depende de si se juega), así que no consulta GameManager ni necesita Reset*().
/// Los emisores cuelgan del objeto raíz "Ambience" de Main.unity y NO de Env_Fairground:
/// Setup Exterior destruye y regenera Env_Fairground y se los llevaría por delante. Por eso
/// tampoco siguen a los modelos: si se mueve el tiovivo o la noria, hay que mover su emisor.
/// </summary>
public class AmbientEmitter : MonoBehaviour
{
    [Tooltip("Bucle continuo (opcional)")]
    [SerializeField] private AudioClip loopClip;
    [Tooltip("Sonidos sueltos que se eligen al azar (opcional)")]
    [SerializeField] private AudioClip[] oneShots;
    [Tooltip("Segundos entre sonidos sueltos (mínimo, máximo), contados desde que empieza el anterior")]
    [SerializeField] private Vector2 oneShotInterval = new Vector2(8f, 18f);

    [Tooltip("Grupo Ambience del MainMixer. Vacío = directo al AudioListener")]
    [SerializeField] private AudioMixerGroup output;
    [Range(0f, 1f)] [SerializeField] private float loopVolume = 1f;
    [Range(0f, 1f)] [SerializeField] private float oneShotVolume = 1f;
    [Tooltip("Tono del bucle. < 1 = más grave y lento (la noria reutiliza la grabación de atracciones)")]
    [Range(0.5f, 2f)] [SerializeField] private float loopPitch = 1f;
    [Tooltip("Variación de tono de cada sonido suelto (± esta fracción), para que no suenen siempre idénticos")]
    [Range(0f, 0.3f)] [SerializeField] private float oneShotPitchVariation = 0.06f;

    [Header("Espacialización")]
    [Tooltip("0 = 2D (capa de fondo que no viene de ningún sitio), 1 = 3D")]
    [Range(0f, 1f)] [SerializeField] private float spatialBlend = 1f;
    [Tooltip("Hasta esta distancia suena a volumen completo; a partir de ahí baja como 1/distancia")]
    [Min(0.1f)] [SerializeField] private float minDistance = 5f;
    [Tooltip("Más allá ya no baja más")]
    [Min(0.1f)] [SerializeField] private float maxDistance = 80f;
    [Tooltip("Apertura en grados: una atracción grande no suena desde un punto")]
    [Range(0f, 360f)] [SerializeField] private float spread = 60f;

    private AudioSource loopSource;
    private AudioSource oneShotSource;
    private float nextOneShotTime;

    private void Awake()
    {
        // Por código, como en WeaponEffects y MusicManager: así cada emisor se ajusta con pocos
        // campos y todos comparten la misma configuración 3D
        if (loopClip != null)
        {
            loopSource = CreateSource();
            loopSource.clip = loopClip;
            loopSource.loop = true;
            loopSource.volume = loopVolume;
            loopSource.pitch = loopPitch;
        }

        if (oneShots != null && oneShots.Length > 0)
        {
            oneShotSource = CreateSource();
            oneShotSource.volume = oneShotVolume;
            ScheduleNextOneShot();
        }
    }

    private void Start()
    {
        if (loopSource == null)
            return;

        // Empieza en un punto al azar: el tiovivo y la noria salen de la misma grabación y,
        // arrancando a la vez desde 0, se oiría el mismo momento en los dos sitios
        loopSource.timeSamples = Random.Range(0, loopClip.samples);
        loopSource.Play();
    }

    private AudioSource CreateSource()
    {
        AudioSource source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.outputAudioMixerGroup = output;
        source.spatialBlend = spatialBlend;
        source.rolloffMode = AudioRolloffMode.Logarithmic;
        source.minDistance = minDistance;
        source.maxDistance = maxDistance;
        source.spread = spread;
        // Sin Doppler: hoy ni los emisores ni el AudioListener se desplazan (la cámara solo gira),
        // y así un desplazamiento futuro de la cámara no desafinaría el ambiente
        source.dopplerLevel = 0f;
        return source;
    }

    private void Update()
    {
        if (oneShotSource == null || Time.unscaledTime < nextOneShotTime)
            return;

        AudioClip clip = oneShots[Random.Range(0, oneShots.Length)];
        if (clip != null)
        {
            // El pitch es de la fuente, no de PlayOneShot (ver WeaponEffects). Aquí basta una
            // sola fuente: el intervalo mínimo es mayor que los clips, así que no se pisan
            oneShotSource.pitch = 1f + Random.Range(-oneShotPitchVariation, oneShotPitchVariation);
            oneShotSource.PlayOneShot(clip);
        }

        ScheduleNextOneShot();
    }

    // unscaledTime, como el fundido de MusicManager: una pausa con timeScale = 0 (Fase 8)
    // no congelaría el ambiente, que tampoco se para en los menús
    private void ScheduleNextOneShot()
    {
        nextOneShotTime = Time.unscaledTime + Random.Range(oneShotInterval.x, oneShotInterval.y);
    }
}
