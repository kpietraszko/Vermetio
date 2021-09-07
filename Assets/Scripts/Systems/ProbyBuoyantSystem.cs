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
        private static bool _debugDraw = true;
        private Segments.Batch _batch;
        
        private const float WATER_DENSITY = 1000;

        protected override void OnCreate()
        {
            base.OnCreate();

            _buildPhysicsWorld = World.GetOrCreateSystem<BuildPhysicsWorld>();
            _endFramePhysics = World.GetOrCreateSystem<EndFramePhysicsSystem>();
            Segments.Core.CreateBatch(out _batch);
        }

        protected override void OnUpdate()
        {
            _batch.Dependency.Complete();

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

            Entities.ForEach((in DynamicBuffer<ForcePoint> forcePoints) =>
            {
                forcePointsCountArray[0] += forcePoints.Length + 1; // 1 because I'm also adding Translation (or COM?) as an additional force point
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

            Entities
                .WithName("Prepare_query_points")
                .WithoutBurst() // rethink, this will be slow
                .ForEach((Entity entity, in LocalToWorld localToWorld, in ProbyBuoyantComponent buoyantComponent, in DynamicBuffer<ForcePoint> forcePoints) =>
                {
                    for (int i = 0; i < forcePoints.Length; i++)
                    {
                        _queryPoints[queryPointIndex + i] = math.transform(localToWorld.Value, 
                            forcePoints[i].Offset + new float3(0f, buoyantComponent.CenterOfMass.y, 0f));
                    }
                    
                    entitiesStartingIndex.Add(entity, queryPointIndex);
                    queryPointIndex += forcePoints.Length;
                    
                    _queryPoints[queryPointIndex] = math.transform(localToWorld.Value, buoyantComponent.CenterOfMass);
                    queryPointIndex++;
                }).Run();
            
            var status = collProvider.Query(GetHashCode(), 0f, _queryPoints, _waterHeights, null, _velocities);
            if (!collProvider.RetrieveSucceeded(status))
            {
                #if UNITY_EDITOR
                Debug.LogWarning($"query failed: {(CollProviderBakedFFT.QueryStatus) status} on tick {tick}");
                // Debug.Log($"Fail at {tick}");
                #endif
                entitiesStartingIndex.Dispose();
                return;
            }
            
            var waterHeights = new NativeArray<float>(_waterHeights, Allocator.TempJob);
            var waterVelocities = new NativeArray<Vector3>(_velocities, Allocator.TempJob).Reinterpret<float3>();
            
            var debugDraw = _debugDraw;
            var buffer = _batch.buffer;
            
            Entities
                .WithName("Apply_proby_buoyancy")
                // .WithoutBurst()
                .WithReadOnly(waterHeights)
                .WithReadOnly(waterVelocities)
                .WithReadOnly(entitiesStartingIndex)
                .WithDisposeOnCompletion(waterHeights)
                .WithDisposeOnCompletion(waterVelocities)
                .WithDisposeOnCompletion(entitiesStartingIndex)
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
                    // buffer.Length = forcePoints.Length * 3; // 3 lines per cross

                    // Apply buoyancy on force points
                    for (int i = 0; i < forcePoints.Length; i++)
                    {
                        var waterHeight = waterHeights[startingIndex + i];
                        var worldSpaceForcePoint = math.transform(localToWorld.Value, 
                            forcePoints[i].Offset + new float3(0f, buoyant.CenterOfMass.y, 0f));

                        if (debugDraw)
                        {
                            // worldSpaceForcePoint.DrawCross(0.2f, Color.green, 1/30f);
                            // new float3(worldSpaceForcePoint.x, waterHeight, worldSpaceForcePoint.z).DrawCross(0.2f, Color.red, 1/30f);
                            // buffer.AddRange(new float3(worldSpaceForcePoint.x, waterHeight, worldSpaceForcePoint.z).GetCross(1f));
                        }
                        
                        var heightDiff = waterHeight - worldSpaceForcePoint.y; // TODO: query point or force point?
                        if (heightDiff > 0)
                        {
                            // Debug.Log($"worldSpaceForcePoint: {worldSpaceForcePoint} translation: {translation.Value} rotation: {rotation.Value}");
                            pm.GetImpulseFromForce(archimedesForceMagnitude * heightDiff * Vector3.up * forcePoints[i].Weight * buoyant.ForceMultiplier / totalWeight, 
                                ForceMode.Force, deltaTime, out var impulse, out var impulseMass);
                            // Debug.DrawLine(worldSpaceForcePoint, worldSpaceForcePoint + (impulse/6f));
                            pv.ApplyImpulse(impulseMass, translation, rotation, impulse, worldSpaceForcePoint); // TODO: transform to world space?
                        }
                    }
                    
                    // Apply drag relative to water
                    var lastForcePointIndex = startingIndex + forcePoints.Length; // this is actually COM
                    var waterHeightAtCom = waterHeights[lastForcePointIndex];
                    if (math.transform(localToWorld.Value, buoyant.CenterOfMass).y > waterHeightAtCom)
                        return; // don't apply drag if COM is above water, might be weird, but it should be rare
                    
                    var verticalVelocityRelativeToWater = pv.Linear - waterVelocities[lastForcePointIndex]; // uses last force point, which is COM - see Prepare_query_points
                    var forcePosition = math.transform(localToWorld.Value, buoyant.ForceHeightOffset * up); // should be ok?
                    pm.GetImpulseFromForce(up * math.dot(up, -verticalVelocityRelativeToWater) * buoyant.DragInWaterUp, ForceMode.Acceleration, deltaTime, out var dragImpulse, out var dragImpulseMass);
                    pv.ApplyImpulse(dragImpulseMass, translation, rotation, dragImpulse, forcePosition);
                    
                    // right and forward assumes water velocity is 0
                    pm.GetImpulseFromForce(localToWorld.Right * math.dot(localToWorld.Right, -pv.Linear) * buoyant.DragInWaterRight, ForceMode.Acceleration, deltaTime, out dragImpulse, out dragImpulseMass);
                    // pv.ApplyImpulse(dragImpulseMass, translation, rotation, dragImpulse, forcePosition);

                    var forwardDrag = localToWorld.Forward * math.dot(localToWorld.Forward, -pv.Linear) * buoyant.DragInWaterForward;
                    // Debug.DrawLine(translation.Value, translation.Value + forwardDrag, Color.green, deltaTime);
                    pm.GetImpulseFromForce(forwardDrag, ForceMode.Acceleration, deltaTime, out dragImpulse, out dragImpulseMass);
                    // pv.ApplyImpulse(dragImpulseMass, translation, rotation, dragImpulse, forcePosition);

                    // _rb.AddForceAtPosition(transform.right * Vector3.Dot(transform.right, -_velocityRelativeToWater) * _dragInWaterRight, forcePosition, ForceMode.Acceleration);
                    // _rb.AddForceAtPosition(transform.forward * Vector3.Dot(transform.forward, -_velocityRelativeToWater) * _dragInWaterForward, forcePosition, ForceMode.Acceleration);
                }).Run();
            
            _buildPhysicsWorld.AddInputDependencyToComplete(Dependency);
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            _batch.Dispose();
        }
    }
    
}