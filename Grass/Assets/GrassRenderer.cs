using UnityEngine;

public class GrassRenderer : MonoBehaviour
{
    [Header("Settings")]
    public Mesh grassMesh;             // Assign a Quad or Grass Blade mesh here
    public Material grassMaterial;     // Assign your "IndirectGrassMat" here
    public int gridSize = 100;         // 100x100 = 10,000 blades
    public float spacing = 0.5f;       // Distance between blades
    public float scaleY = 1.0f;        // Height of the grass

    // GPU Data
    private ComputeBuffer positionBuffer;
    private ComputeBuffer argsBuffer;
    private uint[] args = new uint[5] { 0, 0, 0, 0, 0 };
    private Bounds renderBounds;

    void Start()
    {
        InitializeBuffers();
    }

    void InitializeBuffers()
    {
        // 1. Calculate how many blades we need
        int instanceCount = gridSize * gridSize;

        // 2. Create the buffer to hold positions (Stride 64 = 4x4 Matrix size in bytes)
        // If this is null or wrong, the shader gets no data.
        if (instanceCount <= 0) return;
        
        positionBuffer = new ComputeBuffer(instanceCount, 64);
        Matrix4x4[] matrixData = new Matrix4x4[instanceCount];

        // 3. Fill the array with positions
        int i = 0;
        for (int x = 0; x < gridSize; x++)
        {
            for (int z = 0; z < gridSize; z++)
            {
                // Calculate position centered around (0,0,0)
                Vector3 position = new Vector3(x * spacing, 0, z * spacing);
                
                // You can add random rotation here later if you want
                Quaternion rotation = Quaternion.identity;
                Vector3 scale = new Vector3(1, scaleY, 1);

                matrixData[i] = Matrix4x4.TRS(position, rotation, scale);
                i++;
            }
        }

        // 4. Send the position data to the GPU
        positionBuffer.SetData(matrixData);
        
        // CRITICAL: Tell the material to use this specific buffer
        // The string "_PositionBuffer" MUST match the variable name in your HLSL file
        grassMaterial.SetBuffer("_PositionBuffer", positionBuffer);

        // 5. Setup the Indirect Arguments (The instructions for the GPU)
        argsBuffer = new ComputeBuffer(1, args.Length * sizeof(uint), ComputeBufferType.IndirectArguments);
        
        if (grassMesh != null)
        {
            args[0] = (uint)grassMesh.GetIndexCount(0); // How many vertices per blade?
            args[1] = (uint)instanceCount;             // How many blades? (IF THIS IS 1, YOU SEE 1 GRASS)
            args[2] = (uint)grassMesh.GetIndexStart(0);
            args[3] = (uint)grassMesh.GetBaseVertex(0);
            args[4] = 0;
        }
        
        argsBuffer.SetData(args);

        // 6. Create a huge bounding box so Unity doesn't stop rendering it
        renderBounds = new Bounds(Vector3.zero, new Vector3(10000, 10000, 10000));
    }

    void Update()
    {
        // Draw the mesh using the buffers
        if (positionBuffer != null && argsBuffer != null)
        {
            Graphics.DrawMeshInstancedIndirect(grassMesh, 0, grassMaterial, renderBounds, argsBuffer);
        }
    }

    void OnDisable()
    {
        // Clean up memory to avoid crashes/leaks
        if (positionBuffer != null) positionBuffer.Release();
        if (argsBuffer != null) argsBuffer.Release();
        positionBuffer = null;
        argsBuffer = null;
    }
}