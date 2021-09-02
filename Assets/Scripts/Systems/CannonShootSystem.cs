using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;

namespace Vermetio.Server
{
    [UpdateInGroup(typeof(GhostPredictionSystemGroup))]
    [UpdateInWorld(UpdateInWorld.TargetWorld.Server)] // no client side prediction for now
    public class CannonShootSystem : SystemBase
    {
        private EndSimulationEntityCommandBufferSystem _endSimulationEcbSystem;
        private EntityQuery _bulletSpawnPointsQuery;

        protected override void OnCreate()
        {
            base.OnCreate();
            _endSimulationEcbSystem = World.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();
            _bulletSpawnPointsQuery = EntityManager.CreateEntityQuery(typeof(BulletSpawnPointComponent));
        }

        protected override void OnUpdate()
        {
            var bulletPrefab = GetGhostPrefab<BulletTag>();
            // TODO: networkId from entity, get SpawnPoint with that networkId.Value, get localToWorld of that SpawnPoint, use its position 
            
            var ecb = _endSimulationEcbSystem.CreateCommandBuffer().AsParallelWriter();

            Entities.ForEach(
                (Entity reqEnt, int entityInQueryIndex, ref ShootCommand cmd, ref ReceiveRpcCommandRequestComponent req) =>
                {
                    ecb.DestroyEntity(entityInQueryIndex, reqEnt);
                    var bullet = ecb.Instantiate(entityInQueryIndex, bulletPrefab);
                    // ecb.SetComponent(entityInQueryIndex, bullet, new Translation() { Value = localToWorld.Position });
                }).Schedule();

            _endSimulationEcbSystem.AddJobHandleForProducer(Dependency);
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