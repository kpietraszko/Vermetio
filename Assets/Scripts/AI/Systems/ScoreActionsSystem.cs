using System;
using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Entities.CodeGeneratedJobForEach;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Rendering;
using Vermetio.AI;

[AlwaysUpdateSystem]
[UpdateInGroup(typeof(ServerSimulationSystemGroup))]
public class ScoreActionsSystem : SystemBase
{
    private EntityQuery _allActionsQuery;
    private static Entity _allActionsEntity;
    private EntityQuery _aiBrainsQuery;
    private NativeMultiHashMap<Entity, ConsiderationPermutation> _considerationsPerEntity; // static or not?
    private EndSimulationEntityCommandBufferSystem _endSimulationEcbSystem;

    public struct ConsiderationPermutation
    {
        public ConsiderationInputType ConsiderationType;
        public float Value;
        public Entity Target; // optional

        public ConsiderationPermutation(ConsiderationInputType type, float value, Entity target = default)
        {
            ConsiderationType = type;
            Value = value;
            Target = target;
        }
    }

    // used internally here, but also as a component on AI agent
    public struct ActionPermutation : IComponentData
    {
        public int ActionId;
        public float Score;
        public Entity Target; // optional
    }

    protected override void OnCreate()
    {
        base.OnCreate();
        _endSimulationEcbSystem = World.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();
    }

    protected override void OnUpdate()
    {
        _allActionsQuery = GetEntityQuery(typeof(AIAllActionsComponent));
        if (!TryGetSingletonEntity<AIAllActionsComponent>(out _allActionsEntity))
            return;
        
        var considerationsPerEntity = new NativeMultiHashMap<Entity, ConsiderationPermutation>(_aiBrainsQuery.CalculateEntityCount(), Allocator.TempJob);

        // for every action type I need a list of Consideration
                Entities.WithNone<Prefab>().WithAll<AIAgentComponent>()
                    .ForEach((Entity entity, in HealthComponent health) =>
                    {
                        considerationsPerEntity.Add(entity, 
                            new ConsiderationPermutation(ConsiderationInputType.MyHealth, health.Value / 3f));
                    }).Run();
        
                Entities.WithNone<Prefab>().WithAll<AIAgentComponent>()
                    .ForEach((Entity entity, in PlayerInventoryComponent inventory) =>
                    {
                        considerationsPerEntity.Add(entity,
                            new ConsiderationPermutation(ConsiderationInputType.MyAmmo, inventory.Coconuts / 20f));
                    }).Run();

                var testAiBrains = _aiBrainsQuery.ToEntityArray(Allocator.TempJob);
                Entities.WithNone<Prefab>().WithAll<AIAgentComponent>().WithDisposeOnCompletion(testAiBrains)
                    .ForEach((Entity entity, in AIAgentComponent aiBrain) =>
                    {
                        // imagine I'm getting enemies position here
                        var distanceToEnemy1 = 100f;
                        
                        considerationsPerEntity.Add(entity,
                            new ConsiderationPermutation(ConsiderationInputType.DistanceToEnemy, distanceToEnemy1 / 100f, testAiBrains[0]));

                        var distanceToEnemy2 = 170f;
                        
                        considerationsPerEntity.Add(entity,
                            new ConsiderationPermutation(ConsiderationInputType.DistanceToEnemy, distanceToEnemy2 / 100f, testAiBrains[1]));
                    }).Run();

        _considerationsPerEntity = considerationsPerEntity;

        var allActionsComponent = GetComponent<AIAllActionsComponent>(_allActionsEntity);
        var actionsPermutations = new NativeMultiHashMap<Entity, ActionPermutation>(allActionsComponent.AllActionsCount * 2, Allocator.TempJob);
        MergeInActionPermutations(actionsPermutations, EntityManager.GetComponentData<RoamActionComponent>(_allActionsEntity), out var roamActionId);
        MergeInActionPermutations(actionsPermutations, EntityManager.GetComponentData<AttackActionComponent>(_allActionsEntity), out var attackActionId);

        var keyValues = actionsPermutations.GetKeyValueArrays(Allocator.TempJob);
        ;
        // generic job that takes the roam/attackComponent, iterates over that action's blob's considerations, pulls their values from the NativeMultiHashMap,
        // runs them through the response curve, and multiplies them together, returns NativeMultiHashMap<Entity, ActionScorePermutation> for that action
        // still have to find best permutation and act on it though :/
        // maybe also a generic method that returns a list of ActionScorePermutation for given action and out roamActionId, then merge lists and sort by score

        var endFrameEcb = _endSimulationEcbSystem.CreateCommandBuffer();
        
        Entities.WithStoreEntityQueryInField(ref _aiBrainsQuery).ForEach((Entity entity, in AIAgentComponent brain) =>
        {
            ActionPermutation bestActionPermutation = default;
            foreach (var permutation in actionsPermutations.GetValuesForKey(entity))
            {
                if (permutation.Score > bestActionPermutation.Score)
                    bestActionPermutation = permutation;
            }

            if (bestActionPermutation.ActionId == roamActionId)
                endFrameEcb.AddComponent<RoamActionComponent>(entity);

            if (bestActionPermutation.ActionId == attackActionId)
            {
                endFrameEcb.AddComponent<AttackActionComponent>(entity);
                endFrameEcb.SetComponent(entity, new AIActionTargetComponent() { Value = bestActionPermutation.Target });
            }

        }).Run();
        
        _endSimulationEcbSystem.AddJobHandleForProducer(Dependency);
    }

