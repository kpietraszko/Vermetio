using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using static Unity.Mathematics.math;
using Unity.NetCode;
using Unity.Transforms;

[UpdateInGroup(typeof(ServerSimulationSystemGroup))]
public class BuoyancySystem : SystemBase
{
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
        
        
        
        Entities.ForEach((ref Translation translation, in Rotation rotation, in BuoyantComponent buoyant) =>
        {
            translation = new Translation() {Value = new float3()};
        }).Schedule();
    }
    
    public bool SampleDisplacement(float3 i_worldPos, float i_minSpatialLength, out float3 o_displacement, float windDirAngle, float chop)
    {
        o_displacement = new float3();

        if (_amplitudes == null)
        {
            return false;
        }

        var pos = new float2(i_worldPos.x, i_worldPos.z);
        float mytime = (float)Time.ElapsedTime;
        float windAngle = windDirAngle;
        float minWavelength = i_minSpatialLength / 2f;

        for (int j = 0; j < _amplitudes.Length; j++)
        {
            if (_amplitudes[j] <= 0.001f) continue;
            if (_wavelengths[j] < minWavelength) continue;

            float C = ComputeWaveSpeed(_wavelengths[j]);

            // direction
            var D = new float2(cos((radians(windAngle + _angleDegs[j]))), sin((radians(windAngle + _angleDegs[j]))));
            // wave number
            float k = 2f * PI / _wavelengths[j];

            float x = dot(D, pos);
            float t = k * (x + C * mytime) + _phases[j];
            float disp = -chop * sin(t);
            o_displacement += _amplitudes[j] * new float3(
                D.x * disp,
                cos(t),
                D.y * disp
            );
        }

        return true;
    }
    
    float ComputeWaveSpeed(float wavelength/*, float depth*/)
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
