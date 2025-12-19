using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(GrassRenderer))]
public class GrassPainterEditor : Editor
{
    private GrassRenderer grassRenderer;
    
    // Painting Variables
    private bool isPainting = false;
    private bool showOverlay = true;
    private float brushSize = 5f;
    private float brushOpacity = 0.5f;
    private float brushHardness = 0.5f;
    private float targetDensity = 1.0f;
    
    private Material overlayMaterial;
    private Mesh overlayMesh;

    // Serialized Properties
    private SerializedProperty customGrassMesh;
    private SerializedProperty grassCount;
    private SerializedProperty terrainSize;
    private SerializedProperty minGrassHeight;
    private SerializedProperty maxGrassHeight;
    
    // LOD Settings
    private SerializedProperty lod0Segments;
    private SerializedProperty lod1Segments;
    private SerializedProperty lod2Segments;
    private SerializedProperty lod0Distance;
    private SerializedProperty lod1Distance;
    private SerializedProperty maxDrawDistance;
    private SerializedProperty windDisableDistance;

    // Density
    private SerializedProperty enableDensityScaling;
    private SerializedProperty minDensity;
    private SerializedProperty densityFalloffStart;
    private SerializedProperty widthCompensation;

    // Generation
    private SerializedProperty clumpScale;
    private SerializedProperty clumpStrength;
    private SerializedProperty heightScale;
    private SerializedProperty heightFactor;
    private SerializedProperty angleVariation;
    private SerializedProperty cameraFacing;

    // References
    private SerializedProperty computeShader;
    private SerializedProperty hizComputeShader;
    private SerializedProperty grassMaterial;
    private SerializedProperty terrain;

    // Rendering
    private SerializedProperty castShadows;
    private SerializedProperty castShadowsLOD1;
    private SerializedProperty castShadowsLOD2;
    private SerializedProperty receiveShadows;
    private SerializedProperty useOcclusionCulling;
    private SerializedProperty occlusionBias;
    private SerializedProperty mainCamera;
    private SerializedProperty renderInEditMode;

    // Debug
    private SerializedProperty showDebugUI;

    // Painting
    private SerializedProperty densityMap;
    private SerializedProperty densityThreshold;

    // Foldouts
    private bool showGrassSettings = true;
    private bool showDensitySettings = true;
    private bool showGenerationSettings = true;
    private bool showReferences = true;
    private bool showRendering = true;
    private bool showDebug = false;
    private bool showPainting = true;

    private void OnEnable()
    {
        grassRenderer = (GrassRenderer)target;
        
        // Initialize Serialized Properties
        customGrassMesh = serializedObject.FindProperty("customGrassMesh");
        grassCount = serializedObject.FindProperty("grassCount");
        terrainSize = serializedObject.FindProperty("terrainSize");
        minGrassHeight = serializedObject.FindProperty("minGrassHeight");
        maxGrassHeight = serializedObject.FindProperty("maxGrassHeight");
        
        lod0Segments = serializedObject.FindProperty("lod0Segments");
        lod1Segments = serializedObject.FindProperty("lod1Segments");
        lod2Segments = serializedObject.FindProperty("lod2Segments");
        lod0Distance = serializedObject.FindProperty("lod0Distance");
        lod1Distance = serializedObject.FindProperty("lod1Distance");
        maxDrawDistance = serializedObject.FindProperty("maxDrawDistance");
        windDisableDistance = serializedObject.FindProperty("windDisableDistance");

        enableDensityScaling = serializedObject.FindProperty("enableDensityScaling");
        minDensity = serializedObject.FindProperty("minDensity");
        densityFalloffStart = serializedObject.FindProperty("densityFalloffStart");
        widthCompensation = serializedObject.FindProperty("widthCompensation");

        clumpScale = serializedObject.FindProperty("clumpScale");
        clumpStrength = serializedObject.FindProperty("clumpStrength");
        heightScale = serializedObject.FindProperty("heightScale");
        heightFactor = serializedObject.FindProperty("heightFactor");
        angleVariation = serializedObject.FindProperty("angleVariation");
        cameraFacing = serializedObject.FindProperty("cameraFacing");

        computeShader = serializedObject.FindProperty("computeShader");
        hizComputeShader = serializedObject.FindProperty("hizComputeShader");
        grassMaterial = serializedObject.FindProperty("grassMaterial");
        terrain = serializedObject.FindProperty("terrain");

        castShadows = serializedObject.FindProperty("castShadows");
        castShadowsLOD1 = serializedObject.FindProperty("castShadowsLOD1");
        castShadowsLOD2 = serializedObject.FindProperty("castShadowsLOD2");
        receiveShadows = serializedObject.FindProperty("receiveShadows");
        useOcclusionCulling = serializedObject.FindProperty("useOcclusionCulling");
        occlusionBias = serializedObject.FindProperty("occlusionBias");
        mainCamera = serializedObject.FindProperty("mainCamera");
        renderInEditMode = serializedObject.FindProperty("renderInEditMode");

        showDebugUI = serializedObject.FindProperty("showDebugUI");

        densityMap = serializedObject.FindProperty("densityMap");
        densityThreshold = serializedObject.FindProperty("densityThreshold");

        // Use our custom density overlay shader
        Shader shader = Shader.Find("Hidden/GrassDensityOverlay");
        if (shader == null) shader = Shader.Find("Hidden/Internal-Colored");
        
        overlayMaterial = new Material(shader);
        overlayMaterial.hideFlags = HideFlags.HideAndDontSave;
    }

