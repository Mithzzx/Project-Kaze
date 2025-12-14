# 🌿 GPU Instanced Grass Renderer

A high-performance, procedurally generated grass rendering system built in Unity (URP). This project demonstrates advanced graphics programming techniques including Compute Shaders, GPU Instancing, and Indirect Drawing to render millions of interactive grass blades with minimal CPU overhead.

https://github.com/user-attachments/assets/Screen%20Recording.MP4

<video src="Grass/Documentation/Screen%20Recording.MP4" controls width="100%"></video>

---

## 🖥️ Technical Core

- **Procedural Indirect Instancing**: Uses `Graphics.DrawMeshInstancedIndirect` to render millions of blades with zero CPU overhead for position management.

- **Compute Shader Culling**:
  - **Frustum Culling**: Blades outside the camera view are discarded before rendering.
  - **Distance LOD**: Blades transition from High-Poly (Bezier) to Low-Poly (Triangle) meshes based on distance buffers.

- **Terrain Integration**: Automatically samples Terrain Heightmaps and Color Textures for perfect placement and blending.

---

## 🎨 Visuals & Physics

- **Bezier Blade Geometry**: Blades are not static meshes. They are bent procedurally in the Vertex Shader using Cubic Bezier Curves, allowing for infinite variation in curvature.

- **Volumetric Wind**: Two-stage wind system:
  - **Macro**: Scrolling Perlin Noise for large "rolling waves."
  - **Micro**: Sine-based flutter for individual blade vibration.

- **Stylized Lighting**:
  - **Translucency**: Simulates light passing through thin blades (Backlighting).
  - **Fake AO**: Height-based darkening at the roots for depth.
  - **Shadow Casting**: Custom ShadowCaster pass that respects vertex deformation.

---

## 🌟 Key Features

*   **Massive Scale**: Renders up to **1,000,000+** grass blades at high frame rates.
*   **GPU Driven**: All position calculation, transformation, and culling happens entirely on the GPU via Compute Shaders.
*   **Level of Detail (LOD) System**:
    *   **LOD0 (Near)**: High-fidelity mesh with Bezier curve bending and multiple segments.
    *   **LOD1 (Far)**: Optimized low-poly mesh for distant grass.
    *   **Shadow Optimization**: Distant grass does not cast shadows to save performance.

---

## 🔧 Implementation Details

### Compute Shader Generation
Instead of storing millions of GameObjects, the system uses a **Compute Shader** (`GrassCompute.compute`) to generate and manage grass data.
- **Positioning**: Blades are placed in a grid with randomized jitter and organic "clumping" noise.
- **Terrain Snapping**: The shader samples the Terrain's heightmap texture to position blades correctly on the Y-axis.

### Indirect Drawing
The system uses `Graphics.DrawMeshInstancedIndirect`. This allows the CPU to issue a single draw call for the entire grass field. The GPU reads the argument buffer (populated by the Compute Shader) to know exactly how many visible instances to draw.

### Culling & LOD Pipeline
To ensure high performance, the Compute Shader performs a culling pass every frame:
1.  **Distance Check**: Calculates squared distance to the camera (optimized).
2.  **Frustum Check**: Tests each blade against the 6 camera frustum planes.
3.  **Append Buffers**: Visible blades are appended to either the `LOD0` or `LOD1` ComputeBuffer based on distance.

### Custom Shading
The grass is rendered using a custom HLSL shader (`GrassShader.shader`) compatible with the Universal Render Pipeline (URP).
- **Bezier Bending**: The vertex shader calculates a Bezier curve for each blade to simulate bending under wind load.
- **Wind Simulation**: Uses a scrolling noise texture combined with per-blade random offsets for realistic movement.

---

## 🎮 Controls & Demo

The project includes a **Skybox Switcher** to demonstrate the grass under different lighting conditions (Day, Sunset, Night).

*   **Right Arrow**: Next Skybox/Lighting Profile
*   **Left Arrow**: Previous Skybox/Lighting Profile

---

## 📦 Requirements

*   Unity 2022.3+ (Recommended)
*   Universal Render Pipeline (URP)
*   Input System Package

---

## 🚀 Performance Optimizations

*   **Squared Distance Checks**: Avoids expensive `sqrt` operations in the shader.
*   **Early Exit Culling**: Skips frustum calculations for objects beyond the max draw distance.
*   **Shadow Culling**: Shadows are disabled for LOD1 grass to reduce shadow map rendering cost.
*   **Procedural Mesh Generation**: Grass meshes are generated via code, allowing for adjustable quality settings (segments per blade).

---

*Created by Mithun*
