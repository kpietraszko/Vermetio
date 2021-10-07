using System.IO;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;

[UpdateInGroup(typeof(ClientSimulationSystemGroup))]
public class LogPlayerPosition : SystemBase
{
    protected override void OnUpdate()
    {

        Entities
            .WithoutBurst()
            .WithAll<MovableBoatComponent>()
            .ForEach((in LocalToWorld localToWorld) =>
            {
                using (StreamWriter sw = new StreamWriter("boatPositionClient.csv", true))
                {
                    sw.WriteLine($"{localToWorld.Position.x};{localToWorld.Position.z}");
                }
            }).Run();
    }
}
