#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.NetCode;
using Unity.NetCode.Editor;
using UnityEngine;

namespace Overrides.NetCodeGen
{
    public class DisablePhysicsOnClient : IGhostDefaultOverridesModifier
    {
        public void Modify(Dictionary<string, GhostComponentModifier> overrides)
        {
            var fields = new[] {"Linear", "Angular"}.Select(f => new GhostFieldModifier() {name = f});

            overrides["Unity.Physics.PhysicsDamping"] = new GhostComponentModifier()
            {
                typeFullName = "Unity.Physics.PhysicsDamping",
                attribute = new GhostComponentAttribute() {PrefabType = GhostPrefabType.Server},
                fields = fields.ToArray()
            };
            
            overrides["Unity.Physics.PhysicsVelocity"] = new GhostComponentModifier()
            {
                typeFullName = "Unity.Physics.PhysicsVelocity",
                attribute = new GhostComponentAttribute() {PrefabType = GhostPrefabType.Server},
                fields = fields.ToArray()
            };
            
            fields = new [] { "Transform", "InverseMass", "InverseInertia", "AngularExpansionFactor"}.Select(f => new GhostFieldModifier() {name = f});
            overrides["Unity.Physics.PhysicsMass"] = new GhostComponentModifier()
            {
                typeFullName = "Unity.Physics.PhysicsMass",
                attribute = new GhostComponentAttribute() {PrefabType = GhostPrefabType.Server},
                fields = fields.ToArray()
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
#endif