using UnityEngine;

/// <summary>
/// GPU Instanced Grass Renderer
/// Renders up to 1 million grass blades using compute shaders and GPU instancing
/// </summary>
[ExecuteAlways]
public class GrassRenderer : MonoBehaviour
{
    [Header("Grass Settings")]
    [SerializeField] private int grassCount = 1000000; // 1 million grass blades
    [SerializeField] private float terrainSize = 100f;
    [SerializeField] private float minGrassHeight = 0.3f;
    [SerializeField] private float maxGrassHeight = 1.0f;
    [SerializeField, Range(1, 15)] private int bladeSegments = 3;
    [SerializeField] private float lod0Distance = 30.0f;
    [SerializeField] private float maxDrawDistance = 150.0f;

    [Header("Generation Settings")]
    [SerializeField] private float clumpScale = 0.05f;
    [SerializeField] private float clumpStrength = 0.8f;
    [SerializeField] private float heightScale = 0.05f;
    [SerializeField] private float heightFactor = 0.5f;
    [SerializeField, Range(0, 1)] private float angleVariation = 1.0f;

    [Header("References")]
    [SerializeField] private ComputeShader computeShader;
    [SerializeField] private ComputeShader hizComputeShader; // Added HiZ Compute Shader
    [SerializeField] private Material grassMaterial;
    [SerializeField] private Terrain terrain; // Added Terrain reference
    

    private Mesh grassMeshLOD0;
    private Mesh grassMeshLOD1;

    [Header("Rendering")]
    [SerializeField] private bool castShadows = true;
    [SerializeField] private bool castShadowsLOD1 = false; // Optimization: Disable shadows for far grass
    [SerializeField] private bool receiveShadows = true;
    [SerializeField] private bool useOcclusionCulling = true;
    [SerializeField] private float occlusionBias = 0.1f;
    [SerializeField] private Camera mainCamera;
    [SerializeField] public bool renderInEditMode = true;

    // HiZ Variables
    private RenderTexture hizTexture;
    private int hizKernelBlit;
    private int hizKernelReduce;
    private Vector2 hizTextureSize;

    [Header("Debug")]
    [SerializeField] private bool showDebugUI = false;
    private uint[] debugArgsLOD0 = new uint[5];
    private uint[] debugArgsLOD1 = new uint[5];
    private int visibleCountLOD0;
    private int visibleCountLOD1;

    [Header("Painting")]
    [SerializeField] public Texture2D densityMap;
    [SerializeField] public float densityThreshold = 0.1f;

    // Compute buffer to hold grass data
    private ComputeBuffer sourceGrassBuffer;
    private ComputeBuffer culledGrassBufferLOD0;
    private ComputeBuffer culledGrassBufferLOD1;
    
    // Indirect arguments buffer for DrawMeshInstancedIndirect
    private ComputeBuffer argsBufferLOD0;
    private ComputeBuffer argsBufferLOD1;

    private MaterialPropertyBlock visualMPB;
    
    // Grass data structure - must match compute shader
    private struct GrassData
    {
        public Vector3 position;
        public float height;
        public Vector2 facing;
        public float windPhase;
        public float stiffness;
        
        // Size in bytes: 3*4 + 4 + 2*4 + 4 + 4 = 28 bytes
        public static int Size => sizeof(float) * 8;
    }

    private Bounds renderBounds;
    private int kernelIndex;
    private int cullKernelIndex;
    private uint threadGroupSize;
    private Plane[] cameraFrustumPlanes = new Plane[6];
    private float[] frustumPlaneFloats = new float[24]; // 6 planes * 4 floats
    private bool needsUpdate = false;

    private void OnValidate()
    {
        needsUpdate = true;
    }

    public void Refresh()
    {
        needsUpdate = true;
    }

