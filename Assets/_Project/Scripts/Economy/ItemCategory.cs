namespace Market.Economy
{
    /// <summary>
    /// Категория товара. Используется для:
    /// — фильтрации в инвентаре/прилавке
    /// — предпочтений NPC (NPCTypeSO.PreferredCategories)
    /// </summary>
    public enum ItemCategory
    {
        Food,
        Fish,
        Animal,
        Craft,
        Flower,
        Ingredient,
        Tool,
        Misc
    }
}
