using UnityEditor;
using UnityEngine;

namespace Market.DebugTools.Editor.Checks
{
    /// <summary>Finds missing MonoBehaviour scripts in project-owned prefabs.</summary>
    public sealed class PrefabMissingScriptCheck : IProjectHealthCheck
    {
        public string Name => "Prefab missing scripts";
        public ProjectHealthCategory Category => ProjectHealthCategory.Prefabs;

        public void Scan(ProjectHealthContext context, ProjectHealthReport report)
        {
            foreach (string path in context.FindAssetPaths("t:Prefab"))
            {
                GameObject prefab = context.Load<GameObject>(path);
                if (prefab == null)
                    continue;

                foreach (Transform child in prefab.GetComponentsInChildren<Transform>(true))
                {
                    int missing = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(child.gameObject);
                    if (missing <= 0)
                        continue;

                    report.Add(new ProjectHealthIssue(
                        ProjectHealthSeverity.Error,
                        ProjectHealthCategory.Prefabs,
                        "Prefab contains a missing script",
                        $"'{HierarchyPath(child)}' has {missing} missing MonoBehaviour slot(s).",
                        path));
                }
            }
        }

        private static string HierarchyPath(Transform transform)
        {
            string path = transform.name;
            while (transform.parent != null)
            {
                transform = transform.parent;
                path = $"{transform.name}/{path}";
            }

            return path;
        }
    }
}
