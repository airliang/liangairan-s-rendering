using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum BSDFTextureType
{
    Constant,
    Bilerp,
    Image,
}

public struct BSDFFloatTexture
{
    public BSDFTextureType type;
    public float constantValue;
    public Texture2D image;
}

public struct BSDFSpectrumTexture
{
    public BSDFTextureType type;
    public Color spectrum;
    public Texture2D image;
}


public struct Plastic
{
    public BSDFSpectrumTexture kd;
    public BSDFSpectrumTexture ks;

    public BSDFFloatTexture roughnessTexture;
}

public struct Mirror
{
    public BSDFSpectrumTexture kr;
}

public struct Matte
{
    public BSDFSpectrumTexture kd;
    public BSDFFloatTexture sigma;
}

public class BSDFMaterial : MonoBehaviour
{
    public enum BSDFType
    {
        Matte,
        Plastic,
        Mirror,
        Metal,
    }

    public BSDFType materialType;

    public Plastic plastic;
    public Matte matte;
    public Mirror mirror;

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
