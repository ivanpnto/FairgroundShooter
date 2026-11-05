// Habilidades: una fila por cada mejora del catálogo de ProfileManager (se aplican a todas las armas),
// así que una mejora nueva aparece sin tocar la UI
public class SkillsPanelUI : ShopListPanelUI
{
    protected override void AddRows(int credits)
    {
        if (profileManager.Upgrades == null)
            return;

        foreach (UpgradeData upgrade in profileManager.Upgrades)
        {
            if (upgrade == null)
                continue;

            int level = profileManager.GetUpgradeLevel(upgrade.id);
            int cost = upgrade.NextLevelCost(level);
            bool maxed = cost < 0;

            NewRow().Setup(
                $"{upgrade.displayName}   {level}/{upgrade.maxLevel}",
                upgrade.description,
                maxed ? "MÁXIMO" : $"{cost} CR.",
                !maxed && credits >= cost,
                () => profileManager.BuyUpgradeLevel(upgrade));
        }
    }
}