    private void Start()
    {
        if (mainCamera == null) mainCamera = Camera.main;

        // Validate references
        if (computeShader == null || grassMaterial == null)
        {
            Debug.LogError("GrassRenderer: Missing compute shader or material reference!");
            enabled = false;
            return;
        }
        // Create grass mesh
        grassMeshLOD0 = CreateGrassBladeMesh(bladeSegments);
        grassMeshLOD1 = CreateGrassBladeMesh(1); // Low quality mesh (1 segment = 1 quad + tip)

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
        sourceGrassBuffer = new ComputeBuffer(grassCount, GrassData.Size);
        culledGrassBufferLOD0 = new ComputeBuffer(grassCount, GrassData.Size, ComputeBufferType.Append);
        culledGrassBufferLOD1 = new ComputeBuffer(grassCount, GrassData.Size, ComputeBufferType.Append);

        // Create indirect arguments buffer for LOD0
        uint[] argsLOD0 = new uint[5];
        argsLOD0[0] = grassMeshLOD0.GetIndexCount(0);
        argsLOD0[1] = 0; 
        argsLOD0[2] = grassMeshLOD0.GetIndexStart(0);
        argsLOD0[3] = grassMeshLOD0.GetBaseVertex(0);
        argsLOD0[4] = 0;

        argsBufferLOD0 = new ComputeBuffer(1, argsLOD0.Length * sizeof(uint), ComputeBufferType.IndirectArguments);
        argsBufferLOD0.SetData(argsLOD0);

        // Create indirect arguments buffer for LOD1
        uint[] argsLOD1 = new uint[5];
        argsLOD1[0] = grassMeshLOD1.GetIndexCount(0);
        argsLOD1[1] = 0; 
        argsLOD1[2] = grassMeshLOD1.GetIndexStart(0);
        argsLOD1[3] = grassMeshLOD1.GetBaseVertex(0);
        argsLOD1[4] = 0;

        argsBufferLOD1 = new ComputeBuffer(1, argsLOD1.Length * sizeof(uint), ComputeBufferType.IndirectArguments);
        argsBufferLOD1.SetData(argsLOD1);

        // Set render bounds (large enough to contain all grass)
        renderBounds = new Bounds(Vector3.zero, Vector3.one * terrainSize * 2);

        // Get compute shader kernel
        kernelIndex = computeShader.FindKernel("CSMain");
        cullKernelIndex = computeShader.FindKernel("CSCull");
        computeShader.GetKernelThreadGroupSizes(kernelIndex, out threadGroupSize, out _, out _);
    }

    private void DispatchComputeShader()
    {
        int grassPerRow = Mathf.CeilToInt(Mathf.Sqrt(grassCount));

        // Set compute shader parameters
        computeShader.SetBuffer(kernelIndex, "grassDataBuffer", sourceGrassBuffer);
        computeShader.SetVector("_ObjectPosition", transform.position); // Pass object position
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
        computeShader.SetFloat("_AngleVariation", angleVariation);

        // Terrain Integration
        if (terrain != null)
        {
            computeShader.SetTexture(kernelIndex, "_HeightMap", terrain.terrainData.heightmapTexture);
            computeShader.SetVector("_TerrainSizeData", terrain.terrainData.size);
            computeShader.SetVector("_TerrainPos", terrain.transform.position);
        }

        // Density Map Integration
        if (densityMap != null)
        {
            computeShader.SetTexture(kernelIndex, "_DensityMap", densityMap);
            computeShader.SetBool("_UseDensityMap", true);
            computeShader.SetFloat("_DensityThreshold", densityThreshold);
        }
        else
        {
            computeShader.SetBool("_UseDensityMap", false);
        }

        // Calculate thread groups needed
        int threadGroups = Mathf.CeilToInt((float)grassCount / threadGroupSize);
        
        // Dispatch compute shader
        computeShader.Dispatch(kernelIndex, threadGroups, 1, 1);

        // Set material buffer (initially source, but will be overridden by culling)
        grassMaterial.SetBuffer("_GrassDataBuffer", sourceGrassBuffer);
        
        Debug.Log($"Compute shader dispatched with {threadGroups} thread groups");
    }

