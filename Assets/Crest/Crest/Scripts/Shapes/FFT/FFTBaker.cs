// Crest Ocean System

// This file is subject to the MIT License as seen in the root of this folder structure (LICENSE)

//#define CREST_DEBUG_DUMP_EXRS

#if UNITY_EDITOR

using System.Linq;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Crest
{
    /// <summary>
    /// Contains editor code for baking FFT shape to use for CPU collision.
    /// </summary>
    public static class FFTBaker
    {
        static string s_bakeFolder = null;

        /// <summary>
        /// Bakes FFT data for a ShapeFFT component
        /// </summary>
        public static FFTBakedData BakeShapeFFT(ShapeFFT fftWaves)
        {
            // Compute how many cascades are needed. Both the spectrum octaves and the wave cascades increase
            // in powers of 2, so use the spectrum count.
            ComputeRequiredOctaves(fftWaves._spectrum, fftWaves._smallestWavelengthRequired, out var smallestOctaveIndex, out var largestOctaveIndex);

            if (largestOctaveIndex == -1 || smallestOctaveIndex == -1 || smallestOctaveIndex > largestOctaveIndex)
            {
                Debug.LogError("Crest: No waves in spectrum. Increase the spectrum sliders.", fftWaves);
                return null;
            }

            // Assuming two samples per wave, then:
            // _smallestWavelengthRequired = 2 * sliceWidth / sliceRes
            //     sliceWidth = sliceRes * _smallestWavelengthRequired / 2f
            //     0.5 * 2 ^ idx = sliceRes * _smallestWavelengthRequired / 2f
            //     2 ^ idx = sliceRes * _smallestWavelengthRequired
            //     idx = log2(sliceRes * _smallestWavelengthRequired)
            var firstLod = Mathf.RoundToInt(Mathf.Log(fftWaves._smallestWavelengthRequired * fftWaves._resolution, 2f));

            // A single spectrum bar adds wavelengths before and after the bar i.e. two scales, so relationship
            // is the following:
            var lodCount = largestOctaveIndex - smallestOctaveIndex + 2;

            var baked = BakeFFT(fftWaves, firstLod, lodCount, fftWaves._timeResolution, fftWaves.LoopPeriod);

            return baked;
        }

        /// <summary>
        /// Runs FFT for a bunch of time steps and saves all the resulting data to a scriptable object
        /// </summary>
        static FFTBakedData BakeFFT(ShapeFFT fftWaves, int firstLod, int lodCount, int resolutionTime, float loopPeriod)
        {
            // Need min scale, maybe max too - unlikely to need 16 orders of magnitude

            // Need to decide how many time samples to take. As first step can just divide
            // loopPeriod evenly like before. Probably always taking eg 16 samples per period
            // works well. So we can take 16 slices, and in the future we know that the period
            // of a bunch of the lods was much smaller, so we could take much denser samples.

            var buf = new CommandBuffer();

            var waveCombineShader = Resources.Load<ComputeShader>("FFT/FFTBake");
            var kernel = waveCombineShader.FindKernel("FFTBakeMultiRes");

            var bakedWaves = new RenderTexture(fftWaves._resolution, fftWaves._resolution * lodCount, 1, RenderTextureFormat.ARGBFloat, 0);
            bakedWaves.enableRandomWrite = true;
            bakedWaves.Create();

            var stagingTexture = new Texture2D(fftWaves._resolution, fftWaves._resolution * lodCount, TextureFormat.RGBAHalf, false, true);

            var frameCount = (int)(resolutionTime * loopPeriod);
            var frames = new half[frameCount][];

            for (int timeIndex = 0; timeIndex < frameCount; timeIndex++) // this means resolutionTime is actually FPS
            {
                float t = timeIndex / (float)resolutionTime;

                buf.Clear();

                // Generate multi-res FFT into a texture array
                var fftWaveDataTA = FFTCompute.GenerateDisplacements(buf, fftWaves._resolution, loopPeriod,
                    fftWaves._windTurbulence, fftWaves.WindDirRadForFFT, fftWaves.WindSpeedForFFT, t,
                    fftWaves._spectrum, true);

                // Compute shader generates the final waves
                buf.SetComputeFloatParam(waveCombineShader, "_BakeTime", t);
                buf.SetComputeIntParam(waveCombineShader, "_MinSlice", firstLod);
                buf.SetComputeTextureParam(waveCombineShader, kernel, "_InFFTWaves", fftWaveDataTA);
                buf.SetComputeTextureParam(waveCombineShader, kernel, "_OutDisplacements", bakedWaves);
                buf.DispatchCompute(waveCombineShader, kernel, bakedWaves.width / 8, bakedWaves.height / 8, 1);

                Graphics.ExecuteCommandBuffer(buf);

                // Readback data to CPU
                RenderTexture.active = bakedWaves;
                stagingTexture.ReadPixels(new Rect(0, 0, bakedWaves.width, bakedWaves.height), 0, 0);

#if CREST_DEBUG_DUMP_EXRS
                const string folderName = "FFTBaker";
                if (Directory.Exists(folderName))
                {
                    Directory.Delete(folderName, true);
                }
                Directory.CreateDirectory(folderName);
                var encodedTexture = stagingTexture.EncodeToEXR(Texture2D.EXRFlags.OutputAsFloat);
                File.WriteAllBytes($"{folderName}/test_{timeIndex}.exr", encodedTexture);
#endif

                frames[timeIndex] = stagingTexture.GetRawTextureData<half>().ToArray();
            }

            var framesFlattened = frames.SelectMany(x => x).ToArray();
            //Debug.Log($"Width: {fftWaves._resolution}, frame count: {frameCount}, slices: {lodCount}, floats per frame: {frames[0].Length}, total floats: {framesFlattened.Length}");

            var bakedDataSO = ScriptableObject.CreateInstance<FFTBakedData>();
            var framesAsFloats = framesFlattened.Select(x => (float)x);
            bakedDataSO.Initialize(
                loopPeriod,
                fftWaves._resolution,
                firstLod,
                lodCount,
                fftWaves.WindSpeedForFFT,
                frames.Length,
                new half(framesAsFloats.Min()),
                new half(framesAsFloats.Max()),
                framesFlattened);

            if (!SaveBakedDataAsset(bakedDataSO, fftWaves.gameObject.scene.name, fftWaves.gameObject.name))
            {
                return null;
            }

            return bakedDataSO;
        }

        private static bool SaveBakedDataAsset(ScriptableObject bakedDataSO, string sceneName, string shapeFFTName)
        {
            // Default folder
            if (string.IsNullOrEmpty(s_bakeFolder))
            {
                s_bakeFolder = "Assets";
            }

            // Select bake folder
            {
                var folderPath = EditorUtility.SaveFolderPanel("Folder for baked data", s_bakeFolder, "");
                if (string.IsNullOrEmpty(folderPath))
                {
                    return false;
                }
                s_bakeFolder = FileUtil.GetProjectRelativePath(folderPath);
            }

            var assetPath = AssetDatabase.GenerateUniqueAssetPath($"{s_bakeFolder}/{sceneName}-{shapeFFTName}-BakedData.asset");
            AssetDatabase.CreateAsset(bakedDataSO, assetPath);

            Debug.Log($"Baked wave data to {assetPath}.", bakedDataSO);

            return true;
        }

        internal static void ComputeRequiredOctaves(OceanWaveSpectrum spectrum, float minIncludedWavelength, out int smallest, out int largest)
        {
            smallest = largest = -1;

            for (var i = 0; i < OceanWaveSpectrum.NUM_OCTAVES; i++)
            {
                var pow = spectrum._powerDisabled[i] ? 0f : Mathf.Pow(10f, spectrum._powerLog[i]);
                if (pow > Mathf.Pow(10f, OceanWaveSpectrum.MIN_POWER_LOG))
                {
                    var minWL = Mathf.Pow(2f, OceanWaveSpectrum.SMALLEST_WL_POW_2 + i);
                    if (2f * minWL > minIncludedWavelength && smallest == -1 && minWL >= smallest)
                    {
                        smallest = i;
                    }

                    largest = i;
                }
            }
        }
    }
}

#endif