    private void OnDisable()
    {
        if (overlayMaterial != null) DestroyImmediate(overlayMaterial);
        if (overlayMesh != null) DestroyImmediate(overlayMesh);
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        // Custom Header
        GUIStyle headerStyle = new GUIStyle(EditorStyles.boldLabel);
        headerStyle.fontSize = 14;
        headerStyle.alignment = TextAnchor.MiddleCenter;
        headerStyle.margin = new RectOffset(0, 0, 10, 10);
        
        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Grass Renderer", headerStyle);
        EditorGUILayout.Space(5);

        DrawSection("General Settings", ref showGrassSettings, () => {
            EditorGUILayout.PropertyField(customGrassMesh);
            EditorGUILayout.PropertyField(grassCount);
            EditorGUILayout.PropertyField(terrainSize);
            EditorGUILayout.PropertyField(minGrassHeight);
            EditorGUILayout.PropertyField(maxGrassHeight);
            EditorGUILayout.Space(5);
            
            if (customGrassMesh.objectReferenceValue == null)
            {
                EditorGUILayout.LabelField("LOD Settings", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(lod0Segments);
                EditorGUILayout.PropertyField(lod1Segments);
                EditorGUILayout.PropertyField(lod2Segments);
                EditorGUILayout.Space(2);
            }
            else
            {
                EditorGUILayout.HelpBox("Using Custom Mesh. LOD Segment settings are ignored.", MessageType.Info);
                EditorGUILayout.Space(2);
            }

            EditorGUILayout.PropertyField(lod0Distance);
            EditorGUILayout.PropertyField(lod1Distance);
            EditorGUILayout.PropertyField(maxDrawDistance);
            EditorGUILayout.PropertyField(windDisableDistance);
        });

        DrawSection("Density Scaling", ref showDensitySettings, () => {
            EditorGUILayout.PropertyField(enableDensityScaling);
            if (enableDensityScaling.boolValue)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(minDensity);
                EditorGUILayout.PropertyField(densityFalloffStart);
                EditorGUILayout.PropertyField(widthCompensation);
                EditorGUI.indentLevel--;
            }
        });

        DrawSection("Generation", ref showGenerationSettings, () => {
            EditorGUILayout.PropertyField(clumpScale);
            EditorGUILayout.PropertyField(clumpStrength);
            EditorGUILayout.PropertyField(heightScale);
            EditorGUILayout.PropertyField(heightFactor);
            EditorGUILayout.PropertyField(angleVariation);
            EditorGUILayout.PropertyField(cameraFacing);
        });

        DrawSection("Rendering", ref showRendering, () => {
            EditorGUILayout.PropertyField(renderInEditMode);
            EditorGUILayout.PropertyField(mainCamera);
            EditorGUILayout.Space(5);
            EditorGUILayout.PropertyField(castShadows);
            if (castShadows.boolValue)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(castShadowsLOD1);
                EditorGUILayout.PropertyField(castShadowsLOD2);
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.PropertyField(receiveShadows);
            EditorGUILayout.Space(5);
            EditorGUILayout.PropertyField(useOcclusionCulling);
            if (useOcclusionCulling.boolValue)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(occlusionBias);
                EditorGUI.indentLevel--;
            }
        });

        DrawSection("References", ref showReferences, () => {
            EditorGUILayout.PropertyField(computeShader);
            EditorGUILayout.PropertyField(hizComputeShader);
            EditorGUILayout.PropertyField(grassMaterial);
            EditorGUILayout.PropertyField(terrain);
        });