    private void GenerateHiZ(Texture depthTexture)
    {
        if (hizComputeShader == null || depthTexture == null) return;

        int width = depthTexture.width;
        int height = depthTexture.height;

        // Ensure HiZ texture matches screen size (power of two is better for mips)
        int hizWidth = Mathf.NextPowerOfTwo(width);
        int hizHeight = Mathf.NextPowerOfTwo(height);

        if (hizTexture == null || hizTexture.width != hizWidth || hizTexture.height != hizHeight)
        {
            if (hizTexture != null) hizTexture.Release();
            
            hizTexture = new RenderTexture(hizWidth, hizHeight, 0, RenderTextureFormat.RFloat);
            hizTexture.enableRandomWrite = true;
            hizTexture.useMipMap = true;
            hizTexture.autoGenerateMips = false;
            hizTexture.filterMode = FilterMode.Point;
            hizTexture.Create();
            
            hizTextureSize = new Vector2(hizWidth, hizHeight);
            
            hizKernelBlit = hizComputeShader.FindKernel("BlitDepth");
            hizKernelReduce = hizComputeShader.FindKernel("ReduceDepth");
        }

        // Clear HiZ Texture to "Near" value (1 for Rev-Z, 0 for Std-Z)
        // This ensures padding doesn't bleed "Far" values into the reduction
        // Note: RFloat is red channel.
        float clearValue = SystemInfo.usesReversedZBuffer ? 1.0f : 0.0f;
        // We can't easily clear a specific channel of a specific mip with Graphics.Clear
        // But since we overwrite the whole valid area, and we clamp reads in the shader,
        // explicit clearing is less critical IF the shader clamping is correct.
        // However, for safety, we can clear.
        // Graphics.SetRenderTarget(hizTexture);
        // GL.Clear(true, true, new Color(clearValue, 0, 0, 0));
        // But switching RTs is heavy. Let's rely on the shader clamping I just added.

        // Pass 0: Blit Depth
        hizComputeShader.SetTexture(hizKernelBlit, "_InputDepthTexture", depthTexture);
        hizComputeShader.SetTexture(hizKernelBlit, "_OutputDepthTexture", hizTexture); // Mip 0
        hizComputeShader.SetVector("_TextureSize", new Vector4(hizWidth, hizHeight, 0, 0)); // Output Size (HiZ Mip 0)
        hizComputeShader.SetVector("_InputTextureSize", new Vector4(width, height, 0, 0)); // Input Size (Screen)
        hizComputeShader.SetBool("_ReverseZ", SystemInfo.usesReversedZBuffer);
        
        int threadGroupsX = Mathf.CeilToInt(hizWidth / 8.0f);
        int threadGroupsY = Mathf.CeilToInt(hizHeight / 8.0f);
        hizComputeShader.Dispatch(hizKernelBlit, threadGroupsX, threadGroupsY, 1);

        // Pass 1..N: Reduce
        int mips = Mathf.FloorToInt(Mathf.Log(Mathf.Max(hizWidth, hizHeight), 2));
        
        for (int i = 0; i < mips; i++)
        {
            int currentWidth = Mathf.Max(1, hizWidth >> i);
            int currentHeight = Mathf.Max(1, hizHeight >> i);
            
            int nextWidth = Mathf.Max(1, hizWidth >> (i + 1));
            int nextHeight = Mathf.Max(1, hizHeight >> (i + 1));

            // Input is Mip i, Output is Mip i+1
            hizComputeShader.SetTexture(hizKernelReduce, "_InputDepthTexture", hizTexture);
            hizComputeShader.SetInt("_SourceMip", i);
            hizComputeShader.SetTexture(hizKernelReduce, "_OutputDepthTexture", hizTexture, i + 1);
            hizComputeShader.SetVector("_TextureSize", new Vector4(nextWidth, nextHeight, 0, 0)); // Output Size
            hizComputeShader.SetVector("_InputTextureSize", new Vector4(currentWidth, currentHeight, 0, 0)); // Input Size
            hizComputeShader.SetBool("_ReverseZ", SystemInfo.usesReversedZBuffer);

            threadGroupsX = Mathf.CeilToInt(nextWidth / 8.0f);
            threadGroupsY = Mathf.CeilToInt(nextHeight / 8.0f);
            hizComputeShader.Dispatch(hizKernelReduce, threadGroupsX, threadGroupsY, 1);
        }
    }

