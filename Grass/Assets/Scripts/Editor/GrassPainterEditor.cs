using UnityEngine;
using UnityEditor;

/// <summary>
/// Custom Editor for the GrassRenderer that enables terrain painting functionality.
/// Allows painting grass density masks directly in the scene view.
/// </summary>
[CustomEditor(typeof(GrassRenderer))]
public class GrassPainterEditor : Editor
{
    private GrassRenderer grassRenderer;
    
    // Painting settings
    private bool isPaintMode = false;
    private float brushSize = 5f;
    private float brushStrength = 1f;
    private float brushHardness = 0.5f;
    private bool isErasing = false;
    
    // Serialized properties
    private SerializedProperty grassMaskProp;
    private SerializedProperty terrainProp;
    private SerializedProperty terrainSizeProp;
    
    // Brush preview
    private Vector3 lastBrushPosition;
    private bool isValidBrushPosition;
    
    // Styles
    private GUIStyle headerStyle;
    private GUIStyle buttonStyle;
    
    private void OnEnable()
    {
        grassRenderer = (GrassRenderer)target;
        grassMaskProp = serializedObject.FindProperty("grassMask");
        terrainProp = serializedObject.FindProperty("terrain");
        terrainSizeProp = serializedObject.FindProperty("terrainSize");
    }
    
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        
        // Initialize styles
        if (headerStyle == null)
        {
            headerStyle = new GUIStyle(EditorStyles.boldLabel);
            headerStyle.fontSize = 14;
        }
        
        // Draw default inspector
        DrawDefaultInspector();
        
        EditorGUILayout.Space(20);
        
        // ===== GRASS PAINTING SECTION =====
        EditorGUILayout.LabelField("Grass Painting Tool", headerStyle);
        EditorGUILayout.Space(5);
        
        // Grass Mask field
        EditorGUILayout.PropertyField(grassMaskProp, new GUIContent("Grass Mask"));
        
        GrassPaintMask currentMask = grassMaskProp.objectReferenceValue as GrassPaintMask;
        
        EditorGUILayout.BeginHorizontal();
        
        if (GUILayout.Button("Create New Mask", GUILayout.Height(25)))
        {
            CreateNewMask();
        }
        
        EditorGUI.BeginDisabledGroup(currentMask == null);
        if (GUILayout.Button("Clear Mask (Fill)", GUILayout.Height(25)))
        {
            if (currentMask != null)
            {
                Undo.RecordObject(currentMask, "Clear Grass Mask");
                currentMask.ClearMask(1f);
                EditorUtility.SetDirty(currentMask);
                grassRenderer.RefreshGrass();
            }
        }
        
        if (GUILayout.Button("Clear Mask (Empty)", GUILayout.Height(25)))
        {
            if (currentMask != null)
            {
                Undo.RecordObject(currentMask, "Clear Grass Mask");
                currentMask.ClearMask(0f);
                EditorUtility.SetDirty(currentMask);
                grassRenderer.RefreshGrass();
            }
        }
        EditorGUI.EndDisabledGroup();
        
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.Space(10);
        
        // Paint Mode Toggle
        EditorGUI.BeginDisabledGroup(currentMask == null);
        
        Color originalColor = GUI.backgroundColor;
        GUI.backgroundColor = isPaintMode ? Color.green : originalColor;
        
        if (GUILayout.Button(isPaintMode ? "Exit Paint Mode" : "Enter Paint Mode", GUILayout.Height(30)))
        {
            isPaintMode = !isPaintMode;
            if (isPaintMode)
            {
                Tools.current = Tool.None;
                SceneView.RepaintAll();
            }
        }
        
        GUI.backgroundColor = originalColor;
        
        if (isPaintMode)
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.HelpBox(
                "Paint Mode Active!\n\n" +
                "• Left Click: Paint grass\n" +
                "• Shift + Left Click: Erase grass\n" +
                "• Mouse Wheel: Adjust brush size\n" +
                "• Press Escape to exit paint mode",
                MessageType.Info);
            
            EditorGUILayout.Space(5);
            
            // Brush Settings
            EditorGUILayout.LabelField("Brush Settings", EditorStyles.boldLabel);
            
            brushSize = EditorGUILayout.Slider("Brush Size", brushSize, 0.5f, 50f);
            brushStrength = EditorGUILayout.Slider("Brush Strength", brushStrength, 0.1f, 1f);
            brushHardness = EditorGUILayout.Slider("Brush Hardness", brushHardness, 0f, 1f);
            
            EditorGUILayout.Space(5);
            
            // Paint/Erase Toggle
            EditorGUILayout.BeginHorizontal();
            
            GUI.backgroundColor = !isErasing ? Color.cyan : originalColor;
            if (GUILayout.Button("Paint", GUILayout.Height(25)))
            {
                isErasing = false;
            }
            
            GUI.backgroundColor = isErasing ? Color.red : originalColor;
            if (GUILayout.Button("Erase", GUILayout.Height(25)))
            {
                isErasing = true;
            }
            
