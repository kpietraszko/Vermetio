using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;

namespace Vermetio
{
    [UpdateInGroup(typeof(GhostSimulationSystemGroup))]
    [UpdateInWorld(UpdateInWorld.TargetWorld.ClientAndServer)]
    public class BoatChildrenApplyFromProxySystem : SystemBase
    {
        protected override void OnUpdate()
        {
            Entities.ForEach((in BoatChildrenRotationProxy proxy, in BoatCageReference cageRef, in CannonAxleReference axleRef) =>
            {
                SetComponent(cageRef.Cage, new Rotation() { Value = proxy.CageRotation });
                SetComponent(axleRef.Axle, new Rotation() { Value = proxy.AxleRotation });
            }).Schedule();
        }
    }
}
