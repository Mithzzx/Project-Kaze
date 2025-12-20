# HiZ Occlusion Culling

## Goal
Cull blades hidden behind terrain or geometry using a hierarchical depth pyramid to save vertex work.

## How It Works
1) Camera renders depth; URP must have Depth Texture enabled.
2) HiZGenerator.compute copies depth to mip 0 (BlitDepth) with reverse-Z support.
3) ReduceDepth builds mips, each storing the farthest depth of a 2x2 block.
4) CSCull projects each blade to screen, selects mip from projected size, samples HiZ.
5) If blade depth is greater than sampled depth plus bias, the blade is discarded.

## Settings (GrassRenderer)
- `useOcclusionCulling`: master toggle.
- `occlusionBias`: small offset to avoid surface flicker; start at 0.1–0.2.
- `mainCamera`: camera providing depth; ensure Depth Texture is on in URP Renderer Data.

## Mip Selection
- Projected radius in pixels → mip = ceil(log2(radius)).
- Ensures conservative sampling: larger blades use coarser mips to cover their extent.

## Reverse-Z Handling
- Uses SystemInfo.usesReversedZBuffer to configure depth linearization.
- LinearEyeDepth utility inside CSCull converts sampled depth for consistent comparison.

## Performance Notes
- HiZ build cost: ~0.5 ms at 1080p on M2 Pro; typically outweighed by vertex savings.
- Disable temporarily by toggling useOcclusionCulling for A/B profiling.

## Debugging
- Enable showDebugUI in [Assets/Scripts/GrassRenderer.cs](Assets/Scripts/GrassRenderer.cs).
- Check "Depth Texture: Found"; if missing, enable Depth Texture in URP Renderer Data.
- Adjust occlusionBias upward if blades flicker on steep slopes.
- Verify mip chain by inspecting hizTexture in the Frame Debugger; filtering must be Point.

## Integration Checklist
- Depth Texture enabled in the active URP Renderer asset.
- hizComputeShader assigned to GrassRenderer.
- HiZ RenderTexture created with enableRandomWrite, useMipMap, autoGenerateMips = false, FilterMode.Point.
- VP matrix passed from camera each frame.

## Files
- [Assets/Shaders/HiZGenerator.compute](Assets/Shaders/HiZGenerator.compute)
- [Assets/Shaders/GrassCompute.compute](Assets/Shaders/GrassCompute.compute) (CSCull)
- [Assets/Scripts/GrassRenderer.cs](Assets/Scripts/GrassRenderer.cs)
