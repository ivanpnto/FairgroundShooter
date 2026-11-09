using UnityEngine;

public enum UpgradeEffect
{
    Ammo,             // tamaño del cargador
    FireRate,         // tiempo entre disparos
    ScoreMultiplier,  // no toca el arma: lo lee la puntuación
    Damage,

    // Mejoras caras (I6.2). Se añaden AL FINAL: los assets guardan el enum como número y
    // meterlas en medio cambiaría el efecto de las mejoras que ya existen
    Penetration,      // valuePerLevel = objetivos extra que atraviesa (1 → hasta 2 objetivos)
    ExtraRays,        // valuePerLevel = rayos extra por disparo (1 → disparo doble)
    Explosive,        // valuePerLevel = radio de la onda expansiva en metros

    // I7.6: añade el nivel 4 al final de la partida (maxLevel 1; valuePerLevel no se usa)
    UnlockBossLevel
}

// Una mejora de la tienda. Es global (no por arma): su nivel se guarda en el perfil por id.
// Solo se lee en runtime: los niveles comprados viven en PlayerProfile, nunca aquí
[CreateAssetMenu(fileName = "NewUpgrade", menuName = "FairgroundShooter/Upgrade Data")]
public class UpgradeData : ScriptableObject
{
    [Tooltip("Identificador único y estable: es la clave en profiles.json, no se debe cambiar tras publicar")]
    public string id;
    public string displayName;
    [TextArea] public string description;
    public Sprite icon;

    public UpgradeEffect effect;
    [Min(1)] public int maxLevel = 3;
    [Tooltip("Coste de cada nivel: el elemento 0 es lo que cuesta pasar del nivel 0 al 1")]
    public int[] costPerLevel = new int[3];
    [Tooltip("Lo que suma cada nivel (0.25 = +25 % por nivel; en FireRate, 0.10 = −10 % de tiempo entre disparos)")]
    public float valuePerLevel = 0.25f;

    public bool IsMaxed(int currentLevel) => currentLevel >= maxLevel;

    // Coste de pasar de currentLevel al siguiente; -1 si ya está al máximo o falta el dato
    public int NextLevelCost(int currentLevel)
    {
        if (IsMaxed(currentLevel) || costPerLevel == null || currentLevel < 0 || currentLevel >= costPerLevel.Length)
            return -1;
        return costPerLevel[currentLevel];
    }
}
