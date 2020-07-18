using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

#if UNITY_EDITOR

public enum RampmapStyle
{
    Curve = 0,
    Segment2,
    Segment3,
}

public class RampmapEditor : EditorWindow
{
    static RampmapEditor _windowInstance;

    Texture2D m_texture;
    AnimationCurve animationCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

    int selected = 0;
    Vector3 rampMapSegment = Vector3.one;
    Vector2 intervals = Vector2.zero;

    [MenuItem("Tools/RampMap Editor", false, 0)]
    static void ShowEditorWindow()
    {
        if (_windowInstance == null)
        {
            _windowInstance = EditorWindow.GetWindow(typeof(RampmapEditor), true, "rampmap editor") as RampmapEditor;
            //SceneView.onSceneGUIDelegate += OnSceneGUI;
        }
    }

    private void OnGUI()
    {
        /*
        RampMap rampMap = new RampMap();
        Rect position = new Rect(10, 10, 200, 200);
        Rect suffixPos = EditorGUI.PrefixLabel(position, GUIUtility.GetControlID(FocusType.Passive), new GUIContent("Click Me"));
        suffixPos.y = position.y;
        suffixPos.height = EditorGUIUtility.singleLineHeight;
        GUI.DrawTexture(suffixPos, rampMap?.texture);
        
        position.x += 10;
        position.width -= 10;
        position.y += EditorGUIUtility.singleLineHeight;
        var sizeRect = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
        position.y += EditorGUIUtility.singleLineHeight;
        var curveRect = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);

        EditorGUI.BeginChangeCheck();
        var modifiedSize = (TextureSize)EditorGUI.EnumPopup(sizeRect, "Size", rampMap.size);
        var modifiedCurve = EditorGUI.CurveField(curveRect, "Curve", rampMap.curve);
        if (EditorGUI.EndChangeCheck())
        {
            rampMap.size = modifiedSize;
            rampMap.curve = modifiedCurve;
            rampMap?.UpdateRampMap();
        }
        */

        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("ramp map：");
        
        EditorGUILayout.EndHorizontal();

        
        string[] options = new string[]
        {
            "curve", "2 level", "3 level",
        };
        selected = EditorGUILayout.Popup("Rampmap type", selected, options);
        Debug.Log("selected = " + selected);
        int previewPositionY = 80;
        if (selected == (int)RampmapStyle.Curve)
        {
            EditorGUILayout.BeginHorizontal();
            animationCurve = EditorGUILayout.CurveField("Animation on Rampmap", animationCurve);
            EditorGUILayout.EndHorizontal();
        }
        else if (selected == (int)RampmapStyle.Segment2)
        {
            EditorGUILayout.BeginHorizontal();
            intervals.x = EditorGUILayout.Slider(new GUIContent("interval point"), intervals.x, 0.0f, 1.0f);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal();
            rampMapSegment.x = EditorGUILayout.FloatField(new GUIContent("segment1"), rampMapSegment.x);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal();
            rampMapSegment.y = EditorGUILayout.FloatField(new GUIContent("segment2"), rampMapSegment.x);
            EditorGUILayout.EndHorizontal();
        }
        else
        {
            EditorGUILayout.BeginHorizontal();
            rampMapSegment.x = EditorGUILayout.FloatField(new GUIContent("segment1"), rampMapSegment.x);
            EditorGUILayout.EndHorizontal();
        }
        

        if (GUILayout.Button("Update Rampmap"))
            UpdateRampMap();

        EditorGUI.PrefixLabel(new Rect(15, previewPositionY, 100, 15), 0, new GUIContent("Preview:"));
        if (m_texture == null)
        {
            m_texture = new Texture2D(128, 1, TextureFormat.R8, true);
        }
        EditorGUI.DrawPreviewTexture(new Rect(15, previewPositionY + 15, 100, 100), m_texture);
    }

    void UpdateRampMap()
    {
        if (m_texture == null)
        {
            m_texture = new Texture2D(128, 1, TextureFormat.R8, true);
        }
        for (int u = 0; u < m_texture.width; ++u)
        {
            float val = animationCurve.Evaluate((u + 0.5f) / m_texture.width);
            m_texture.SetPixel(u, 0, new Color(val, val, val));
        }
        m_texture.Apply();
    }

    private void OnDestroy()
    {
        
    }
}
#endif