        DrawSection("Painting Tools", ref showPainting, () => {
            EditorGUILayout.PropertyField(densityMap);
            EditorGUILayout.PropertyField(densityThreshold);
            
            EditorGUILayout.Space(10);
            
            GUI.backgroundColor = isPainting ? new Color(1f, 0.7f, 0.7f) : new Color(0.7f, 1f, 0.7f);
            if (GUILayout.Button(isPainting ? "Stop Painting" : "Start Painting", GUILayout.Height(30)))
            {
                isPainting = !isPainting;
                if (isPainting)
                {
                    // Ensure density map exists
                    if (grassRenderer.densityMap == null)
                    {
                        CreateDensityMap();
                    }
                }
            }
            GUI.backgroundColor = Color.white;

            if (isPainting)
            {
                EditorGUILayout.Space(5);
                showOverlay = EditorGUILayout.Toggle("Show Density Overlay", showOverlay);
                brushSize = EditorGUILayout.Slider("Brush Size", brushSize, 1f, 50f);
                brushOpacity = EditorGUILayout.Slider("Brush Opacity", brushOpacity, 0.01f, 1f);
                brushHardness = EditorGUILayout.Slider("Brush Hardness", brushHardness, 0.01f, 1f);
                targetDensity = EditorGUILayout.Slider("Target Density", targetDensity, 0f, 1f);
                
                EditorGUILayout.Space(5);
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Clear Map"))
                {
                    if (EditorUtility.DisplayDialog("Clear Map", "Are you sure you want to clear the density map?", "Yes", "No"))
                    {
                        Undo.RegisterCompleteObjectUndo(grassRenderer.densityMap, "Clear Grass Map");
                        ClearMap();
                    }
                }
                
                if (GUILayout.Button("Fill Map"))
                {
                    if (EditorUtility.DisplayDialog("Fill Map", "Are you sure you want to fill the density map?", "Yes", "No"))
                    {
                        Undo.RegisterCompleteObjectUndo(grassRenderer.densityMap, "Fill Grass Map");
                        FillMap();
                    }
                }
                
                if (GUILayout.Button("Save Mask"))
                {
                    SaveMask();
                }
                GUILayout.EndHorizontal();
                
                EditorGUILayout.HelpBox("Left Click to Paint.\nHold Shift to Erase (Target Density = 0).\nEnsure a Terrain is assigned to the GrassRenderer.", MessageType.Info);
            }
        });

        DrawSection("Debug", ref showDebug, () => {
            EditorGUILayout.PropertyField(showDebugUI);
        });

        EditorGUILayout.Space(10);
        
        GUI.backgroundColor = new Color(0.7f, 1.0f, 0.7f);
        if (GUILayout.Button("Regenerate Grass", GUILayout.Height(30)))
        {
            grassRenderer.Refresh();
        }
        GUI.backgroundColor = Color.white;

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawSection(string title, ref bool foldout, System.Action drawContent)
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        
        GUIStyle foldoutStyle = new GUIStyle(EditorStyles.foldout);
        foldoutStyle.fontStyle = FontStyle.Bold;
        foldoutStyle.fontSize = 12;
        
        EditorGUI.indentLevel++;
        foldout = EditorGUILayout.Foldout(foldout, title, true, foldoutStyle);
        EditorGUI.indentLevel--;

