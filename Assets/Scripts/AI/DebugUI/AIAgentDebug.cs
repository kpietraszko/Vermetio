using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Vermetio;
using Vermetio.AI;

public class AIAgentDebug : MonoBehaviour
{
    public Entity AIAgentEntity { get; set; }
    [HideInInspector] public List<ActionDebug> Actions = new List<ActionDebug>();
    [SerializeField]
    public ActionDebug SelectedAction;

    [Serializable]
    public struct ActionDebug
    {
        public string ActionName;
        public Entity Target; // optional
        public List<ConsiderationDebug> Considerations;
    }

    public struct ConsiderationDebug
    {
        public ConsiderationDef ConsiderationDef;
        public float ValueBeforeCurve;
        public float ValueAfterCurve;
    }

    private void Start()
    {
    }

    // TODO: disable this in release builds
    private void Update()
    {
        if (AIAgentEntity == Entity.Null || Selection.activeGameObject != gameObject)
            return;

        // Note that this means the server needs boats proxies GameObjects
        var world = EntityHelpers.GetWorldWith<ServerSimulationSystemGroup>(World.All);
        if (world == null)
            return;

        var system = world.GetExistingSystem<ScoreActionsSystem>();
        // mismatch, using client entity in server world
        if (!system.ConsiderationsPerEntity.ContainsKey(AIAgentEntity))
            return;

        Actions.Clear();
        var considerationsPermutations = system.ConsiderationsPerEntity.GetValuesForKey(AIAgentEntity);
        var actionsPermutations = system.ActionsPermutations.GetValuesForKey(AIAgentEntity);

        foreach (var actionPermutation in actionsPermutations)
        {
            ref var actionDef = ref system.ActionDefRefPerActionId[actionPermutation.ActionId].Value;
            var actionDebug = new ActionDebug()
                { ActionName = actionDef.ActionName.ToString(), Target = actionPermutation.Target };
            var considerationsDebugs = new List<ConsiderationDebug>();
            ref var considerationsDefs = ref actionDef.Considerations;
            for (int i = 0; i < considerationsDefs.Length; i++)
            {
                foreach (var consideration in considerationsPermutations)
                {
                    var curve = considerationsDefs[i].Curve;
                    if (consideration.ConsiderationType == considerationsDefs[i].InputType &&
                        (consideration.Target == actionPermutation.Target || consideration.Target == Entity.Null))
                        considerationsDebugs.Add(new ConsiderationDebug()
                        {
                            ConsiderationDef = considerationsDefs[i],
                            ValueBeforeCurve = consideration.Value,
                            ValueAfterCurve = ScoreActionsSystem.ProcessWithCurve(consideration.Value, curve.CurveType,
                                curve.M, curve.K, curve.B, curve.C)
                        });
                }

                considerationsDebugs = considerationsDebugs.OrderBy(c => c.ConsiderationDef.InputType).ToList();
            }

            actionDebug.Considerations = considerationsDebugs;
            Actions.Add(actionDebug);
        }

        Actions = Actions.OrderBy(a => a.ActionName).ToList();
        ;
    }
}