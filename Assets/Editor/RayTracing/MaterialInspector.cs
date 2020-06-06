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

        //base.OnInspectorGUI();

        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("BSDF type:");
        bsdf.materialType =
            (BSDFMaterial.BSDFType)EditorGUILayout.EnumPopup(bsdf.materialType);
        EditorGUILayout.EndHorizontal();

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
        else if (bsdf.materialType == BSDFMaterial.BSDFType.Glass)
        {
            DisplayBSDFTexture(ref bsdf.glass.kr, "Glass reflection");
            DisplayBSDFTexture(ref bsdf.glass.ks, "Glass transmission");
            DisplayBSDFTexture(ref bsdf.glass.uRougness, "u-rougness");
            DisplayBSDFTexture(ref bsdf.glass.vRougness, "u-rougness");
            DisplayBSDFTexture(ref bsdf.glass.index, "refraction index", 3.0f);
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

            texture.imageFile = System.IO.Path.GetFileName(AssetDatabase.GetAssetPath(texture.image));
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(texture.imageFile);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            texture.gamma = EditorGUILayout.Toggle("gamma", texture.gamma);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            texture.wrap = (TextureWrapMode)EditorGUILayout.EnumPopup("Wrap mode:", texture.wrap);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            texture.mappingType = (BSDFTextureUVMapping)EditorGUILayout.EnumPopup("mapping type:", texture.mappingType);
            EditorGUILayout.EndHorizontal();

            if (texture.mappingType == BSDFTextureUVMapping.UVMapping2D)
            {
                if (texture.uvMapping2D == null)
                {
                    texture.uvMapping2D = new UVMapping2D();
                }
                EditorGUILayout.BeginHorizontal();
                texture.uvMapping2D.su = EditorGUILayout.FloatField("su:", texture.uvMapping2D.su);
                texture.uvMapping2D.sv = EditorGUILayout.FloatField("sv:", texture.uvMapping2D.sv);
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                texture.uvMapping2D.du = EditorGUILayout.FloatField("du:", texture.uvMapping2D.du);
                texture.uvMapping2D.dv = EditorGUILayout.FloatField("dv:", texture.uvMapping2D.dv);
                EditorGUILayout.EndHorizontal();
            }
        }

        
    }

    private void DisplayBSDFTexture(ref BSDFFloatTexture texture, string textureName, float maxConstant = 1.0f)
    {
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label(textureName);
        texture.type =
            (BSDFTextureType)EditorGUILayout.EnumPopup(texture.type);
        EditorGUILayout.EndHorizontal();

        if (texture.type == BSDFTextureType.Constant)
        {
            EditorGUILayout.BeginHorizontal();
            texture.constantValue = EditorGUILayout.Slider(texture.constantValue, 0, maxConstant);
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

            texture.imageFile = System.IO.Path.GetFileName(AssetDatabase.GetAssetPath(texture.image));
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(texture.imageFile);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            texture.gamma = EditorGUILayout.Toggle("gamma", texture.gamma);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            texture.wrap = (TextureWrapMode)EditorGUILayout.EnumPopup("Wrap mode:", texture.wrap);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            texture.mappingType = (BSDFTextureUVMapping)EditorGUILayout.EnumPopup("mapping type:", texture.mappingType);
            EditorGUILayout.EndHorizontal();

            if (texture.mappingType == BSDFTextureUVMapping.UVMapping2D)
            {
                if (texture.uvMapping2D == null)
                {
                    texture.uvMapping2D = new UVMapping2D();
                }

                EditorGUILayout.BeginHorizontal();
                texture.uvMapping2D.su = EditorGUILayout.FloatField("su:", texture.uvMapping2D.su);
                texture.uvMapping2D.sv = EditorGUILayout.FloatField("sv:", texture.uvMapping2D.sv);
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                texture.uvMapping2D.du = EditorGUILayout.FloatField("du:", texture.uvMapping2D.du);
                texture.uvMapping2D.dv = EditorGUILayout.FloatField("dv:", texture.uvMapping2D.dv);
                EditorGUILayout.EndHorizontal();
            }
        }

        
    }
}
