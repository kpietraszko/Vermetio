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
public class SampleBoatInput : SystemBase
{
    protected override void OnCreate()
    {
        RequireSingletonForUpdate<NetworkIdComponent>();
    }
    
    protected override void OnUpdate()
    {
        var localInputEntity = GetSingleton<CommandTargetComponent>().targetEntity;
        if (localInputEntity == Entity.Null)
        {
            Debug.Log("Adding keyboard input buffer");
            AddKeyboardInputBuffer();
            return;
        }
        
        var input = default(BoatKeyboardInput);
        input.Tick = World.GetExistingSystem<ClientSimulationSystemGroup>().ServerTick;

        var keyboard = Keyboard.current;

        if (keyboard == null) // somehow user has no keyboard
            return;

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
        EntityManager.SetComponentData(localInputEntity, new BoatTargetHeadingComponent() { Value = targetHeading });
        
        var throttleInput = (keyboard.wKey.isPressed ? 1f : 0) + (keyboard.sKey.isPressed ? -1f : 0);
        input.Throttle = throttleInput;
        
        var playerPosition = EntityManager.GetComponentData<Translation>(localInputEntity).Value;
        var playerForward = EntityManager.GetComponentData<LocalToWorld>(localInputEntity).Forward;
        Debug.DrawLine(playerPosition, playerPosition + targetHeading * 4, Color.black, Time.DeltaTime);

        var inputBuffer = EntityManager.GetBuffer<BoatKeyboardInput>(localInputEntity);
        inputBuffer.AddCommandData(input);
        
        // Direction line
        Entities
            .WithName("boat_direction_line")
            .WithStructuralChanges()
            .ForEach((Entity entity, ref LineSegment lineSegment, in Parent parent) =>
        {
            if (parent.Value != localInputEntity)
                return;
            
            lineSegment = new LineSegment(playerPosition + targetHeading * 16f, playerPosition + targetHeading * 22f, lineSegment.lineWidth);

            var barelyTurning = math.abs(math.dot(targetHeading, math.normalize(playerForward))) > 0.99f;
            var isHidden = HasComponent<DisableRendering>(entity);
            if (isHidden && math.abs(throttleInput) > 0.001f && !barelyTurning)
                EntityManager.RemoveComponent<DisableRendering>(entity);

            if (!isHidden && (math.abs(throttleInput) < 0.001 || barelyTurning))
                EntityManager.AddComponent<DisableRendering>(entity);
        }).Run();
    }

    private void AddKeyboardInputBuffer()
    {
        var localPlayerId = GetSingleton<NetworkIdComponent>().Value;
        Entities.WithStructuralChanges().WithAll<MovableBoatComponent>().WithNone<BoatKeyboardInput>().ForEach(
            (Entity ent, ref GhostOwnerComponent ghostOwner) =>
            {
                if (ghostOwner.NetworkId == localPlayerId)
                {
                    EntityManager.AddBuffer<BoatKeyboardInput>(ent);
                    EntityManager.SetComponentData(GetSingletonEntity<CommandTargetComponent>(),
                        new CommandTargetComponent {targetEntity = ent});
                }
            }).Run();
    }
}
