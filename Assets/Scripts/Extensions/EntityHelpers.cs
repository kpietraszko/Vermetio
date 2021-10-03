using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using Unity.NetCode;
using Unity.Physics;
using Unity.Transforms;
using UnityEngine;

namespace Vermetio
{
    public static class EntityHelpers
    {
        public static Entity GetGhostPrefab<T>(EntityManager entityManager) where T : struct
        {
            var ghostCollection = entityManager.CreateEntityQuery(typeof(GhostPrefabCollectionComponent))
                .GetSingletonEntity();
            var prefabs = entityManager.GetBuffer<GhostPrefabBuffer>(ghostCollection);
            for (int ghostId = 0; ghostId < prefabs.Length; ++ghostId)
            {
                if (entityManager.HasComponent<T>(prefabs[ghostId].Value))
                    return prefabs[ghostId].Value;
            }

            return Entity.Null;
        }

        public static Entity GetRootParent(Entity entity, ComponentDataFromEntity<Parent> parentPerEntity)
        {
            var currentParent = entity;
            while (parentPerEntity.HasComponent(currentParent)) // while currentParent has a parent
            {
                currentParent = parentPerEntity[currentParent].Value;
            }

            return currentParent;
        }
        
        public static World GetWorldWith<T>(World.NoAllocReadOnlyCollection<World> worlds) where T : ComponentSystemBase
        {
            foreach (var world in worlds)
            {
                if (world.GetExistingSystem<T>() != null)
                    return world;
            }

            return null;
        }

        public static Entity GetEntityFromEvent<T>(this ComponentDataFromEntity<T> dictionary, CollisionEvent collisionEvent) where T : struct, IComponentData
        {
            return dictionary.HasComponent(collisionEvent.EntityA) ? collisionEvent.EntityA :
                dictionary.HasComponent(collisionEvent.EntityB) ? collisionEvent.EntityB : Entity.Null;
        }
    }
}
