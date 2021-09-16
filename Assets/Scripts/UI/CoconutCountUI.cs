// using System;
// using System.Collections;
// using System.Collections.Generic;
// using Unity.Entities;
// using Unity.NetCode;
// using Unity.NetCode.Editor;
// using UnityEngine;
// using UnityEngine.UIElements;
// using Vermetio;
//
// #if UNITY_EDITOR || UNITY_CLIENT
// public class CoconutCountUI : MonoBehaviour
// {
//     // private Label _coconutCountLabel;
//
//     void OnEnable()
//     {
//     }
//
//     // Start is called before the first frame update
//     void Start()
//     {
//     }
//
//     private static int? GetCoconutCountFromInventory()
//     {
//         var world =
//             #if UNITY_EDITOR
//                 MultiplayerPlayModeControllerSystem.PresentedClient.World;
//             #else
//                 EntityHelpers.GetWorldWith<ClientSimulationSystemGroup>(World.All);
//             #endif
//
//         if (world == null)
//             return null;
//
//         var networkIdQuery = world.EntityManager.CreateEntityQuery(typeof(NetworkIdComponent));
//         if (networkIdQuery.CalculateEntityCount() != 1)
//             return null;
//
//         var playerEntity = world.EntityManager.GetComponentData<CommandTargetComponent>(networkIdQuery.GetSingletonEntity())
//             .targetEntity;
//         var inventory = world.EntityManager.GetComponentData<PlayerInventoryComponent>(playerEntity);
//         return inventory.Coconuts;
//     }
//
//     // Update is called once per frame
//     void Update()
//     {
//         
//     }
// }
// #endif