using System.Runtime.InteropServices.WindowsRuntime;
using Crest;
using E7.ECS.LineRenderer;
using Unity.Assertions;
using Unity.Burst;
using Unity.Collections;
using Unity.Core;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.InputSystem;

[UpdateInGroup(typeof(GhostInputSystemGroup))] // this executes only on the client
public class SampleBoatInputSystem : SystemBase
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
            AddInputBuffers();
            return;
        }
        
        var tick = World.GetExistingSystem<ClientSimulationSystemGroup>().ServerTick;

        var input = default(BoatInput);
        input.Tick = tick;
        
        var keyboard = Keyboard.current;
        if (keyboard != null)
            HandleKeyboardInput(localInputEntity, keyboard, ref input);
        
        var mouse = Mouse.current;
        if (mouse != null)
            HandleMouseInput(mouse, localInputEntity, ref input);

        var inputBuffer = EntityManager.GetBuffer<BoatInput>(localInputEntity);
        inputBuffer.AddCommandData(input);
    }

    private void HandleMouseInput(Mouse mouse, Entity localInputEntity, ref BoatInput input)
    {
        var camera = Camera.main;
        var ray = camera.ScreenPointToRay(mouse.position.ReadValue());
        var camPosition = camera.transform.position;
        _rayTraceHelper.Init(camPosition, ray.direction);
        if (!_rayTraceHelper.Trace(out var distanceFromCam))
        {
            _batch.buffer.Clear();
            return;
        }
        var playerPos = GetComponent<Translation>(localInputEntity).Value;
        var aimPosition = new float3(camPosition + ray.direction * distanceFromCam);

        input.AimPosition = aimPosition;
        DrawAimCircle(camPosition, aimPosition, playerPos);

        input.Shoot = false;

        if (mouse.leftButton.wasPressedThisFrame)
        {
            // var clientTickRate = GetSingleton<ClientTickRate>();
            // var ack = GetSingleton<NetworkSnapshotAckComponent>();
            // var estimatedRTT = math.min(ack.EstimatedRTT, clientTickRate.MaxPredictAheadTimeMS);
            // input.FinishRotationAt = Time.ElapsedTime + estimatedRTT; // probably wrong
            var shootParams = GetComponent<ShootParametersComponent>(localInputEntity);
            var inventory = GetComponent<PlayerInventoryComponent>(localInputEntity);
            if (inventory.Coconuts < 1)
                return;
            
            shootParams.LastShotRequestedAt = Time.ElapsedTime;
            SetComponent(localInputEntity, shootParams);
            input.Shoot = true;
        }
    }

    private void HandleKeyboardInput(Entity localInputEntity, Keyboard keyboard, ref BoatInput input)
    {
        var targetHeading = EntityManager.GetComponentData<LocalToWorld>(localInputEntity).Forward;
        if (EntityManager.HasComponent<BoatTargetHeadingComponent>(localInputEntity))
        {
            var existingHeading = EntityManager.GetComponentData<BoatTargetHeadingComponent>(localInputEntity).Value;
            targetHeading = math.all(existingHeading == default) ? targetHeading : existingHeading;
        }
        else EntityManager.AddComponent<BoatTargetHeadingComponent>(localInputEntity);

        var rotationDirection = (keyboard.aKey.isPressed ? -1f : 0) + (keyboard.dKey.isPressed ? 1f : 0);
        var angleToRotateHeadingBy = math.PI / 2f * Time.DeltaTime * rotationDirection; // 90 degrees per second
        var rotationOfHeading = quaternion.AxisAngle(new float3(0, 1, 0), angleToRotateHeadingBy);
        targetHeading = math.normalizesafe(math.mul(rotationOfHeading, targetHeading));

        input.TargetDirection = targetHeading;
        EntityManager.SetComponentData(localInputEntity, new BoatTargetHeadingComponent() {Value = targetHeading});

        var throttleInput = (keyboard.wKey.isPressed ? 1f : 0) + (keyboard.sKey.isPressed ? -1f : 0);
        input.Throttle = throttleInput;

        var playerPosition = EntityManager.GetComponentData<Translation>(localInputEntity).Value;
        var playerForward = EntityManager.GetComponentData<LocalToWorld>(localInputEntity).Forward;
        // Debug.DrawLine(playerPosition, playerPosition + targetHeading * 4, Color.black, Time.DeltaTime);

        // Direction line
        Entities
            .WithName("boat_direction_line")
            .WithStructuralChanges()
            .ForEach((Entity entity, ref LineSegment lineSegment, in Parent parent) =>
            {
                if (parent.Value != localInputEntity)
                    return;

                lineSegment = new LineSegment(playerPosition + targetHeading * 24f, playerPosition + targetHeading * 29f,
                    lineSegment.lineWidth);

                var barelyTurning = math.abs(math.dot(targetHeading, math.normalize(playerForward))) > 0.985f;
                var isHidden = HasComponent<DisableRendering>(entity);
                if (isHidden && math.abs(throttleInput) > 0.001f && !barelyTurning)
                    EntityManager.RemoveComponent<DisableRendering>(entity);

                if (!isHidden && (math.abs(throttleInput) < 0.001 || barelyTurning))
                    EntityManager.AddComponent<DisableRendering>(entity);
            }).Run();
    }

    private void AddInputBuffers()
    {
        var localPlayerId = GetSingleton<NetworkIdComponent>().Value;
        Entities.WithStructuralChanges().WithAll<MovableBoatComponent>().WithNone<BoatInput>().ForEach(
            (Entity ent, ref GhostOwnerComponent ghostOwner) =>
            {
                if (ghostOwner.NetworkId == localPlayerId)
                {
                    EntityManager.AddBuffer<BoatInput>(ent);
                    EntityManager.SetComponentData(GetSingletonEntity<CommandTargetComponent>(),
                        new CommandTargetComponent {targetEntity = ent});
                }
            }).Run();
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
