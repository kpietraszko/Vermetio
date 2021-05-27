using System.Collections;
using System.Collections.Generic;
using Unity.NetCode;
using Unity.NetCode.Editor;
using UnityEngine;

namespace Overrides.NetCodeGen
{
    public class DisablePhysicsOnClient : IGhostDefaultOverridesModifier
    {
        public void Modify(Dictionary<string, GhostComponentModifier> overrides)
        {
            overrides["Unity.Physics.PhysicsDamping"] = new GhostComponentModifier()
            {
                typeFullName = "Unity.Physics.PhysicsDamping",
                attribute = new GhostComponentAttribute() {PrefabType = GhostPrefabType.Server}
            };
            overrides["Unity.Physics.PhysicsMass"] = new GhostComponentModifier()
            {
                typeFullName = "Unity.Physics.PhysicsMass",
                attribute = new GhostComponentAttribute() {PrefabType = GhostPrefabType.Server}
            };
            overrides["Unity.Physics.PhysicsVelocity"] = new GhostComponentModifier()
            {
                typeFullName = "Unity.Physics.PhysicsVelocity",
                attribute = new GhostComponentAttribute() {PrefabType = GhostPrefabType.Server}
            };
        }

        public void ModifyAlwaysIncludedAssembly(HashSet<string> alwaysIncludedAssemblies)
        {
        }

        public void ModifyTypeRegistry(TypeRegistry typeRegistry, string netCodeGenAssemblyPath)
        {
        }
    }
}
