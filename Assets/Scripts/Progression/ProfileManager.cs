using System.Collections.Generic;
using UnityEngine;

// CONTRATO (Etapa 0): solo firmas. La implementación llega en la Etapa 4.
//
// Único dueño de los perfiles: los carga de profiles.json (Application.persistentDataPath), es el único
// que los modifica y guarda tras cada cambio. Va en un objeto de la escena y se busca con
// FindFirstObjectByType, como el resto de sistemas.
//
// Persistencia prevista:
// - Guardado seguro: se escribe en profiles.json.tmp y luego se reemplaza profiles.json, dejando la
//   versión anterior como profiles.json.bak. Un cierre a mitad de escritura no deja el archivo a medias.
// - Carga: si profiles.json no se puede leer o no es JSON válido, se copia aparte como
//   profiles.corrupt-<fecha>.json (para no perderlo) y se intenta profiles.json.bak; si tampoco vale,
//   se empieza sin perfiles. Nunca se sobrescribe un archivo roto sin haberlo copiado antes.
public class ProfileManager : MonoBehaviour
{
    // El perfil activo (null hasta que se elige o se crea uno)
    public PlayerProfile Current => throw new System.NotImplementedException("Etapa 4");
    public int Credits => throw new System.NotImplementedException("Etapa 4");
    public IReadOnlyList<PlayerProfile> AllProfiles => throw new System.NotImplementedException("Etapa 4");

    // Cualquier cambio del perfil activo (créditos, mejoras, armas) o cambio de perfil.
    // WeaponController recalcula las estadísticas y la tienda se repinta al recibirlo
#pragma warning disable CS0067   // aún no se lanza: el contrato no tiene implementación (Etapa 4)
    public event System.Action ProfileChanged;
#pragma warning restore CS0067

    // false (y no crea nada) si el nombre está vacío o ya existe (sin distinguir mayúsculas)
    public bool CreateProfile(string profileName) => throw new System.NotImplementedException("Etapa 4");

    public bool SelectProfile(string profileName) => throw new System.NotImplementedException("Etapa 4");

    // Resta los créditos solo si hay suficientes; false si no
    public bool TrySpend(int amount) => throw new System.NotImplementedException("Etapa 4");

    public void AddCredits(int amount) => throw new System.NotImplementedException("Etapa 4");

    public int GetUpgradeLevel(string id) => throw new System.NotImplementedException("Etapa 4");

    // Cobra el siguiente nivel y lo sube; false si está al máximo o no hay créditos
    public bool BuyUpgradeLevel(UpgradeData upgrade) => throw new System.NotImplementedException("Etapa 4");

    public bool IsWeaponUnlocked(string id) => throw new System.NotImplementedException("Etapa 4");

    // Desbloquea sin cobrar (arma inicial de un perfil nuevo, menú de depuración)
    public void UnlockWeapon(string id) => throw new System.NotImplementedException("Etapa 4");

    // PROPUESTA: cobra weapon.price y desbloquea; false si ya está desbloqueada o no hay créditos
    public bool BuyWeapon(WeaponData weapon) => throw new System.NotImplementedException("Etapa 4");

    // PROPUESTA: al acabar la partida. Suma max(0, puntuación) / 10 créditos, actualiza bestScore y
    // gamesPlayed, guarda y devuelve los créditos ganados (para el GameOverPanel)
    public int RecordGameResult(int finalScore) => throw new System.NotImplementedException("Etapa 4");

    public void Save() => throw new System.NotImplementedException("Etapa 4");
}
