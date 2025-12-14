using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;

/// <summary>
/// Hi-Z Occlusion Culling Renderer Feature for Unity 6 URP
/// Uses Render Graph API properly for GPU-driven occlusion culling
/// Based on GPU-Driven Rendering Pipelines (SIGGRAPH 2015)
/// </summary>
public class HiZRendererFeature : ScriptableRendererFeature
{
    [System.Serializable]
    public class HiZSettings
    {
        public ComputeShader hiZComputeShader;
        [Tooltip("Global shader property name for the Hi-Z texture")]
        public string textureGlobalName = "_HiZTexture";
        public bool debugMode = false;
    }
    
    public HiZSettings settings = new HiZSettings();
    
    private HiZRenderPass hiZPass;
    
    // Static reference for grass renderer to access
    public static RenderTexture HiZTexture { get; private set; }
    public static int MipLevels { get; private set; }
    public static bool IsValid { get; private set; }
    public static Matrix4x4 ViewProjectionMatrix { get; private set; }
    public static Vector2 ScreenSize { get; private set; }
    public static int HiZSize { get; private set; }
    
    public override void Create()
    {
        if (settings.hiZComputeShader == null)
        {
            Debug.LogWarning("[Hi-Z] Compute shader not assigned in HiZRendererFeature!");
            return;
        }
        
        hiZPass = new HiZRenderPass(settings);
        // Inject AFTER opaque/skybox so the depth buffer is fully populated
        hiZPass.renderPassEvent = RenderPassEvent.AfterRenderingSkybox;
        
        if (settings.debugMode)
            Debug.Log("[Hi-Z] Feature Created");
    }
    
    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (settings.hiZComputeShader == null)
            return;
            
        // Only run for game/scene cameras
        if (renderingData.cameraData.cameraType != CameraType.Game && 
            renderingData.cameraData.cameraType != CameraType.SceneView)
            return;
            
