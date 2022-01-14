using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class RaytracingUtils
{
    public static uint TEXTURED_PARAM_MASK = 0x80000000;

    public static uint SetTextureID(uint textureId)
    {
        return TEXTURED_PARAM_MASK | (textureId & 0x1fffffff);
    }

    public static int GetTextureIndex(Texture texture, List<Texture> textures)
    {
        if (textures.Count == 0)
            return 0;
        return textures.FindIndex(0, textures.Count, a => (texture == a));
    }

    public static GPUMaterial ConvertUnityStandardMaterial(Material material, List<Texture> textures)
    {
        GPUMaterial gpuMaterial = new GPUMaterial();
        if (material.HasProperty("_BaseColor"))
        {
            Color baseColor = material.GetColor("_BaseColor");
            gpuMaterial.baseColor = baseColor.LinearToVector4();
        }

        if (material.HasProperty("_MainTex"))
        {
            Texture mainTex = material.GetTexture("_MainTex");
            if (mainTex != null)
            {
                int textureId = GetTextureIndex(mainTex, textures);
                
                gpuMaterial.baseColor.w = MathUtil.Int32BitsToSingle((int)SetTextureID((uint)textureId));
            }
        }

        return gpuMaterial;
    }
}
