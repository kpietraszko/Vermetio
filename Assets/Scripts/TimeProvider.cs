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
                value = GetInterpolationTime();
            }
            else
            {
                value = (float) OceanRenderer.LastUpdateEditorTime;
            }
#else
                value = GetInterpolationTime();
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

    private float GetInterpolationTime()
    {
        for (int i = 0; i < World.All.Count; i++)
        {
            var clientSimSystemGroup = World.All[i].GetExistingSystem<ClientSimulationSystemGroup>();
            if (clientSimSystemGroup == null)
                continue;

            return (clientSimSystemGroup.InterpolationTick + clientSimSystemGroup.InterpolationTickFraction) *
                   clientSimSystemGroup.ServerTickDeltaTime;
        }

        return 0f;
    }
}