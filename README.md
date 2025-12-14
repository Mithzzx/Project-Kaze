# 🌿 GPU Instanced Grass Renderer

A high-performance, procedurally generated grass rendering system built in Unity (URP). This project demonstrates advanced graphics programming techniques including Compute Shaders, GPU Instancing, and Indirect Drawing to render millions of interactive grass blades with minimal CPU overhead.

![Project Screenshot](Grass/Documentation/Screen Recording.MP4)


## 🌟 Key Features

*   **Massive Scale**: Renders up to **1,000,000+** grass blades at high frame rates.
*   **GPU Driven**: All position calculation, transformation, and culling happens entirely on the GPU via Compute Shaders.
*   **Advanced Culling**:
    *   **Frustum Culling**: Blades outside the camera view are discarded before rendering.
    *   **Distance Culling**: Blades beyond a max distance are skipped.
*   **Level of Detail (LOD) System**:
    *   **LOD0 (Near)**: High-fidelity mesh with Bezier curve bending and multiple segments.
    *   **LOD1 (Far)**: Optimized low-poly mesh for distant grass.
    *   **Shadow Optimization**: Distant grass does not cast shadows to save performance.
*   **Terrain Integration**: Automatically snaps grass to Unity Terrain heightmaps.
*   **Dynamic Wind**: Procedural wind animation with gusting, fluttering, and stiffness variation.
*   **Interactive Lighting**: Custom lighting support including translucency (subsurface scattering) and specular highlights.

## 🔧 Technical Implementation

### 1. Compute Shader Generation
Instead of storing millions of GameObjects, the system uses a **Compute Shader** (`GrassCompute.compute`) to generate and manage grass data.
- **Positioning**: Blades are placed in a grid with randomized jitter and organic "clumping" noise.
- **Terrain Snapping**: The shader samples the Terrain's heightmap texture to position blades correctly on the Y-axis.

### 2. Indirect Drawing
The system uses `Graphics.DrawMeshInstancedIndirect`. This allows the CPU to issue a single draw call for the entire grass field. The GPU reads the argument buffer (populated by the Compute Shader) to know exactly how many visible instances to draw.

### 3. Culling & LOD Pipeline
To ensure high performance, the Compute Shader performs a culling pass every frame:
1.  **Distance Check**: Calculates squared distance to the camera (optimized).
2.  **Frustum Check**: Tests each blade against the 6 camera frustum planes.
3.  **Append Buffers**: Visible blades are appended to either the `LOD0` or `LOD1` ComputeBuffer based on distance.

### 4. Custom Shading
The grass is rendered using a custom HLSL shader (`GrassShader.shader`) compatible with the Universal Render Pipeline (URP).
- **Bezier Bending**: The vertex shader calculates a Bezier curve for each blade to simulate bending under wind load.
- **Wind Simulation**: Uses a scrolling noise texture combined with per-blade random offsets for realistic movement.

## 🎮 Controls & Demo

The project includes a **Skybox Switcher** to demonstrate the grass under different lighting conditions (Day, Sunset, Night).

*   **Right Arrow**: Next Skybox/Lighting Profile
*   **Left Arrow**: Previous Skybox/Lighting Profile

## 📦 Requirements

*   Unity 2022.3+ (Recommended)
*   Universal Render Pipeline (URP)
*   Input System Package

## 🚀 Performance Optimizations

*   **Squared Distance Checks**: Avoids expensive `sqrt` operations in the shader.
*   **Early Exit Culling**: Skips frustum calculations for objects beyond the max draw distance.
*   **Shadow Culling**: Shadows are disabled for LOD1 grass to reduce shadow map rendering cost.
*   **Procedural Mesh Generation**: Grass meshes are generated via code, allowing for adjustable quality settings (segments per blade).

---
*Created by [Your Name]*
