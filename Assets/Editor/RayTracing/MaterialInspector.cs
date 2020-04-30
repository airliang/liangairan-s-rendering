using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

[CanEditMultipleObjects]
[CustomEditor(typeof(BSDFMaterial))]
public class MaterialInspector : Editor
{
    public override void OnInspectorGUI()
    {
        BSDFMaterial bsdf = target as BSDFMaterial;

        Undo.RecordObject(bsdf, "Record BSDFMaterial");

        base.OnInspectorGUI();

        if (bsdf.materialType == BSDFMaterial.BSDFType.Plastic)
        {
            DisplayBSDFTexture(ref bsdf.plastic.kd, "Plastic kd：");

            DisplayBSDFTexture(ref bsdf.plastic.ks, "Plastic ks：");

            DisplayBSDFTexture(ref bsdf.plastic.roughnessTexture, "Plastic roughness texture：");
            
        }
        else if (bsdf.materialType == BSDFMaterial.BSDFType.Matte)
        {
            DisplayBSDFTexture(ref bsdf.matte.kd, "Matte kd：");
            DisplayBSDFTexture(ref bsdf.matte.sigma, "Matte sigma：");
        }
        else if (bsdf.materialType == BSDFMaterial.BSDFType.Mirror)
        {
            DisplayBSDFTexture(ref bsdf.mirror.kr, "Mirror reflection：");
        }
        else if (bsdf.materialType == BSDFMaterial.BSDFType.Metal)
        {

        }

        
    }

    private void DisplayBSDFTexture(ref BSDFSpectrumTexture texture, string textureName)
    {
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label(textureName);
        texture.type =
            (BSDFTextureType)EditorGUILayout.EnumPopup(texture.type);
        EditorGUILayout.EndHorizontal();

        if (texture.type == BSDFTextureType.Constant)
        {
            EditorGUILayout.BeginHorizontal();
            texture.spectrum = EditorGUILayout.ColorField(texture.spectrum);
            EditorGUILayout.EndHorizontal();
        }
        else if (texture.type == BSDFTextureType.Bilerp)
        {
            
        }
        else if (texture.type == BSDFTextureType.Image)
        {
            EditorGUILayout.BeginHorizontal();
            texture.image = EditorGUILayout.ObjectField(texture.image, typeof(Texture2D), true) as Texture2D;
            EditorGUILayout.EndHorizontal();
        }

        
    }

    private void DisplayBSDFTexture(ref BSDFFloatTexture texture, string textureName)
    {
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label(textureName);
        texture.type =
            (BSDFTextureType)EditorGUILayout.EnumPopup(texture.type);
        EditorGUILayout.EndHorizontal();

        if (texture.type == BSDFTextureType.Constant)
        {
            EditorGUILayout.BeginHorizontal();
            texture.constantValue = EditorGUILayout.Slider(texture.constantValue, 0, 1);
            EditorGUILayout.EndHorizontal();
        }
        else if (texture.type == BSDFTextureType.Bilerp)
        {

        }
        else if (texture.type == BSDFTextureType.Image)
        {
            EditorGUILayout.BeginHorizontal();
            texture.image = EditorGUILayout.ObjectField(texture.image, typeof(Texture2D), true) as Texture2D;
            EditorGUILayout.EndHorizontal();
        }


    }
}
