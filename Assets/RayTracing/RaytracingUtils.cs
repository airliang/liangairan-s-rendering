using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class RaytracingUtils
{
    public static GPUMaterial ConvertUnityStandardMaterial(Material material)
    {
        GPUMaterial gpuMaterial = new GPUMaterial();
        if (material.HasProperty("_MainTex"))
        {
            Texture2D mainTex = material.GetTexture("_MainTex") as Texture2D;
            if (mainTex != null)
            {

            }
        }

        return gpuMaterial;
    }
}
