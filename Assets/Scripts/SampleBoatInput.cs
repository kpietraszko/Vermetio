using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.NetCode;
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
        var localInput = GetSingleton<CommandTargetComponent>().targetEntity;
        if (localInput == Entity.Null)
        {
            AddKeyboardInputBuffer();
            return;
        }
        
        var input = default(BoatKeyboardInput);
        input.Tick = World.GetExistingSystem<ClientSimulationSystemGroup>().ServerTick;

        var keyboard = Keyboard.current;

        if (keyboard == null) // somehow user has no keyboard
            return;

        var targetHeading = EntityManager.GetComponentData<LocalToWorld>(localInput).Forward;
        if (EntityManager.HasComponent<BoatTargetHeadingComponent>(localInput))
            targetHeading = EntityManager.GetComponentData<BoatTargetHeadingComponent>(localInput).Value;
        else EntityManager.AddComponent<BoatTargetHeadingComponent>(localInput);

        var rotationDirection = (keyboard.aKey.isPressed ? -1f : 0) + (keyboard.dKey.isPressed ? 1f : 0);
        var angleToRotateHeadingBy = math.PI / 2f * Time.DeltaTime * rotationDirection; // 90 degrees per second
        var rotationOfHeading = quaternion.AxisAngle(new float3(0, 1, 0), angleToRotateHeadingBy);
        targetHeading = math.normalize(math.mul(rotationOfHeading, targetHeading));

        input.TargetDirection = targetHeading;
        EntityManager.SetComponentData(localInput, new BoatTargetHeadingComponent() { Value = targetHeading });
        
        var throttleInput = (keyboard.wKey.isPressed ? 1f : 0) + (keyboard.sKey.isPressed ? -1f : 0);
        input.Throttle = throttleInput;
        
        var playerPosition = EntityManager.GetComponentData<Translation>(localInput).Value;
        Debug.DrawLine(playerPosition, playerPosition + targetHeading * 10f * throttleInput, Color.green, Time.DeltaTime);

        // if (keyboard.aKey.isPressed)
        //     input
        
        var inputBuffer = EntityManager.GetBuffer<BoatKeyboardInput>(localInput);
        inputBuffer.AddCommandData(input);
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
