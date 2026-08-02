using UnityEngine;

/// <summary>
/// Keeps the underwater volume box of the WaterWorks renderer feature aligned with this water
/// plane, so the volumetric fog starts exactly at the water surface. Put it on the object that
/// renders the SSR_Water material.
/// Hardened against missing references and redundant writes to the shared volume material
/// (the original ran a Resources.Load and a material write every single editor frame).
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(MeshRenderer))]
public class Water_Settings : MonoBehaviour
{
    private static readonly int PosId = Shader.PropertyToID("pos");
    private static readonly int BoundsId = Shader.PropertyToID("bounds");
    private static readonly int DisplacementId = Shader.PropertyToID("_Displacement_Amount");

    [Tooltip("Volume material used by the Water_Volume renderer feature. Leave empty to fall back " +
             "to the package material in Resources - but if the feature was pointed at a project " +
             "copy, this must point at the same copy or the volume box never follows the surface.")]
    [SerializeField] private Material _volumeMaterial;

    private Material _waterVolume;
    private Material _waterMaterial;
    private Vector4 _lastPos = new Vector4(float.NaN, float.NaN, float.NaN, float.NaN);

    /// <summary>Points this component at the same volume material the renderer feature uses.</summary>
    public void SetVolumeMaterial(Material volumeMaterial)
    {
        _volumeMaterial = volumeMaterial;
        _waterVolume = volumeMaterial;
        _lastPos = new Vector4(float.NaN, float.NaN, float.NaN, float.NaN);
    }

    private void OnEnable()
    {
        _waterVolume = _volumeMaterial != null
            ? _volumeMaterial
            : Resources.Load<Material>("Water_Volume");
        _waterMaterial = GetComponent<MeshRenderer>().sharedMaterial;
    }

    private void Update()
    {
        if (_waterVolume == null)
            _waterVolume = _volumeMaterial != null
                ? _volumeMaterial
                : Resources.Load<Material>("Water_Volume");
        if (_waterMaterial == null)
            _waterMaterial = GetComponent<MeshRenderer>().sharedMaterial;
        if (_waterVolume == null || _waterMaterial == null)
            return;

        float displacement = _waterMaterial.HasProperty(DisplacementId)
            ? _waterMaterial.GetFloat(DisplacementId)
            : 0f;
        float height = (_waterVolume.GetVector(BoundsId).y / -2f)
            + transform.position.y
            + (displacement / 3f);

        Vector4 pos = new Vector4(0f, height, 0f, 0f);
        if (pos == _lastPos)
            return;

        _lastPos = pos;
        _waterVolume.SetVector(PosId, pos);
    }
}
