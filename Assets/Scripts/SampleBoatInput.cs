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
        Debug.DrawLine(playerPosition, playerPosition + targetHeading * 4, Color.black, Time.DeltaTime);

        // if (keyboard.aKey.isPressed)
        //     input
        
        var inputBuffer = EntityManager.GetBuffer<BoatKeyboardInput>(localInputEntity);
        inputBuffer.AddCommandData(input);
        
        // Direction line
        Entities.WithStructuralChanges().ForEach((Entity entity, ref LineSegment lineSegment, in Parent parent) =>
        {
            if (parent.Value == localInputEntity)
                lineSegment = new LineSegment(playerPosition + targetHeading * 12f, playerPosition + targetHeading * 18f, lineSegment.lineWidth);

            var isHidden = HasComponent<DisableRendering>(entity);
            if (math.abs(throttleInput) > 0.001f && isHidden)
            {
                EntityManager.RemoveComponent<DisableRendering>(entity);
                Debug.Log("Unhiding");
            }

            if (math.abs(throttleInput) < 0.001 && !isHidden)
            {
                EntityManager.AddComponent<DisableRendering>(entity);
                Debug.Log("Hiding");
            }
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
