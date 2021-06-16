using Unity.Entities;
using Unity.Mathematics;

namespace Vermetio
{
    public struct GerstnerWaveComponent4 : IBufferElementData
    {
        public float4 _twoPiOverWavelength;
        public float4 _amp;
        public float4 _waveDirX;
        public float4 _waveDirZ;
        public float4 _omega;
        public float4 _phase;
        public float4 _chopAmp;
    }
}