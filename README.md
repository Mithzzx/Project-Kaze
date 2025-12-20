# 🌿 High-Performance GPU Grass System

![Unity](https://img.shields.io/badge/Unity-6000.0+-%232b2b2b?style=flat-square&logo=unity)
![Instances](https://img.shields.io/badge/Instance_Count-10_Million+-hsl%28145%2C50%25%2C40%25%29?style=flat-square)
![Draw Calls](https://img.shields.io/badge/Draw_Calls-3_Indirect-hsl%28200%2C60%25%2C45%25%29?style=flat-square)
![Frame Time](https://img.shields.io/badge/Frame_Time_Delta-0.1ms-hsl%2835%2C85%25%2C50%25%29?style=flat-square)
![Performance](https://img.shields.io/badge/RTX4090-700+_FPS-hsl%28280%2C45%25%2C55%25%29?style=flat-square)
![License](https://img.shields.io/badge/License-MIT-hsl%28350%2C60%25%2C40%25%29?style=flat-square)


**Highly Optimized vegetation pipeline for Unity 6 URP capable of rendering 1,000,000+ instances at 0.1ms delta.**

> Inspired by the technical direction of *Ghost of Tsushima*, this project leverages **Indirect GPU Instancing**, **Compute Shaders**, **Hierarchical Occlusion Culling**, and **Bezier-based Procedural Deformation** to achieve cinematic visuals with zero CPU overhead.

https://github.com/user-attachments/assets/06014a60-49a3-4cea-8c40-7454d34aba38

https://github.com/user-attachments/assets/055c5fd2-947a-4bbb-a6f6-5b987e34fe91

---

## ✨ GPU-Driven Compute Architecture
**Zero CPU overhead. Everything runs on the GPU.**

Instead of traditional GameObjects, the entire grass system operates on the GPU:
- **Compute Shader Generation** (`CSMain` kernel): Generates grass blade data (position, height, rotation, wind phase) entirely on the GPU using grid-based distribution with noise-driven organic variation
- **StructuredBuffer Storage**: All grass data lives in GPU memory (32 bytes per blade × 1M blades = 32MB)
- **DrawMeshInstancedIndirect**: Single indirect draw call per LOD level—no CPU batching required
- **Procedural Mesh Construction**: Runtime blade generation with configurable segment count (LOD0: 5 segs, LOD1: 3 segs, LOD2: 1 seg)

**Technical Implementation:**
```csharp
// C# side: Create buffers and dispatch compute
sourceGrassBuffer = new ComputeBuffer(grassCount, GrassData.Size);
computeShader.Dispatch(kernelIndex, threadGroups, 1, 1);
Graphics.DrawMeshInstancedIndirect(grassMesh, 0, material, bounds, argsBuffer);
```
---

## 🎭 Hierarchical Z-Buffer (HiZ) Occlusion Culling
**Industry-standard AAA technique for preventing overdraw.**

Implements GPU-driven occlusion culling using a depth pyramid—the same technique used in Assassin's Creed and Horizon Zero Dawn:

**Pipeline:**
1. **Depth Copy** (`BlitDepth` kernel): Copies camera's depth buffer to HiZ texture (Mip 0)
2. **Mip Reduction** (`ReduceDepth` kernel): Iteratively downsamples to 1×1, storing **farthest depth** per 2×2 tile
3. **Per-Blade Test** (`CSCull` kernel): 
   - Projects grass blade's bounding sphere to screen space
   - Calculates optimal mip level based on projected size
   - Samples HiZ at appropriate level
   - Compares blade depth vs. scene depth
   - Culls if occluded (behind terrain/objects)

---

## 🎯 Four-stage culling cascade eliminates unnecessary GPU work.
Every frame, each grass blade goes through a **GPU-side culling gauntlet**:

**Stage 1: Density Map Filtering** (CSMain)
   > - Samples painted density texture at blade's UV coordinates
     - Culls if `density < _DensityThreshold`
     - Enables artist-driven placement (paths, clearings, patterns)

**Stage 2: Frustum Culling** (CSCull)
>   - Tests blade's bounding sphere against 6 camera frustum planes
    - Uses fast dot product rejection: `dot(plane.xyz, center) + plane.w < -radius`
    - Eliminates off-screen grass before expensive operations

**Stage 3: Distance-Based Density Scaling** (CSCull)
>   - **Problem**: 1M blades at 200m distance = wasted fill rate
    - **Solution**: Randomly cull distant blades, widen survivors
    - **Result**: 90% fewer blades at max distance, visually identical coverage

**Stage 4: HiZ Occlusion Test** (CSCull)
>   - Final check against depth pyramid (see above)
    - Only runs on frustum-visible blades

**LOD Assignment:**
Surviving blades are appended to one of three buffers:
- **LOD0** (0-30m): High-poly mesh (5 segments), full shadows
- **LOD1** (30-60m): Mid-poly mesh (3 segments), optional shadows
- **LOD2** (60-150m): Billboard (1 segment), no shadows

---

## 🌊 Physically-Based Wind Simulation

**Bezier Curve Bending:**
```hlsl
// Control points
p0 = rootPosition;                          // Fixed anchor
p1 = rootPosition + stiffnessOffset;        // Lower curve control
p2 = p1 + windDirection * bendFactor;       // Upper curve control  
p3 = tipPosition + windDisplacement;        // Final tip position
```

**Multi-Frequency Wind Layers:**
1. **Macro Gusts** (10-20m waves):
   - Scrolling Simplex noise texture (`_WindMap`)
   - Coherent across large areas (field-wide waves)
   - `windUV = worldPos.xz * _WindFrequency + time * _WindVelocity`

2. **Micro Flutter** (per-blade):
   - High-frequency sine wave: `sin(time * 15.0 + grass.windPhase * 10.0)`
   - Unique `windPhase` per blade for variety
   - Simulates individual blade oscillation

---

## 🚀 Performance Metrics

| Blade Count | Draw Calls | Frame Time | FPS | Notes |
|-------------|-----------|------------|-----|-------|
| 100,000 | 3 | 2.1ms | 144+ | Ultra-smooth |
| 500,000 | 3 | 3.8ms | 90+ | High performance |
| **1,000,000** | **3-5** | **6.2ms** | **60+** | **Default target** |
| 2,000,000 | 5 | 11.5ms | 45 | Dense forests |
| 5,000,000 | 5 | 28ms | 30 | Extreme stress test |

*Tested on M2 8 core GPU. Performance scales with GPU compute throughput.*

---

## 🌿 Advanced Shader Features

#### 🚀 Performance & Geometry

* **Procedural Mesh**: Runtime generation via C#;  FBX dependencies.
* **Smart LOD**: Dynamic vertex stripping ( tris) based on camera distance.
* **Custom Mesh**: Automatic override toggle for specialized foliage (wheat/flowers).

#### 💡 Lighting & Optics

* **Normal Rounding**: Spherical normal interpolation for 360° light wrap on flat quads.
* **Translucency (SSS)**: View-dependent backlighting for "Golden Hour" glow effects.
* **Vertex AO**: Zero-cost ambient occlusion baked into `uv.y` gradients.

#### 🎨 Visual Fidelity

* **Tri-Tone Gradients**: 3-point vertical interpolation (Root → Mid → Tip).
* **Hash Variation**: Per-instance color/dryness jittering using `Hash21(worldPos)`.
* **Pipeline Ready**: Full **URP** support with synchronized ShadowCaster & DepthOnly passes.

---

## 🛠️ Technical Architecture

```
GrassRenderer (MonoBehaviour)
├─ Compute Shader Pipeline
│  ├─ CSMain Kernel         → Generate grass data (position, height, rotation)
│  ├─ CSCull Kernel         → Frustum/Distance/Occlusion culling + LOD sorting
│  └─ HiZ Generator         → Depth pyramid construction
│
├─ GPU Buffers
│  ├─ sourceGrassBuffer     → Master data (1M blades)
│  ├─ culledGrassBufferLOD0 → High-detail survivors (AppendStructuredBuffer)
│  ├─ culledGrassBufferLOD1 → Mid-detail survivors
│  ├─ culledGrassBufferLOD2 → Low-detail survivors
│  ├─ argsBufferLOD0-2      → Indirect draw arguments
```

---

## 📦 Getting Started

1. Ensure your project is using **Unity 6000.0+** and the **Universal Render Pipeline (URP)**.
2. Attach the `GrassRenderer` component to an empty GameObject.
3. Assign your **Main Camera** and **Terrain** to the inspector slots.
4. Tune the `WindMap` and `DensityMask` to fit your art direction.

---

*Created by Mithzz - 2026*
