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
            var shouldPlayShootSound = new NativeArray<bool>(1, Allocator.Temp);

            Entities
                .WithAll<CoconutAgeComponent>() // TODO: just for audio test, change this
                .WithNone<BulletFiredSoundPlayedComponent>()
                .ForEach((Entity entity) =>
                {
                    Debug.Log("Will play sound");
                    shouldPlayShootSound[0] = true;
                    endFrameEcb.AddComponent<BulletFiredSoundPlayedComponent>(entity);
                }).Run();

            if (!shouldPlayShootSound[0])
                return;
            
            Debug.Log("Will play sound");

            Entities
                .WithoutBurst()
                .WithAll<MovableBoatComponent>() // TODO: this shoots from every boat, make this only shoot from players who shot this frame
                .ForEach((AudioSource audioSource) =>
                {
                    audioSource.PlayOneShot(audioSource.clip);
                }).Run();
        }
    }
}