namespace Market.Core.Events
{
    /// <summary>
    /// Маркер для типов событий, которые ходят через EventBus.
    /// Делать события structs — дешевле по аллокациям.
    /// </summary>
    public interface IGameEvent { }
}
