# 🌿 High-Performance GPU Grass System

![Unity](https://img.shields.io/badge/Unity-6000.0+-%232b2b2b?style=flat&logo=unity&logoColor=white)
![Instances](https://img.shields.io/badge/Instance_Count-10_Million+-brightgreen)
![Draw Calls](https://img.shields.io/badge/Draw_Calls-3_Indirect-blue)
![Frame Time](https://img.shields.io/badge/Frame_Time_Delta-0.1ms-orange)
![Performance](https://img.shields.io/badge/RTX4090-700+_FPS-blueviolet)

**A production-grade vegetation rendering system capable of simulating 10 million+ interactive grass blades in real-time.**

Inspired by the technical direction of *Ghost of Tsushima*, this project leverages **Indirect GPU Instancing**, **Compute Shaders**, and **Bezier-based Procedural Deformation** to achieve cinematic visuals with minimal CPU overhead.

https://github.com/user-attachments/assets/055c5fd2-947a-4bbb-a6f6-5b987e34fe91

https://github.com/user-attachments/assets/06014a60-49a3-4cea-8c40-7454d34aba38

---

## ✨ Key Features

### 1. GPU Instancing & Compute Generation
**How it works:**
Instead of using GameObjects, grass data (position, height, rotation) is generated entirely on the GPU using a **Compute Shader**. This data is stored in a `StructuredBuffer` and rendered using `DrawMeshInstancedIndirect`, bypassing the CPU overhead completely.
*   **Organic Look:** Uses Simplex Noise for natural clumping and height variation.
*   **Performance:** Handles 10,000,000+ instances with ease.

### 2. Hierarchical Z-Buffer (HiZ) Occlusion Culling
**How it works:**
To prevent overdraw, the system implements **GPU-driven Occlusion Culling**.
1.  A **HiZ Depth Pyramid** is generated every frame using a compute shader (`HiZGenerator.compute`), reducing the depth buffer down to 1x1.
2.  The grass compute shader projects each blade's bounding box to screen space.
3.  It samples the HiZ texture at the correct mip level to check if the grass is hidden behind terrain or objects.
4.  Occluded blades are discarded before they ever reach the vertex shader.



### 3. Advanced LOD & Frustum Culling
**How it works:**
*   **Frustum Culling:** The compute shader checks every grass blade against the 6 camera frustum planes. Blades outside the view are instantly culled.
*   **Distance LOD:** Visible blades are sorted into three separate `AppendStructuredBuffers` (LOD0, LOD1, LOD2) based on distance.
    *   **LOD0:** High detail (5 segments)
    *   **LOD1:** Medium detail (3 segments)
    *   **LOD2:** Low detail (1 segment, no shadows)

| Frustum Culling |
| :---: |
| <img width="1128" height="551" alt="Screenshot 2025-12-19 at 1 52 27 PM" src="https://github.com/user-attachments/assets/c6b1cf0f-6852-4ff1-a518-2d5a7b144c83" />

### 4. Interactive Wind Simulation
**How it works:**
Wind is simulated in the vertex shader using a **Bezier Curve** deformation technique.
*   **Global Wind:** Samples a scrolling noise texture (`_WindMap`) for coherent wind waves.
*   **Procedural Animation:** Calculates "Gust" and "Flutter" effects based on time and position.
*   **Physical Bending:** Blades bend physically along their facing direction, preserving their length to avoid "stretching" artifacts.

| Wind Waves | Blade Flutter |
| :---: | :---: |
| ![Wind GIF 1](gif_url_7) | ![Wind GIF 2](gif_url_8) |

### 5. Density Scaling & Painting Tools
**How it works:**
*   **Density Falloff:** To maintain performance at huge distances, the system randomly culls blades in the distance while simultaneously **widening** the remaining blades (`_WidthCompensation`). This keeps the visual volume consistent while reducing vertex count.
*   **Painting:** Includes a custom Editor tool that paints onto a `Texture2D`. The compute shader reads this mask to allow users to paint paths or clearings in real-time.

| Density Falloff | Painting Tool |
| :---: | :---: |
| <img width="1128" height="728" alt="Screenshot 2025-12-19 at 1 54 09 PM" src="https://github.com/user-attachments/assets/a5f61e02-e359-45e8-9675-908aeda0e37b" /> | <img width="1128" height="728" alt="Screenshot 2025-12-19 at 1 54 19 PM" src="https://github.com/user-attachments/assets/e7f60097-fc35-4ce9-b42d-4158a841e72a" />
 |





---

## 🎨 Advanced Shader Features

### Auto Mesh Generation
The system automatically generates grass blade meshes at runtime based on LOD settings.
*   **Procedural Geometry:** Instead of loading a static model, `GrassRenderer.cs` constructs the mesh vertex-by-vertex.
*   **LOD Segments:**
    *   **LOD0:** 5 vertical segments for smooth bending.
    *   **LOD1:** 3 segments for efficiency.
    *   **LOD2:** 1 segment (triangle) for distant grass.
*   **Tapering:** The mesh generation logic automatically tapers the width from base to tip for a natural blade shape.

### Fake Normals & Lighting
To achieve a fluffy, volumetric look without expensive geometry, the shader manipulates normals:
*   **Normal Rounding:** Normals are rotated around the Y-axis in the vertex shader (`RotateAroundY`). This makes flat blades catch light as if they were cylindrical, preventing them from disappearing when viewed edge-on.
*   **Fake Ambient Occlusion:** AO is calculated per-vertex based on height (`input.uv.y`). The base of the blade is darkened (`_AmbientOcclusionStrength`) to simulate self-shadowing near the ground, grounding the grass visually.

### Bezier Curve Wind System
The wind animation is not a simple rotation but a physical deformation using **Cubic Bezier Curves**.
*   **Control Points:**
    *   `P0`: Root position (fixed).
    *   `P1`: Lower control point (stiffness).
    *   `P2`: Upper control point (wind direction).
    *   `P3`: Tip position (wind + flutter).
*   **Length Preservation:** The control points are constrained to the blade's height, ensuring the grass bends naturally without stretching or shearing.
*   **Gusts & Flutter:** Combines a macro noise texture (`_WindMap`) for large waves with high-frequency sine waves for individual blade flutter.

### Shadow Casting & Translucency
*   **Shadow Casting:** A dedicated `ShadowCaster` pass ensures grass casts shadows on itself and the terrain. It uses the same procedural vertex logic to match the visible mesh perfectly.
*   **Translucency (Subsurface Scattering):**
    *   Simulates light passing through thin blades.
    *   Calculates `BackLightDir` (light coming from behind the object).
    *   Adds a glowing effect when looking towards the sun, essential for realistic vegetation lighting.

---

## � Performance Comparison

The system is optimized for massive scale. Below is a comparison between standard GameObjects and this GPU Instanced solution.

| Metric | Standard GameObjects | GPU Grass System |
| :--- | :--- | :--- |
| **Count** | < 10,000 Blades | **1,000,000+ Blades** |
| **Draw Calls** | 10,000+ | **~5 (Indirect Draws)** |
| **Memory** | High (Transform overhead) | **Low (StructuredBuffer)** |

### Visual & Performance Demo
*(Recorded on Apple Silicon M2 8-Core GPU)*

| 0 Blades (Off) | 1 Million Blades (On) |
| :---: | :---: |
| ![0 Blades](gif_url_0) | ![1M Blades](gif_url_1m) |

---

## �🛠 Technical Implementation Details

### The Compute Shader (`GrassCompute.compute`)
The heart of the system. It runs one thread per grass blade:
1.  **Generation:** Calculates world position from grid index + Noise offset.
2.  **Culling:**
    *   Checks `_DensityMap` (Painting).
    *   Checks `_FrustumPlanes` (Frustum Culling).
    *   Checks `_HiZTexture` (Occlusion Culling).
    *   Checks Distance (Density Scaling).
3.  **LOD Sorting:** Appends surviving blades to the correct LOD buffer.

### The Render Shader (`GrassShader.shader`)
A custom URP shader that handles the visual representation:
*   **Procedural Setup:** Uses `#pragma instancing_options procedural:setup` to read the `StructuredBuffer` data.
*   **Bezier Bending:** Deforms the mesh vertices along a curve defined by control points `p0` (root) to `p3` (tip).
*   **Lighting:** Custom lighting model with Translucency (Subsurface Scattering) for realistic backlighting.

---

## 📦 Installation
1.  Import the package into a URP project.
2.  Add `GrassRenderer` to an empty GameObject.
3.  Assign your Terrain and Main Camera.
4.  Press "Regenerate Grass".

---

*Created by [Your Name] - [Year]*
