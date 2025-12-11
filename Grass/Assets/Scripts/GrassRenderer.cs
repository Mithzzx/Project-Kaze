using UnityEngine;

/// <summary>
/// GPU Instanced Grass Renderer
/// Renders up to 1 million grass blades using compute shaders and GPU instancing
/// </summary>
public class GrassRenderer : MonoBehaviour
{
    [Header("Grass Settings")]
    [SerializeField] private int grassCount = 1000000; // 1 million grass blades
    [SerializeField] private float terrainSize = 100f;
    [SerializeField] private float minGrassHeight = 0.3f;
    [SerializeField] private float maxGrassHeight = 1.0f;

    [Header("Generation Settings")]
    [SerializeField] private float clumpScale = 0.05f;
    [SerializeField] private float clumpStrength = 0.8f;
    [SerializeField] private float heightScale = 0.05f;
    [SerializeField] private float heightFactor = 0.5f;

    [Header("References")]
    [SerializeField] private ComputeShader computeShader;
    [SerializeField] private Material grassMaterial;
    [SerializeField] private Mesh grassMesh;

    [Header("Rendering")]
    [SerializeField] private bool castShadows = true;
    [SerializeField] private bool receiveShadows = true;

    // Compute buffer to hold grass data
    private ComputeBuffer grassDataBuffer;
    
    // Indirect arguments buffer for DrawMeshInstancedIndirect
    private ComputeBuffer argsBuffer;
    
    // Grass data structure - must match compute shader
    private struct GrassData
    {
        public Vector3 position;
        public float height;
        public Vector2 facing;
        public float windPhase;
        
        // Size in bytes: 3*4 + 4 + 2*4 + 4 = 24 bytes
        public static int Size => sizeof(float) * 7;
    }

    private Bounds renderBounds;
    private int kernelIndex;
    private uint threadGroupSize;

    private void Start()
    {
        // Validate references
        if (computeShader == null || grassMaterial == null)
        {
            Debug.LogError("GrassRenderer: Missing compute shader or material reference!");
            enabled = false;
            return;
        }

        // Create grass mesh if not assigned
        if (grassMesh == null)
        {
            grassMesh = CreateGrassBladeMesh();
        }

        InitializeBuffers();
        DispatchComputeShader();
    }

    private void InitializeBuffers()
    {
        // Calculate grass per row (square root for grid)
        int grassPerRow = Mathf.CeilToInt(Mathf.Sqrt(grassCount));
        
        // Adjust grass count to be a perfect square for cleaner grid
        grassCount = grassPerRow * grassPerRow;
        
        Debug.Log($"Initializing grass renderer with {grassCount:N0} blades ({grassPerRow}x{grassPerRow} grid)");

        // Create grass data buffer
        grassDataBuffer = new ComputeBuffer(grassCount, GrassData.Size);

        // Create indirect arguments buffer
        // Args: [indexCount, instanceCount, indexStart, baseVertex, instanceStart]
        uint[] args = new uint[5];
        args[0] = grassMesh.GetIndexCount(0);
        args[1] = (uint)grassCount;
        args[2] = grassMesh.GetIndexStart(0);
        args[3] = grassMesh.GetBaseVertex(0);
        args[4] = 0;

        argsBuffer = new ComputeBuffer(1, args.Length * sizeof(uint), ComputeBufferType.IndirectArguments);
        argsBuffer.SetData(args);

        // Set render bounds (large enough to contain all grass)
        renderBounds = new Bounds(Vector3.zero, Vector3.one * terrainSize * 2);

        // Get compute shader kernel
        kernelIndex = computeShader.FindKernel("CSMain");
        computeShader.GetKernelThreadGroupSizes(kernelIndex, out threadGroupSize, out _, out _);
    }

