using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public struct WaveData
{
    //波的方向
    //x  wavelength
    //y  amplitude
    //zw direction
    public Vector4 waveData;
    //圆形波的中心位置(0, 1)
    public Vector2 circle;
    //波长
    //public float wavelength;
    //public float amplitude;
    //public float speed;
    public float steepness;
}

[System.Serializable]
[CreateAssetMenu(fileName = "GerstnerWave", menuName = "water/GerstnerWave", order = 0)]
public class GerstnerWaveAsset : ScriptableObject
{
    public WaveData[] Waves;
}


public class GerstnerWave : WaterWave
{
    //public WaterResources waterResources;

    //public WaveData[] Waves = new WaveData[3];
    public GerstnerWaveAsset waveAsset;
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public override void ApplyMaterial(Material waterMaterial)
    {
        if (waveAsset == null)
        {
            waveAsset = Resources.Load<GerstnerWaveAsset>("GerstnerWave");
        }
        if (waveAsset != null && waveAsset.Waves != null)
        {
            if (waveAsset.Waves.Length > 2)
            {
                waterMaterial.SetVector("_Wave1", waveAsset.Waves[0].waveData);
                waterMaterial.SetVector("_Wave2", waveAsset.Waves[1].waveData);
                waterMaterial.SetVector("_Wave3", waveAsset.Waves[2].waveData);
            }
        }
    }

    public override float GetWaterMaxHeight()
    {
        float maxHeight = 0;
        if (waveAsset != null && waveAsset.Waves != null)
        {
            for (int i = 0; i < waveAsset.Waves.Length; i++)
            {
                maxHeight = Mathf.Max(maxHeight, waveAsset.Waves[i].waveData.y);
            }
        }

        return maxHeight;
    }
}
