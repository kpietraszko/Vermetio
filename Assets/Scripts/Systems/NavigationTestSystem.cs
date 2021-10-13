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
        protected override void OnUpdate()
        {
            // Assign values to local variables captured in your job here, so that it has
            // everything it needs to do its work when it runs later.
            // For example,
            //     float deltaTime = Time.DeltaTime;

            // This declares a new kind of job, which is a unit of work to do.
            // The job is declared as an Entities.ForEach with the target components as parameters,
            // meaning it will process all entities in the world that have both
            // Translation and Rotation components. Change it to process the component
            // types you want.
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
            
            Entities.ForEach((Entity entity, in DynamicBuffer<NavPathBufferElement> pathBuffer, in LocalToWorld ltw) =>
            {
                var drawPathOffset = math.up() * 4f;
                
                for (var i = 0; i < pathBuffer.Length - 1; ++i)
                    Debug.DrawLine(pathBuffer[i].Value + drawPathOffset, pathBuffer[i + 1].Value + drawPathOffset, Color.red);
                
                Debug.DrawLine(pathBuffer[pathBuffer.Length - 1].Value + drawPathOffset, ltw.Position + drawPathOffset, Color.red);
            }).Run();
            
            ecb.Playback(EntityManager);
        }
    }
}
