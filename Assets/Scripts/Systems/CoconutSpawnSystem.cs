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
    [UpdateInWorld(UpdateInWorld.TargetWorld.Server)]
    public class CoconutSpawnSystem : SystemBase
    {
        public const int TargetNumberOfCoconuts = 60;

        // private const double Cooldown = 1f;
        public EntityQuery _existingCoconutsQuery { get; private set; }
        private EndSimulationEntityCommandBufferSystem _endSimulationEcbSystem;

        protected override void OnCreate()
        {
            base.OnCreate();
            _existingCoconutsQuery = GetEntityQuery(new EntityQueryDesc()
            {
                All = new ComponentType[] { typeof(CoconutAgeComponent) },
                None = new ComponentType[] { typeof(BulletComponent) }
            });

            _endSimulationEcbSystem = World.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();
        }

        protected override void OnUpdate()
        {
            var existingCoconuts = _existingCoconutsQuery.CalculateEntityCount();
            var coconutsToSpawn = TargetNumberOfCoconuts - existingCoconuts;
            var coconutPrefab = GetGhostPrefab<CoconutAgeComponent>();
            var endFrameEcb = _endSimulationEcbSystem.CreateCommandBuffer();
            var endFrameEcbParallel = _endSimulationEcbSystem.CreateCommandBuffer().AsParallelWriter();
            var elapsedTime = Time.ElapsedTime;
            var random = new Random((uint)UnityEngine.Random.Range(1, int.MaxValue));

            Entities
                .ForEach((Entity entity, int entityInQueryIndex, CoconutSpawnCooldownComponent cooldown) =>
                {
                    if (cooldown.CooldownStartedAt + 1.0 <= elapsedTime) // 1 second cooldown
                        endFrameEcb.RemoveComponent<CoconutSpawnCooldownComponent>(entity);
                }).Run();

            Entities
                .WithAll<CoconutSpawnPointTag>()
                .WithNone<CoconutSpawnCooldownComponent>()
                .ForEach((Entity entity, int entityInQueryIndex, in LocalToWorld localToWorld) =>
                {
                    if (entityInQueryIndex >= coconutsToSpawn)
                        return;

                    var coconut = endFrameEcbParallel.Instantiate(entityInQueryIndex, coconutPrefab);
                    endFrameEcbParallel.SetComponent(entityInQueryIndex, coconut,
                        new Translation() { Value = localToWorld.Position });
                    random.InitState((uint)(random.state + entityInQueryIndex));
                    var randomInitialAge = random.NextFloat(0, 20f);
                    endFrameEcbParallel.SetComponent(entityInQueryIndex, coconut,
                        new CoconutAgeComponent() { Age = randomInitialAge });
                    endFrameEcbParallel.AddComponent(entityInQueryIndex, entity,
                        new CoconutSpawnCooldownComponent() { CooldownStartedAt = elapsedTime });
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