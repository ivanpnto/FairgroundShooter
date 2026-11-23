using UnityEngine;

// Ajustes del ratón del menú de opciones: sensibilidad e invertir el eje vertical, guardados entre sesiones.
//
// Por qué PlayerPrefs y no el perfil, igual que VolumeSettings: dependen del ratón y del ordenador (un ratón
// con otros DPI mueve la vista el doble), no de quién juega.
//
// Por qué una clase estática y no un componente: PlayerAim los lee cada frame y OptionsPanelUI los cambia, y
// así no hay que asignar nada en la escena. Se leen de PlayerPrefs una sola vez y luego se usa la copia.
public static class ControlSettings
{
    // Multiplicador sobre PlayerAim.sensitivity. El rango cubre de un ratón muy rápido a uno muy lento
    public const float MinSensitivity = 0.25f;
    public const float MaxSensitivity = 3f;
    public const float DefaultSensitivity = 1f;

    private const string SensitivityKey = "controls.sensitivity";
    private const string InvertYKey = "controls.invertY";

    private static bool loaded;
    private static float sensitivity = DefaultSensitivity;
    private static bool invertY;

    public static float Sensitivity
    {
        get { Load(); return sensitivity; }
        set
        {
            Load();
            sensitivity = ClampSensitivity(value);
            PlayerPrefs.SetFloat(SensitivityKey, sensitivity);
        }
    }

    public static bool InvertY
    {
        get { Load(); return invertY; }
        set
        {
            Load();
            invertY = value;
            PlayerPrefs.SetInt(InvertYKey, value ? 1 : 0);
        }
    }

    private static void Load()
    {
        if (loaded) return;
        loaded = true;
        sensitivity = ClampSensitivity(PlayerPrefs.GetFloat(SensitivityKey, DefaultSensitivity));
        invertY = PlayerPrefs.GetInt(InvertYKey, 0) == 1;
    }

    // ── Funciones puras (con tests) ────────────────────────────────────────────

    // También para valores guardados a mano o de una versión con otro rango
    public static float ClampSensitivity(float value)
    {
        if (float.IsNaN(value)) return DefaultSensitivity;
        return Mathf.Clamp(value, MinSensitivity, MaxSensitivity);
    }

    // Delta del ratón → grados de giro (x: derecha, y: ARRIBA). PlayerAim lo resta del pitch, así que invertir
    // es cambiar el signo de y aquí y no tocar sus límites
    public static Vector2 MouseToDegrees(Vector2 mouseDelta, float baseSensitivity, float multiplier, bool invert)
    {
        float k = baseSensitivity * multiplier;
        return new Vector2(mouseDelta.x * k, (invert ? -mouseDelta.y : mouseDelta.y) * k);
    }
}
