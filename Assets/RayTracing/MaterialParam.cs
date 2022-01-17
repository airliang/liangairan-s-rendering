using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum BxDFType
{
    BSDF_REFLECTION = 1 << 0,
    BSDF_TRANSMISSION = 1 << 1,
    BSDF_DIFFUSE = 1 << 2,
    BSDF_GLOSSY = 1 << 3,
    BSDF_SPECULAR = 1 << 4,
    BSDF_DISNEY = 1 << 5,
    BSDF_ALL = BSDF_DIFFUSE | BSDF_GLOSSY | BSDF_SPECULAR | BSDF_REFLECTION |
               BSDF_TRANSMISSION,
};

public class MaterialParam
{
    public Texture2D AlbedoMap;
    //public uint AlbedoMapMask = 0;
    public Color BaseColor = Color.white;
    public Texture2D NormalMap;
    //public uint NormalMapMask = 0;
    //public Texture2D SpecularGlossMap;
    public Texture2D MetallicGlossMap;
    //public uint MetallicGlossMapMask = 0;
    public float Metallic = 0;
    public float Specular = 0;
    public float Roughness = 0;
    public float Anisotropy = 0;
    public float Cutoff = 0;

    public static MaterialParam ConvertUnityMaterial(Material material)
    {
        MaterialParam materialParam = new MaterialParam();
        if (material.shader.name == "Standard")
        {

        }
        else if (material.shader.name == "RayTracing/Disney")
        {
            Texture mainTex = material.GetTexture("_MainTex");
            if (mainTex != null)
                materialParam.AlbedoMap = mainTex as Texture2D;
            materialParam.BaseColor = material.GetColor("_BaseColor");
            Texture normalTex = material.GetTexture("_NormalTex");
            if (normalTex != null)
                materialParam.NormalMap = normalTex as Texture2D;
            Texture metallicTex = material.GetTexture("_MetallicGlossMap");
            if (metallicTex != null)
                materialParam.MetallicGlossMap = metallicTex as Texture2D;
            materialParam.Metallic = material.GetFloat("_metallic");
            materialParam.Roughness = material.GetFloat("_roughness");
            materialParam.Specular = material.GetFloat("_specular");
            materialParam.Anisotropy = material.GetFloat("_anisotropy");
            materialParam.Cutoff = material.GetFloat("_Cutoff");
        }
        else
        {
            //return null;
            Color color = Color.black;
            if (material.HasProperty("_Color"))
            {
                color = material.GetColor("_Color");
            }
            else if (material.HasProperty("_MainColor"))
            {
                color = material.GetColor("_MainColor");
            }
            else if (material.HasProperty("_BaseColor"))
            {
                color = material.GetColor("_BaseColor");
            }

            if (color.a < 1.0f)
                return null;
        }
        return materialParam;
    }

    public GPUMaterial ConvertToGPUMaterial()
    {
        GPUMaterial gpuMaterial = new GPUMaterial();
        if (AlbedoMap != null)
        {
            gpuMaterial.albedoMapMask = MathUtil.UInt32BitsToSingle(RayTracingTextures.Instance.AddAlbedoTexture(AlbedoMap));
            uint mask = MathUtil.SingleToUint32Bits(gpuMaterial.albedoMapMask) & 0x80000000;
            mask = mask >> 31;
            if (((MathUtil.SingleToUint32Bits(gpuMaterial.albedoMapMask) & 0x80000000) >> 31) > 0)
                Debug.Log("mask = " + mask);
        }
        else
            gpuMaterial.albedoMapMask = MathUtil.UInt32BitsToSingle(0);
        gpuMaterial.baseColor = BaseColor.LinearToVector4();

        if (NormalMap != null)
        {
            gpuMaterial.normalMapMask = MathUtil.UInt32BitsToSingle(RayTracingTextures.Instance.AddNormalTexture(NormalMap));
        }
        else
            gpuMaterial.normalMapMask = MathUtil.UInt32BitsToSingle(0);

        if (MetallicGlossMap != null)
        {
            gpuMaterial.metallicMapMask = MathUtil.UInt32BitsToSingle(RayTracingTextures.Instance.AddMetallicTexture(MetallicGlossMap));
            //set the channel mask;
        }
        else
            gpuMaterial.metallicMapMask = MathUtil.UInt32BitsToSingle(0);
        gpuMaterial.materialType = (int)BxDFType.BSDF_DIFFUSE;
        gpuMaterial.metallic = Metallic;
        gpuMaterial.roughness = Roughness;
        gpuMaterial.specular = Specular;
        gpuMaterial.anisotropy = Anisotropy;
        //gpuMaterial.albedoMapMask = AlbedoMapMask;
        //gpuMaterial.normalMapMask = NormalMapMask;
        //gpuMaterial.metallicMapMask = MetallicGlossMapMask;

        return gpuMaterial;
    }
}

