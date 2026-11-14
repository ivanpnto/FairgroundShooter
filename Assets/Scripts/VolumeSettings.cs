using UnityEngine;
using UnityEngine.Audio;

// Canales de volumen del menú de opciones (Fase 8). El orden es el de los deslizadores
public enum VolumeChannel
{
    Master,
    Music,
    Effects,
    Ambience
}

// Volumen de cada canal del MainMixer, guardado entre sesiones.
//
// Por qué PlayerPrefs y no el perfil: el volumen es del ordenador, no del jugador. Si dos personas comparten
// el equipo, cambiar de perfil no debería cambiar el volumen de los altavoces.
//
// Por qué el mixer y no AudioSource.volume: cada grupo (Music, SFX, Ambience) ya tiene expuesto su parámetro de
// volumen, así que un SetFloat ajusta todos los sonidos del grupo a la vez, incluidos los que aún no existen.
//
// Va en el objeto GameManager. Se aplica en Start y no en Awake: Unity ignora los SetFloat del mixer
// hechos en Awake del primer frame.
public class VolumeSettings : MonoBehaviour
{
    [SerializeField] private AudioMixer mixer;

    // Nombres de los parámetros expuestos del MainMixer, en el orden de VolumeChannel
    private static readonly string[] Parameters = { "MasterVolume", "MusicVolume", "SFXVolume", "AmbienceVolume" };
    private const string PrefsPrefix = "volume.";
    private const float DefaultLevel = 1f;

    // dB que tenía cada grupo en el mixer al arrancar: el deslizador al 100 % los deja como los mezcló Jaime,
    // y por debajo atenúa desde ahí (no sustituye la mezcla por 0 dB)
    private readonly float[] baselineDb = new float[Parameters.Length];
    private readonly float[] levels = new float[Parameters.Length];
    private bool ready;

    private void Start()
    {
        for (int i = 0; i < Parameters.Length; i++)
        {
            baselineDb[i] = mixer != null && mixer.GetFloat(Parameters[i], out float db) ? db : 0f;
            levels[i] = Mathf.Clamp01(PlayerPrefs.GetFloat(PrefsPrefix + Parameters[i], DefaultLevel));
            Apply(i);
        }
        ready = true;
    }

    // 0..1, tal y como lo muestra el deslizador
    public float Get(VolumeChannel channel) => levels[(int)channel];

    public void Set(VolumeChannel channel, float level)
    {
        int i = (int)channel;
        levels[i] = Mathf.Clamp01(level);
        if (ready) Apply(i);
        PlayerPrefs.SetFloat(PrefsPrefix + Parameters[i], levels[i]);
    }

    // PlayerPrefs se escribe a disco al salir del juego; se fuerza al cerrar el menú por si el juego se cierra mal
    public void Save() => PlayerPrefs.Save();

    private void Apply(int i)
    {
        if (mixer != null)
            mixer.SetFloat(Parameters[i], ToDecibels(levels[i], baselineDb[i]));
    }

    // ── Funciones puras (con tests) ────────────────────────────────────────────

    public const float SilenceDb = -80f;   // el mínimo del mixer: silencio

    // El oído percibe el volumen en escala logarítmica. Un deslizador lineal sobre los dB haría que casi todo el
    // recorrido sonara igual de alto y el último tramo cayera de golpe; con 20·log10 el 50 % suena a la mitad
    // de fuerte (−6 dB) y el recorrido se reparte de forma natural
    public static float ToDecibels(float level, float baselineDb = 0f)
    {
        if (level <= 0.0001f)
            return SilenceDb;
        return Mathf.Max(SilenceDb, baselineDb + 20f * Mathf.Log10(Mathf.Clamp01(level)));
    }
}