    private void Update()
    {
        // Edit Mode Rendering Check
        if (!Application.isPlaying && !renderInEditMode) return;

        if (needsUpdate && sourceGrassBuffer != null)
        {
            needsUpdate = false;
            DispatchComputeShader();
        }

        if (sourceGrassBuffer == null || argsBufferLOD0 == null || culledGrassBufferLOD0 == null) return;

        // 1. Frustum Culling
        // Reset counters
        culledGrassBufferLOD0.SetCounterValue(0);
        culledGrassBufferLOD1.SetCounterValue(0);

        // Get frustum planes
        GeometryUtility.CalculateFrustumPlanes(mainCamera, cameraFrustumPlanes);
        for (int i = 0; i < 6; i++)
        {
            Vector3 normal = cameraFrustumPlanes[i].normal;
            float distance = cameraFrustumPlanes[i].distance;
            frustumPlaneFloats[i * 4 + 0] = normal.x;
            frustumPlaneFloats[i * 4 + 1] = normal.y;
            frustumPlaneFloats[i * 4 + 2] = normal.z;
            frustumPlaneFloats[i * 4 + 3] = distance;
        }

        // Set culling parameters
        computeShader.SetBuffer(cullKernelIndex, "_SourceGrassBuffer", sourceGrassBuffer);
        computeShader.SetBuffer(cullKernelIndex, "_CulledGrassBufferLOD0", culledGrassBufferLOD0);
        computeShader.SetBuffer(cullKernelIndex, "_CulledGrassBufferLOD1", culledGrassBufferLOD1);
        computeShader.SetFloats("_FrustumPlanes", frustumPlaneFloats);
        computeShader.SetInt("_GrassCount", grassCount);
        // Optimization: Pass squared distances to avoid sqrt in shader
        computeShader.SetFloat("_LOD0DistanceSq", lod0Distance * lod0Distance);
        computeShader.SetFloat("_MaxDrawDistanceSq", maxDrawDistance * maxDrawDistance);
        computeShader.SetVector("_CameraPosition", mainCamera.transform.position);

        // Occlusion Culling
        Texture depthTexture = Shader.GetGlobalTexture("_CameraDepthTexture");
        bool canUseOcclusion = useOcclusionCulling && depthTexture != null && hizComputeShader != null;

        if (canUseOcclusion)
        {
            GenerateHiZ(depthTexture);
        }

        computeShader.SetBool("_UseOcclusionCulling", canUseOcclusion);
        if (canUseOcclusion)
        {
            computeShader.SetTexture(cullKernelIndex, "_HiZTexture", hizTexture);
            computeShader.SetVector("_HiZTextureSize", hizTextureSize);
            
            // Calculate VP Matrix
            Matrix4x4 proj = GL.GetGPUProjectionMatrix(mainCamera.projectionMatrix, false);
            Matrix4x4 vp = proj * mainCamera.worldToCameraMatrix;
            computeShader.SetMatrix("_VPMatrix", vp);
            
            computeShader.SetFloat("_OcclusionBias", occlusionBias);

            // Calculate ZBufferParams for LinearEyeDepth
            float near = mainCamera.nearClipPlane;
            float far = mainCamera.farClipPlane;
            Vector4 zBufferParams;

            // Unity uses reversed Z buffer on most modern platforms (DX11, DX12, Metal, Consoles)
            if (SystemInfo.usesReversedZBuffer)
            {
                zBufferParams = new Vector4(-1.0f + far / near, 1.0f, (-1.0f + far / near) / far, 1.0f / far);
            }
            else
            {
                zBufferParams = new Vector4(1.0f - far / near, far / near, (1.0f - far / near) / far, (far / near) / far);
            }
            computeShader.SetVector("_ZBufferParams", zBufferParams);
        }

        // Dispatch culling
        int threadGroups = Mathf.CeilToInt((float)grassCount / threadGroupSize);
        computeShader.Dispatch(cullKernelIndex, threadGroups, 1, 1);

        // Copy count to args buffers
        ComputeBuffer.CopyCount(culledGrassBufferLOD0, argsBufferLOD0, 4);
        ComputeBuffer.CopyCount(culledGrassBufferLOD1, argsBufferLOD1, 4);

        // Debug Readback (Warning: Causes GPU stall, only use for debugging)
        if (showDebugUI)
        {
            argsBufferLOD0.GetData(debugArgsLOD0);
            argsBufferLOD1.GetData(debugArgsLOD1);
            visibleCountLOD0 = (int)debugArgsLOD0[1];
            visibleCountLOD1 = (int)debugArgsLOD1[1];
        }

        // Initialize MaterialPropertyBlocks if needed
        if (visualMPB == null) visualMPB = new MaterialPropertyBlock();

        // Update bounds to follow object
        renderBounds.center = transform.position;

        // 2. Render LOD0 (High Quality)
        visualMPB.SetBuffer("_GrassDataBuffer", culledGrassBufferLOD0);
        Graphics.DrawMeshInstancedIndirect(
            grassMeshLOD0,
            0,
            grassMaterial,
            renderBounds,
            argsBufferLOD0,
            0,
            visualMPB,
            castShadows ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off,
            receiveShadows,
            0,
            null
        );

        // 3. Render LOD1 (Low Quality)
        visualMPB.SetBuffer("_GrassDataBuffer", culledGrassBufferLOD1);
        Graphics.DrawMeshInstancedIndirect(
            grassMeshLOD1,
            0,
            grassMaterial,
            renderBounds,
            argsBufferLOD1,
            0,
            visualMPB,
            (castShadows && castShadowsLOD1) ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off,
            receiveShadows,
            0,
            null
        );
    }

