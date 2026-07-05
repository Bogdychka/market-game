namespace Market.Core.Events
{
    /// <summary>
    /// Marker interface for event types that travel through <see cref="EventBus"/>.
    /// Prefer structs for events -- cheaper allocations.
    /// </summary>
    public interface IGameEvent { }
}
