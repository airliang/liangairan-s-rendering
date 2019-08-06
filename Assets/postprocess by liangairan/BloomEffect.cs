using System.Collections;
using System.Collections.Generic;
using UnityEngine;
//using UnityStandardAssets.ImageEffects;

[ExecuteInEditMode]
[RequireComponent(typeof(Camera))]
[AddComponentMenu("Image Effects/Camera/BloomEffect")]
public class BloomEffect : MonoBehaviour
{
    private bool isSupported = true;
    List<float> weights = new List<float>();

    // Use this for initialization
    private void Start()
    {
		//CheckSupport(false, false);

		if (_Material == null)
		{
			Shader shader = Shader.Find("liangairan/postprocess/Bloom");
			if (shader != null)
			{
                _Material = new Material(shader);

				if (_Material == null)
				{
					Debug.LogError("Bloom shader Material create failed!");
				}
			}
			else
			{
                Debug.LogError("Bloom shader Not Exist!");
			}
		}

        weights.Add(0.227027f);
        weights.Add(0.1945946f);
        weights.Add(0.1216216f);
        weights.Add(0.054054f);
        weights.Add(0.016216f);
    }
	
	// Update is called once per frame
	void Update () {
		
	}

	//分辨率  
	public int downSample = 2;
	//采样率  
	//[Range(1.0f, 1.5f)]
	//public int samplerScale = 1;
	//高亮部分提取阈值  
	public Color colorThreshold = Color.gray;
	//Bloom泛光颜色  
	public Color bloomColor = Color.white;
	//Bloom权值  
	[Range(0.0f, 1.0f)]
	public float bloomFactor = 0.5f;

    [Range(0.25f, 7.5f)]
    public float blurSize = 0.5f;

    [Range(0.0f, 2.5f)]
    public float intensity = 0.75f;
    [Range(1, 4)]
    public int blurIterator = 1;

    Material _Material = null;

    

	void OnRenderImage(RenderTexture source, RenderTexture destination)
	{
		if (!isSupported || _Material == null)
		{
			Graphics.Blit(source, destination);
			return;
		}
		if (_Material != null)
		{
            //申请两块RT，并且分辨率按照downSameple降低  
            RenderTexture rt = RenderTexture.GetTemporary(source.width >> downSample, source.height >> downSample, 0, source.format);

            rt.filterMode = FilterMode.Bilinear;
            //temp2.filterMode = FilterMode.Bilinear;
            //直接将场景图拷贝到低分辨率的RT上达到降分辨率的效果  
            //Graphics.Blit(source, temp1);


            //根据阈值提取高亮部分,使用pass0进行高亮提取  
            _Material.SetVector("_colorThreshold", colorThreshold);
            _Material.SetFloat("_thresholdIntensity", intensity);
			Graphics.Blit(source, rt, _Material, 0);

            int lastWidth = source.width;
            int lastHeight = source.height;

            lastWidth = lastWidth >> downSample;
            lastHeight = lastHeight >> downSample;

            for (int i = 0; i < blurIterator; ++i)
            {
                
                RenderTexture temp1 = RenderTexture.GetTemporary(lastWidth, lastHeight, 0, source.format);
                //RenderTexture temp2 = RenderTexture.GetTemporary(source.width >> downSample, source.height >> downSample, 0, source.format);
                //高斯模糊，两次模糊，横向纵向，使用pass1进行高斯模糊  
                _Material.SetVector("_offsets", new Vector4(0, blurSize * 0.5f + i * 1.0f, 0, 0));
                _Material.SetFloatArray("weight", weights);
                Graphics.Blit(rt, temp1, _Material, 1);
                RenderTexture.ReleaseTemporary(rt);
                rt = temp1;

                _Material.SetVector("_offsets", new Vector4(blurSize * 0.5f + i * 1.0f, 0, 0, 0));
                temp1 = RenderTexture.GetTemporary(lastWidth, lastHeight, 0, source.format);
                Graphics.Blit(rt, temp1, _Material, 1);
                RenderTexture.ReleaseTemporary(rt);
                rt = temp1;
            }

			//Bloom，将模糊后的图作为Material的Blur图参数  
			_Material.SetTexture("_BlurTex", rt);
			_Material.SetVector("_bloomColor", bloomColor);
			_Material.SetFloat("_bloomFactor", bloomFactor);

            //使用pass2进行景深效果计算，清晰场景图直接从source输入到shader的_MainTex中
            //Graphics.Blit(temp2, destination, _Material, 2);
            Graphics.Blit(source, destination, _Material, 2);

            //释放申请的RT  
            RenderTexture.ReleaseTemporary(rt);
			//RenderTexture.ReleaseTemporary(temp2);
		}
	}

	void OnDestroy()
	{
		_Material = null;
    }
}
