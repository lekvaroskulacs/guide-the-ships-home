using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class Ship : MonoBehaviour
{
    GameObject ocean;
    NavMeshAgent agent;

    [Tooltip("How quickly the ship tilts to match the wave normal")]
    [SerializeField] float tiltSmoothing = 4f;

    [Tooltip("Fixed-point iterations used to undo the Gerstner waves' horizontal displacement")]
    [SerializeField] int heightIterations = 3;

    [SerializeField] List<Transform> samplePoints;

    [SerializeField] int moneyGiven;

    [Tooltip("Money lost when the ship explodes on a rock")]
    [SerializeField] int explosionPenalty = 100;

    [Tooltip("Seconds the ship keeps avoiding rocks after the beam leaves it")]
    [SerializeField] float litMemory = 1.5f;

    [Tooltip("NavMesh cost of the Rocks area while lit; high enough that paths go around")]
    [SerializeField] float litRockCost = 1000f;

    [Tooltip("Agent speed right after spawning")]
    [SerializeField] float spawnSpeed = 0.1f;

    [Tooltip("Seconds the ship moves at spawnSpeed before switching to its normal speed")]
    [SerializeField] float spawnSlowDuration = 1f;

    [Tooltip("Optional effect spawned when the ship hits a rock")]
    [SerializeField] GameObject explosionPrefab;

    // Ships register themselves so the light beam can test them without searching the scene
    public static readonly List<Ship> All = new List<Ship>();

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

    Player player;

    Vector3 destination;
    int rocksArea;
    float litTimer;
    bool isLit;

    void OnEnable()
    {
        All.Add(this);
    }

    void OnDisable()
    {
        All.Remove(this);
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        heading = transform.eulerAngles.y;
        ocean = GameObject.FindWithTag("Ocean");
        material = ocean.GetComponent<MeshRenderer>().sharedMaterial;

        agent = GetComponent<NavMeshAgent>();
        agent.updatePosition = false;
        agent.updateRotation = false;
        StartCoroutine(SlowStart());

        // Ships start in the dark: rocks cost the same as open water, so they sail straight at them
        rocksArea = NavMesh.GetAreaFromName("Rocks");
        if (rocksArea < 0)
            Debug.LogWarning("No NavMesh area named 'Rocks'; ships won't avoid rocks when lit.");
        else
            agent.SetAreaCost(rocksArea, 1f);

        GameObject lighthouse = GameObject.FindWithTag("Lighthouse");
        if (lighthouse != null)
        {
            destination = lighthouse.transform.position;
            agent.SetDestination(destination);
        }

        player = GameObject.FindWithTag("Player").GetComponent<Player>();
    }

    System.Collections.IEnumerator SlowStart()
    {
        float normalSpeed = agent.speed;
        agent.speed = spawnSpeed;
        yield return new WaitForSeconds(spawnSlowDuration);
        agent.speed = normalSpeed;
    }

    // Called every frame by LightBeam while the ship is inside the beam
    public void SetLit()
    {
        litTimer = litMemory;
    }

    // Repath only when the lit state actually changes, since cost changes don't repath on their own
    void UpdateLitState()
    {
        litTimer -= Time.deltaTime;
        bool lit = litTimer > 0f;
        if (lit == isLit)
            return;

        isLit = lit;
        if (rocksArea >= 0)
            agent.SetAreaCost(rocksArea, isLit ? litRockCost : 1f);
        agent.SetDestination(destination);
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Rock"))
            Explode();
    }

    void Explode()
    {
        if (explosionPrefab != null)
            Instantiate(explosionPrefab, transform.position, Quaternion.identity);
        player.IncreaseMoney(-explosionPenalty);
        Destroy(gameObject);
    }

    // Update is called once per frame
    void Update()
    {
        UpdateLitState();

        Vector3 position = agent.nextPosition;
        position.y = transform.position.y;
        transform.position = position;

        Vector3 velocity = agent.velocity;
        velocity.y = 0f;
        if (velocity.sqrMagnitude > 0.01f)
        {
            float targetHeading = Mathf.Atan2(velocity.x, velocity.z) * Mathf.Rad2Deg;
            heading = Mathf.MoveTowardsAngle(heading, targetHeading, agent.angularSpeed * Time.deltaTime);
        }

        Vector3 sumNormal = Vector3.zero;
        foreach (var point in samplePoints)
        {
            SampleWaterHeightNormal(point.position, out _, out normal);
            sumNormal += normal;
        }

        SampleWaterHeightNormal(transform.position, out height, out normal);
        sumNormal += normal;
        normal = sumNormal.normalized;

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

    public void GoalReached()
    {
        player.IncreaseMoney(moneyGiven);
        Destroy(gameObject);
    }
}
