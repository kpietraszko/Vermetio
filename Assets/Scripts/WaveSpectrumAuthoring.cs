using System.Linq;
using Crest;
using Unity.Assertions;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using Vermetio;

// [DisallowMultipleComponent]
// public class WaveSpectrumAuthoring : MonoBehaviour, IConvertGameObjectToEntity
// {
//     public const int NUM_OCTAVES = 14;
//     public static readonly float SMALLEST_WL_POW_2 = -4f;
//     // Add fields to your component here. Remember that:
//     //
//     // * The purpose of this class is to store data for authoring purposes - it is not for use while the game is
//     //   running.
//     // 
//     // * Traditional Unity serialization rules apply: fields must be public or marked with [SerializeField], and
//     //   must be one of the supported types.
//     //
//     // For example,
//     //    public float scale;
//     
//     
//
//     public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
//     {
//         // Call methods on 'dstManager' to create runtime components on 'entity' here. Remember that:
//         //
//         // * You can add more than one component to the entity. It's also OK to not add any at all.
//         //
//         // * If you want to create more than one entity from the data in this class, use the 'conversionSystem'
//         //   to do it, instead of adding entities through 'dstManager' directly.
//         //
//         // For example,
//         //   dstManager.AddComponentData(entity, new Unity.Transforms.Scale { Value = scale });
//
//         var gerstner = GetComponent<ShapeGerstner>();
//         Assert.IsNotNull(gerstner);
//         var spectrum = gerstner._spectrum;
//         Assert.IsNotNull(spectrum);
//
//         var attenuationInShallowsMaybe = OceanRenderer.Instance?._lodDataAnimWaves?.Settings?.AttenuationInShallows;
//         if (!attenuationInShallowsMaybe.HasValue)
//             Debug.LogWarning("Failed to get attenuation in shallows");
//
//         var attenuationInShallows = attenuationInShallowsMaybe ?? 0f;
//         
//         dstManager.AddComponentData(entity, new WaveSpectrumComponent()
//         {
//             WindDirectionAngle = gerstner._waveDirectionHeadingAngle,
//             Chop = spectrum._chop,
//             AttenuationInShallows = attenuationInShallows
//         });
//
//         dstManager.AddBuffer<WavelengthElement>(entity);
//         dstManager.AddBuffer<WaveAmplitudeElement>(entity);
//         dstManager.AddBuffer<PhaseElement>(entity);
//         dstManager.AddBuffer<WaveAngleElement>(entity);
//         dstManager.AddBuffer<GerstnerWaveComponent4>(entity);
//
//         var wavelengthBuffer = dstManager.GetBuffer<WavelengthElement>(entity);
//         wavelengthBuffer.AddRange(new NativeArray<WavelengthElement>(gerstner._wavelengths.Select(x => new WavelengthElement { Value =  x}).ToArray(), Allocator.Temp));
//         
//         var amplitudeBuffer = dstManager.GetBuffer<WaveAmplitudeElement>(entity);
//         amplitudeBuffer.AddRange(new NativeArray<WaveAmplitudeElement>(gerstner._amplitudes.Select(x => new WaveAmplitudeElement { Value =  x}).ToArray(), Allocator.Temp));
//         
//         var phaseBuffer = dstManager.GetBuffer<PhaseElement>(entity);
//         phaseBuffer.AddRange(new NativeArray<PhaseElement>(gerstner._phases.Select(x => new PhaseElement { Value =  x}).ToArray(), Allocator.Temp));
//         
//         var angleBuffer = dstManager.GetBuffer<WaveAngleElement>(entity);
//         angleBuffer.AddRange(new NativeArray<WaveAngleElement>(gerstner._angleDegs.Select(x => new WaveAngleElement { Value =  x}).ToArray(), Allocator.Temp));
//
//         var waveDataBuffer = dstManager.GetBuffer<GerstnerWaveComponent4>(entity);
//         waveDataBuffer.AddRange(new NativeArray<GerstnerWaveComponent4>(gerstner._waveData.Select(x => new GerstnerWaveComponent4()
//         {
//             _twoPiOverWavelength = x._twoPiOverWavelength,
//             _amp = x._amp,
//             _waveDirX = x._waveDirX,
//             _waveDirZ = x._waveDirZ,
//             _omega = x._omega,
//             _phase = x._phase,
//             _chopAmp = x._chopAmp
//         }).ToArray(), 
//             Allocator.Temp));
//
//
//     }
//     
// }
