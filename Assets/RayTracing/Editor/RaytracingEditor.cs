using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class RaytracingEditor
{
    [MenuItem("Tools/RunPathTracingCPU")]
    private static void RunPathTracingCPU()
    {
        Raytracing mono = Component.FindObjectOfType<Raytracing>();
        if (mono != null)
        {
            mono.InitScene();
            Vector3[] pixels = new Vector3[Screen.width * Screen.height];

            for (int y = 0; y < Screen.height; ++y)
            {
                for (int x = 0; x < Screen.width; ++x)
                {
                    int index = x + y * Screen.width;
                    pixels[index] = mono.OnePathTracing(x, y, Screen.width, 64);
                }
            }

            Texture2D output = new Texture2D(Screen.width, Screen.height, TextureFormat.ARGB32, false);
            for (int y = 0; y < Screen.height; ++y)
            {
                for (int x = 0; x < Screen.width; ++x)
                {
                    int index = x + y * Screen.width;
                    Color color = pixels[index].ToColorGamma(); ;
                    output.SetPixel(x, y, color);
                }
            }
            AssetDatabase.CreateAsset(output, "Assets/RayTracing/output.png");
            Object.Destroy(output);
            mono.ReleaseGPUDatas();
        }
    }
}
