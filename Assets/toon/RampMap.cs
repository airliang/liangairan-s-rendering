using UnityEngine;
using UnityEditor;

public enum TextureSize
{
    _1 = 1,
    _2 = 2,
    _4 = 4,
    _8 = 8,
    _16 = 16,
    _32 = 32,
    _64 = 64,
    _128 = 128,
    _256 = 256,
    _512 = 512,
    _1024 = 1024,
    _2048 = 2048,
    _4096 = 4096,
    _8192 = 8192
}

[System.Serializable]
public class RampMap
{
    
    [SerializeField]
    protected TextureSize m_size = TextureSize._128;
    public TextureSize size
    {
        get => m_size;
        set
        {
            if (m_size != value)
            {
                m_size = value;
                texture.Resize((int)m_size, 1);
                UpdateRampMap();
            }
        }
    }
    [SerializeField]
    protected AnimationCurve m_curve;
    public AnimationCurve curve
    {
        get
        {
            if(m_curve == null)
            {
                m_curve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
            }
            return m_curve;
        }
        set
        {
            if(value != null && value != m_curve)
            {
                m_curve = value;
                UpdateRampMap();
            }
        }
    }

    protected Texture2D m_texture;
    public Texture2D texture
    {
        get
        {
            if(m_texture == null)
            {
                m_texture = new Texture2D((int)size, 1, TextureFormat.R8, false)
                {
                    wrapMode = TextureWrapMode.Clamp
                };
                UpdateRampMap();
            }
            return m_texture;
        }
    }
        
    public RampMap(AnimationCurve curve = null, int size = 128)
    {
        this.curve = curve == null ? AnimationCurve.Constant(0f, 1f, 1f) : curve;
    }

    public void UpdateRampMap()
    {
        for (int u = 0; u < texture.width; ++u)
        {
            float val = curve.Evaluate((u + 0.5f) / texture.width);
            texture.SetPixel(u, 0, new Color(val, val, val));
        }
        texture.Apply();
    }

    public static implicit operator RampMap(AnimationCurve curve) => new RampMap(curve);

    public static implicit operator Texture2D(RampMap rampMap) => rampMap?.texture;
}

#if UNITY_EDITOR
[CustomPropertyDrawer(typeof(RampMap))]
public class RampMapDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        RampMap rampMap = fieldInfo.GetValue(property.serializedObject.targetObject) as RampMap;

        Rect suffixPos = EditorGUI.PrefixLabel(position, GUIUtility.GetControlID(FocusType.Passive), label);
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
        if(EditorGUI.EndChangeCheck())
        {
            rampMap.size = modifiedSize;
            rampMap.curve = modifiedCurve;
            rampMap?.UpdateRampMap();
        }
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        return EditorGUIUtility.singleLineHeight * 3;
    }
}
#endif