public class RayTracingTextures : Singleton<RayTracingTextures>
{
    public Texture2DArray m_albedos128;
    public Texture2DArray m_albedos256;
    public Texture2DArray m_albedos512;
    public RenderTexture m_albedos1024;
    public Texture2DArray m_albedos2048;

    public Texture2DArray m_normals128;
    public Texture2DArray m_normals256;
    public Texture2DArray m_normals512;
    public Texture2DArray m_normals1024;
    public Texture2DArray m_normals2048;

    public Texture2DArray m_metallics128;
    public Texture2DArray m_metallics256;
    public Texture2DArray m_metallics512;
    public Texture2DArray m_metallics1024;
    public Texture2DArray m_metallics2048;

    private Dictionary<Texture, uint> albedoMapMasks = new Dictionary<Texture, uint>();
    private Dictionary<Texture, uint> normalMapMasks = new Dictionary<Texture, uint>();
    private Dictionary<Texture, uint> metallicMapMasks = new Dictionary<Texture, uint>();
    private int[] m_textureArrayCounters = new int[5];
    private int[] m_normalArrayCounters = new int[5];
    private int[] m_metallicArrayCounters = new int[5];

    public Texture GetAlbedo2DArray(int size)
    {
        switch (size)
        {
            case 128:
                if (m_albedos128 == null)
                    m_albedos128 = new Texture2DArray(size, size, 16, TextureFormat.DXT1, true)
                    {
                        name = "albedoArray128"
                    };
                return m_albedos128;
            case 256:
                if (m_albedos256 == null)
                    m_albedos256 = new Texture2DArray(size, size, 16, TextureFormat.DXT1, true)
                    {
                        name = "albedoArray256"
                    };
                return m_albedos256;
            case 512:
                if (m_albedos512 == null)
                    m_albedos512 = new Texture2DArray(size, size, 16, TextureFormat.DXT1, true)
                    {
                        name = "albedoArray512"
                    };
                return m_albedos512;
            case 1024:
                if (m_albedos1024 == null)
                    //m_albedos1024 = new Texture2DArray(size, size, 16, TextureFormat.DXT1, true)
                    m_albedos1024 = new RenderTexture(size, size, 0, UnityEngine.Experimental.Rendering.GraphicsFormat.R8G8B8A8_UNorm)
                    {
                        name = "albedoArray1024",
                        dimension = UnityEngine.Rendering.TextureDimension.Tex2DArray,
                        volumeDepth = 16,
                    };
                return m_albedos1024;
        }
        

        return null;
    }

    public Texture2DArray GetNormal2DArray(int size)
    {
        switch (size)
        {
            case 128:
                if (m_normals128 == null)
                    m_normals128 = new Texture2DArray(size, size, 16, TextureFormat.DXT1, true)
                    {
                        name = "normalArray128"
                    };
                return m_normals128;
            case 256:
                if (m_normals256 == null)
                    m_normals256 = new Texture2DArray(size, size, 16, TextureFormat.DXT1, true)
                    {
                        name = "normalArray256"
                    };
                return m_normals256;
            case 512:
                if (m_normals512 == null)
                    m_normals512 = new Texture2DArray(size, size, 16, TextureFormat.DXT1, true)
                    {
                        name = "normalArray512"
                    };
                return m_normals512;
            case 1024:
                if (m_normals1024 == null)
                    m_normals1024 = new Texture2DArray(size, size, 16, TextureFormat.DXT1, true)
                    {
                        name = "normalArray1024"
                    };
                return m_normals1024;
        }

        return null;
    }

