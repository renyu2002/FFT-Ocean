﻿using UnityEngine;

public struct SpectrumSettings
{
    public float scale;
    public float angle;
    public float spreadBlend;
    public float swell;
    public float alpha;
    public float peakOmega;
    public float gamma;
    public float shortWavesFade;
}

[System.Serializable]
public struct DisplaySpectrumSettings
{
    [Range(0, 1)]
    public float scale;
    public float windSpeed;
    public float windDirection;
    public float fetch;
    [Range(0, 1)]
    public float spreadBlend;
    [Range(0, 1)]
    public float swell;
    public float peakEnhancement;
    public float shortWavesFade;
}

[CreateAssetMenu(fileName = "New waves settings", menuName = "Ocean/Waves Settings")]
public class WavesSettings : ScriptableObject
{
    public float g;
    public float depth;
    [Range(0, 1)]
    public float lambda;
    public DisplaySpectrumSettings local;
    public DisplaySpectrumSettings swell;

    SpectrumSettings[] spectrums = new SpectrumSettings[2];

    public void SetParametersToShader(ComputeShader shader, int kernelIndex, ComputeBuffer paramsBuffer)
    {
        shader.SetFloat(G_PROP, g);
        shader.SetFloat(DEPTH_PROP, depth);

        FillSettingsStruct(local, ref spectrums[0]);
        FillSettingsStruct(swell, ref spectrums[1]);

        paramsBuffer.SetData(spectrums);
        shader.SetBuffer(kernelIndex, SPECTRUMS_PROP, paramsBuffer);
    }

    void FillSettingsStruct(DisplaySpectrumSettings display, ref SpectrumSettings settings)
    {
        settings.scale = display.scale;
        settings.angle = display.windDirection / 180 * Mathf.PI;
        settings.spreadBlend = display.spreadBlend;
        settings.swell = Mathf.Clamp(display.swell, 0.01f, 1);
        settings.alpha = WendtAlpha(g, display.fetch, display.windSpeed);
        settings.peakOmega = WendtPeakFrequency(g, display.fetch, display.windSpeed);
        settings.gamma = display.peakEnhancement;
        settings.shortWavesFade = display.shortWavesFade;
    }

    float WendtAlpha(float g, float fetch, float windSpeed)
    {
        // 文氏海浪谱的 alpha 计算
        return 0.076f * Mathf.Pow(g * fetch / windSpeed / windSpeed, -0.22f); // 考虑到风场影响
    }

    float WendtPeakFrequency(float g, float fetch, float windSpeed)
    {
        // 文氏海浪谱的峰值频率计算
        return 2 * Mathf.PI * 0.877f * windSpeed / Mathf.Sqrt(g * fetch);
    }

    readonly int G_PROP = Shader.PropertyToID("GravityAcceleration");
    readonly int DEPTH_PROP = Shader.PropertyToID("Depth");
    readonly int SPECTRUMS_PROP = Shader.PropertyToID("Spectrums");
}
