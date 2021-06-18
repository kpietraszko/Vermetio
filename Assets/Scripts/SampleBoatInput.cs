using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;

[UpdateInGroup(typeof(GhostInputSystemGroup))]
public class SampleBoatInput : SystemBase
{
    protected override void OnCreate()
    {
        RequireSingletonForUpdate<NetworkIdComponent>();
    }
    
    protected override void OnUpdate()
    {
        var localInput = GetSingleton<CommandTargetComponent>().targetEntity;
        if (localInput == Entity.Null)
        {
            var localPlayerId = GetSingleton<NetworkIdComponent>().Value;
            Entities.WithStructuralChanges().WithAll<MovableBoatComponent>().WithNone<BoatInput>().ForEach((Entity ent, ref GhostOwnerComponent ghostOwner) =>
            {
                if (ghostOwner.NetworkId == localPlayerId)
                {
                    EntityManager.AddBuffer<BoatInput>(ent);
                    EntityManager.SetComponentData(GetSingletonEntity<CommandTargetComponent>(), new CommandTargetComponent {targetEntity = ent});
                }
            }).Run();
            return;
        }
        
        var input = default(BoatInput);
        input.Tick = World.GetExistingSystem<ClientSimulationSystemGroup>().ServerTick;
        
        var inputBuffer = EntityManager.GetBuffer<BoatInput>(localInput);
        inputBuffer.AddCommandData(input);
    }
}
