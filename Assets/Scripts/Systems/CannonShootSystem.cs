using System.Linq;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Physics;
using Unity.Physics.Extensions;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;

namespace Vermetio.Server
{
    [UpdateInGroup(typeof(GhostPredictionSystemGroup))]
    [UpdateInWorld(UpdateInWorld.TargetWorld.Server)] // no client side prediction for now
    [UpdateAfter(typeof(CannonAimingSystem))]
    public class CannonShootSystem : SystemBase
    {
        private EndSimulationEntityCommandBufferSystem _endSimulationEcbSystem;

        protected override void OnCreate()
        {
            base.OnCreate();
            _endSimulationEcbSystem = World.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();
        }

        // TODO: rewrite this into max 2 ForEaches to avoid complexity around passing the targetPos
        protected override void OnUpdate()
        {
            var bulletPrefab = GetGhostPrefab<BulletTag>();
            var commandTargetPerEntity = GetComponentDataFromEntity<CommandTargetComponent>(true);

            var ecb = new EntityCommandBuffer(Allocator.Temp);//_endSimulationEcbSystem.CreateCommandBuffer();
            var endFrameEcb = _endSimulationEcbSystem.CreateCommandBuffer();
            // var ecbParallel = _endSimulationEcbSystem.CreateCommandBuffer().AsParallelWriter();

            Entities
                .WithReadOnly(commandTargetPerEntity)
                .ForEach(
                (Entity reqEnt, ref ShootCommand cmd, ref ReceiveRpcCommandRequestComponent req) =>
                {
                    var playerEntity = commandTargetPerEntity[req.SourceConnection].targetEntity;
                    ecb.AddComponent(playerEntity, cmd);
                    ecb.DestroyEntity(reqEnt);
                }).Run();

            ecb.Playback(EntityManager); // so that we can spawn the bullet instantly
            
            ecb = new EntityCommandBuffer(Allocator.Temp);
            var elapsedTime = Time.ElapsedTime;

            Entities
                .ForEach((Entity playerEntity, ref ShootCommand cmd, ref ShootParametersComponent shootParams, in BulletSpawnPointReference spawnPointReference) =>
                {
                    var afterCooldown = elapsedTime - shootParams.LastShotAt > shootParams.Cooldown;
                    if (afterCooldown && shootParams.TargetLegit)
                    {
                        var bullet = ecb.Instantiate(bulletPrefab);
                        var spawnPointLTW = GetComponent<LocalToWorld>(spawnPointReference.BulletSpawnPoint);
                        ecb.SetComponent(bullet, new Translation() {Value = spawnPointLTW.Position});
                        ecb.AddComponent(bullet, new SpawnedByComponent() {Player = playerEntity});
                        shootParams.LastShotAt = elapsedTime;
                    }
                    else
                    {
                        Debug.Log("Wait for cooldown or aim properly!");
                    }

                    ecb.RemoveComponent<ShootCommand>(playerEntity);
                }).Run();
            
            ecb.Playback(EntityManager);

            // Entities.WithAll<BulletFiredComponent>().ForEach((Entity entity, ref PhysicsVelocity pv, in PhysicsMass pm, in LocalToWorld localToWorld) =>
            // {
            //     pv.ApplyLinearImpulse(pm, localToWorld.Forward * 100);
            //     endFrameEcb.RemoveComponent<BulletFiredComponent>(entity);
            // }).Run();

            // _endSimulationEcbSystem.AddJobHandleForProducer(Dependency); // I think only necessary if using ParallelWriter
        }

        private Entity GetGhostPrefab<T>() where T : struct // TODO: move to common
        {
            var ghostCollection = GetSingletonEntity<GhostPrefabCollectionComponent>();
            var prefabs = EntityManager.GetBuffer<GhostPrefabBuffer>(ghostCollection);
            for (int ghostId = 0; ghostId < prefabs.Length; ++ghostId)
            {
                if (EntityManager.HasComponent<T>(prefabs[ghostId].Value))
                    return prefabs[ghostId].Value;
            }

            return Entity.Null;
        }
    }
}