# Wind, Lighting, and Shading

## Goal
Deliver believable motion and lighting on thin geometry without heavy geometry cost.

## Wind System
- Deformation: cubic Bezier curve per blade; preserves length and reduces shear.
- Inputs: wind map texture, gust strength, flutter strength, frequency, per-blade phase, stiffness.
- Macro motion: scrolling noise for broad waves.
- Micro motion: high-frequency flutter and optional tip flapping.
- Distance optimization: wind disabled beyond windDisableDistance with a short fade.

## Shading Stack (GrassShader.shader)
- Gradient color: root/mid/tip colors with optional dry tint via noise.
- Fake normals: rotate normals across blade width to fake cylindrical volume.
- Ambient occlusion: vertical gradient darkens bases.
- Translucency: backlit glow using view + light direction; controlled by Translucency and TranslucencyPower.
- Shadows: ShadowCaster and DepthOnly passes reuse deformation so shadows match bent blades.

## Material Keys (GrassMat.mat)
- Colors: _RootColor, _MidColor, _TipColor, _DryColor, _DryAmount.
- Wind: _WindGustStrength, _WindFlutterStrength, _WindFrequency, _WindDisableDistance, _WindVelocity.
- Shape: _BladeHeight, _BladeWidth, _BladeCurvature, _BladeBend, _AngleVariation, _CameraFacing.
- Lighting: _AmbientOcclusionStrength, _ShadowSoftness, _SpecularStrength, _Translucency, _TranslucencyPower.

## Tuning Tips
- For calm scenes: lower _WindGustStrength and _WindFlutterStrength; raise windDisableDistance fade.
- For storms: raise gust strength, add tip flapping, increase curvature slightly to avoid clipping.
- For bright backlight shots: raise _Translucency and lower _TranslucencyPower for softer falloff.
- For performance: shorten windDisableDistance; disable LOD1 shadows; keep translucency modest.

## Files
- [Assets/Shaders/GrassShader.shader](Assets/Shaders/GrassShader.shader)
- [Assets/Materials/GrassMat.mat](Assets/Materials/GrassMat.mat)
- [Assets/Scripts/GrassRenderer.cs](Assets/Scripts/GrassRenderer.cs)
