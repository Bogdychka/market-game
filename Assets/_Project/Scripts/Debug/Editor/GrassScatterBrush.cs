using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Market.DebugTools.Editor
{
    /// <summary>
    /// Scene-view terrain brush that scatters random mixes of two grass sources (mesh assets or
    /// scene GameObjects) around the cursor. Hold left mouse and drag over a TerrainCollider to paint.
    /// </summary>
    public class GrassScatterBrush : EditorWindow
    {
        private const string ContainerName = "GrassScatter";
        private const string GrassCardFolder = "Assets/_Project/Art/Nature/Grass";
        private const string GrassMaterialPath = "Assets/blender/Grass_1.mat";

        private static readonly string[] LegacyGrassPaths =
        {
            "Assets/blender/Grass_1.fbx",
            "Assets/blender/Grass_2.fbx",
        };

        private readonly List<GameObject> _sources = new List<GameObject>();
        private Material _material;
        private float _radius = 2f;
        private int _instancesPerStroke = 3;
        private float _minScale = 0.85f;
        private float _maxScale = 1.15f;
        private float _strokeInterval = 0.05f;
        private bool _alignToSlope;
        private bool _paintingEnabled;
        private double _lastPaintTime = double.NegativeInfinity;

        [MenuItem("Market/Debug/Grass Scatter Brush")]
        public static void Open()
        {
            GetWindow<GrassScatterBrush>("Grass Scatter Brush");
        }

        private void OnEnable()
        {
            if (_sources.Count == 0)
                LoadDefaultSources();
            SceneView.duringSceneGui += OnSceneGUI;
        }

        /// <summary>
        /// Fills the palette with every built grass card clump. Card prefabs carry their own
        /// material, so no override is needed; only the older raw FBX sources need one.
        /// </summary>
        private void LoadDefaultSources()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { GrassCardFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.EndsWith("_Clump.prefab", System.StringComparison.Ordinal))
                    continue;

                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab != null)
                    _sources.Add(prefab);
            }

            if (_sources.Count > 0)
                return;

            foreach (string path in LegacyGrassPaths)
            {
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (model != null)
                    _sources.Add(model);
            }

            _material = AssetDatabase.LoadAssetAtPath<Material>(GrassMaterialPath);
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
        }

        private void OnGUI()
        {
            DrawSourceList();

            _material = (Material)EditorGUILayout.ObjectField("Material Override", _material, typeof(Material), false);
            EditorGUILayout.HelpBox("Optional. Leave empty for grass card prefabs, which already carry their own material; set it for raw FBX sources that would otherwise paint with their imported gray material.", MessageType.None);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Brush", EditorStyles.boldLabel);
            _radius = EditorGUILayout.Slider("Radius", _radius, 0.25f, 15f);
            _instancesPerStroke = EditorGUILayout.IntSlider("Instances / Tick", _instancesPerStroke, 1, 30);
            _strokeInterval = EditorGUILayout.Slider("Tick Interval (s)", _strokeInterval, 0.02f, 0.5f);
            EditorGUILayout.MinMaxSlider("Scale Jitter", ref _minScale, ref _maxScale, 0.25f, 2.5f);
            EditorGUILayout.LabelField($"  {_minScale:0.00}x - {_maxScale:0.00}x");
            _alignToSlope = EditorGUILayout.Toggle("Align To Slope", _alignToSlope);

            EditorGUILayout.Space();
            _paintingEnabled = EditorGUILayout.ToggleLeft("Enable Painting", _paintingEnabled);
            EditorGUILayout.HelpBox("With painting enabled, hold Left Mouse and drag over the terrain in the Scene view to scatter grass.", MessageType.Info);
        }

        private void OnSceneGUI(SceneView view)
        {
            if (!_paintingEnabled || !HasSource())
                return;

            Event e = Event.current;
            int controlId = GUIUtility.GetControlID(FocusType.Passive);

            Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
            bool hasHit = Physics.Raycast(ray, out RaycastHit hit, 2000f) && hit.collider is TerrainCollider;

            if (hasHit)
            {
                Handles.color = new Color(0.2f, 1f, 0.4f, 0.6f);
                Handles.DrawWireDisc(hit.point, hit.normal, _radius);

                bool isPaintEvent = e.type is EventType.MouseDown or EventType.MouseDrag && e.button == 0 && !e.alt;
                if (isPaintEvent)
                {
                    double now = EditorApplication.timeSinceStartup;
                    if (now - _lastPaintTime >= _strokeInterval)
                    {
                        PaintAt(hit);
                        _lastPaintTime = now;
                    }

                    GUIUtility.hotControl = controlId;
                    e.Use();
                }
                else if (e.type == EventType.MouseUp && e.button == 0)
                {
                    if (GUIUtility.hotControl == controlId)
                        GUIUtility.hotControl = 0;
                    e.Use();
                }
                else if (e.type == EventType.MouseMove)
                {
                    view.Repaint();
                }
            }

            if (e.type == EventType.Layout)
                HandleUtility.AddDefaultControl(controlId);
        }

        private void PaintAt(RaycastHit centerHit)
        {
            Transform parent = GetOrCreateContainer();

            for (int i = 0; i < _instancesPerStroke; i++)
            {
                GameObject source = PickSource();
                if (source == null)
                    continue;

                Vector2 offset = Random.insideUnitCircle * _radius;
                Vector3 samplePos = centerHit.point + new Vector3(offset.x, 0f, offset.y);

                if (!Physics.Raycast(samplePos + Vector3.up * 500f, Vector3.down, out RaycastHit localHit, 2000f) ||
                    localHit.collider is not TerrainCollider)
                    continue;

                Quaternion rotation = source.transform.rotation;
                if (_alignToSlope)
                    rotation = Quaternion.FromToRotation(source.transform.up, localHit.normal) * rotation;
                rotation = Quaternion.AngleAxis(Random.Range(0f, 360f), localHit.normal) * rotation;

                GameObject instance = PrefabUtility.IsPartOfPrefabAsset(source)
                    ? (GameObject)PrefabUtility.InstantiatePrefab(source, parent)
                    : Instantiate(source, parent);

                instance.transform.SetPositionAndRotation(localHit.point, rotation);
                instance.transform.localScale = source.transform.localScale * Random.Range(_minScale, _maxScale);
                instance.name = source.name;

                if (_material != null)
                {
                    foreach (Renderer sourceRenderer in instance.GetComponentsInChildren<Renderer>())
                    {
                        var materials = new Material[sourceRenderer.sharedMaterials.Length];
                        for (int slot = 0; slot < materials.Length; slot++)
                            materials[slot] = _material;
                        sourceRenderer.sharedMaterials = materials;
                    }
                }

                Undo.RegisterCreatedObjectUndo(instance, "Paint Grass");
            }
        }

        [MenuItem("Market/Debug/Fix Painted Grass Materials")]
        public static void FixPaintedGrassMaterials()
        {
            GameObject container = GameObject.Find(ContainerName);
            if (container == null)
            {
                Debug.LogWarning($"[GrassScatterBrush] No '{ContainerName}' object in the active scene.");
                return;
            }

            var material = AssetDatabase.LoadAssetAtPath<Material>(GrassMaterialPath);
            if (material == null)
            {
                Debug.LogError($"[GrassScatterBrush] Could not load material at {GrassMaterialPath}.");
                return;
            }

            int fixedCount = 0;
            foreach (Renderer renderer in container.GetComponentsInChildren<Renderer>(true))
            {
                Undo.RecordObject(renderer, "Fix Painted Grass Material");
                var materials = new Material[renderer.sharedMaterials.Length];
                for (int slot = 0; slot < materials.Length; slot++)
                    materials[slot] = material;
                renderer.sharedMaterials = materials;
                fixedCount++;
            }

            EditorSceneManager.MarkSceneDirty(container.scene);
            Debug.Log($"[GrassScatterBrush] Fixed material on {fixedCount} renderer(s) under '{ContainerName}'.");
        }

        /// <summary>Palette of grass sources: every painted instance picks one at random.</summary>
        private void DrawSourceList()
        {
            EditorGUILayout.LabelField("Grass Sources", EditorStyles.boldLabel);
            for (int index = 0; index < _sources.Count; index++)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    _sources[index] = (GameObject)EditorGUILayout.ObjectField(
                        $"Grass {index + 1}",
                        _sources[index],
                        typeof(GameObject),
                        true);
                    if (!GUILayout.Button("-", GUILayout.Width(24f)))
                        continue;

                    _sources.RemoveAt(index);
                    return;
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Add slot"))
                    _sources.Add(null);
                if (GUILayout.Button("Reload grass cards"))
                {
                    _sources.Clear();
                    LoadDefaultSources();
                }
            }

            if (!HasSource())
            {
                EditorGUILayout.HelpBox(
                    "Assign at least one grass source (prefab, model, or scene object).",
                    MessageType.Warning);
            }
        }

        private bool HasSource()
        {
            for (int index = 0; index < _sources.Count; index++)
            {
                if (_sources[index] != null)
                    return true;
            }

            return false;
        }

        private GameObject PickSource()
        {
            if (!HasSource())
                return null;

            for (int attempt = 0; attempt < 8; attempt++)
            {
                GameObject candidate = _sources[Random.Range(0, _sources.Count)];
                if (candidate != null)
                    return candidate;
            }

            for (int index = 0; index < _sources.Count; index++)
            {
                if (_sources[index] != null)
                    return _sources[index];
            }

            return null;
        }

        private static Transform GetOrCreateContainer()
        {
            GameObject existing = GameObject.Find(ContainerName);
            if (existing != null)
                return existing.transform;

            var go = new GameObject(ContainerName);
            Undo.RegisterCreatedObjectUndo(go, "Create Grass Container");
            return go.transform;
        }
    }
}