    private void DispatchComputeShader()
    {
        int grassPerRow = Mathf.CeilToInt(Mathf.Sqrt(grassCount));

        // Set compute shader parameters
        computeShader.SetBuffer(kernelIndex, "grassDataBuffer", grassDataBuffer);
        computeShader.SetFloat("_TerrainSize", terrainSize);
        computeShader.SetFloat("_MinHeight", minGrassHeight);
        computeShader.SetFloat("_MaxHeight", maxGrassHeight);
        computeShader.SetFloat("_Time", Time.time);
        computeShader.SetInt("_GrassCount", grassCount);
        computeShader.SetInt("_GrassPerRow", grassPerRow);

        // New Parameters
        computeShader.SetFloat("_ClumpScale", clumpScale);
        computeShader.SetFloat("_ClumpStrength", clumpStrength);
        computeShader.SetFloat("_HeightScale", heightScale);
        computeShader.SetFloat("_HeightFactor", heightFactor);

        // Calculate thread groups needed
        int threadGroups = Mathf.CeilToInt((float)grassCount / threadGroupSize);
        
        // Dispatch compute shader
        computeShader.Dispatch(kernelIndex, threadGroups, 1, 1);

        // Set material buffer
        grassMaterial.SetBuffer("_GrassDataBuffer", grassDataBuffer);
        
        Debug.Log($"Compute shader dispatched with {threadGroups} thread groups");
    }

    private void Update()
    {
        // Render grass using GPU instancing
        if (grassDataBuffer != null && argsBuffer != null)
        {
            Graphics.DrawMeshInstancedIndirect(
                grassMesh,
                0, // submesh index
                grassMaterial,
                renderBounds,
                argsBuffer,
                0, // args offset
                null, // property block
                castShadows ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off,
                receiveShadows
            );
        }
    }

    /// <summary>
    /// Creates a simple grass blade mesh (quad that's thin and tall)
    /// </summary>
    private Mesh CreateGrassBladeMesh()
    {
        Mesh mesh = new Mesh();
        mesh.name = "GrassBlade";

        // Create a simple quad for grass blade
        float width = 0.05f;
        float height = 1f; // Will be scaled by grass height

        // Vertices - a simple quad with 4 vertices
        Vector3[] vertices = new Vector3[]
        {
            new Vector3(-width * 0.5f, 0, 0),      // Bottom left
            new Vector3(width * 0.5f, 0, 0),       // Bottom right
            new Vector3(-width * 0.3f, height * 0.5f, 0),  // Middle left (slightly narrower)
            new Vector3(width * 0.3f, height * 0.5f, 0),   // Middle right
            new Vector3(0, height, 0)              // Top (point)
        };

        // UVs
        Vector2[] uvs = new Vector2[]
        {
            new Vector2(0, 0),
            new Vector2(1, 0),
            new Vector2(0.2f, 0.5f),
            new Vector2(0.8f, 0.5f),
            new Vector2(0.5f, 1)
        };

        // Normals (facing forward)
        Vector3[] normals = new Vector3[]
        {
            Vector3.forward,
            Vector3.forward,
            Vector3.forward,
            Vector3.forward,
            Vector3.forward
        };

        // Triangles
        int[] triangles = new int[]
        {
            0, 2, 1,  // Bottom-left triangle
            1, 2, 3,  // Bottom-right triangle
            2, 4, 3   // Top triangle
        };

        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.normals = normals;
        mesh.triangles = triangles;
        
        mesh.RecalculateBounds();
        
        return mesh;
    }

    private void OnDestroy()
    {
        // Clean up compute buffers
        grassDataBuffer?.Release();
        argsBuffer?.Release();
    }

    private void OnDisable()
    {
        // Clean up compute buffers
        grassDataBuffer?.Release();
        argsBuffer?.Release();
    }

    // Editor helper to regenerate grass
    [ContextMenu("Regenerate Grass")]
    private void RegenerateGrass()
    {
        if (Application.isPlaying)
        {
            OnDestroy();
            InitializeBuffers();
            DispatchComputeShader();
        }
    }

    // Gizmos for visualization in editor
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0, 1, 0, 0.2f);
        Gizmos.DrawWireCube(Vector3.zero, new Vector3(terrainSize, maxGrassHeight, terrainSize));
    }
}
