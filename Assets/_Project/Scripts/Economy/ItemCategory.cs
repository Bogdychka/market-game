namespace Market.Economy
{
    /// <summary>
    /// Item category. Used for:
    /// -- inventory/stall filtering
    /// -- NPC preferences (NPCTypeSO.PreferredCategories)
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
