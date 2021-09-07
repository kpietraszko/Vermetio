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
    [UpdateInWorld(UpdateInWorld.TargetWorld.Server)] // no client-side prediction
    [UpdateAfter(typeof(CannonAimingSystem))]
    public class CannonShootSystem : SystemBase
    {
        private GhostPredictionSystemGroup _ghostPredictionSystemGroup;
        private EndSimulationEntityCommandBufferSystem _endSimulationEcbSystem;

        protected override void OnCreate()
        {
            base.OnCreate();
            _ghostPredictionSystemGroup = World.GetExistingSystem<GhostPredictionSystemGroup>();
            _endSimulationEcbSystem = World.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();
        }
        
        protected override void OnUpdate()
        {
            var bulletPrefab = GetGhostPrefab<BulletTag>();
            var commandTargetPerEntity = GetComponentDataFromEntity<CommandTargetComponent>(true);

            var ecb = new EntityCommandBuffer(Allocator.Temp);//_endSimulationEcbSystem.CreateCommandBuffer();
            var endFrameEcb = _endSimulationEcbSystem.CreateCommandBuffer();
            // var ecbParallel = _endSimulationEcbSystem.CreateCommandBuffer().AsParallelWriter();

            ecb.Playback(EntityManager); // so that we can spawn the bullet instantly
            
            ecb = new EntityCommandBuffer(Allocator.Temp);
            var elapsedTime = Time.ElapsedTime;
            var deltaTime = Time.DeltaTime;
            var tick = _ghostPredictionSystemGroup.PredictingTick;

            var rttPerEntity = new NativeHashMap<Entity, float>(100, Allocator.TempJob);

            Entities
                .ForEach((NetworkSnapshotAckComponent ack, CommandTargetComponent target) =>
                {
                    rttPerEntity.Add(target.targetEntity, ack.EstimatedRTT);
                }).Run();

            Entities
                .ForEach((Entity playerEntity,
                    ref ShootParametersComponent shootParams,
                    in BulletSpawnPointReference spawnPointReference,
                    in DynamicBuffer<BoatInput> inputBuffer,
                    in PredictedGhostComponent prediction) =>
                {
                    // if (!GhostPredictionSystemGroup.ShouldPredict(tick, prediction))
                    //     return;
                    
                    inputBuffer.GetDataAtTick(tick, out var input);

                    if (!input.Shoot)
                        return;
                    
                    var afterCooldown = elapsedTime - shootParams.LastShotFiredAt > shootParams.Cooldown;
                    if (afterCooldown && shootParams.TargetLegit)
                    { 
                        shootParams.LastShotFiredAt = elapsedTime;
                        var rtt = rttPerEntity[playerEntity];
                        
                        // if the input already took more than minimum delay to reach the server, don't delay the shot further
                        if (rtt > shootParams.MinimumShotDelay)
                        {
                            var spawnPointLTW = GetComponent<LocalToWorld>(spawnPointReference.BulletSpawnPoint);
                            SpawnBullet(ecb, bulletPrefab, spawnPointLTW, playerEntity);
                            return;
                        }
                        
                        ecb.AddComponent(playerEntity, new DelayedBulletSpawnComponent()
                        {
                            Velocity = shootParams.Velocity,
                            SpawnAtTick =  tick + (long)math.ceil((shootParams.MinimumShotDelay - rtt) / deltaTime)
                        });

                    }
                    else
                    {
                        Debug.Log("Wait for cooldown or aim properly!");
                    }
                }).Run();
            
            ecb.Playback(EntityManager);

            ecb = new EntityCommandBuffer(Allocator.Temp);

            Entities.ForEach((Entity entity, DelayedBulletSpawnComponent delayedBulletSpawn, BulletSpawnPointReference spawnPointReference) =>
            {
                var spawnPointLTW = GetComponent<LocalToWorld>(spawnPointReference.BulletSpawnPoint);
                SpawnBullet(ecb, bulletPrefab, spawnPointLTW, entity);
                ecb.RemoveComponent<DelayedBulletSpawnComponent>(entity);
            }).Run();

            // Entities.WithAll<BulletFiredComponent>().ForEach((Entity entity, ref PhysicsVelocity pv, in PhysicsMass pm, in LocalToWorld localToWorld) =>
            // {
            //     pv.ApplyLinearImpulse(pm, localToWorld.Forward * 100);
            //     endFrameEcb.RemoveComponent<BulletFiredComponent>(entity);
            // }).Run();

            // _endSimulationEcbSystem.AddJobHandleForProducer(Dependency); // I think only necessary if using ParallelWriter
        }

        private static void SpawnBullet(EntityCommandBuffer ecb, Entity bulletPrefab, LocalToWorld spawnPointLTW,
            Entity playerEntity)
        {
            var bullet = ecb.Instantiate(bulletPrefab);
            ecb.SetComponent(bullet, new Translation() {Value = spawnPointLTW.Position});
            ecb.AddComponent(bullet, new SpawnedByComponent() {Player = playerEntity});
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