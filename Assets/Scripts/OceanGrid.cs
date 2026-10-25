using UnityEngine;
using UnityEngine.Rendering;

// Generates a flat, evenly subdivided grid on the XZ plane for the ocean shader to displace.
[ExecuteAlways]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class OceanGrid : MonoBehaviour
{
    [Tooltip("Width and length of the grid in world units")]
    [SerializeField] Vector2 size = new Vector2(200f, 200f);

    [Tooltip("Number of quads along each side")]
    [SerializeField] Vector2Int resolution = new Vector2Int(256, 256);

    [Tooltip("Extra vertical bounds so the mesh isn't culled when waves displace it")]
    [SerializeField] float boundsPadding = 10f;

    [Tooltip("Seed for the random values the ocean shader uses for wave directions")]
    [SerializeField] int randomSeed = 1228;

    // Must match the array size declared in Ocean.shader
    const int RandomVectorCount = 32;
    static readonly int OceanRandomId = Shader.PropertyToID("_OceanRandom");

    Mesh mesh;
    bool dirty = true;

    void OnEnable()
    {
        dirty = true;
        UploadRandomValues();
    }

    void OnValidate()
    {
        size = Vector2.Max(size, new Vector2(0.01f, 0.01f));
        resolution = Vector2Int.Max(resolution, Vector2Int.one);
        resolution = Vector2Int.Min(resolution, new Vector2Int(2048, 2048));
        boundsPadding = Mathf.Max(boundsPadding, 0f);

        // Meshes can't be assigned during OnValidate, so rebuild on the next Update
        dirty = true;

        UploadRandomValues();
    }

    void UploadRandomValues()
    {
        var rand = new System.Random(randomSeed);
        var values = new Vector4[RandomVectorCount];
        for (int i = 0; i < values.Length; i++)
        {
            values[i] = new Vector4(
                (float)rand.NextDouble(),
                (float)rand.NextDouble(),
                (float)rand.NextDouble(),
                (float)rand.NextDouble());
        }
        Shader.SetGlobalVectorArray(OceanRandomId, values);
    }

    void Update()
    {
        if (dirty)
            Generate();
    }

    void OnDestroy()
    {
        if (mesh == null)
            return;

        if (Application.isPlaying)
            Destroy(mesh);
        else
            DestroyImmediate(mesh);
    }

    public void Generate()
    {
        dirty = false;

        if (mesh == null)
        {
            mesh = new Mesh { name = "Ocean Grid", hideFlags = HideFlags.DontSave };
            GetComponent<MeshFilter>().sharedMesh = mesh;
        }

        int vertsX = resolution.x + 1;
        int vertsZ = resolution.y + 1;
        var vertices = new Vector3[vertsX * vertsZ];
        var uvs = new Vector2[vertices.Length];
        var normals = new Vector3[vertices.Length];
        var triangles = new int[resolution.x * resolution.y * 6];

        for (int z = 0; z < vertsZ; z++)
        {
            for (int x = 0; x < vertsX; x++)
            {
                int i = z * vertsX + x;
                float u = (float)x / resolution.x;
                float v = (float)z / resolution.y;
                vertices[i] = new Vector3((u - 0.5f) * size.x, 0f, (v - 0.5f) * size.y);
                uvs[i] = new Vector2(u, v);
                normals[i] = Vector3.up;
            }
        }

        int t = 0;
        for (int z = 0; z < resolution.y; z++)
        {
            for (int x = 0; x < resolution.x; x++)
            {
                int i = z * vertsX + x;
                triangles[t++] = i;
                triangles[t++] = i + vertsX;
                triangles[t++] = i + 1;
                triangles[t++] = i + 1;
                triangles[t++] = i + vertsX;
                triangles[t++] = i + vertsX + 1;
            }
        }

        mesh.Clear();
        mesh.indexFormat = vertices.Length > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16;
        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.normals = normals;
        mesh.triangles = triangles;
        mesh.bounds = new Bounds(Vector3.zero, new Vector3(size.x, boundsPadding * 2f, size.y));
    }
}
