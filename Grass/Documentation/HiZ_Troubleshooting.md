# Hi-Z Occlusion Culling - Troubleshooting Guide

You are absolutely correct about the differences between Shader Graph (Automatic) and Compute Shaders (Manual). The implementation I provided adheres to the "Manual" approach, but here is a checklist to verify everything is aligned with the "Production Level" requirements.

## 1. Manual Binding Verification
The video correctly states that Compute Shaders don't get automatic bindings.
*   **My Code**: In `GrassRenderer.cs`, I use `Shader.GetGlobalTexture("_CameraDepthTexture")` and then explicitly call `hizComputeShader.SetTexture(..., "_InputDepthTexture", depthTexture)`.
*   **Verification**: This is the "Manual Binding" the video calls for. We are **not** relying on Unity to magically bind `_CameraDepthTexture` to the shader.

## 2. Sampler vs. Load
The video uses `SampleSceneDepth` (Linear Sampler).
*   **My Code**: In `HiZGenerator.compute`, I use `_InputDepthTexture.Load(int3(id.xy, 0))`.
*   **Verification**: This is correct for Hi-Z. We **must** use `Load` (Point sampling) to get exact depth values. Linear blending would create "fake" depths that break the conservative culling logic.

## 3. Depth Texture Availability
The video mentions the "Depth Texture" checkbox.
*   **Action**: Please double-check your **URP Renderer Data** asset.
    *   Go to **Project Settings > Graphics > URP Global Settings**.
    *   Find the **Renderer List**. Click the active Renderer Data asset.
    *   Ensure **Depth Texture** is checked.
    *   *Note*: If this is off, `Shader.GetGlobalTexture` returns null, and my Debug UI will show "WARNING".

## 4. Terrain Shader "Prepass"
The video mentions that custom shaders need a `DepthOnly` pass.
*   **Action**: If you are using a custom shader for your Terrain, ensure it has a `LightMode = DepthOnly` pass.
*   **Test**: Place a standard Unity Cube in front of the grass. If the grass is culled by the Cube but not the Terrain, your Terrain shader is missing the depth pass.

## 5. Execution Order (Update vs. Render Loop)
Currently, we are running the culling in `Update()`.
*   **Pros**: Simple, works with `Graphics.DrawMeshInstancedIndirect`.
*   **Cons**: It uses the **Previous Frame's** depth buffer.
*   **Result**: When you move the camera fast, you might see grass "popping" in 1 frame late. This is usually acceptable for grass.
*   **Production Fix**: To fix this 1-frame lag, we would need to move the logic into a `ScriptableRenderPass` (Render Feature) to execute *after* the opaque pass but *before* the transparent pass. This is significantly more complex but I can implement it if you require absolute frame-perfect culling.

## 6. Debugging the "Black Texture"
If the Debug UI shows a black HiZ texture:
1.  **Check Shader Assignment**: Ensure `HiZGenerator.compute` is assigned in the Inspector.
2.  **Check Depth**: Ensure the "Depth Texture Preview" in the Debug UI is not black.
3.  **Check Bias**: If HiZ looks good but culling fails, increase **Occlusion Bias** to `0.5` or `1.0` temporarily to see if it kicks in.
