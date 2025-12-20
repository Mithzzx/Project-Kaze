# Debugging and Troubleshooting

## Quick Checks
- Depth Texture enabled in URP Renderer Data when using HiZ.
- Compute shaders assigned: GrassCompute.compute and HiZGenerator.compute.
- Material assigned: GrassMat with correct shader; instancing enabled.
- Terrain size matches GrassRenderer terrainSize; bounds should enclose grass.
- densityMap readable and assigned; densityThreshold reasonable (≈0.1).

## Common Issues
- Grass missing entirely
  - Depth Texture disabled; enable it or turn off useOcclusionCulling to confirm.
  - Material or compute shader reference missing.
  - densityMap fully black; paint or fill map.

- Grass flickers on slopes
  - Increase occlusionBias slightly (0.15–0.25).
  - Verify HiZ RenderTexture uses Point filtering.

- Low FPS
  - Reduce grassCount or maxDrawDistance first.
  - Enable density scaling; lower minDensity and use widthCompensation near 1.0.
  - Turn off LOD1 shadows; shorten windDisableDistance.

- Painting not affecting runtime
  - Render In Edit Mode off; enable it during painting.
  - Texture not readable or compressed; set to R8/uncompressed and reimport.

- Popping at LOD swaps
  - Increase lod0Distance/lod1Distance slightly or raise segment counts.
  - Ensure widthCompensation not excessively high causing width jumps.

## Debug UI
- Toggle showDebugUI in [Assets/Scripts/GrassRenderer.cs](Assets/Scripts/GrassRenderer.cs).
- View per-LOD counts, HiZ presence, and bias slider at runtime.
- Avoid leaving debug readbacks on in shipping builds to prevent GPU stalls.

## Validation Steps
- Frame Debugger: confirm three indirect draws and HiZ compute dispatches before CSCull.
- Profiler: verify HiZ cost vs. vertex savings; test with useOcclusionCulling toggled.
- Scene view: animate camera fast to see if depth lag is acceptable; move to SRP pass if needed.

## Support Boundaries
- Requires compute shader capable GPU and URP depth texture.
- Tested on DX11/12, Metal, Vulkan; adjust settings for mobile targets.
