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


public class GerstnerWave : WaterWave
{
    //public WaterResources waterResources;

    public WaveData[] Waves = new WaveData[3];
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
        waterMaterial.SetVector("_Wave1", Waves[0].waveData);
        waterMaterial.SetVector("_Wave2", Waves[1].waveData);
        waterMaterial.SetVector("_Wave3", Waves[2].waveData);

        
    }
}
