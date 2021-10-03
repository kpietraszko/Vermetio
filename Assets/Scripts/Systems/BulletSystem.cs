using Latios;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Physics;
using Unity.Physics.Extensions;
using Unity.Transforms;
using UnityEngine;
using ForceMode = Unity.Physics.Extensions.ForceMode;

namespace Vermetio.Server
{
    [UpdateInGroup(typeof(GhostPredictionSystemGroup))]
    [UpdateInWorld(UpdateInWorld.TargetWorld.Server)] // no client side prediction for now
    public class BulletSystem : SubSystem
    {
        private EndSimulationEntityCommandBufferSystem _endSimulationEcbSystem;

        protected override void OnCreate()
        {
            base.OnCreate();
            _endSimulationEcbSystem = World.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();
        }

        private struct BulletFiredComponent : ISystemStateComponentData
        {
        }


        protected override void OnUpdate()
        {
            var syncPointEcb = latiosWorld.syncPoint.CreateEntityCommandBuffer().AsParallelWriter();//_endSimulationEcbSystem.CreateCommandBuffer().AsParallelWriter();
            var deltaTime = Time.DeltaTime;

            Entities
                .WithAll<BulletComponent>().WithNone<BulletFiredComponent>()
                .ForEach((Entity entity, int entityInQueryIndex, ref LocalToWorld localToWorld, ref PhysicsVelocity pv,
                    in PhysicsMass pm, in PhysicsGravityFactor gravityFactor, in SpawnedByComponent spawnedBy) =>
                {
                    var shootParams = GetComponent<ShootParametersComponent>(spawnedBy.Player);

                    syncPointEcb.AddComponent<BulletFiredComponent>(entityInQueryIndex, entity);
                    pm.GetImpulseFromForce(shootParams.Velocity, ForceMode.VelocityChange, deltaTime, out var impulse, out var impulseMass);
                    pv.ApplyLinearImpulse(impulseMass, impulse);
                }).Schedule();
            
            // _endSimulationEcbSystem.AddJobHandleForProducer(Dependency);
        }
    }
}