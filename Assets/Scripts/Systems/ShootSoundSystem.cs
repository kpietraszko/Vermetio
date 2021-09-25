using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;
using UnityEditorInternal;
using UnityEngine;

namespace Vermetio.Client
{
    [UpdateInGroup(typeof(ClientPresentationSystemGroup))]
    public class ShootSoundSystem : SystemBase
    {
        private BeginInitializationEntityCommandBufferSystem _commandBufferSystem;
        
        private struct BulletFiredSoundPlayedComponent : ISystemStateComponentData
        {
        }

        protected override void OnCreate()
        {
            base.OnCreate();
            _commandBufferSystem = World.GetOrCreateSystem<BeginInitializationEntityCommandBufferSystem>();
        }

        protected override void OnUpdate()
        {
            var endFrameEcb = _commandBufferSystem.CreateCommandBuffer();

            Entities
                .WithoutBurst()
                .WithAll<BulletComponent>()
                .WithNone<BulletFiredSoundPlayedComponent>()
                .ForEach((Entity entity, AudioSource audioSource, in LocalToWorld ltw) =>
                {
                    Debug.Log("Will play sound");
                    audioSource.PlayOneShot(audioSource.clip);
                    
                    endFrameEcb.AddComponent<BulletFiredSoundPlayedComponent>(entity);
                }).Run();

        }
    }
}