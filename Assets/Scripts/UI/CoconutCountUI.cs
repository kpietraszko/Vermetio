using System;
using System.Collections;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Cysharp.Threading.Tasks.Linq;
using TMPro;
using Unity.Entities;
using Unity.NetCode;
using UnityEngine;
using UnityEngine.UIElements;

#if UNITY_EDITOR || UNITY_CLIENT
namespace Vermetio.Client
{
    public class CoconutCountUI : MonoBehaviour
    {
        private TMP_Text _coconutCountLabel;

        void OnEnable()
        {
            _coconutCountLabel = GetComponent<TMP_Text>();
        }

        // Start is called before the first frame update
        async UniTaskVoid Start()
        {
            await foreach(var _ in UniTaskAsyncEnumerable.IntervalFrame(7, PlayerLoopTiming.PreLateUpdate))
            {
                var count = GetCoconutCountFromInventory();
                if (count == null)
                    continue;

                _coconutCountLabel.text = count.ToString();
            }
        }

        private static int? GetCoconutCountFromInventory()
        {
            var world =
#if UNITY_EDITOR
                Unity.NetCode.Editor.MultiplayerPlayModeControllerSystem.PresentedClient?.World;
#else
                EntityHelpers.GetWorldWith<ClientSimulationSystemGroup>(World.All);
#endif

            if (world == null)
                return null;

            var networkIdQuery = world.EntityManager.CreateEntityQuery(typeof(NetworkIdComponent));
            if (networkIdQuery.CalculateEntityCount() != 1)
                return null;

            var playerEntity = world.EntityManager
                .GetComponentData<CommandTargetComponent>(networkIdQuery.GetSingletonEntity()).targetEntity;
            if (!world.EntityManager.HasComponent<PlayerInventoryComponent>(playerEntity))
                return null;
            
            var inventory = world.EntityManager.GetComponentData<PlayerInventoryComponent>(playerEntity);
            return inventory.Coconuts;
        }
    }
}
#endif