// #if !UNITY_EDITOR

using Reese.Nav;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Physics;
using Unity.Transforms;

[UpdateInGroup(typeof(ClientSimulationSystemGroup))]
[UpdateInWorld(UpdateInWorld.TargetWorld.Client)]
public class RemovePhysicsOnClient : SystemBase
{
    protected override void OnUpdate()
    {
        var ecb = World.GetExistingSystem<ClientSimulationSystemGroup>().PostUpdateCommands;

        Entities.WithAny<PhysicsDamping, PhysicsVelocity, PhysicsMass>().ForEach((Entity entity) =>
        {
            ecb.RemoveComponent<PhysicsDamping>(entity);
            ecb.RemoveComponent<PhysicsVelocity>(entity);
            ecb.RemoveComponent<PhysicsMass>(entity);
            // ecb.RemoveComponent<PhysicsCollider>(entity);
        }).Run();

        Entities.WithAny<NavAgent>().ForEach((Entity entity) =>
        {
            ecb.RemoveComponent<NavAgent>(entity);
        }).Run();
    }
}
// #endif