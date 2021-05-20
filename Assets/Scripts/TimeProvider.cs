// Crest Ocean System

// Copyright 2020 Wave Harmonic Ltd

using System;
using System.Linq;
using Crest;
using Unity.Core;
using Unity.Entities;
using Unity.NetCode;
using UnityEngine;

public class TimeProvider : TimeProviderBase
{
    private float _previousTime = 0f;
    private float _delta = 0f;

    public override float CurrentTime
    {
        get
        {
            float value = 0f;
#if UNITY_EDITOR
            if (UnityEditor.EditorApplication.isPlaying)
            {
                value = (float) GetTimeData().ElapsedTime;
            }
            else
            {
                value = (float) OceanRenderer.LastUpdateEditorTime;
            }
#else
                value = (float) GetTimeData().ElapsedTime;
#endif
            _delta = value - _previousTime;
            _previousTime = value;
            return value;
        }
    }

    public override float DeltaTime
    {
        get
        {
#if UNITY_EDITOR
            if (UnityEditor.EditorApplication.isPlaying)
            {
                return GetTimeData().DeltaTime;
            }
            else
            {
                return 1f / 20f;
            }
#else
            return GetTimeData().DeltaTime;
#endif
            ;
        }
    }

    public override float DeltaTimeDynamics => DeltaTime;

    private TimeData GetTimeData()
    {
        for (int i = 0; i < World.All.Count; i++)
        {
            var clientSimSystemGroup = World.All[i].GetExistingSystem<ClientSimulationSystemGroup>();
            if (clientSimSystemGroup == null)
                continue;

            return World.All[i].Time;
        }

        return default(TimeData);
    }
}