    /// <summary>
    /// Creates a simple grass blade mesh (quad that's thin and tall)
    /// </summary>
    private Mesh CreateGrassBladeMesh(int segments)
    {
        Mesh mesh = new Mesh();
        mesh.name = "GrassBlade";

        // Validate segments
        segments = Mathf.Max(1, segments);

        float height = 1f; // Will be scaled by grass height
        float baseWidth = 0.05f; // Fixed base width, actual width controlled by shader

        // Vertices
        // We have (segments) levels of quads, but the last one converges to a point.
        // Levels 0 to segments-1 are pairs of vertices.
        // Level segments is a single vertex (the tip).
        // Total vertices: segments * 2 + 1
        
        int numVerts = segments * 2 + 1;
        Vector3[] vertices = new Vector3[numVerts];
        Vector2[] uvs = new Vector2[numVerts];
        Vector3[] normals = new Vector3[numVerts];
        
        for (int i = 0; i < segments; i++)
        {
            float t = i / (float)segments; // 0 to almost 1
            float y = t * height;
            
            // Taper width: starts at baseWidth, goes to 0 at top
            // Using a slight curve for more organic look: width * (1 - t^2) or similar
            // Let's stick to the previous look: slightly wider at bottom
            float w = baseWidth * (1.0f - t); 
            
            // Left vertex
            vertices[i * 2] = new Vector3(-w * 0.5f, y, 0);
            uvs[i * 2] = new Vector2(0, t);
            normals[i * 2] = Vector3.forward;
            
            // Right vertex
            vertices[i * 2 + 1] = new Vector3(w * 0.5f, y, 0);
            uvs[i * 2 + 1] = new Vector2(1, t);
            normals[i * 2 + 1] = Vector3.forward;
        }
        
        // Top vertex (Tip)
        vertices[numVerts - 1] = new Vector3(0, height, 0);
        uvs[numVerts - 1] = new Vector2(0.5f, 1);
        normals[numVerts - 1] = Vector3.forward;
        
        // Triangles
        // (segments - 1) quads + 1 top triangle
        // Each quad is 2 triangles (6 indices)
        // Top triangle is 3 indices
        int numTriangles = (segments - 1) * 2 + 1;
        int[] triangles = new int[numTriangles * 3];
        
        int triIndex = 0;
        for (int i = 0; i < segments - 1; i++)
        {
            int baseIndex = i * 2;
            // Triangle 1
            triangles[triIndex++] = baseIndex;
            triangles[triIndex++] = baseIndex + 2;
            triangles[triIndex++] = baseIndex + 1;
            
            // Triangle 2
            triangles[triIndex++] = baseIndex + 1;
            triangles[triIndex++] = baseIndex + 2;
            triangles[triIndex++] = baseIndex + 3;
        }
        
        // Top triangle
        int lastBaseIndex = (segments - 1) * 2;
        triangles[triIndex++] = lastBaseIndex;
        triangles[triIndex++] = numVerts - 1; // Top tip
        triangles[triIndex++] = lastBaseIndex + 1;

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
        sourceGrassBuffer?.Release();
        culledGrassBufferLOD0?.Release();
        culledGrassBufferLOD1?.Release();
        argsBufferLOD0?.Release();
        argsBufferLOD1?.Release();
        if (hizTexture != null) hizTexture.Release();
    }