            GUI.backgroundColor = originalColor;
            EditorGUILayout.EndHorizontal();
        }
        
        EditorGUI.EndDisabledGroup();
        
        EditorGUILayout.Space(10);
        
        // Preview Mask
        if (currentMask != null && currentMask.MaskTexture != null)
        {
            EditorGUILayout.LabelField("Mask Preview", EditorStyles.boldLabel);
            
            Rect previewRect = GUILayoutUtility.GetRect(200, 200, GUILayout.ExpandWidth(true));
            EditorGUI.DrawPreviewTexture(previewRect, currentMask.MaskTexture, null, ScaleMode.ScaleToFit);
        }
        
        serializedObject.ApplyModifiedProperties();
    }
    
    private void CreateNewMask()
    {
        string path = EditorUtility.SaveFilePanelInProject(
            "Create Grass Mask",
            "GrassMask",
            "asset",
            "Choose a location to save the grass mask");
        
        if (!string.IsNullOrEmpty(path))
        {
            GrassPaintMask newMask = ScriptableObject.CreateInstance<GrassPaintMask>();
            newMask.CreateMask(512);
            
            AssetDatabase.CreateAsset(newMask, path);
            AssetDatabase.SaveAssets();
            
            grassMaskProp.objectReferenceValue = newMask;
            serializedObject.ApplyModifiedProperties();
            
            Debug.Log($"Created new grass mask at: {path}");
        }
    }
    
    private void OnSceneGUI()
    {
        if (!isPaintMode) return;
        
        GrassPaintMask currentMask = grassMaskProp.objectReferenceValue as GrassPaintMask;
        if (currentMask == null) return;
        
        Event e = Event.current;
        
        // Handle keyboard input
        if (e.type == EventType.KeyDown)
        {
            if (e.keyCode == KeyCode.Escape)
            {
                isPaintMode = false;
                Repaint();
                e.Use();
                return;
            }
        }
        
        // Handle mouse wheel for brush size
        if (e.type == EventType.ScrollWheel && e.control)
        {
            brushSize = Mathf.Clamp(brushSize - e.delta.y * 0.5f, 0.5f, 50f);
            Repaint();
            e.Use();
        }
        
        // Raycast to find paint position
        Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
        isValidBrushPosition = false;
        
        // Try to hit terrain first
        Terrain terrain = terrainProp.objectReferenceValue as Terrain;
        if (terrain != null)
        {
            TerrainCollider terrainCollider = terrain.GetComponent<TerrainCollider>();
            if (terrainCollider != null)
            {
                RaycastHit hit;
                if (terrainCollider.Raycast(ray, out hit, 10000f))
                {
                    lastBrushPosition = hit.point;
                    isValidBrushPosition = true;
                }
            }
        }
        
        // Fallback: raycast against a plane at the grass renderer's position
        if (!isValidBrushPosition)
        {
            Plane groundPlane = new Plane(Vector3.up, grassRenderer.transform.position);
            float distance;
            if (groundPlane.Raycast(ray, out distance))
            {
                lastBrushPosition = ray.GetPoint(distance);
                isValidBrushPosition = true;
            }
        }
        
        // Draw brush preview
        if (isValidBrushPosition)
        {
            Handles.color = isErasing ? new Color(1, 0, 0, 0.3f) : new Color(0, 1, 0, 0.3f);
            Handles.DrawSolidDisc(lastBrushPosition, Vector3.up, brushSize);
            
            Handles.color = isErasing ? Color.red : Color.green;
            Handles.DrawWireDisc(lastBrushPosition, Vector3.up, brushSize);
            
            // Inner hardness circle
            Handles.color = new Color(Handles.color.r, Handles.color.g, Handles.color.b, 0.5f);
            Handles.DrawWireDisc(lastBrushPosition, Vector3.up, brushSize * brushHardness);
        }
        
        // Handle painting
        bool isPainting = (e.type == EventType.MouseDown || e.type == EventType.MouseDrag) && 
                          e.button == 0 && 
                          isValidBrushPosition;
        
        // Check for shift key to toggle erase mode temporarily
        bool tempErase = e.shift;
        
        if (isPainting)
        {
            PaintAtPosition(currentMask, lastBrushPosition, tempErase ? true : isErasing);
            e.Use();
        }
        
        // Prevent selection in paint mode
        if (e.type == EventType.Layout)
        {
            HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));
        }
        
        SceneView.RepaintAll();
    }
    
    private void PaintAtPosition(GrassPaintMask mask, Vector3 worldPosition, bool erase)
    {
        // Convert world position to normalized UV coordinates
        Vector2 normalizedPos = WorldToNormalizedPosition(worldPosition);
        
        // Convert brush size to normalized radius
        float terrainSize = terrainSizeProp.floatValue;
        Terrain terrain = terrainProp.objectReferenceValue as Terrain;
        
        if (terrain != null)
        {
            terrainSize = Mathf.Max(terrain.terrainData.size.x, terrain.terrainData.size.z);
        }
        
        float normalizedRadius = brushSize / terrainSize;
        
        // Paint
        Undo.RecordObject(mask, erase ? "Erase Grass" : "Paint Grass");
        mask.Paint(normalizedPos, normalizedRadius, erase ? 0f : 1f, brushHardness);
        EditorUtility.SetDirty(mask);
        
        // Refresh grass
        grassRenderer.RefreshGrass();
    }
    
    private Vector2 WorldToNormalizedPosition(Vector3 worldPos)
    {
        Vector3 rendererPos = grassRenderer.transform.position;
        float terrainSize = terrainSizeProp.floatValue;
        
        Terrain terrain = terrainProp.objectReferenceValue as Terrain;
        if (terrain != null)
        {
            Vector3 terrainPos = terrain.transform.position;
            Vector3 terrainSizeVec = terrain.terrainData.size;
            
            float u = (worldPos.x - terrainPos.x) / terrainSizeVec.x;
            float v = (worldPos.z - terrainPos.z) / terrainSizeVec.z;
            
            return new Vector2(u, v);
        }
        
        // Fallback for non-terrain mode
        float halfSize = terrainSize * 0.5f;
        float u2 = (worldPos.x - rendererPos.x + halfSize) / terrainSize;
        float v2 = (worldPos.z - rendererPos.z + halfSize) / terrainSize;
        
        return new Vector2(u2, v2);
    }
}
