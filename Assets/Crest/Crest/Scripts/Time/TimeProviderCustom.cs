// Crest Ocean System

// Copyright 2020 Wave Harmonic Ltd

using UnityEngine;

namespace Crest
{
    public class TimeProviderCustom : TimeProviderBase
    {
        public override float CurrentTime
        {
            get
            {
#if UNITY_EDITOR
                if (UnityEditor.EditorApplication.isPlaying)
                {
                    return (System.Diagnostics.Stopwatch.GetTimestamp() / System.TimeSpan.TicksPerMillisecond) / 1000f;
                }
                else
                {
                    return (float) OceanRenderer.LastUpdateEditorTime;
                }
#else
                return (System.Diagnostics.Stopwatch.GetTimestamp() / System.TimeSpan.TicksPerMillisecond) / 1000f;
#endif
            }
        }

        public override float DeltaTime
        {
            get
            {
#if UNITY_EDITOR
                if (UnityEditor.EditorApplication.isPlaying)
                {
                    return Time.deltaTime;
                }
                else
                {
                    return 1f / 20f;
                }
#else
                return Time.deltaTime;
#endif
                ;
            }
        }

        public override float DeltaTimeDynamics => DeltaTime;
    }
}
