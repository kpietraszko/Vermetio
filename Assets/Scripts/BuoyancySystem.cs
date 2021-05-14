using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using static Unity.Mathematics.math;
using Unity.NetCode;
using Unity.Transforms;
using UnityEngine;

[UpdateInGroup(typeof(ServerSimulationSystemGroup))]
public class BuoyancySystem : SystemBase
{
    protected override void OnCreate()
    {
        base.OnCreate();
        RequireSingletonForUpdate<WaveSpectrumComponent>();
        RequireSingletonForUpdate<WavelengthElement>();
        RequireSingletonForUpdate<WaveAmplitudeElement>();
        RequireSingletonForUpdate<WaveAngleElement>();
        RequireSingletonForUpdate<PhaseElement>();
    }

    protected override void OnUpdate()
    {
        // Assign values to local variables captured in your job here, so that it has
        // everything it needs to do its work when it runs later.
        // For example,
        //     float deltaTime = Time.DeltaTime;

        // This declares a new kind of job, which is a unit of work to do.
        // The job is declared as an Entities.ForEach with the target components as parameters,
        // meaning it will process all entities in the world that have both
        // Translation and Rotation components. Change it to process the component
        // types you want.

        var spectrum = GetSingleton<WaveSpectrumComponent>();
        var spectrumEntity = GetSingletonEntity<WaveSpectrumComponent>();
        var wavelengthBuffer = EntityManager.GetBuffer<WavelengthElement>(spectrumEntity);
        var waveAmplitudeBuffer = EntityManager.GetBuffer<WaveAmplitudeElement>(spectrumEntity);
        var waveAngleBuffer = EntityManager.GetBuffer<WaveAngleElement>(spectrumEntity);
        var phaseBuffer = EntityManager.GetBuffer<PhaseElement>(spectrumEntity);

        var elapsedTime = Time.ElapsedTime;

        Entities
            .ForEach((ref Translation translation, in Rotation rotation, in BuoyantComponent buoyant) =>
        {
            if (!SampleDisplacement(
                (float) elapsedTime, 
                translation.Value,
                0.5f,
                out var displacement,
                spectrum.WindDirectionAngle,
                spectrum.Chop,
                wavelengthBuffer,
                waveAmplitudeBuffer,
                waveAngleBuffer,
                phaseBuffer))
            {
                Debug.LogWarning("Failed to sample displacement");
            }
            translation = new Translation() {Value = displacement};
        }).Schedule();
    }

    // TODO: add shore attenuation - adapt code from compute shader, get depth from ray downward to terrain
    private static bool SampleDisplacement(
        float elapsedTime, 
        float3 i_worldPos,
        float i_minSpatialLength,
        out float3 o_displacement,
        float windDirAngle,
        float chop,
        DynamicBuffer<WavelengthElement> wavelengthBuffer,
        DynamicBuffer<WaveAmplitudeElement> waveAmplitudeBuffer,
        DynamicBuffer<WaveAngleElement> waveAngleBuffer,
        DynamicBuffer<PhaseElement> phaseBuffer)
    {
        o_displacement = new float3();

        if (waveAmplitudeBuffer.IsEmpty || !waveAmplitudeBuffer.IsCreated)
        {
            return false;
        }

        var pos = new float2(i_worldPos.x, i_worldPos.z);
        float windAngle = windDirAngle;
        float minWavelength = i_minSpatialLength / 2f;

        for (int j = 0; j < waveAmplitudeBuffer.Length; j++)
        {
            if (waveAmplitudeBuffer[j] <= 0.001f) continue;
            if (wavelengthBuffer[j] < minWavelength) continue;

            float C = ComputeWaveSpeed(wavelengthBuffer[j]);

            // direction
            var D = new float2(cos((radians(windAngle + waveAngleBuffer[j]))), sin((radians(windAngle + waveAngleBuffer[j]))));
            // wave number
            float k = 2f * PI / wavelengthBuffer[j];

            float x = dot(D, pos);
            float t = k * (x + C * elapsedTime) + phaseBuffer[j];
            float disp = -chop * sin(t);
            o_displacement += waveAmplitudeBuffer[j] * new float3(
                D.x * disp,
                cos(t),
                D.y * disp
            );
        }

        return true;
    }

    private static float ComputeWaveSpeed(float wavelength /*, float depth*/)
    {
        // wave speed of deep sea ocean waves: https://en.wikipedia.org/wiki/Wind_wave
        // https://en.wikipedia.org/wiki/Dispersion_(water_waves)#Wave_propagation_and_dispersion
        float g = 9.81f;
        float k = 2f * PI / wavelength;
        //float h = max(depth, 0.01);
        //float cp = sqrt(abs(tanh_clamped(h * k)) * g / k);
        float cp = sqrt(g / k);
        return cp;
    }
}