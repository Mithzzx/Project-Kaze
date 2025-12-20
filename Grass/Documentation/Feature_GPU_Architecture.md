# GPU Architecture

## Goal
Render dense grass fields with minimal CPU cost by generating, culling, and drawing entirely on the GPU.

## Pipeline
1) **Generation (CSMain in GrassCompute.compute)**
- Layout: grid with jitter plus noise clumping.
- Outputs per blade: position, height, facing vector, stiffness, wind phase, width scale.
- Buffer: `grassDataBuffer` (StructuredBuffer, 32 bytes per blade).

2) **Culling and LOD (CSCull in GrassCompute.compute)**
- Inputs: source buffer, density map, frustum planes, HiZ pyramid, camera data.
- Steps per blade: density mask check → frustum test → distance thinning + width compensation → HiZ occlusion → LOD routing to append buffers (LOD0/1/2).
- Outputs: three AppendStructuredBuffers plus counts copied into indirect args.

3) **HiZ Generation (HiZGenerator.compute)**
- Pass 1: blit camera depth to mip 0 (point-sampled, reverse-Z aware).
- Pass 2: reduce depth to mip chain storing farthest depth per 2x2 block.
- Consumer: CSCull samples mip chosen from projected blade size.

4) **Rendering (GrassShader.shader)**
- Instancing: procedural setup reads GrassData per instance.
- Deformation: Bezier wind, camera-facing tweak, normal rounding.
- Lighting: gradient color, AO gradient, translucency, optional shadows.
- Draws: three Graphics.DrawMeshInstancedIndirect calls (one per LOD).

## Buffers
- Source: `sourceGrassBuffer` (all blades).
- LOD0/1/2: append buffers for survivors.
- Args: three indirect argument buffers written via ComputeBuffer.CopyCount.
- HiZ: RenderTexture with mips, RFloat, point filter.

## Bounds and Transforms
- Render bounds: centered on renderer transform, size = terrainSize * 2.
- Procedural transform matrix built per instance from position, facing, width scale.

## Key Settings (GrassRenderer)
- Counts: grassCount, terrainSize.
- LOD: lod0Distance, lod1Distance, maxDrawDistance, segment counts per LOD.
- Culling: enableDensityScaling, minDensity, densityFalloffStart, widthCompensation, useOcclusionCulling, occlusionBias.
- Wind: windDisableDistance, cameraFacing.

## Verification Steps
- Enable showDebugUI to view per-LOD counts and HiZ status.
- In Frame Debugger, confirm three indirect draws and no per-instance CPU meshes.
- In GPU Profiler, HiZ pass should be sub-millisecond at 1080p.

## Files
- [Assets/Scripts/GrassRenderer.cs](Assets/Scripts/GrassRenderer.cs)
- [Assets/Shaders/GrassCompute.compute](Assets/Shaders/GrassCompute.compute)
- [Assets/Shaders/HiZGenerator.compute](Assets/Shaders/HiZGenerator.compute)
- [Assets/Shaders/GrassShader.shader](Assets/Shaders/GrassShader.shader)
