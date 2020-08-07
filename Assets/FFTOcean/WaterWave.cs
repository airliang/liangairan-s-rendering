using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WaterWave :MonoBehaviour
{
    public virtual void ApplyMaterial(Material material)
    {

    }

    public virtual float GetWaterMaxHeight()
    {
        return 0;
    }
}
