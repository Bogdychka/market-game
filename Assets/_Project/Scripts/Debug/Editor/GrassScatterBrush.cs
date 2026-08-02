using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Market.DebugTools.Editor
{
    /// <summary>
    /// Scene-view terrain brush that scatters a random mix of grass clumps around the cursor:
    /// random variant, random yaw, random width/height, random lean, and a share of X-cross clumps
    /// so the patch never reads as a field of identical cards.
    /// Hold left mouse and drag over a TerrainCollider to paint, add Shift to erase.
    /// </summary>
    public class GrassScatterBrush : EditorWindow
    {
        private const string ContainerName = "GrassScatter";
        private const string GrassMaterialPath = "Assets/blender/Grass_1.mat";

        private static readonly string[] LegacyGrassPaths =
        {
            "Assets/blender/Grass_1.fbx",
            "Assets/blender/Grass_2.fbx",
        };

        private readonly List<GameObject> _sources = new List<GameObject>();
        private readonly List<GameObject> _crossSources = new List<GameObject>();
        private Material _material;
        private float _radius = 2f;
        private int _instancesPerStroke = 8;
        // Width and height jitter separately: uniform scaling alone keeps every clump the same
        // silhouette, just nearer or further away.
        private float _minWidth = 0.8f;
        private float _maxWidth = 1.3f;
        private float _minHeight = 0.7f;
        private float _maxHeight = 1.45f;
        // Share of clumps painted as X-crosses. A minority is enough: they fill the gaps the flat
        // cards leave when viewed edge-on, at twice the fill rate, so they are the expensive half.
        private float _crossChance = 0.35f;
        private float _maxTilt = 10f;
        private float _sink = 0.03f;
        private float _strokeInterval = 0.05f;
        private bool _alignToSlope;
        private bool _paintingEnabled;
        private bool _showCrossPalette;
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
        /// Fills both palettes from the grass card builder. Card prefabs carry their own material,
        /// so no override is needed; only the older raw FBX sources need one.
        /// </summary>
        private void LoadDefaultSources()
        {
            _sources.AddRange(GrassCardBuilder.LoadPalettePrefabs(false));
            _crossSources.AddRange(GrassCardBuilder.LoadPalettePrefabs(true));

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
            DrawSourceList("Grass Sources (single card)", _sources);

            _showCrossPalette = EditorGUILayout.Foldout(
                _showCrossPalette, $"Cross Sources (X-clumps): {_crossSources.Count}", true);
            if (_showCrossPalette)
                DrawSourceList("Cross Sources", _crossSources);

            if (GUILayout.Button("Reload grass cards"))
            {
                _sources.Clear();
                _crossSources.Clear();
                LoadDefaultSources();
            }

            _material = (Material)EditorGUILayout.ObjectField("Material Override", _material, typeof(Material), false);
            EditorGUILayout.HelpBox("Optional. Leave empty for grass card prefabs, which already carry their own material; set it for raw FBX sources that would otherwise paint with their imported gray material.", MessageType.None);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Brush", EditorStyles.boldLabel);
            _radius = EditorGUILayout.Slider("Radius", _radius, 0.25f, 15f);
            _instancesPerStroke = EditorGUILayout.IntSlider("Instances / Tick", _instancesPerStroke, 1, 40);
            _strokeInterval = EditorGUILayout.Slider("Tick Interval (s)", _strokeInterval, 0.02f, 0.5f);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Randomisation", EditorStyles.boldLabel);
            EditorGUILayout.MinMaxSlider("Width Jitter", ref _minWidth, ref _maxWidth, 0.25f, 2.5f);
            EditorGUILayout.LabelField($"  {_minWidth:0.00}x - {_maxWidth:0.00}x");
            EditorGUILayout.MinMaxSlider("Height Jitter", ref _minHeight, ref _maxHeight, 0.25f, 2.5f);
            EditorGUILayout.LabelField($"  {_minHeight:0.00}x - {_maxHeight:0.00}x");
            using (new EditorGUI.DisabledScope(_crossSources.Count == 0))
                _crossChance = EditorGUILayout.Slider("Cross Chance", _crossChance, 0f, 1f);
            _maxTilt = EditorGUILayout.Slider("Max Lean (deg)", _maxTilt, 0f, 35f);
            _sink = EditorGUILayout.Slider("Sink Into Ground", _sink, 0f, 0.25f);
            _alignToSlope = EditorGUILayout.Toggle("Align To Slope", _alignToSlope);

            EditorGUILayout.Space();
            _paintingEnabled = EditorGUILayout.ToggleLeft("Enable Painting", _paintingEnabled);
            EditorGUILayout.HelpBox("With painting enabled, hold Left Mouse and drag over the terrain in the Scene view to scatter grass. Hold Shift while dragging to erase inside the brush.", MessageType.Info);
        }

        private void OnSceneGUI(SceneView view)
        {
            // Erasing needs no palette, so only painting is gated on having one.
            if (!_paintingEnabled || (!HasSource() && !Event.current.shift))
                return;

            Event e = Event.current;
            int controlId = GUIUtility.GetControlID(FocusType.Passive);

            Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
            bool hasHit = Physics.Raycast(ray, out RaycastHit hit, 2000f) && hit.collider is TerrainCollider;

            if (hasHit)
            {
                bool erasing = e.shift;
                Handles.color = erasing
                    ? new Color(1f, 0.4f, 0.3f, 0.6f)
                    : new Color(0.2f, 1f, 0.4f, 0.6f);
                Handles.DrawWireDisc(hit.point, hit.normal, _radius);

                bool isPaintEvent = e.type is EventType.MouseDown or EventType.MouseDrag && e.button == 0 && !e.alt;
                if (isPaintEvent)
                {
                    double now = EditorApplication.timeSinceStartup;
                    if (now - _lastPaintTime >= _strokeInterval)
                    {
                        if (erasing)
                            EraseAt(hit);
                        else
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
            if (!HasSource())
                return;

            Transform parent = GetOrCreateContainer();

            for (int i = 0; i < _instancesPerStroke; i++)
            {
                bool cross = _crossSources.Count > 0 && Random.value < _crossChance;
                GameObject source = PickSource(cross ? _crossSources : _sources) ?? PickSource(_sources);
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
                // A flat card seen from behind is its own mirror image, so a full turn of yaw already
                // doubles the silhouette count for free - no negative scaling needed.
                rotation = Quaternion.AngleAxis(Random.Range(0f, 360f), localHit.normal) * rotation;
                if (_maxTilt > 0f)
                {
                    Vector3 leanAxis = Quaternion.AngleAxis(Random.Range(0f, 360f), Vector3.up) * Vector3.right;
                    rotation = Quaternion.AngleAxis(Random.Range(-_maxTilt, _maxTilt), leanAxis) * rotation;
                }

                GameObject instance = PrefabUtility.IsPartOfPrefabAsset(source)
                    ? (GameObject)PrefabUtility.InstantiatePrefab(source, parent)
                    : Instantiate(source, parent);

                float width = Random.Range(_minWidth, _maxWidth);
                float height = Random.Range(_minHeight, _maxHeight);
                // Leaning clumps lift their root off the ground; sink scales with height so a tall
                // card is buried as deep as it is tilted, instead of hovering over the terrain.
                instance.transform.SetPositionAndRotation(
                    localHit.point - localHit.normal * (_sink * height),
                    rotation);
                instance.transform.localScale = Vector3.Scale(
                    source.transform.localScale,
                    new Vector3(width, height, width));
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

        /// <summary>Removes painted clumps whose root falls inside the brush disc.</summary>
        private void EraseAt(RaycastHit centerHit)
        {
            GameObject container = GameObject.Find(ContainerName);
            if (container == null)
                return;

            float radiusSquared = _radius * _radius;
            Transform parent = container.transform;
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                Transform child = parent.GetChild(i);
                Vector3 delta = child.position - centerHit.point;
                // Horizontal distance only: on a slope the vertical gap is terrain, not brush miss.
                delta.y = 0f;
                if (delta.sqrMagnitude <= radiusSquared)
                    Undo.DestroyObjectImmediate(child.gameObject);
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
        private void DrawSourceList(string label, List<GameObject> sources)
        {
            EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
            for (int index = 0; index < sources.Count; index++)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    sources[index] = (GameObject)EditorGUILayout.ObjectField(
                        $"Grass {index + 1}",
                        sources[index],
                        typeof(GameObject),
                        true);
                    if (!GUILayout.Button("-", GUILayout.Width(24f)))
                        continue;

                    sources.RemoveAt(index);
                    return;
                }
            }

            if (GUILayout.Button("Add slot"))
                sources.Add(null);

            if (sources == _sources && !HasSource())
            {
                EditorGUILayout.HelpBox(
                    "Assign at least one grass source (prefab, model, or scene object), " +
                    "or run Market/Debug/Grass Card/2. Build Material + Clump Prefab first.",
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

        private static GameObject PickSource(List<GameObject> sources)
        {
            if (sources.Count == 0)
                return null;

            for (int attempt = 0; attempt < 8; attempt++)
            {
                GameObject candidate = sources[Random.Range(0, sources.Count)];
                if (candidate != null)
                    return candidate;
            }

            for (int index = 0; index < sources.Count; index++)
            {
                if (sources[index] != null)
                    return sources[index];
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