        if (foldout)
        {
            EditorGUILayout.Space(5);
            drawContent?.Invoke();
            EditorGUILayout.Space(5);
        }
        
        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(5);
    }

    private void OnSceneGUI()
    {
        if (!isPainting || grassRenderer == null) return;

        // Draw Overlay
        if (showOverlay && grassRenderer.densityMap != null)
        {
            DrawOverlay();
            
            // Draw 2D MiniMap
            Handles.BeginGUI();
            GUILayout.BeginArea(new Rect(10, 10, 150, 150), "Density Map", GUI.skin.window);
            EditorGUI.DrawTextureTransparent(new Rect(10, 20, 130, 120), grassRenderer.densityMap);
            GUILayout.EndArea();
            Handles.EndGUI();
        }

        Event e = Event.current;
        
        // Raycast from mouse position
        Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
        RaycastHit hit;

        // Try to hit terrain specifically if possible, otherwise anything
        if (Physics.Raycast(ray, out hit, 10000f)) 
        {
            // Draw Brush Handle
            Handles.color = new Color(0, 1, 0, 0.5f);
            Handles.DrawWireDisc(hit.point, hit.normal, brushSize);
            // Inner circle for hardness
            Handles.color = new Color(0, 1, 0, 0.2f);
            Handles.DrawWireDisc(hit.point, hit.normal, brushSize * brushHardness);
            Handles.DrawSolidDisc(hit.point, hit.normal, brushSize * 0.1f);

            // Handle Input
            if ((e.type == EventType.MouseDrag || e.type == EventType.MouseDown) && e.button == 0 && !e.alt)
            {
                // Register Undo on MouseDown
                if (e.type == EventType.MouseDown)
                {
                    Undo.RegisterCompleteObjectUndo(grassRenderer.densityMap, "Paint Grass");
                }
                
                bool isErasing = e.shift;
                Paint(hit.point, isErasing);
                e.Use(); // Consume event
            }
        }
        
        // Force repaint to show handle updates and overlay
        if (isPainting)
        {
            SceneView.RepaintAll();
        }
    }

    private void DrawOverlay()
    {
        SerializedProperty terrainProp = serializedObject.FindProperty("terrain");
        Terrain terrain = terrainProp.objectReferenceValue as Terrain;
        if (terrain == null) return;

        // Rebuild mesh if needed
        if (overlayMesh == null)
        {
            RebuildOverlayMesh(terrain);
        }

        if (overlayMaterial != null)
        {
            // Update material properties
            if (overlayMaterial.HasProperty("_MainTex"))
                overlayMaterial.SetTexture("_MainTex", grassRenderer.densityMap);
            
            if (overlayMaterial.HasProperty("_Color"))
                overlayMaterial.SetColor("_Color", new Color(1, 0, 0, 0.5f)); // Red tint
            
            overlayMaterial.SetPass(0);
            
            // Draw mesh in world space (identity matrix)
            Graphics.DrawMeshNow(overlayMesh, Matrix4x4.identity);
        }
    }

    private void RebuildOverlayMesh(Terrain terrain)
    {
        if (overlayMesh == null)
        {
            overlayMesh = new Mesh();
            overlayMesh.name = "OverlayMesh";
        }

        Vector3 terrainPos = terrain.transform.position;
        Vector3 terrainSize = terrain.terrainData.size;
        
        // Use a reasonable resolution for the overlay mesh
        // 128x128 is a good balance between performance and visual accuracy
        int res = 128; 
        
        Vector3[] verts = new Vector3[(res + 1) * (res + 1)];
        Vector2[] uvs = new Vector2[verts.Length];
        int[] tris = new int[res * res * 6];

        for (int z = 0; z <= res; z++)
        {
            for (int x = 0; x <= res; x++)
            {
                float u = x / (float)res;
                float v = z / (float)res;
                
                float worldX = terrainPos.x + u * terrainSize.x;
                float worldZ = terrainPos.z + v * terrainSize.z;
                
                // Sample height on CPU for robustness
                float height = terrain.SampleHeight(new Vector3(worldX, 0, worldZ));
                float worldY = terrainPos.y + height + 0.2f; // Offset slightly above terrain to avoid z-fighting
                
                int index = z * (res + 1) + x;
                verts[index] = new Vector3(worldX, worldY, worldZ);
                uvs[index] = new Vector2(u, v);
            }
        }

        int triIndex = 0;
        for (int z = 0; z < res; z++)
        {
            for (int x = 0; x < res; x++)
            {
                int bl = z * (res + 1) + x;
                int br = bl + 1;
                int tl = (z + 1) * (res + 1) + x;
                int tr = tl + 1;

                tris[triIndex++] = bl;
                tris[triIndex++] = tl;
                tris[triIndex++] = br;
                
                tris[triIndex++] = br;
                tris[triIndex++] = tl;
                tris[triIndex++] = tr;
            }
        }

        overlayMesh.Clear();
        overlayMesh.vertices = verts;
        overlayMesh.uv = uvs;
        overlayMesh.triangles = tris;
        overlayMesh.RecalculateNormals();
    }

    private void CreateDensityMap()
    {
        int resolution = 512; // Default resolution
        grassRenderer.densityMap = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false);
        // Initialize with black (0 density)
        Color[] colors = new Color[resolution * resolution];
        for (int i = 0; i < colors.Length; i++) colors[i] = Color.clear; // Clear is transparent black
        grassRenderer.densityMap.SetPixels(colors);
        grassRenderer.densityMap.Apply();
    }

    private void ClearMap()
    {
        if (grassRenderer.densityMap == null) return;
        int w = grassRenderer.densityMap.width;
        int h = grassRenderer.densityMap.height;
        Color[] colors = new Color[w * h];
        for (int i = 0; i < colors.Length; i++) colors[i] = Color.clear;
        grassRenderer.densityMap.SetPixels(colors);
        grassRenderer.densityMap.Apply();
        grassRenderer.Refresh();
    }

    private void FillMap()
    {
        if (grassRenderer.densityMap == null) return;
        int w = grassRenderer.densityMap.width;
        int h = grassRenderer.densityMap.height;
        Color[] colors = new Color[w * h];
        for (int i = 0; i < colors.Length; i++) colors[i] = Color.white;
        grassRenderer.densityMap.SetPixels(colors);
        grassRenderer.densityMap.Apply();
        grassRenderer.Refresh();
    }

    private void Paint(Vector3 worldPos, bool isErasing)
    {
        if (grassRenderer.densityMap == null) return;

        SerializedProperty terrainProp = serializedObject.FindProperty("terrain");
        Terrain terrain = terrainProp.objectReferenceValue as Terrain;
        
        Vector3 terrainPos = Vector3.zero;
        Vector3 terrainSize = Vector3.one * 100;

        if (terrain != null)
        {
            terrainPos = terrain.transform.position;
            terrainSize = terrain.terrainData.size;
        }
        else
        {
            Debug.LogWarning("Please assign a Terrain to the GrassRenderer to paint.");
            return;
        }

        // Calculate UV
        float u = (worldPos.x - terrainPos.x) / terrainSize.x;
        float v = (worldPos.z - terrainPos.z) / terrainSize.z;

        if (u < 0 || u > 1 || v < 0 || v > 1) return;

        // Paint on texture
        int texW = grassRenderer.densityMap.width;
        int texH = grassRenderer.densityMap.height;
        
        int centerX = (int)(u * texW);
        int centerY = (int)(v * texH);
        
        // Brush radius in pixels
        int radiusX = (int)(brushSize / terrainSize.x * texW);
        int radiusY = (int)(brushSize / terrainSize.z * texH);
        
        // Avoid 0 radius
        radiusX = Mathf.Max(1, radiusX);
        radiusY = Mathf.Max(1, radiusY);

        for (int y = -radiusY; y <= radiusY; y++)
        {
            for (int x = -radiusX; x <= radiusX; x++)
            {
                // Circular brush
                float distSq = x*x + y*y;
                float radiusSq = radiusX*radiusX;
                
                if (distSq <= radiusSq) 
                {
                    int px = centerX + x;
                    int py = centerY + y;
                    
                    if (px >= 0 && px < texW && py >= 0 && py < texH)
                    {
                        float current = grassRenderer.densityMap.GetPixel(px, py).r;
                        float target = isErasing ? 0 : targetDensity;
                        
                        // Calculate Falloff
                        float dist = Mathf.Sqrt(distSq);
                        float normalizedDist = dist / radiusX;
                        
                        // Hardness: 1 = hard edge, 0 = softest
                        // Remap normalizedDist based on hardness
                        // If hardness is 1, falloff is 1 until edge
                        // If hardness is 0.5, falloff starts at 0.5
                        
                        float falloff = 1.0f;
                        if (normalizedDist > brushHardness)
                        {
                            // Smoothstep falloff from hardness to 1
                            float t = (normalizedDist - brushHardness) / (1.0f - brushHardness);
                            falloff = 1.0f - t;
                            // Optional: Smoothstep for nicer curve
                            falloff = falloff * falloff * (3 - 2 * falloff); 
                        }
                        
                        // Apply opacity and falloff
                        float influence = brushOpacity * falloff;
                        
                        float newValue = Mathf.Lerp(current, target, influence);
                        // Set Alpha to match value for transparency in overlay
                        grassRenderer.densityMap.SetPixel(px, py, new Color(newValue, newValue, newValue, newValue));
                    }
                }
            }
        }
        
        grassRenderer.densityMap.Apply();
        grassRenderer.Refresh();
    }
    
    private void SaveMask()
    {
        string path = EditorUtility.SaveFilePanelInProject("Save Grass Mask", "GrassMask", "png", "Save the painted grass mask");
        if (string.IsNullOrEmpty(path)) return;
        
        byte[] bytes = grassRenderer.densityMap.EncodeToPNG();
        System.IO.File.WriteAllBytes(path, bytes);
        AssetDatabase.Refresh();
        
        // Load it back to ensure it's linked as an asset
        Texture2D savedTex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        
        // Ensure texture is readable
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer != null)
        {
            importer.isReadable = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }
        
        grassRenderer.densityMap = savedTex;
        serializedObject.ApplyModifiedProperties();
    }
}
