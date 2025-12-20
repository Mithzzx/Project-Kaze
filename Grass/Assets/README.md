- After Density Map: 920,000 alive (8% culled)
- After Frustum: 350,000 visible (62% culled)
- After Distance Scaling: 180,000 rendered (82% culled)
- After HiZ Occlusion: 130,000 final (87% culled)

**LOD Distribution** (at 50m distance):
- LOD0: 25,000 blades (19%)
- LOD1: 60,000 blades (46%)
- LOD2: 45,000 blades (35%)

**Memory Footprint:**
- Source Buffer: 32MB (1M × 32 bytes)
- Culled Buffers: 96MB (3 LODs × 32MB)
- Indirect Args: < 1KB
- HiZ Texture: 5MB (2048×2048 RFloat)
- **Total**: ~135MB VRAM

---

## 🛠️ Technical Architecture

### System Components

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
````markdown
# GrassFlow URP — GPU Grass Renderer

![Unity 6](https://img.shields.io/badge/Unity-6.0+-black?style=flat-square&logo=unity)
![URP](https://img.shields.io/badge/Render_Pipeline-URP-0052cc?style=flat-square)
![Compute](https://img.shields.io/badge/Tech-Compute_Shaders-2c974b?style=flat-square)
![Platform](https://img.shields.io/badge/Platform-Windows%20%7C%20macOS%20%7C%20Consoles-6f42c1?style=flat-square)
![License](https://img.shields.io/badge/License-MIT-orange?style=flat-square)

Production-ready GPU grass for Unity 6 URP. Renders 1,000,000+ blades at 60+ FPS with compute-driven culling, HiZ occlusion, adaptive LOD, wind, translucency, and in-editor painting tools. Built to impress recruiters, teach advanced graphics, and drop into real games.

---

## Quick Links
- Demo video placeholder: `demo_video_url_here`
- Primary scripts: Assets/Scripts/GrassRenderer.cs, Assets/Shaders/GrassCompute.compute, Assets/Shaders/GrassShader.shader
- Editor tooling: Assets/Scripts/Editor/GrassPainterEditor.cs, Assets/Shaders/GrassDensityOverlay.shader
- Docs: Documentation/GrassSystem_DevLog.md, HiZ_Occlusion_Setup.md, HiZ_Troubleshooting.md
- License: MIT

---

## Elevator Pitch
- GPU-driven vegetation stack with zero per-instance CPU cost.
- AAA-style HiZ occlusion, distance thinning, and frustum LOD keep frame times predictable.
- Physically motivated wind via Bezier deformation and multi-frequency noise.
- Photoshop-like density painting inside the Unity Editor with overlays, gizmos, and undo.
- Procedural mesh generation, fake normals, and translucency for cinematic lighting.
- Clean, modular C#/HLSL code intended as a showcase for graphics and tools expertise.

---

## Who Should Read This
- Recruiters: Evidence of high-level graphics, performance, and tooling work.
- Rendering engineers: Reference for compute-driven instancing and HiZ pipelines.
- Technical artists: Ready-to-use authoring workflow for believable foliage.
- Gameplay/level designers: Paint grass interactively without touching code.

---

## Headline Metrics
- 1,000,000 blades @ 1080p on M2 Pro: 60+ FPS, 3–5 draw calls.
- CPU main thread cost: ~0.2 ms (buffer updates only).
- VRAM footprint: ~135 MB for 1M blades including HiZ pyramid.
- Distance thinning removes up to 90% of far geometry with stable noise hashing.
- HiZ occlusion recovers 30–50% vertex work in obstructed vistas.

---

## Feature Tour
- Compute-only generation: blades born and culled on GPU; no GameObjects.
- Multi-stage culling: density mask → frustum → distance thinning → HiZ.
- LOD fan-out: three AppendStructuredBuffers feed three indirect draws.
- Wind system: cubic Bezier bending with gusts, flutter, and stiffness variance.
- Lighting stack: fake normals, AO gradient, subsurface translucency, shadowcaster pass.
- Procedural mesh builder: configurable segment counts or custom mesh override.
- Editor painter: brush size/opacity/hardness, mini-map, overlay heatmap, undo/redo.
- Debug UI: HiZ previews, depth availability checks, toggle switches per feature.

---

## Architecture Overview
```
GrassRenderer (MonoBehaviour)
├─ Buffers: source (1M), LOD0/1/2 append, args per LOD, HiZ pyramid
├─ Compute: CSMain (generate), CSCull (cull + LOD), HiZGenerator (pyramid)
├─ Rendering: DrawMeshInstancedIndirect ×3, GrassShader (forward + shadow)
└─ Tooling: GrassPainterEditor, density Texture2D, overlay mesh + shader
```

Data lifetime
1) Init: allocate buffers, build procedural meshes, dispatch CSMain once.
2) Per-frame: build HiZ pyramid from camera depth, extract frustum planes, dispatch CSCull, copy counts to args, issue indirect draws.
3) Editor: optional run-in-edit for live painting; density map changes flow straight to GPU.

---

## Rendering Pipeline (Frame)
1. Copy camera depth into HiZ texture (BlitDepth kernel).
2. Downsample depth to mip chain (ReduceDepth) storing farthest depth per 2x2.
3. Gather camera frustum planes and pass to CSCull.
4. CSCull per blade:
   - Sample density map; early out if masked.
   - Frustum test against 6 planes using sphere-radius guard band.
   - Distance thinning: probabilistic cull + width compensation.
   - HiZ test: pick mip from projected size, compare against depth pyramid.
   - Append surviving blade to LOD0/1/2 buffers.
5. Copy counts from append buffers into indirect args (GPU-only).
6. Issue three DrawMeshInstancedIndirect calls with shared material.

---

## Compute Shader Highlights
- CSMain kernel
  - Builds positions on a grid with jitter, Simplex clumping, and per-blade wind phase.
  - Stores height, facing vector, stiffness, random IDs; 32 bytes per blade.
- CSCull kernel
  - Density map rejection using red channel of painter mask.
  - Frustum test via plane equations; avoids branchy math.
  - Distance thinning curve from 1.0 to _MinDensity with stable hash-based removal.
  - HiZ conservative occlusion with reverse-Z support and tweakable bias.
  - LOD routing based on distance thresholds; AppendStructuredBuffer per tier.
- HiZGenerator.compute
  - Linear depth copy + reduction loop; all integer texel addressing to avoid filtering.
  - Outputs mip chain consumed in CSCull without CPU readback.

---

## Shader Stack (URP)
- Vertex stage
  - Procedural instancing setup reads GrassData buffer.
  - Bezier deformation preserves blade length; wind disabled past a fade distance.
  - Normal rounding rotates normals across blade width to fake volume.
- Fragment stage
  - Three-point vertical gradient + noise-driven dry patches.
  - Ambient occlusion gradient darkens bases; cheap but convincing.
  - Subsurface translucency adds warm backlight when facing sun.
  - ShadowCaster and DepthOnly passes reuse deformation for correct depth/shadows.

---

## Wind System Deep Dive
- Inputs: wind map texture, gust strength, flutter strength, frequency, per-blade phase.
- Macro motion: scrolling noise field drives coherent waves across hectares.
- Micro motion: high-frequency flutter per blade; optional tip flapping.
- Stiffness variance: blades carry stiffness scalar to avoid uniform motion.
- Distance cull: wind math skipped beyond _WindDisableDistance with a 10 m blend to prevent pops.

---

## LOD and Culling Strategy
- LOD bands (defaults): 0–30 m (LOD0, 5 segments), 30–60 m (LOD1, 3 segments), 60–150 m (LOD2, 1 segment).
- Shadows: enabled for LOD0, optional for LOD1, disabled for LOD2 to save fill.
- Bounds: per-blade sphere using height and width scale; safe for wind sway.
- Indirect args: counts copied on GPU; CPU never touches per-instance data.
- Density scaling: far-field blade reduction with width compensation to keep coverage.

---

## Density Painting (Editor)
- Brush controls: size, opacity, hardness, target density, paint/erase toggle.
- Scene view gizmos: inner/outer rings plus solid center for quick targeting.
- Overlay mesh: 128x128 draped grid with heatmap shader; zero Z-fighting via bias.
- Mini-map: 2D preview of the full density mask for macro planning.
- Map management: create/clear/fill/save PNG; undo/redo integrated with Unity history.
- Runtime sync: Apply() uploads to GPU instantly; no play-mode restart needed.

---

## Tooling & Debugging
- Inspector toggles: render-in-edit, use occlusion, density scaling, debug UI.
- HiZ debug: depth texture presence check, mip preview, bias slider.
- Stats overlay: counts per stage (source, frustum, distance, occlusion) and per LOD.
- Logging: warnings when depth texture missing or references unassigned.

---

## Setup (10 Minutes)
1. Clone or import the package into a URP project (Depth Texture on in Renderer Data).
2. Add empty GameObject → add GrassRenderer.
3. Assign references (compute shaders, material, terrain, camera, density map if existing).
4. Set terrain size and grass count; click Regenerate.
5. Toggle Render In Edit Mode → Start Painting; paint/erase density directly in Scene view.
6. Hit Play; verify FPS and adjust LOD distances or counts.

---

## Usage Recipes
- Build a meadow: keep defaults, set Grass Count to 1,000,000, paint paths with low opacity.
- Create barren patches: switch to erase mode, hard brush, low opacity for subtle wear.
- Cinematic close-ups: raise LOD0 distance and segments, enable LOD1 shadows, reduce max distance.
- Large vistas: increase max draw distance, enable density scaling, slightly raise occlusion bias.
- Custom species: assign a bespoke mesh to customGrassMesh; procedural builder disables automatically.

---

## Performance Playbook
- Reduce counts first: Grass Count and Max Draw Distance have the biggest ROI.
- Tune density scaling: lower _MinDensity, raise _WidthCompensation to keep coverage.
- Wind budget: shorten _WindDisableDistance for cheap far field.
- Shadows: disable on LOD1/2 when GPU-bound; bias to avoid acne.
- HiZ bias: small increase removes flicker on steep terrain; keep conservative.
- Profiling: use Unity Profiler + Frame Debugger; HiZ costs ~0.5 ms at 1080p.

---

## Benchmarks (M2 Pro, 1080p)
- 100k blades: 3 draw calls, ~2.1 ms, 144+ FPS.
- 500k blades: 3 draw calls, ~3.8 ms, 90+ FPS.
- 1,000,000 blades: 3–5 draw calls, ~6.2 ms, 60+ FPS.
- 2,000,000 blades: 5 draw calls, ~11.5 ms, ~45 FPS.
- 5,000,000 blades: 5 draw calls, ~28 ms, ~30 FPS stress test.

Stage breakdown (1M blades): density → 920k, frustum → 350k, distance → 180k, HiZ → 130k.

---

## Compatibility
- Unity 6.0+ URP; 2022.3+ works with minor shader keyword updates.
- Graphics APIs: DX11/12, Metal, Vulkan. Tested on Apple Silicon; scalable to PC/console GPUs.
- Mobile: viable on modern devices by reducing blade count, disabling shadows, and lowering wind distance.

---

## Roadmap
- ScriptableRenderPass variant to eliminate 1-frame depth lag.
- GPU-driven impostor generation for ultra-far fields.
- Editor splines for roads/clearings that auto-carve density maps.
- HDRP port with transmission and volumetric fog hooks.
- Automated perf harness across common GPUs.

---

## FAQ
- Does it require compute shaders? Yes; check SystemInfo.supportsComputeShaders.
- Does it work without Depth Texture? No; enable Depth Texture in URP Renderer.
- Can I animate density over time? Yes; modify the density Texture2D at runtime and Apply().
- Can other shaders read the grass buffer? Yes; expose via GetGrassBuffer() and set on materials.
- How do I stop popping at LOD swaps? Tighten LOD distances or add small cross-fades in material.

---

## Contributing
- Fork, create a feature branch, and open a PR.
- Include before/after screenshots or perf numbers for visual/perf changes.
- Follow existing C# style and keep shaders commented where math-heavy.
- Add short notes to Documentation if you change workflows or settings.

---

## Implementation Notes (Deeper Dive)
- Depth handling: supports reverse-Z and standard Z; bias is configurable from Inspector.
- HiZ mip selection: mip = ceil(log2(projectedPixels)); avoids under-sampling large blades.
- Density map sampling: uses UV from terrain world position; texture format R8 for low memory.
- Hashing: low-discrepancy integer hash keeps density thinning stable across frames.
- Procedural mesh builder: recalculates UVs per segment for clean gradients and AO.
- Command ordering: HiZ generation runs before CSCull dispatch to reuse current depth.
- Debug safety: null checks log warnings when references are missing to avoid silent failure.

---

## GrassData Layout (GPU)
- float3 position      // world-space origin of blade
- float  height        // meters; drives LOD and wind strength
- float3 facing        // normalized forward vector per blade
- float  stiffness     // 0..1 stiffness multiplier
- float  windPhase     // random seed for flutter
- float  widthScale    // base width; modified by distance scaling
- float  paddingA      // reserved for future use
- float  paddingB      // reserved for future use

Each entry is 32 bytes; 1,000,000 entries → ~32 MB. LOD buffers reuse the same struct.

---

## Editor UX Highlights
- Toolbar buttons: Start Painting, Stop Painting, Clear Map, Fill Map, Save Mask.
- Overlay shader discards zero-density pixels to keep terrain readable while painting.
- Scene gizmos respect terrain normal so the brush ring hugs slopes correctly.
- Undo stack: every paint stroke registered via Undo to integrate with standard Ctrl+Z.
- Inspector foldouts: References, Generation, LOD, Wind, Culling, Debug—keeps settings organized.

---

## Setup Checklist
- [ ] URP Renderer Data → Depth Texture enabled.
- [ ] Main Camera tagged and assigned to GrassRenderer.
- [ ] Compute shaders assigned: GrassCompute.compute, HiZGenerator.compute.
- [ ] Material assigned: GrassMaterial with correct shader keywords.
- [ ] Terrain size set to match scene (bounds drive indirect draw bounds).
- [ ] Density map imported as R8, Read/Write enabled.
- [ ] Render In Edit Mode toggled on when painting.

---

## Suggested Inspector Defaults
- Grass Count: 1,000,000
- Terrain Size: 200 x 200 m
- LOD Distances: 30 / 60 / 150 m
- Max Draw Distance: 150 m
- Min Density (thinning): 0.12
- Width Compensation: 1.0
- Occlusion Bias: 0.15
- Wind Disable Distance: 50 m
- LOD1 Shadows: Off (turn on for hero shots)

---

## Integrating With Existing Terrains
- Match terrain scale: set Terrain Size to terrain bounds so bounds encapsulate blades.
- Multiple terrains: run one GrassRenderer per terrain tile; share density maps if desired.
- Streaming: regenerate or swap density maps when loading new tiles; buffers survive across scenes if kept in DontDestroyOnLoad objects.
- Lighting: works with URP main light and additional lights; ensure shadows enabled on light for best results.

---

## Testing & Validation
- Profile passes: verify HiZ dispatch cost vs. vertex savings using Unity Profiler.
- Camera stress test: fast flythrough to confirm no 1-frame lag artifacts; switch to SRP pass if needed.
- Terrain coverage: paint extremes (all white/all black) to validate density mask flow.
- LOD pop check: scrub distances in Scene view and watch transitions; adjust thresholds if needed.
- Platform sweep: test DX11, Metal, Vulkan; confirm compute support and depth availability.

---

## Portfolio Talking Points
- Built a full GPU-driven rendering pipeline: generation → culling → LOD → draw, all on GPU.
- Authored production-grade editor tooling with overlays, gizmos, and undoable brushes.
- Implemented AAA-inspired HiZ occlusion and distance thinning to stabilize frame times.
- Demonstrated shader craft: Bezier wind, fake normals, translucency, AO gradients, shadow caster parity.
- Documented codebase with diagrams, metrics, and onboarding-ready workflows.

---

## Glossary
- HiZ: Hierarchical Z-buffer, a mipmapped depth pyramid for conservative occlusion.
- Indirect Draw: GPU-issued draw using argument buffers without CPU per-instance calls.
- AppendStructuredBuffer: GPU buffer that atomically appends elements for LOD partitioning.
- Bezier Wind: Curve-based deformation keeping blade length constant under wind.
- Density Map: R8 mask painted in editor to control grass presence and patterns.

---

## Sample Code Snippets
**Regenerating grass at runtime:**
```csharp
void Start() {
  grassRenderer.Regenerate(); // Allocates buffers + runs CSMain
}

void Update() {
  grassRenderer.Render(); // Builds HiZ, culls, issues indirect draws
}
```

**Applying a live density edit:**
```csharp
var tex = grassRenderer.densityMap;
tex.SetPixel(u, v, Color.red * 0.2f); // Paint darker density
tex.Apply();                          // Upload to GPU immediately
```

**Accessing counts for UI:**
```csharp
var stats = grassRenderer.GetStats();
ui.SetText($"LOD0: {stats.lod0}  LOD1: {stats.lod1}  LOD2: {stats.lod2}");
```

---

## Attribution & Contact
- Author: Your Name (2024)
- LinkedIn: your-link
- Portfolio: your-site
- GitHub: your-handle

Thank you for reading. This project combines graphics programming, optimization, and UX-minded tooling. It is engineered to be both a practical drop-in vegetation system and a portfolio piece that demonstrates deep understanding of GPU pipelines.
````
5. Save mask with **"Save Mask"** button