        renderer.EnqueuePass(hiZPass);
    }
    
    public static void SetHiZData(RenderTexture texture, int size, int mipLevels, Matrix4x4 viewProj, Vector2 screenSize)
    {
        HiZTexture = texture;
        HiZSize = size;
        MipLevels = mipLevels;
        ViewProjectionMatrix = viewProj;
        ScreenSize = screenSize;
        IsValid = texture != null && mipLevels > 0;
    }
    
    protected override void Dispose(bool disposing)
    {
        hiZPass?.Cleanup();
    }
    
    // ========== RENDER PASS ==========
    class HiZRenderPass : ScriptableRenderPass
    {
        private HiZSettings m_Settings;
        private int m_KernelCopy;
        private int m_KernelReduce;
        
        // Persistent Hi-Z texture (we keep this outside render graph for external access)
        private RenderTexture m_HiZTexture;
        private int m_CurrentSize;
        
        // Empty pass data class for render graph
        private class PassData { }
        
        public HiZRenderPass(HiZSettings settings)
        {
            m_Settings = settings;
            profilingSampler = new ProfilingSampler("Hi-Z Generation");
            
            m_KernelCopy = settings.hiZComputeShader.FindKernel("CopyDepth");
            m_KernelReduce = settings.hiZComputeShader.FindKernel("ReduceDepth");
        }
        
        public void Cleanup()
        {
            if (m_HiZTexture != null)
            {
                m_HiZTexture.Release();
                m_HiZTexture = null;
            }
            HiZRendererFeature.SetHiZData(null, 0, 0, Matrix4x4.identity, Vector2.zero);
        }
        
        private void EnsureHiZTexture(int size)
        {
            if (m_HiZTexture != null && m_HiZTexture.width == size)
                return;
                
            if (m_HiZTexture != null)
                m_HiZTexture.Release();
            
            int mipCount = (int)Mathf.Floor(Mathf.Log(size, 2)) + 1;
            
            var desc = new RenderTextureDescriptor(size, size, RenderTextureFormat.RFloat, 0);
            desc.msaaSamples = 1;
            desc.useMipMap = true;
            desc.autoGenerateMips = false;
            desc.enableRandomWrite = true;
            desc.sRGB = false;
            desc.mipCount = mipCount;
            
            m_HiZTexture = new RenderTexture(desc);
            m_HiZTexture.filterMode = FilterMode.Point;
            m_HiZTexture.wrapMode = TextureWrapMode.Clamp;
            m_HiZTexture.name = "Hi-Z Pyramid";
            m_HiZTexture.Create();
            
            m_CurrentSize = size;
            
            if (m_Settings.debugMode)
                Debug.Log($"[Hi-Z] Texture created: {size}x{size}, {mipCount} mip levels");
        }
        
        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            // Get URP Resources
            UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
            UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
            
            TextureHandle cameraDepth = resourceData.cameraDepthTexture;
            
            // Validation: Ensure depth exists
            if (!cameraDepth.IsValid())
            {
                if (m_Settings.debugMode)
                    Debug.LogWarning("[Hi-Z] Camera depth texture is not valid!");
                return;
            }
            
            // Calculate POT size
            var camera = cameraData.camera;
            int size = Mathf.NextPowerOfTwo(Mathf.Max(camera.pixelWidth, camera.pixelHeight));
            int mipLevels = (int)Mathf.Floor(Mathf.Log(size, 2));
            
            // Ensure our persistent texture exists
            EnsureHiZTexture(size);
            
            if (m_HiZTexture == null)
                return;
            
            // Use unsafe pass to work with persistent RenderTexture
            using (var builder = renderGraph.AddUnsafePass<PassData>("Hi-Z Generation", out var passData, profilingSampler))
            {
                // Declare we're reading the camera depth
                builder.UseTexture(cameraDepth, AccessFlags.Read);
                
                // Capture all values needed in lambda
                int capturedSize = size;
                int capturedMipLevels = mipLevels;
                ComputeShader cs = m_Settings.hiZComputeShader;
                int copyKernel = m_KernelCopy;
                int reduceKernel = m_KernelReduce;
                RenderTexture hiZTex = m_HiZTexture;
                Camera cam = camera;
                string globalName = m_Settings.textureGlobalName;
                
                builder.SetRenderFunc((PassData data, UnsafeGraphContext ctx) =>
                {
                    // Get native command buffer for full API access
                    CommandBuffer cmd = CommandBufferHelpers.GetNativeCommandBuffer(ctx.cmd);
                    
                    // --------------------------------------------------
                    // PASS 1: COPY (Depth -> HiZ Mip 0)
                    // --------------------------------------------------
                    int groups = Mathf.CeilToInt(capturedSize / 8.0f);
                    
                    cmd.SetComputeTextureParam(cs, copyKernel, "_InputDepth", cameraDepth);
                    cmd.SetComputeTextureParam(cs, copyKernel, "_OutputHiZ", hiZTex, 0);
                    cmd.SetComputeVectorParam(cs, "_OutputSize", new Vector4(capturedSize, capturedSize, 1f/capturedSize, 1f/capturedSize));
                    
                    cmd.DispatchCompute(cs, copyKernel, groups, groups, 1);
                    
                    // --------------------------------------------------
                    // PASS 2: REDUCE (Mip 0 -> Mip N)
                    // --------------------------------------------------
                    for (int i = 0; i < capturedMipLevels; i++)
                    {
                        int srcMip = i;
                        int dstMip = i + 1;
                        int dstSize = capturedSize >> dstMip;
                        
                        if (dstSize < 1) break;
                        
                        int mipGroups = Mathf.CeilToInt(dstSize / 8.0f);
                        
                        cmd.SetComputeTextureParam(cs, reduceKernel, "_InputDepth", hiZTex, srcMip);
                        cmd.SetComputeTextureParam(cs, reduceKernel, "_OutputHiZ", hiZTex, dstMip);
                        cmd.SetComputeVectorParam(cs, "_OutputSize", new Vector4(dstSize, dstSize, 1f/dstSize, 1f/dstSize));
                        
                        cmd.DispatchCompute(cs, reduceKernel, Mathf.Max(1, mipGroups), Mathf.Max(1, mipGroups), 1);
                    }
                    
                    // --------------------------------------------------
                    // Set global texture for shaders
                    // --------------------------------------------------
                    cmd.SetGlobalTexture(globalName, hiZTex);
                    
                    // Update static data for GrassRenderer
                    Matrix4x4 view = cam.worldToCameraMatrix;
                    Matrix4x4 proj = GL.GetGPUProjectionMatrix(cam.projectionMatrix, false);
                    
                    HiZRendererFeature.SetHiZData(
                        hiZTex,
                        capturedSize,
                        capturedMipLevels,
                        proj * view,
                        new Vector2(cam.pixelWidth, cam.pixelHeight)
                    );
                });
            }
        }
    }
}