    public uint AddAlbedoTexture(Texture2D texture)
    {
        uint mask = 0x80000000;
        if (albedoMapMasks.TryGetValue(texture, out mask))
        {
            return mask;
        }

        int textureArrayId = 0;
        Texture dstTextureArray = null;

        if (texture.width == 128 && texture.height == 128)
        {
            textureArrayId = 0;
            if (m_albedos128 == null)
            {
                m_albedos128 = new Texture2DArray(128, 128, 16, texture.format, true)
                {
                    name = "albedoArray128"
                };
            }
            dstTextureArray = m_albedos128;
        }

        if (texture.width == 256 && texture.height == 256)
        {
            textureArrayId = 1;
            if (m_albedos256 == null)
            {
                m_albedos256 = new Texture2DArray(256, 256, 16, texture.format, true)
                {
                    name = "albedoArray256"
                };
            }
            dstTextureArray = m_albedos256;
        }

        if (texture.width == 512 && texture.height == 512)
        {
            textureArrayId = 2;
            if (m_albedos512 == null)
            {
                m_albedos512 = new Texture2DArray(512, 512, 16, texture.format, true)
                {
                    name = "albedoArray512"
                };
            }
            dstTextureArray = m_albedos512;
        }

        if (texture.width == 1024 && texture.height == 1024)
        {
            textureArrayId = 3;
            if (m_albedos1024 == null)
            {
                //m_albedos1024 = new Texture2DArray(1024, 1024, 16, texture.format, true)
                m_albedos1024 = new RenderTexture(1024, 1024, 0, UnityEngine.Experimental.Rendering.GraphicsFormat.R8G8B8A8_UNorm)
                {
                    name = "albedoArray1024",
                    dimension = UnityEngine.Rendering.TextureDimension.Tex2DArray,
                    volumeDepth = 16,
                    useMipMap = true,
                };
            }
            dstTextureArray = m_albedos1024;
        }

        if (texture.width == 2048 && texture.height == 2048)
        {
            textureArrayId = 4;
            if (m_albedos2048 == null)
            {
                m_albedos2048 = new Texture2DArray(2048, 2048, 8, texture.format, true)
                {
                    name = "albedoArray2048"
                };
            }
            dstTextureArray = m_albedos2048;
        }
        
        if (dstTextureArray == m_albedos1024)
        {
            Graphics.Blit(texture, m_albedos1024, 0, m_textureArrayCounters[textureArrayId]);
        }
        else
            Graphics.CopyTexture(texture, 0, dstTextureArray, m_textureArrayCounters[textureArrayId]);
        mask = 0x80000000;
        mask |= (uint)((m_textureArrayCounters[textureArrayId] & 0x000000ff) | ((textureArrayId & 0xff) << 8));

        uint TextureArrayID = (mask & 0x0000ff00) >> 8;
        m_textureArrayCounters[textureArrayId]++;
        albedoMapMasks.Add(texture, mask);

        return mask;
    }

    public uint AddNormalTexture(Texture2D texture)
    {
        uint mask = 0x80000000;
        if (normalMapMasks.TryGetValue(texture, out mask))
        {
            return mask;
        }

        int textureArrayId = 0;
        Texture2DArray dstTextureArray = null;

        if (texture.width == 128 && texture.height == 128)
        {
            textureArrayId = 0;
            if (m_normals128 == null)
            {
                m_normals128 = new Texture2DArray(128, 128, 16, texture.format, true);
            }
            dstTextureArray = m_normals128;
        }

        if (texture.width == 256 && texture.height == 256)
        {
            textureArrayId = 1;
            if (m_normals256 == null)
            {
                m_normals256 = new Texture2DArray(256, 256, 16, texture.format, true);
            }
            dstTextureArray = m_normals256;
        }

        if (texture.width == 512 && texture.height == 512)
        {
            textureArrayId = 2;
            if (m_normals512 == null)
            {
                m_normals512 = new Texture2DArray(512, 512, 16, texture.format, true);
            }
            dstTextureArray = m_normals512;
        }

        if (texture.width == 1024 && texture.height == 1024)
        {
            textureArrayId = 3;
            if (m_normals1024 == null)
            {
                m_normals1024 = new Texture2DArray(1024, 1024, 16, texture.format, true);
            }
            dstTextureArray = m_normals1024;
        }

        if (texture.width == 2048 && texture.height == 2048)
        {
            textureArrayId = 4;
            if (m_normals2048 == null)
            {
                m_normals2048 = new Texture2DArray(2048, 2048, 8, texture.format, true);
            }
            dstTextureArray = m_normals2048;
        }


        Graphics.CopyTexture(texture, 0, dstTextureArray, m_normalArrayCounters[textureArrayId]);
        mask = 0x80000000;
        mask |= (uint)((m_normalArrayCounters[textureArrayId] & 0x000000ff) | (textureArrayId & 0xff << 8));
        m_normalArrayCounters[textureArrayId]++;
        normalMapMasks.Add(texture, mask);

        return mask;
    }

