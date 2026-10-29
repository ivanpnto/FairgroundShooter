using UnityEngine;
using TMPro;

/// <summary>
/// Selector de arma de los menús (inicio y Game Over): botones ◀ ▶, nombre y stats.
/// Solo es UI: la lista de armas y el arma equipada son de WeaponController. Al elegir,
/// el arma se equipa en el momento, así que Start/Restart no necesitan saber nada de ella.
/// </summary>
public class WeaponSelectorUI : MonoBehaviour
{
    [SerializeField] private TMP_Text nameText;
    [Tooltip("Línea de stats bajo el nombre (opcional)")]
    [SerializeField] private TMP_Text statsText;

    private WeaponController weaponController;
    private GameManager gameManager;

    private void Awake()
    {
        weaponController = FindFirstObjectByType<WeaponController>();
        gameManager = FindFirstObjectByType<GameManager>();
    }

    // Repintamos al recibir OnWeaponEquipped en lugar de tras cada clic: así los dos
    // selectores muestran siempre lo mismo, venga el cambio de donde venga
    private void OnEnable()
    {
        if (weaponController == null)
            return;

        weaponController.OnWeaponEquipped += Refresh;

        // Mientras el panel estaba oculto no recibíamos el evento: puede haber cambiado.
        // Si WeaponController aún no ha hecho su Start, CurrentWeapon es null y el
        // evento de ese primer Equip ya nos llegará
        if (weaponController.CurrentWeapon != null)
            Refresh(weaponController.CurrentWeapon);
    }

    private void OnDisable()
    {
        if (weaponController != null)
            weaponController.OnWeaponEquipped -= Refresh;
    }

    // Para el OnClick de los botones ◀ ▶
    public void Previous() => Step(-1);
    public void Next() => Step(1);

    private void Step(int direction)
    {
        // Los menús solo se ven fuera de la partida, pero cambiar de arma a mitad
        // de una sería una trampa (cargador lleno al instante), así que lo impedimos
        if (weaponController == null || gameManager == null
            || gameManager.CurrentState == GameManager.GameState.Playing)
            return;

        var weapons = weaponController.AvailableWeapons;
        if (weapons == null || weapons.Count == 0)
        {
            Debug.LogWarning("WeaponSelectorUI: WeaponController no tiene armas en availableWeapons", this);
            return;
        }

        // Si el arma actual no está en la lista (IndexOf = -1), ◀ y ▶ empiezan por los extremos
        int index = IndexOf(weapons, weaponController.CurrentWeapon);
        if (index < 0)
            index = direction > 0 ? -1 : 0;

        // Módulo positivo para dar la vuelta en ambos sentidos (en C#, -1 % 5 es -1)
        int next = ((index + direction) % weapons.Count + weapons.Count) % weapons.Count;

        weaponController.Equip(weapons[next]);
    }

    private static int IndexOf(System.Collections.Generic.IReadOnlyList<WeaponData> list, WeaponData item)
    {
        for (int i = 0; i < list.Count; i++)
            if (list[i] == item)
                return i;
        return -1;
    }

    private void Refresh(WeaponData data)
    {
        if (nameText != null)
            nameText.text = data.displayName;

        if (statsText != null)
        {
            string mode = data.isAutomatic ? "Automática" : "Semiautomática";
            statsText.text = $"{data.magazineSize} balas · {data.fireRate:0.#} disp/s · {mode}";
        }
    }
}
