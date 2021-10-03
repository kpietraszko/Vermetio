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
using ForceMode = Unity.Physics.Extensions.ForceMode;

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
            var bulletPrefab = GetGhostPrefab<BulletComponent>();
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
                .ForEach((in NetworkSnapshotAckComponent ack, in CommandTargetComponent target) =>
                {
                    if (target.targetEntity == Entity.Null)
                        return;
                    
                    rttPerEntity.Add(target.targetEntity, ack.EstimatedRTT / 1000); // ack.EstimatedRTT is in ms // this sometimes duplicates keys for some reason
                }).Run();

            Entities
                .WithDisposeOnCompletion(rttPerEntity)
                .ForEach((Entity playerEntity,
                    ref ShootParametersComponent shootParams,
                    ref PlayerInventoryComponent inventory, 
                    in BulletSpawnPointReference spawnPointReference,
                    in DynamicBuffer<BoatInput> inputBuffer,
                    in PredictedGhostComponent prediction) =>
                {
                    // if (!GhostPredictionSystemGroup.ShouldPredict(tick, prediction))
                    //     return;
                    
                    inputBuffer.GetDataAtTick(tick, out var input);

                    if (!input.Shoot)
                        return;

                    if (inventory.Coconuts < 1)
                        return;

                    var rtt = rttPerEntity[playerEntity];
                    var inputTravelTime = rtt / 2d;
                    var afterCooldown = (elapsedTime - inputTravelTime) - shootParams.LastShotRequestedAt > shootParams.MinimumShotDelay;
                    if (afterCooldown && shootParams.TargetLegit)
                    {
                        shootParams.LastShotRequestedAt = elapsedTime/* - inputTravelTime*/;
                        // Debug.Log($"{rtt}");

                        var spawnAtTick = tick;
                        
                        // if the input took less than minimum delay to reach the server, delay the shot
                        if (inputTravelTime < shootParams.MinimumShotDelay)
                        {
                            // Debug.Log($"RTT {rtt} lower than min, delaying shot");
                            spawnAtTick = tick + (uint) math.ceil((shootParams.MinimumShotDelay - inputTravelTime) / deltaTime);
                        }
                        
                        ecb.AddComponent(playerEntity, new BulletSpawnComponent()
                        {
                            Velocity = shootParams.Velocity,
                            SpawnAtTick =  spawnAtTick
                        });

                        inventory.Coconuts--;
                    }
                    else
                    {
                        Debug.Log("Wait for cooldown or aim properly!");
                    }
                }).Run();
            
            ecb.Playback(EntityManager);

            ecb = new EntityCommandBuffer(Allocator.Temp);
            var bulletPm = GetComponent<PhysicsMass>(bulletPrefab);

            Entities.ForEach((Entity entity,
                ref PhysicsVelocity pv,
                in PhysicsMass pm, 
                in Translation translation,
                in Rotation rotation, 
                in ShootParametersComponent shootParams,
                in BulletSpawnComponent bulletSpawn,
                in BulletSpawnPointReference spawnPointReference) =>
            {
                if (bulletSpawn.SpawnAtTick < tick) // somehow we missed a spawn, discard it because it's old
                    endFrameEcb.RemoveComponent<BulletSpawnComponent>(entity);
                
                if (tick != bulletSpawn.SpawnAtTick)
                    return;
                
                Debug.Log("Executing spawn");
                var spawnPointLTW = GetComponent<LocalToWorld>(spawnPointReference.BulletSpawnPoint);
                SpawnBullet(ecb, bulletPrefab, spawnPointLTW, entity);
                
                // recoil
                pm.GetImpulseFromForce(-shootParams.Velocity * 15, ForceMode.Impulse, 0f, out var recoilImpulse, out var recoilMass);
                pv.ApplyImpulse(recoilMass, translation, rotation, recoilImpulse, spawnPointLTW.Position);
                
                endFrameEcb.RemoveComponent<BulletSpawnComponent>(entity);
            }).Run();
            
            ecb.Playback(EntityManager);

            // Entities.WithAll<BulletFiredComponent>().ForEach((Entity entity, ref PhysicsVelocity pv, in PhysicsMass pm, in LocalToWorld localToWorld) =>
            // {
            //     pv.ApplyLinearImpulse(pm, localToWorld.Forward * 100);
            //     endFrameEcb.RemoveComponent<BulletFiredComponent>(entity);
            // }).Run();

            // _endSimulationEcbSystem.AddJobHandleForProducer(Dependency); // I think only necessary if using ParallelWriter
        }

        private static void SpawnBullet(EntityCommandBuffer ecb, Entity bulletPrefab, LocalToWorld spawnPointLTW, Entity playerEntity)
        {
            var bullet = ecb.Instantiate(bulletPrefab);
            ecb.SetComponent(bullet, new Translation() {Value = spawnPointLTW.Position});
            ecb.AddComponent(bullet, new SpawnedByComponent() {Player = playerEntity});
            ecb.SetComponent(bullet, new BulletComponent() { FiredByNetworkId = 0 }); // TODO
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