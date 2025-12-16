# Grass System Development Log - December 16, 2025

## Overview
This document outlines the implementation of the **Interactive Grass Painting System** and **Visual Debugging Tools** for the GPU Instanced Grass Renderer. The system allows for real-time painting of grass density directly onto terrain surfaces within the Unity Editor.

## Features Implemented

### 1. Interactive Grass Painter
A custom editor tool (`GrassPainterEditor`) has been integrated into the `GrassRenderer` inspector.
- **Painting Modes**:
  - **Paint**: Left-click to add grass density.
  - **Erase**: Hold `Shift` + Left-click to remove grass.
- **Brush Controls**:
  - **Brush Size**: Controls the radius of the painting area.
  - **Brush Opacity**: Controls the strength of the brush per stroke.
  - **Brush Hardness**: Adjusts the falloff of the brush (0 = soft, 1 = hard edge).
  - **Target Density**: Sets the maximum density value (0-1) for the brush.
- **Undo/Redo Support**: Full support for Unity's Undo system. Painting strokes, map clearing, and filling can be undone.

### 2. Visualization Tools
To improve the user experience, several visual aids were added:
- **Terrain Overlay**: 
  - A custom mesh overlay is generated on top of the terrain.
  - **Heatmap Visualization**: Uses a custom shader (`Hidden/GrassDensityOverlay`) to render the density map in **Red**.
  - **Smart Culling**: The shader automatically discards areas with 0 density to keep the view clean.
  - **Z-Bias**: The overlay uses a depth bias to prevent Z-fighting with the terrain surface.
- **Brush Gizmos**:
  - **Outer Ring**: Shows the full brush size.
  - **Inner Ring**: Shows the hardness threshold (where falloff begins).
  - **Solid Disc**: Indicates the brush center.
- **Mini-Map**: A 2D preview of the density texture is displayed in the Scene View for a quick overview of the entire map.

### 3. Map Management
Tools for managing the underlying density texture:
- **Create Map**: Automatically generates a 512x512 density texture if none exists.
- **Clear Map**: Resets the entire map to 0 density (black).
- **Fill Map**: Sets the entire map to 1 density (white).
- **Save Mask**: Exports the current density map to a PNG file in the project, allowing it to be saved as an asset and reused.

## Technical Implementation Details

### Editor Script (`GrassPainterEditor.cs`)
- **Location**: `Assets/Scripts/Editor/GrassPainterEditor.cs`
- **Key Responsibilities**:
  - Handles Scene View input (Raycasting against terrain).
  - Modifies the `Texture2D` density map using pixel manipulation.
  - Generates and caches a low-resolution (128x128) mesh for the overlay to minimize CPU overhead.
  - Manages the `GrassRenderer`'s `renderInEditMode` state.

### Shaders
- **Overlay Shader**: `Assets/Shaders/GrassDensityOverlay.shader`
  - **Type**: Unlit, Transparent.
  - **Function**: Samples the Red channel of the density map.
  - **Optimization**: Uses `discard` for zero-density pixels to reduce overdraw.
  - **Properties**: `_MainTex` (Density Map), `_Color` (Tint).

### Renderer Integration (`GrassRenderer.cs`)
- The `GrassRenderer` was updated to expose:
  - `densityMap`: The Texture2D used for masking.
  - `renderInEditMode`: A toggle to enable/disable the compute shader execution in the editor.

## Usage Guide
1. Select the **GrassRenderer** GameObject.
2. Ensure a **Terrain** is assigned in the inspector.
3. Click **"Start Painting"** in the inspector.
4. Use the **Brush Settings** to adjust your tool.
5. **Left Click** on the terrain to paint grass.
6. **Shift + Left Click** to erase.
7. Click **"Save Mask"** to save your work to a file.

## Optimization Update - December 16, 2025 (Part 2)

To improve performance for large-scale environments, several optimization techniques were implemented, focusing on reducing vertex count and GPU load for distant grass.

### 1. Three-Stage LOD System
The Level of Detail (LOD) system was expanded from 2 to 3 levels to provide smoother transitions and better performance scaling.
- **LOD 0 (High)**: Full detail mesh. Configurable segment count (default: 5).
- **LOD 1 (Medium)**: Reduced detail. Configurable segment count (default: 3).
- **LOD 2 (Low)**: Lowest detail (billboard-like). Configurable segment count (default: 1).
- **Distance Control**: Separate distance thresholds for LOD 0 and LOD 1 transitions.
- **Shadow Optimization**: Option to disable shadow casting for LOD 1 and LOD 2 independently.

### 2. Density Scaling (Distance-Based Thinning)
A technique to significantly reduce the number of grass blades drawn at a distance without losing visual volume.
- **Concept**: As distance increases, the number of blades is reduced (culled randomly), but the remaining blades are widened to compensate.
- **Parameters**:
  - **Min Density**: The fraction of grass remaining at the max draw distance (e.g., 0.1 = 10%).
  - **Falloff Start**: Distance where thinning begins.
  - **Width Compensation**: Controls how much the remaining blades are widened.
    - `1.0`: Mathematically correct volume preservation.
    - `>1.0`: Thicker appearance.
    - `<1.0`: Thinner/sparser appearance.

### 3. Wind Animation Optimization
To save GPU cycles in the vertex shader, wind calculations are now disabled for distant grass.
- **Wind Disable Distance**: The distance at which wind animation stops completely.
- **Smooth Fade**: A 10-meter fade-out zone prevents "popping" artifacts where grass suddenly freezes.
- **Performance Gain**: Skips complex noise sampling, bezier curve calculations, and rotation operations for distant vertices.

### Files Modified
- `Assets/Scripts/GrassRenderer.cs`: Added new settings and logic for LODs, density scaling, and wind distance.
- `Assets/Shaders/GrassCompute.compute`: Implemented density-based culling and width scaling logic.
- `Assets/Shaders/GrassShader.shader`: Added support for variable blade width and distance-based wind fading.
