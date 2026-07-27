namespace Market.DebugTools.Editor
{
    /// <summary>Contract for one independently selectable project validation.</summary>
    public interface IProjectHealthCheck
    {
        string Name { get; }
        ProjectHealthCategory Category { get; }
        void Scan(ProjectHealthContext context, ProjectHealthReport report);
    }
}
