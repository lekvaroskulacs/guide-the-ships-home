using UnityEngine;

public class Ship : MonoBehaviour
{
    [SerializeField] GameObject ocean;

    [Tooltip("How quickly the ship tilts to match the wave normal")]
    [SerializeField] float tiltSmoothing = 4f;

    [Tooltip("Fixed-point iterations used to undo the Gerstner waves' horizontal displacement")]
    [SerializeField] int heightIterations = 3;

    // Same names as in Ocean.shader
    static readonly int OceanRandomId = Shader.PropertyToID("_OceanRandom");
    static readonly int SteepnessId = Shader.PropertyToID("_Steepness");
    static readonly int WavelengthId = Shader.PropertyToID("_Wavelength");
    static readonly int SteepFactorId = Shader.PropertyToID("_SteepFactor");
    static readonly int WavelengthFactorId = Shader.PropertyToID("_WavelengthFactor");
    static readonly int NumWavesId = Shader.PropertyToID("_NumWaves");
    static readonly int WaveDragId = Shader.PropertyToID("_WaveDrag");
    static readonly int MinVertexWavelengthId = Shader.PropertyToID("_MinVertexWavelength");
    static readonly int WindAngleId = Shader.PropertyToID("_WindAngle");
    static readonly int WindSpreadId = Shader.PropertyToID("_WindSpread");
    static readonly int SpeedId = Shader.PropertyToID("_Speed");

    Material material;
    float heading;

    float height;
    Vector3 normal;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        material = ocean.GetComponent<MeshRenderer>().sharedMaterial;
        heading = transform.eulerAngles.y;
    }

    // Update is called once per frame
    void Update()
    {
        SampleWaterHeightNormal(transform.position, out height, out normal);

        Vector3 position = transform.position;
        position.y = height;
        transform.position = position;

        Quaternion target = Quaternion.FromToRotation(Vector3.up, normal) * Quaternion.Euler(0f, heading, 0f);
        transform.rotation = Quaternion.Slerp(transform.rotation, target, 1f - Mathf.Exp(-tiltSmoothing * Time.deltaTime));
    }

    void SampleWaterHeightNormal(Vector3 worldPos, out float height, out Vector3 normal)
    {
        Vector4[] random = Shader.GetGlobalVectorArray(OceanRandomId);
        float minWavelength = material.GetFloat(MinVertexWavelengthId);

        // Gerstner waves move grid points sideways, so the point rendered above worldPos
        // came from a different grid position. Iterate towards the grid point that lands on it.
        Vector2 target = new Vector2(worldPos.x, worldPos.z);
        Vector2 grid = target;
        Vector3 displacement = Vector3.zero;
        Vector3 tangent = Vector3.right;
        Vector3 binormal = Vector3.forward;
        for (int i = 0; i < heightIterations; i++)
        {
            displacement = GerstnerWave(grid, minWavelength, random, out tangent, out binormal);
            grid += target - (grid + new Vector2(displacement.x, displacement.z));
        }

        height = ocean.transform.position.y + displacement.y;
        normal = Vector3.Cross(binormal, tangent).normalized;
    }

    // C# port of GerstnerWave in Ocean.shader; keep the two in sync
    Vector3 GerstnerWave(Vector2 p, float minWavelength, Vector4[] random, out Vector3 tangent, out Vector3 binormal)
    {
        tangent = Vector3.right;
        binormal = Vector3.forward;
        Vector3 displacement = Vector3.zero;
        if (random == null || random.Length == 0)
            return displacement;

        int numWaves = Mathf.Min((int)material.GetFloat(NumWavesId), random.Length);
        float steepFactor = material.GetFloat(SteepFactorId);
        float wavelengthFactor = material.GetFloat(WavelengthFactorId);
        float waveDrag = material.GetFloat(WaveDragId);
        float windAngle = material.GetFloat(WindAngleId);
        float windSpread = material.GetFloat(WindSpreadId);
        float speed = material.GetFloat(SpeedId);

        // geometric series: s0 * (1 - r^n) / (1 - r) = _Steepness
        float steepness = material.GetFloat(SteepnessId) * (1 - steepFactor) / (1 - Mathf.Pow(steepFactor, numWaves));
        float wavelength = material.GetFloat(WavelengthId);
        // Matches _Time.y in play mode
        float time = Time.timeSinceLevelLoad;
        Vector2 samplePos = p;

        for (int i = 0; i < numWaves; i++)
        {
            float k = 2f * Mathf.PI / wavelength;

            float c = Mathf.Sqrt(9.8f / k) * speed;
            float angle = (windAngle + (random[i].x * 2 - 1) * windSpread) * Mathf.Deg2Rad;
            Vector2 d = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            float f = k * (Vector2.Dot(d, samplePos) - c * time);
            float a = steepness / k;

            float s = Mathf.Sin(f);
            float co = Mathf.Cos(f);

            tangent += new Vector3(-d.x * d.x * steepness * s, d.x * steepness * co, -d.x * d.y * steepness * s);
            binormal += new Vector3(-d.x * d.y * steepness * s, d.y * steepness * co, -d.y * d.y * steepness * s);

            float fade = Mathf.Clamp01((wavelength - minWavelength) / Mathf.Max(minWavelength, 1e-4f));
            displacement += new Vector3(d.x * a * co, a * s, d.y * a * co) * fade;

            samplePos -= d * (steepness * co * a * waveDrag);

            steepness *= steepFactor;
            wavelength *= wavelengthFactor;
        }

        return displacement;
    }
}