    private void OnDisable()
    {
        // Clean up compute buffers
        sourceGrassBuffer?.Release();
        culledGrassBufferLOD0?.Release();
        culledGrassBufferLOD1?.Release();
        argsBufferLOD0?.Release();
        argsBufferLOD1?.Release();
        if (hizTexture != null) hizTexture.Release();
    }

    // Editor helper to regenerate grass
    [ContextMenu("Regenerate Grass")]
    private void RegenerateGrass()
    {
        OnDestroy();
        InitializeBuffers();
        DispatchComputeShader();
    }

    // Gizmos for visualization in editor
    private void OnDrawGizmosSelected()
    {
        // Draw Bounds (Green)
        Gizmos.color = new Color(0, 1, 0, 0.5f);
        Gizmos.DrawWireCube(transform.position, new Vector3(terrainSize, maxGrassHeight, terrainSize));

        // Draw LOD Ranges around the camera (if available)
        Camera cam = mainCamera;
        if (cam == null) cam = Camera.main;

        if (cam != null)
        {
            // LOD0 Range (Yellow)
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(cam.transform.position, lod0Distance);
            
            // Max Draw Distance (Red)
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(cam.transform.position, maxDrawDistance);
        }
    }

    private void OnGUI()
    {
        if (!showDebugUI) return;

        GUILayout.BeginArea(new Rect(10, 10, 350, 600), GUI.skin.box);
        GUILayout.Label("Grass Renderer Debug", GUI.skin.button);
        GUILayout.Space(5);
        
        GUILayout.Label($"Total Grass: {grassCount:N0}");
        GUILayout.Label($"Visible LOD0: {visibleCountLOD0:N0}");
        GUILayout.Label($"Visible LOD1: {visibleCountLOD1:N0}");
        GUILayout.Label($"Total Visible: {visibleCountLOD0 + visibleCountLOD1:N0}");
        
        GUILayout.Space(10);
        GUILayout.Label("Occlusion Culling");
        
        if (hizComputeShader == null)
        {
            GUI.color = Color.red;
            GUILayout.Label("ERROR: HiZ Compute Shader missing!");
            GUILayout.Label("Assign 'HiZGenerator' in Inspector.");
            GUI.color = Color.white;
        }
        
        bool newOcclusion = GUILayout.Toggle(useOcclusionCulling, "Use Occlusion Culling");
        if (newOcclusion != useOcclusionCulling)
        {
            useOcclusionCulling = newOcclusion;
        }

        if (useOcclusionCulling)
        {
            Texture depth = Shader.GetGlobalTexture("_CameraDepthTexture");
            if (depth == null)
            {
                GUI.color = Color.red;
                GUILayout.Label("WARNING: _CameraDepthTexture is null!");
                GUILayout.Label("Enable Depth Texture in URP Asset.");
                GUI.color = Color.white;
            }
            else
            {
                GUI.color = Color.green;
                GUILayout.Label($"Depth Texture: Found ({depth.width}x{depth.height})");
                GUI.color = Color.white;
                
                // Depth Preview
                GUILayout.Label("Depth Texture Preview:");
                Rect rDepth = GUILayoutUtility.GetRect(300, 100);
                GUI.DrawTexture(rDepth, depth, ScaleMode.ScaleToFit, false);
            }
            
            if (hizTexture != null)
            {
                GUILayout.Label($"HiZ Texture: {hizTexture.width}x{hizTexture.height}");
                GUILayout.Label("HiZ Preview (Mips):");
                Rect rHiz = GUILayoutUtility.GetRect(300, 150);
                GUI.DrawTexture(rHiz, hizTexture, ScaleMode.ScaleToFit, false);
            }

            GUILayout.Label($"Bias: {occlusionBias}");
            float newBias = GUILayout.HorizontalSlider(occlusionBias, 0.0f, 1.0f);
            if (Mathf.Abs(newBias - occlusionBias) > 0.001f)
            {
                occlusionBias = newBias;
            }
        }

        GUILayout.EndArea();
    }
}
