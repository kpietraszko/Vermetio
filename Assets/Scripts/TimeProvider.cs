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

    // NOTE that this shouldn't really be float, but changing everything to double is difficult, because shaders don't really handle double
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
            // if (Math.Abs(value % 1) < DeltaTime / 2f)
            // {
            //     Debug.Log($"TP: {value} U: {Time.time}");
            // }
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
        var clientWorld = GetWorldWith<ClientSimulationSystemGroup>(World.All);
        if (clientWorld != null)
        {
            var clientSimSystemGroup = clientWorld.GetExistingSystem<ClientSimulationSystemGroup>();
            return (clientSimSystemGroup.InterpolationTick + clientSimSystemGroup.InterpolationTickFraction) *
                   clientSimSystemGroup.ServerTickDeltaTime;
        }

        var serverWorld = GetWorldWith<ServerSimulationSystemGroup>(World.All);
        if (serverWorld != null)
        {
            var serverSimSystemGroup = serverWorld.GetExistingSystem<ServerSimulationSystemGroup>();
            return serverSimSystemGroup.ServerTick * serverSimSystemGroup.Time.DeltaTime;
        }

        return 0f;
    }
    
    private World GetWorldWith<T>(World.NoAllocReadOnlyCollection<World> worlds) where T : ComponentSystemBase
    {
        foreach (var world in worlds)
        {
            if (world.GetExistingSystem<T>() != null)
                return world;
        }

        return null;
    }
}