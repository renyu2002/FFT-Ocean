﻿using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public class WavesGenerator : MonoBehaviour
{
    public WavesCascade cascade0;
    public WavesCascade cascade1;
    public WavesCascade cascade2;

    // must be a power of 2
    [SerializeField]
    int size = 256;

    [SerializeField]
    WavesSettings wavesSettings;
    [SerializeField]
    bool alwaysRecalculateInitials = false;
    [SerializeField]
    float originalLengthScale0 = 250;
    float lengthScale0;
    [SerializeField]
    float originalLengthScale1 = 17;
    float lengthScale1 ;
    [SerializeField]
    float originalLengthScale2 = 5;
    float lengthScale2;

    [SerializeField]
    ComputeShader fftShader;
    [SerializeField]
    ComputeShader initialSpectrumShader;
    [SerializeField]
    ComputeShader timeDependentSpectrumShader;
    [SerializeField]
    ComputeShader texturesMergerShader;

    // 引用相机对象
    public Camera mainCamera;


    Texture2D gaussianNoise;
    FastFourierTransform fft;
    Texture2D physicsReadback;

    private void Awake()
    {
        Application.targetFrameRate = -1;
        fft = new FastFourierTransform(size, fftShader);
        gaussianNoise = GetNoiseTexture(size);

        cascade0 = new WavesCascade(size, initialSpectrumShader, timeDependentSpectrumShader, texturesMergerShader, fft, gaussianNoise);
        cascade1 = new WavesCascade(size, initialSpectrumShader, timeDependentSpectrumShader, texturesMergerShader, fft, gaussianNoise);
        cascade2 = new WavesCascade(size, initialSpectrumShader, timeDependentSpectrumShader, texturesMergerShader, fft, gaussianNoise);

        InitialiseCascades();

        physicsReadback = new Texture2D(size, size, TextureFormat.RGBAFloat, false);

        // 获取主相机
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
        }
    }

    void InitialiseCascades()
    {
        float boundary1 = 2 * Mathf.PI / originalLengthScale1 * 6f;
        float boundary2 = 2 * Mathf.PI / originalLengthScale2 * 6f;
        cascade0.CalculateInitials(wavesSettings, originalLengthScale0, 0.0001f, boundary1);
        cascade1.CalculateInitials(wavesSettings, originalLengthScale1, boundary1, boundary2);
        cascade2.CalculateInitials(wavesSettings, originalLengthScale2, boundary2, 9999);

        Shader.SetGlobalFloat("LengthScale0", originalLengthScale0);
        Shader.SetGlobalFloat("LengthScale1", originalLengthScale1);
        Shader.SetGlobalFloat("LengthScale2", originalLengthScale2);
    }

    // 根据相机位置计算新的 lengthScale
    private void UpdateLengthScales()
    {
        if (mainCamera != null)
        {
            Vector3 cameraPosition = mainCamera.transform.position;
            // 这里可以根据相机位置自定义计算 lengthScale 的逻辑
            lengthScale0 = CalculateLengthScale(cameraPosition, originalLengthScale0);
            lengthScale1 = CalculateLengthScale(cameraPosition, originalLengthScale1);
            lengthScale2 = CalculateLengthScale(cameraPosition, originalLengthScale2);
        }
    }

    // 自定义计算 lengthScale 的逻辑
    private float CalculateLengthScale(Vector3 cameraPosition, float originalLengthScale)
    {
        // 示例：根据相机的 y 坐标进行缩放
        float scaleFactor = 1 - cameraPosition.y * 0.001f;
        return originalLengthScale * scaleFactor;
    }

    private void Update()
    {
        // 更新 lengthScale
        UpdateLengthScales();

        if (alwaysRecalculateInitials)
        {
            InitialiseCascades();
        }

        // 重新计算初始值
        float boundary1 = 2 * Mathf.PI / lengthScale1 * 6f;
        float boundary2 = 2 * Mathf.PI / lengthScale2 * 6f;
        cascade0.CalculateInitials(wavesSettings, lengthScale0, 0.0001f, boundary1);
        cascade1.CalculateInitials(wavesSettings, lengthScale1, boundary1, boundary2);
        cascade2.CalculateInitials(wavesSettings, lengthScale2, boundary2, 9999);

        Shader.SetGlobalFloat("LengthScale0", lengthScale0);
        Shader.SetGlobalFloat("LengthScale1", lengthScale1);
        Shader.SetGlobalFloat("LengthScale2", lengthScale2);

        cascade0.CalculateWavesAtTime(Time.time);
        cascade1.CalculateWavesAtTime(Time.time);
        cascade2.CalculateWavesAtTime(Time.time);

        RequestReadbacks();
    }

    Texture2D GetNoiseTexture(int size)
    {
        string filename = "GaussianNoiseTexture" + size.ToString() + "x" + size.ToString();
        Texture2D noise = Resources.Load<Texture2D>("GaussianNoiseTextures/" + filename);
        return noise ? noise : GenerateNoiseTexture(size, true);
    }

    Texture2D GenerateNoiseTexture(int size, bool saveIntoAssetFile)
    {
        Texture2D noise = new Texture2D(size, size, TextureFormat.RGFloat, false, true);
        noise.filterMode = FilterMode.Point;
        for (int i = 0; i < size; i++)
        {
            for (int j = 0; j < size; j++)
            {
                noise.SetPixel(i, j, new Vector4(NormalRandom(), NormalRandom()));
            }
        }
        noise.Apply();

#if UNITY_EDITOR
        if (saveIntoAssetFile)
        {
            string filename = "GaussianNoiseTexture" + size.ToString() + "x" + size.ToString();
            string path = "Assets/Resources/GaussianNoiseTextures/";
            AssetDatabase.CreateAsset(noise, path + filename + ".asset");
            Debug.Log("Texture \"" + filename + "\" was created at path \"" + path + "\".");
        }
#endif
        return noise;
    }

    float NormalRandom()
    {
        return Mathf.Cos(2 * Mathf.PI * Random.value) * Mathf.Sqrt(-2 * Mathf.Log(Random.value));
    }

    private void OnDestroy()
    {
        cascade0.Dispose();
        cascade1.Dispose();
        cascade2.Dispose();
    }

    void RequestReadbacks()
    {
        AsyncGPUReadback.Request(cascade0.Displacement, 0, TextureFormat.RGBAFloat, OnCompleteReadback);
    }

    public float GetWaterHeight(Vector3 position)
    {
        Vector3 displacement = GetWaterDisplacement(position);
        displacement = GetWaterDisplacement(position - displacement);
        displacement = GetWaterDisplacement(position - displacement);

        return GetWaterDisplacement(position - displacement).y;
    }

    public Vector3 GetWaterDisplacement(Vector3 position)
    {
        Color c = physicsReadback.GetPixelBilinear(position.x / lengthScale0, position.z / lengthScale0);
        return new Vector3(c.r, c.g, c.b);
    }

    void OnCompleteReadback(AsyncGPUReadbackRequest request) => OnCompleteReadback(request, physicsReadback);

    void OnCompleteReadback(AsyncGPUReadbackRequest request, Texture2D result)
    {
        if (request.hasError)
        {
            Debug.Log("GPU readback error detected.");
            return;
        }
        if (result != null)
        {
            result.LoadRawTextureData(request.GetData<Color>());
            result.Apply();
        }
    }
}
