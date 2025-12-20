# GrassFlow URP Feature Overview

Purpose: High-level map of the GPU grass system so readers can find the right deep-dive doc quickly.

## What This System Delivers
- GPU-only generation and rendering of 1,000,000+ blades with DrawMeshInstancedIndirect.
- Hierarchical Z (HiZ) occlusion to avoid drawing hidden blades.
- Multi-stage culling (density mask, frustum, distance thinning) feeding three LODs.
- Physically motivated wind using Bezier bending plus translucency and fake normals.
- Editor-first painting workflow with overlays, mini-map, and undo/redo.
- Debug UI and safe defaults to keep performance predictable.

## Where to Go Next
- GPU architecture: Feature_GPU_Architecture.md
- HiZ occlusion: Feature_HiZ_Occlusion.md
- LOD, distance thinning, density masks: Feature_LOD_and_Density.md
- Wind, lighting, shading: Feature_Wind_and_Shading.md
- Painting tools: Feature_Painting_and_Tools.md
- Debugging and troubleshooting: Feature_Debugging_and_Troubleshooting.md

## Key Components
- Renderer: [Assets/Scripts/GrassRenderer.cs](Assets/Scripts/GrassRenderer.cs)
- Painter/editor tooling: [Assets/Scripts/Editor/GrassPainterEditor.cs](Assets/Scripts/Editor/GrassPainterEditor.cs)
- Compute kernels: [Assets/Shaders/GrassCompute.compute](Assets/Shaders/GrassCompute.compute), [Assets/Shaders/HiZGenerator.compute](Assets/Shaders/HiZGenerator.compute)
- Material and shader: [Assets/Materials/GrassMat.mat](Assets/Materials/GrassMat.mat), [Assets/Shaders/GrassShader.shader](Assets/Shaders/GrassShader.shader)

## Default Targets
- Unity: 6.0+ URP
- Hardware: Compute shader capable (DX11/12, Metal, Vulkan)
- Scene scale: 200 x 200 m terrain, 1,000,000 blades, 60+ FPS on M2 Pro with HiZ on

## Quick Success Checklist
- Depth Texture enabled in URP Renderer Data.
- GrassRenderer attached with compute shaders, material, terrain, and camera assigned.
- Density map readable (R8 recommended) and painted where grass should live.
- Occlusion bias small but non-zero; LOD distances match your camera far plane.

## Terminology
- HiZ: Hierarchical Z-buffer used for conservative occlusion tests.
- Indirect draw: GPU-issued draw call using argument buffers, zero per-instance CPU.
- Append buffer: GPU buffer that atomically appends survivors for each LOD.
- Density mask: Texture-driven inclusion map painted in the editor.
- Distance thinning: Probabilistic far-field culling with width compensation.
