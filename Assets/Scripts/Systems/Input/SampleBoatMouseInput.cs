using System;
using Crest;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.InputSystem;
using Vermetio;

[UpdateInGroup(typeof(GhostInputSystemGroup))] // this executes only on the client
public class SampleBoatMouseInput : SystemBase
{
    private RayTraceHelper _rayTraceHelper;
    private Segments.Batch _batch;

    protected override void OnCreate()
    {
        RequireSingletonForUpdate<NetworkIdComponent>();
        _rayTraceHelper = new RayTraceHelper(600f, 1f);
        Segments.Core.CreateBatch(out _batch, Resources.Load<Material>("Materials/DirectionLine"));
    }
    
    protected override void OnUpdate()
    {
        _batch.Dependency.Complete();
        
        var localInputEntity = GetSingleton<CommandTargetComponent>().targetEntity;
        if (localInputEntity == Entity.Null)
        {
            return;
        }
        
        var input = default(BoatMouseInput);
        input.Tick = World.GetExistingSystem<ClientSimulationSystemGroup>().ServerTick;

        var mouse = Mouse.current;
        if (mouse == null) // somehow user has no mouse
            return;

        var camera = Camera.main;
        var ray = camera.ScreenPointToRay(mouse.position.ReadValue());
        var camPosition = camera.transform.position;
        _rayTraceHelper.Init(camPosition, ray.direction);
        _rayTraceHelper.Trace(out var distanceFromCam);
        var playerPos = GetComponent<Translation>(localInputEntity).Value;
        var aimPosition = new float3(camPosition + ray.direction * distanceFromCam);
        
        input.AimPosition = aimPosition;
        var inputBuffer = EntityManager.GetBuffer<BoatMouseInput>(localInputEntity);
        inputBuffer.AddCommandData(input);
        
        DrawAimCircle(camPosition, aimPosition, playerPos);
    }

    private void DrawAimCircle(float3 camPosition, float3 aimPosition, float3 playerPos)
    {
        var buffer = _batch.buffer;
        var aimCirclePosition = aimPosition + new float3(0f, 0.5f, 0f);
        var aimCircleRotation = quaternion.AxisAngle(new float3(1, 0, 0), math.PI / 2f);

        var camDistanceToPlayer = math.distance(camPosition, playerPos);
        var index = 0;
        Segments.Plot.Circle(buffer, ref index, camDistanceToPlayer / 70f, aimCirclePosition, aimCircleRotation, 18);
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        _batch.Dispose();
    }
}
