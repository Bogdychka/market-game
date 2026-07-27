namespace Market.World
{
    /// <summary>
    /// Runtime state of a crop plot.
    /// </summary>
    public enum CropState
    {
        Empty,
        Planted,
        Growing,
        Ready
    }

    /// <summary>
    /// Preparation state of the soil in one crop plot.
    /// </summary>
    public enum CropSoilState
    {
        Untilled,
        Tilled,
        Watered
    }
}
