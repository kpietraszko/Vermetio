using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Physics;
using Unity.Transforms;
using UnityEngine;

// namespace Vermetio
// {
// [UpdateInGroup(typeof(ServerSimulationSystemGroup))]
// [AlwaysUpdateSystem]
// public class ServerTest : SystemBase
// {
//     protected override void OnUpdate()
//     {
//         var count = GetEntityQuery(ComponentType.ReadOnly<PhysicsMass>()).CalculateEntityCount();
//         Debug.Log($"[S] With mass: {count}");
//
//     }
// }
//
// [UpdateInGroup(typeof(ClientSimulationSystemGroup))]
// [AlwaysUpdateSystem]
// public class ClientTest : SystemBase
// {
//     protected override void OnUpdate()
//     {
//         var count = GetEntityQuery(ComponentType.ReadOnly<PhysicsMass>()).CalculateEntityCount();
//         Debug.Log($"[C] With mass: {count}");
//
//     }
// }
// }