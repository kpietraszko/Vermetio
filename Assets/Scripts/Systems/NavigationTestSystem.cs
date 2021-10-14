using Reese.Nav;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;
using UnityEngine;

namespace Vermetio.Server
{
    [UpdateInGroup(typeof(ServerSimulationSystemGroup))]
    public class NavigationTestSystem : SystemBase
    {
        private NavSystem _navSystem;
        private Segments.Batch _batch;

        protected override void OnCreate()
        {
            base.OnCreate();
            _navSystem = World.GetOrCreateSystem<NavSystem>();
            Segments.Core.CreateBatch(out _batch, Resources.Load<Material>("Materials/AIPathLine"));
        }

        protected override void OnUpdate()
        {
            _batch.Dependency.Complete();
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            Entities.WithoutBurst().WithAll<NavAgent>().WithNone<NavNeedsSurface, NavDestination>().ForEach(
                (Entity entity) =>
                {
                    Debug.Log("Configuring AI agent");
                    ecb.AddComponent<NavNeedsSurface>(entity);
                    ecb.AddComponent<NavDestination>(entity);
                    ecb.SetComponent(entity, new NavDestination()
                    {
                        Tolerance = 1,
                        CustomLerp = true,
                        WorldPoint = new float3(-55f, 0f, 380f)
                    });
                    
                    EntityManager.SetName(entity, "AIBoat");
                }).Run();

            Entities.ForEach((NavProblem problem) =>
            {
                Debug.LogError($"{problem.Value}");
            }).Run();
            
            var settings = _navSystem.Settings;
            var buffer = _batch.buffer;
            
            Entities.ForEach((Entity entity, in DynamicBuffer<NavPathBufferElement> pathBuffer, in LocalToWorld ltw) =>
            {
                if (pathBuffer.Length == 0)
                    return;
                
                var drawPathOffset = math.up() * 5f;
                var index = 0;
                buffer.Length = 0;

                for (var i = 0; i < pathBuffer.Length - 1; ++i)
                {
                    var start = pathBuffer[i].Value + drawPathOffset;
                    var end = pathBuffer[i + 1].Value + drawPathOffset;
                    var length = math.distance(start, end);
                    // Segments.Plot.DashedLine(buffer, ref index, start, end,  (int)math.ceil(length / 8));
                    Segments.Plot.Line(buffer, ref index, start, end);
                }

                var startLast = pathBuffer[pathBuffer.Length - 1].Value + drawPathOffset;
                var endLast = ltw.Position + drawPathOffset;
                var lengthLast = math.distance(startLast, endLast);
                // Segments.Plot.DashedLine(buffer, ref index, startLast, endLast,  (int)math.ceil(lengthLast / 8));
                Segments.Plot.Line(buffer, ref index, startLast, endLast);
            }).Run();

            var tick = World.GetExistingSystem<ServerSimulationSystemGroup>().ServerTick;
            var boatInputs = GetBufferFromEntity<BoatInput>();

            Entities
                .WithNone<NavProblem>()
                .WithAll<NavCustomLerping>().ForEach((Entity entity, DynamicBuffer<NavPathBufferElement> pathBuffer, in Translation translation) =>
                {
                    if (pathBuffer.Length == 0)
                    {
                        Debug.Log("Nowhere to go");
                        ecb.AppendToBuffer(entity, new BoatInput()
                        {

                            Throttle = 0,
                            Tick = tick + 1, // ??
                        });
                        return;
                    }

                    var pathBufferIndex = pathBuffer.Length - 1;

                    // Debug.Log($"Distance {math.distance(translation.Value, pathBuffer[pathBufferIndex].Value)}");

                    if (NavUtil.ApproxEquals(translation.Value, pathBuffer[pathBufferIndex].Value,
                        settings.StoppingDistance))
                        pathBuffer.RemoveAt(pathBufferIndex);

                    if (pathBuffer.Length == 0)
                    {
                        Debug.Log("Reached destination");
                        ecb.AppendToBuffer(entity, new BoatInput()
                        {

                            Throttle = 0,
                            Tick = tick + 1, // ??
                        });
                        return;
                    }

                    pathBufferIndex = pathBuffer.Length - 1;

                    if (!boatInputs.HasComponent(entity))
                    {
                        ecb.AddBuffer<BoatInput>(entity);
                    }

                    var heading = math.normalizesafe(pathBuffer[pathBufferIndex].Value - translation.Value);
                    ecb.AppendToBuffer(entity, new BoatInput()
                    {

                        Throttle = 1,
                        Tick = tick + 1, // ??
                        TargetDirection = heading
                    });
                }).Run();
            
            ecb.Playback(EntityManager);
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            _batch.Dispose();
        }
    }
}
