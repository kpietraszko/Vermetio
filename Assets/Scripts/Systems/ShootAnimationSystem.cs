using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;
using UnityEngine;

namespace Vermetio.Client
{
    [UpdateInGroup(typeof(ClientPresentationSystemGroup))]
    public class ShootAnimationSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            var elapsedTime = Time.ElapsedTime;
            var tickRate = default(ClientServerTickRate);
            if (HasSingleton<ClientServerTickRate>())
            {
                tickRate = GetSingleton<ClientServerTickRate>();
            }

            tickRate.ResolveDefaults();

            var clientTickRate = NetworkTimeSystem.DefaultClientTickRate;
            if (HasSingleton<ClientTickRate>())
                clientTickRate = GetSingleton<ClientTickRate>();

            var interpolationBuffer = clientTickRate.InterpolationTimeNetTicks * (1f / tickRate.NetworkTickRate);
            
            Entities.ForEach((in ShootParametersComponent shootParams, in CannonReference cannonRef) =>
            {
                var timeSinceShotRequested = elapsedTime - shootParams.LastShotRequestedAt;
                var timeAnimationShouldTake = shootParams.MinimumShotDelay * 2 + interpolationBuffer;
                var animTime = math.clamp(timeSinceShotRequested / timeAnimationShouldTake, 0.0, 1.0);
                SetComponent(cannonRef.Cannon, new AnimTimeProperty() { Value = (float)animTime});
            }).Schedule();
        }
    }
}
