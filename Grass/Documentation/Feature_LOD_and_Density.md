# LOD, Distance Thinning, and Density Masks

## Goal
Keep visuals consistent while reducing work: fewer distant blades, smoother LOD transitions, artist-controlled coverage.

## Stages in CSCull
1) **Density mask**: sample red channel of densityMap; reject if below densityThreshold.
2) **Frustum test**: sphere vs. 6 planes using precomputed floats to avoid heavy math.
3) **Distance thinning**: beyond densityFalloffStart, probabilistically drop blades toward minDensity. Remaining blades widen via widthCompensation.
4) **LOD routing**: distance buckets feed LOD0/1/2 append buffers.

## LOD Defaults
- LOD0: 0–30 m, 5 segments, shadows on.
- LOD1: 30–60 m, 3 segments, shadows optional.
- LOD2: 60–150 m, 1 segment, shadows off.

## Key Settings (GrassRenderer)
- Distances: lod0Distance, lod1Distance, maxDrawDistance.
- Geometry: lod0Segments, lod1Segments, lod2Segments.
- Thinning: enableDensityScaling, minDensity, densityFalloffStart, widthCompensation.
- Masking: densityMap, densityThreshold; assign via Painter or asset.

## Designer Workflow
- Paint clearings and paths in the density map using the Painter (see Feature_Painting_and_Tools.md).
- Lower minDensity to gain FPS in large vistas; counteract visual gaps with widthCompensation near 1.0–1.2.
- Tune lod distances so swaps happen outside the main gameplay bubble.

## Performance Tips
- Start with grassCount aligned to your target hardware, then tune thinning and maxDrawDistance.
- Keep LOD1 shadows off for open-world; enable only for hero shots.
- If pop is visible, slightly overlap LOD bands or raise segment counts on mid LOD.

## Files
- [Assets/Scripts/GrassRenderer.cs](Assets/Scripts/GrassRenderer.cs)
- [Assets/Shaders/GrassCompute.compute](Assets/Shaders/GrassCompute.compute)