    public uint AddMetallicTexture(Texture2D texture)
    {
        uint mask = 0x80000000;
        if (metallicMapMasks.TryGetValue(texture, out mask))
        {
            return mask;
        }

        int textureArrayId = 0;
        Texture2DArray dstTextureArray = null;

        if (texture.width == 128 && texture.height == 128)
        {
            textureArrayId = 0;
            if (m_metallics128 == null)
            {
                m_metallics128 = new Texture2DArray(128, 128, 16, texture.format, true);
            }
            dstTextureArray = m_metallics128;
        }

        if (texture.width == 256 && texture.height == 256)
        {
            textureArrayId = 1;
            if (m_metallics256 == null)
            {
                m_metallics256 = new Texture2DArray(256, 256, 16, texture.format, true);
            }
            dstTextureArray = m_metallics256;
        }

        if (texture.width == 512 && texture.height == 512)
        {
            textureArrayId = 2;
            if (m_metallics512 == null)
            {
                m_metallics512 = new Texture2DArray(512, 512, 16, texture.format, true);
            }
            dstTextureArray = m_metallics512;
        }

        if (texture.width == 1024 && texture.height == 1024)
        {
            textureArrayId = 3;
            if (m_metallics1024 == null)
            {
                m_metallics1024 = new Texture2DArray(1024, 1024, 16, texture.format, true);
            }
            dstTextureArray = m_metallics1024;
        }

        if (texture.width == 2048 && texture.height == 2048)
        {
            textureArrayId = 4;
            if (m_metallics2048 == null)
            {
                m_metallics2048 = new Texture2DArray(2048, 2048, 8, texture.format, true);
            }
            dstTextureArray = m_metallics2048;
        }


        Graphics.CopyTexture(texture, 0, dstTextureArray, m_metallicArrayCounters[textureArrayId]);
        mask = 0x80000000;
        mask |= (uint)((m_metallicArrayCounters[textureArrayId] & 0x000000ff) | (textureArrayId & 0xff << 8));
        m_metallicArrayCounters[textureArrayId]++;
        metallicMapMasks.Add(texture, mask);

        return mask;
    }

    public void Release()
    {
        void ReleaseTextureArray(Texture texture2DArray)
        {
            if (texture2DArray != null)
            {
                Object.Destroy(texture2DArray);
            }
            texture2DArray = null;
        }

        ReleaseTextureArray(m_albedos128);
        ReleaseTextureArray(m_albedos256);
        ReleaseTextureArray(m_albedos512);
        ReleaseTextureArray(m_albedos1024);
        ReleaseTextureArray(m_albedos2048);

        ReleaseTextureArray(m_normals128);
        ReleaseTextureArray(m_normals256);
        ReleaseTextureArray(m_normals512);
        ReleaseTextureArray(m_normals1024);
        ReleaseTextureArray(m_normals2048);

        ReleaseTextureArray(m_metallics128);
        ReleaseTextureArray(m_metallics256);
        ReleaseTextureArray(m_metallics512);
        ReleaseTextureArray(m_metallics1024);
        ReleaseTextureArray(m_metallics2048);

        albedoMapMasks.Clear();
        normalMapMasks.Clear();
        metallicMapMasks.Clear();

        for (int i = 0; i < 5; ++i)
        {
            m_textureArrayCounters[i] = 0;
            m_normalArrayCounters[i] = 0;
            m_metallicArrayCounters[i] = 0;
        }
    }
}
