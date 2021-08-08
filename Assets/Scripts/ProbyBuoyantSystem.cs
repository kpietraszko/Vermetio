using Crest;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Physics;
using Unity.Physics.Extensions;
using Unity.Physics.Systems;
using Unity.Transforms;
using UnityEngine;
using Vermetio.Server;
using ForceMode = Unity.Physics.Extensions.ForceMode;

namespace Vermetio.Server
{
    [UpdateInGroup(typeof(GhostSimulationSystemGroup))]
    [UpdateInWorld(UpdateInWorld.TargetWorld.Server)] // probably redundant
    public class ProbyBuoyantSystem : SystemBase
    {
        BuildPhysicsWorld _buildPhysicsWorld;
        EndFramePhysicsSystem _endFramePhysics;

        private static Vector3[] _queryPoints = new Vector3[0];
        private static float[] _waterHeights = new float[0];
        private static Vector3[] _velocities = new Vector3[0];
        private static bool _debugDraw = false;
        
        private const float WATER_DENSITY = 1000;

        protected override void OnCreate()
        {
            base.OnCreate();

            _buildPhysicsWorld = World.GetOrCreateSystem<BuildPhysicsWorld>();
            _endFramePhysics = World.GetOrCreateSystem<EndFramePhysicsSystem>();
        }

        protected override void OnUpdate()
        {
            Debug.Log("ProbyBuoyancySystem OnUpdate");

            var deltaTime = Time.DeltaTime;
            var tick = World.GetExistingSystem<ServerSimulationSystemGroup>().ServerTick;

            // Following code is copied from SimpleBuoyancySystem

            Dependency = JobHandle.CombineDependencies(Dependency, _endFramePhysics.GetOutputDependency());

            // _buildPhysicsWorld.AddInputDependencyToComplete(Dependency);

            var collProvider = OceanRenderer.Instance.CollisionProvider as CollProviderBakedFFT;
            if (collProvider == null)
            {
                Debug.Log("Collision type is not baked");
                return;
            }
            var forcePointsCount = 0;
            var forcePointsCountArray = new NativeArray<int>(1, Allocator.TempJob);

            Entities.WithoutBurst().ForEach((in DynamicBuffer<ForcePoint> forcePoints) =>
            {
                forcePointsCountArray[0] += forcePoints.Length;
            }).Run();

            forcePointsCount = forcePointsCountArray[0];
            forcePointsCountArray.Dispose();

            // var entities = new NativeArray<Entity>(numberOfBuoyantObjects, Allocator.TempJob);
            if (_queryPoints == null || _queryPoints.Length != forcePointsCount)
            {
                Debug.Log("Proby array size mismatch - reallocating");
                _queryPoints = new Vector3[forcePointsCount];
                _waterHeights = new float[forcePointsCount];
                _velocities = new Vector3[forcePointsCount];
            }

            var queryPointIndex = 0;
            var entitiesStartingIndex = new NativeHashMap<Entity, int>(512, Allocator.TempJob);
            
            var queryPointsWorldSpace = new NativeArray<Vector3>(forcePointsCount, Allocator.Temp);

            Entities
                .WithName("Prepare_query_points")
                .WithoutBurst() // rethink, this will be slow
                .ForEach((Entity entity, in Translation translation, in Rotation rotation, in ProbyBuoyantComponent buoyantComponent, in DynamicBuffer<ForcePoint> forcePoints) =>
                {
                    for (int i = 0; i < forcePoints.Length; i++)
                    {
                        _queryPoints[queryPointIndex + i] = math.transform(new RigidTransform(rotation.Value, translation.Value), forcePoints[i].Offset);
                    }
                    entitiesStartingIndex.Add(entity, queryPointIndex);
                    queryPointIndex += forcePoints.Length;
                }).Run();
            
            var status = collProvider.Query(GetHashCode(), 0f, _queryPoints, _waterHeights, null, _velocities);
            if (!collProvider.RetrieveSucceeded(status))
            {
                Debug.LogWarning($"Proby query failed: {(CollProviderBakedFFT.QueryStatus) status}");
                Debug.Log($"Fail at {tick}");
                return;
            }
            
            var waterHeights = new NativeArray<float>(_waterHeights, Allocator.TempJob);
            var waterVelocities = new NativeArray<Vector3>(_velocities, Allocator.TempJob).Reinterpret<float3>();
            
            var debugDraw = _debugDraw;
            
            Entities
                .WithName("Apply_proby_buoyancy")
                .WithReadOnly(waterHeights)
                .WithReadOnly(waterVelocities)
                .WithDisposeOnCompletion(waterHeights)
                .WithDisposeOnCompletion(waterVelocities)
                .ForEach((Entity entity, ref Translation translation, ref PhysicsVelocity pv,
                    ref ProbyBuoyantComponent buoyant, ref PhysicsMass pm, in LocalToWorld localToWorld, 
                    in Rotation rotation, in DynamicBuffer<ForcePoint> forcePoints) =>
                {
                    pm.CenterOfMass = buoyant.CenterOfMass; // hmmm
                    
                    var up = new float3(0f, 1f, 0f);
                    
                    var archimedesForceMagnitude = WATER_DENSITY * Mathf.Abs(Physics.gravity.y);
                    var totalWeight = 0f;
            
                    for (int i = 0; i < forcePoints.Length; i++)
                    {
                        totalWeight += forcePoints[i].Weight;
                    }
            
                    var startingIndex = entitiesStartingIndex[entity];
            
                    // Apply buoyancy on force points
                    for (int i = 0; i < forcePoints.Length; i++)
                    {
                        var waterHeight = waterHeights[startingIndex + i];
                        var worldSpaceForcePoint = math.transform(new RigidTransform(rotation.Value, translation.Value), forcePoints[i].Offset);
                        var heightDiff = waterHeight - worldSpaceForcePoint.y; // TODO: query point or force point?
                        if (heightDiff > 0)
                        {
                            pm.GetImpulseFromForce(archimedesForceMagnitude * heightDiff * Vector3.up * forcePoints[i].Weight * buoyant.ForceMultiplier / totalWeight, 
                                ForceMode.Force, deltaTime, out var impulse, out var impulseMass);
                            pv.ApplyImpulse(impulseMass, translation, rotation, impulse, worldSpaceForcePoint); // TODO: transform to world space?
                        }
                    }
                    
                    // Apply drag relative to water
                    var velocityRelativeToWater = pv.Linear - waterVelocities[startingIndex]; // note: this means drag depends on velocity at first force point
                    var forcePosition = math.transform(new RigidTransform(rotation.Value, translation.Value), buoyant.ForceHeightOffset * up); // should be ok?
                    pm.GetImpulseFromForce(up * math.dot(up, -velocityRelativeToWater) * buoyant.DragInWaterUp, ForceMode.Acceleration, deltaTime, out var dragImpulse, out var dragImpulseMass);
                    pv.ApplyImpulse(dragImpulseMass, translation, rotation, dragImpulse, forcePosition);
                }).Schedule();
            
            _buildPhysicsWorld.AddInputDependencyToComplete(Dependency);
        }
    }
}