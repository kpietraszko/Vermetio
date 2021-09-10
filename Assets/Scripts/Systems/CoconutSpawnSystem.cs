using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Physics;
using Unity.Transforms;
using UnityEngine;
using Random = Unity.Mathematics.Random;

namespace Vermetio.Server
{
    [UpdateInGroup(typeof(GhostSimulationSystemGroup))]
    public class CoconutSpawnSystem : SystemBase
    {
        private const int TargetNumberOfCoconuts = 200;
        // private const double Cooldown = 1f;
        private EntityQuery _existingCoconutsQuery;
        private EndSimulationEntityCommandBufferSystem _endSimulationEcbSystem;

        protected override void OnCreate()
        {
            base.OnCreate();
            _existingCoconutsQuery = GetEntityQuery(new EntityQueryDesc()
            {
                All = new ComponentType[] {typeof(SimpleBuoyantComponent)},
                None = new ComponentType[] {typeof(BulletTag)}
            });
            
            _endSimulationEcbSystem = World.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();
        }

        protected override void OnUpdate()
        {
            var existingCoconuts = _existingCoconutsQuery.CalculateEntityCount();
            var coconutsToSpawn = TargetNumberOfCoconuts - existingCoconuts;
            var coconutPrefab = GetGhostPrefab<SimpleBuoyantComponent>(); // TODO: hack
            var endFrameEcb = _endSimulationEcbSystem.CreateCommandBuffer().AsParallelWriter();
            var elapsedTime = Time.ElapsedTime;
            var random = new Random((uint) UnityEngine.Random.Range(1, int.MaxValue));

            Entities
                .WithAll<CoconutSpawnPointTag>()
                .WithNone<CoconutSpawnCooldownComponent>()
                .ForEach((Entity entity, int entityInQueryIndex, in LocalToWorld localToWorld) =>
                {
                    if (entityInQueryIndex >= coconutsToSpawn)
                        return;
                    
                    var coconut = endFrameEcb.Instantiate(entityInQueryIndex, coconutPrefab);
                    endFrameEcb.SetComponent(entityInQueryIndex, coconut, new Translation() {Value = localToWorld.Position});
                    random.InitState((uint)(random.state + entityInQueryIndex));
                    var randomInitialAge = random.NextFloat(0, 20f);
                    Debug.Log($"{randomInitialAge}");
                    endFrameEcb.SetComponent(entityInQueryIndex, coconut, new CoconutAgeComponent() { Age = randomInitialAge});
                    // endFrameEcb.AddComponent(entityInQueryIndex, entity, new CoconutSpawnCooldownComponent() {CooldownStartedAt = elapsedTime});
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