    private void MergeInActionPermutations(NativeMultiHashMap<Entity, ActionPermutation> permutations, IActionComponent actionComponent, out int actionId)
    {
        actionId = actionComponent.ActionId;
        // ref var actionDef = ref actionComponent.ActionDef.Value;

        var considerationsPerEntity = _considerationsPerEntity;

        new ActionScorePermutationsJob()
            {
                // change ActionDef - get it from a new mega blob
                ActionDef = actionComponent.ActionDef,
                ActionId = actionComponent.ActionId,
                EntityType = GetEntityTypeHandle(),
                ActionsPermutations = permutations,
                ConsiderationsPerEntity = considerationsPerEntity
            }
            .Run(_aiBrainsQuery);
    }

    // Has to be a job because ForEach rejects "dynamic" code with Branch
    // Does this actually need to be generic?
    public struct ActionScorePermutationsJob/*<TAction>*/ : IJobEntityBatch/* where TAction : struct, IActionComponent*/
    {
        public BlobAssetReference<ActionDef> ActionDef;
        public int ActionId;
        [ReadOnly] public EntityTypeHandle EntityType;
        public NativeMultiHashMap<Entity, ConsiderationPermutation> ConsiderationsPerEntity;
        public NativeMultiHashMap<Entity, ActionPermutation> ActionsPermutations; // output, Entity here is Brain

        [BurstCompile(FloatMode = FloatMode.Fast)]
        public void Execute(ArchetypeChunk batchInChunk, int batchIndex)
        {
            ref var considerationsDefs = ref ActionDef.Value.Considerations;
            // ref var actionName = ref ActionDef.Value.ActionName;
            var entities = batchInChunk.GetNativeArray(EntityType);
            
            for (int entityIdx = 0; entityIdx < batchInChunk.Count; entityIdx++)
            {
                var actionScorePerTarget = new NativeHashMap<Entity, ActionPermutation>(10, Allocator.Temp); // Entity here is Target
                var considerationPermutations = ConsiderationsPerEntity.GetValuesForKey(entities[entityIdx]);
                
                for (int i = 0; i < considerationsDefs.Length; i++)
                {
                    ref var consideration = ref considerationsDefs[i];
                    ref var curve = ref considerationsDefs[i].Curve;
                    // Debug.Log($"Curve type: {curve.CurveType} curve: {curve.B};{curve.C};{curve.K};{curve.M}");
                    foreach (var considerationPermutation in considerationPermutations)
                    {
                        if (considerationPermutation.ConsiderationType == consideration.InputType)
                        {
                            var output = ProcessWithCurve(considerationPermutation.Value, curve.CurveType, curve.M, curve.K, curve.B, curve.C);

                            // Add or update this action's score for this target
                            if (!actionScorePerTarget.TryAdd(considerationPermutation.Target, new ActionPermutation()
                            {
                                ActionId = ActionId,
                                Score = output,
                                Target = considerationPermutation.Target
                            }))
                            {
                                var score = actionScorePerTarget[considerationPermutation.Target];
                                score.Score = score.Score * output;
                                actionScorePerTarget[considerationPermutation.Target] = score;
                            }
                        }
                    }
                }
                
                // Copy final scores per target to output
                var scorePerTarget = actionScorePerTarget.GetKeyValueArrays(Allocator.Temp); // key is Target
                for (int i = 0; i < scorePerTarget.Length; i++)
                {
                    ActionsPermutations.Add(entities[entityIdx], new ActionPermutation()
                    {
                        ActionId = ActionId,
                        Score = scorePerTarget.Values[i].Score,
                        Target = scorePerTarget.Keys[i]
                    });
                }
            }
        }
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ProcessWithCurve(float value, CurveType curveType, float m, float k, float b, float c)
        {
            var x = math.clamp(value, 0f, 1f);
            var output = 0f;
            switch (curveType)
            {
                case CurveType.LinearOrCubic:
                    output = m * math.pow(x - c, k) + b;
                    break;
                case CurveType.SCurve:
                    output = k *
                             (1f / (1f + math.pow(1000f * math.E * m, -x * x + c))) +
                             b;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
    
            output = math.clamp(output, 0f, 1f);
            // Debug.Log($"{output}");
            return output;
        }
}