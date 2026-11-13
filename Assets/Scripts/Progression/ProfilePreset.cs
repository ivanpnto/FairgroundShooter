using System.Collections.Generic;

// Un perfil «prefabricado»: créditos, niveles de mejora y armas (balance, bloque 3).
//
// Lo usan dos cosas:
//  - ProfileManager, para el perfil de demostración (el que usará el profesor para ver el nivel 4 sin tener que
//    jugar varias partidas antes).
//  - BalanceBot, para medir el juego con un equipo concreto (sin mejoras, a medias, todo).
[System.Serializable]
public class ProfilePreset
{
    public string name = "Demo";
    public int credits;
    public List<UpgradeLevel> upgrades = new List<UpgradeLevel>();
    public List<string> weapons = new List<string>();   // ids de WeaponData (el arma inicial se añade sola)

    // Perfil nuevo con este contenido. Las mejoras y armas que no existan en el juego no hacen daño: se ignoran al leerlas
    public PlayerProfile Build()
    {
        PlayerProfile profile = new PlayerProfile(name) { credits = credits };
        foreach (UpgradeLevel u in upgrades)
            if (u != null && !string.IsNullOrEmpty(u.id)) profile.upgradeLevels.Add(new UpgradeLevel(u.id, u.level));
        foreach (string id in weapons)
            if (!string.IsNullOrEmpty(id) && !profile.unlockedWeapons.Contains(id)) profile.unlockedWeapons.Add(id);
        return profile;
    }

    // Perfil de demostración por defecto («nivel 4 + mitad de la tienda»): el nivel del boss comprado, las mejoras
    // baratas a medias, el disparo doble, dos armas más y créditos para probar la tienda. Con el escalado del boss,
    // este equipo no lo convierte en un trámite
    public static ProfilePreset DefaultDemo()
    {
        return new ProfilePreset
        {
            name = "Demo",
            credits = 1500,
            upgrades = new List<UpgradeLevel>
            {
                new UpgradeLevel("ammo", 2), new UpgradeLevel("firerate", 2), new UpgradeLevel("score", 1),
                new UpgradeLevel("damage", 2), new UpgradeLevel("doubleshot", 1), new UpgradeLevel("boss_level", 1)
            },
            weapons = new List<string> { "mp5", "m16a1" }
        };
    }
}
