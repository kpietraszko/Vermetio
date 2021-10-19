using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using Vermetio.AI;

public class ScoreActionsSystem : SystemBase
{
    // TODO: This is dumb. Each consideration can have various different inputs. Rethink
    // Assumes all brains have all actions as components
    
    public struct ScoreActionJob<TAction> : IJobEntityBatch where TAction : struct, IActionComponent
    {
        public ComponentTypeHandle<TAction> ActionTypeHandle;

        public void Execute(ArchetypeChunk batchInChunk, int batchIndex)
        {
            var actions = batchInChunk.GetNativeArray(ActionTypeHandle);
            
            for (int i = 0; i < batchInChunk.Count; i++)
            {
                var action = actions[i];
                action.CurrentScore = i; // TODO: actually implement
                actions[i] = action;
            }
        }
    }

    protected override void OnUpdate()
    {
        throw new System.NotImplementedException();
    }
}
