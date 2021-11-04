using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using Unity.Entities;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

[CustomEditor(typeof(AIAgentDebug))]
public class AIAgentDebugEditor : UnityEditor.Editor
{
    private VisualElement _rootElement;
    private VisualTreeAsset _visualTree;
    private AIAgentDebug _aiAgentDebug;
    private VisualElement _graphContainer;
    private VisualElement _considerationsContainer;
    private VisualTreeAsset _actionBarTree;
    private VisualElement _currentActionBar;
    private float barHeight;

    // Start is called before the first frame update
    private void OnEnable()
    {
        _rootElement = new VisualElement();
        _visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/Scripts/AI/DebugUI/AIAgentDebug.uxml");
        _actionBarTree =
            AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/Scripts/AI/DebugUI/AIAgentGraphBar.uxml");
        var stylesheet = AssetDatabase.LoadAssetAtPath<StyleSheet>("Assets/Scripts/AI/DebugUI/AIAgentDebug.uss");

        _rootElement.styleSheets.Add(stylesheet);
        _aiAgentDebug = target as AIAgentDebug;
    }

    public override VisualElement CreateInspectorGUI()
    {
        _rootElement.Clear();
        _visualTree.CloneTree(_rootElement);
        _rootElement.Q<Label>("entity").text = _aiAgentDebug.AIAgentEntity.ToString();

        _graphContainer = _rootElement.Q("graph");
        _considerationsContainer = _rootElement.Q("considerations");
        _rootElement.Bind(new SerializedObject(Selection.activeObject));
        var bestActionScore = _aiAgentDebug.Actions?
            .Select(a => a.Considerations?
                .Aggregate(1, (float sum, AIAgentDebug.ConsiderationDebug current) => sum * current.ValueAfterCurve))
            .Max();

        for (int actionIdx = 0; actionIdx < _aiAgentDebug.Actions.Count; actionIdx++)
        {
            var actionBar = _actionBarTree.Instantiate(); // has to be defined in loop for closure to work
            var thisActionIdx = actionIdx;
            _graphContainer.hierarchy.Add(actionBar);
            var actionDebug = _aiAgentDebug.Actions[actionIdx];
            actionBar.Q<Label>(className: "action-name").text = actionDebug.ActionName;
            actionBar.Q<Label>(className: "action-target").text =
                actionDebug.Target == Entity.Null ? String.Empty : actionDebug.Target.ToString();
            
            var actionScore = actionDebug.Considerations
                .Aggregate(1, (float sum, AIAgentDebug.ConsiderationDebug current) => sum * current.ValueAfterCurve);

            actionBar.Q(className: "graph-bar").style.height = actionScore * 100;

            actionBar.EnableInClassList("best-action",  actionScore == bestActionScore);

            actionBar.RegisterCallback<MouseEnterEvent>(e =>
            {
                _considerationsContainer.Clear();
                foreach (var graphChild in _graphContainer.Children())
                {
                    graphChild.style.opacity = new StyleFloat(0.7f);
                }

                if (e.target is VisualElement element)
                {
                    element.style.opacity = new StyleFloat(1.0f);
                }

                // _aiAgentDebug.SelectedAction = actionDebug;
                _currentActionBar = actionBar.Q(className: "graph-bar");

                for (int consIdx = 0; consIdx < actionDebug.Considerations?.Count; consIdx++)
                {
                    var consideration = actionDebug.Considerations[consIdx];
                    var considerationIdx = consIdx;

                    var considerationLabel =
                        new Label(
                            $"{consideration.ValueBeforeCurve} | {consideration.ConsiderationDef.InputType} | {consideration.ValueAfterCurve}");
                    considerationLabel.RegisterCallback<MouseEnterEvent>(
                        e => // action bar height is considerations up to the hovered one, multiplied
                        {
                            ((VisualElement)e.target).style.backgroundColor = new StyleColor(Color.gray);
                            barHeight = actionDebug.Considerations
                                .Take(considerationIdx + 1)
                                .Aggregate(1,
                                    (float sum, AIAgentDebug.ConsiderationDebug current) =>
                                        sum * current.ValueAfterCurve) * 100;
                            _currentActionBar.style.height = barHeight;
                        });

                    considerationLabel.RegisterCallback<MouseLeaveEvent>(e =>
                    {
                        // restore full action height (so with all considerations)
                        ((VisualElement)e.target).style.backgroundColor = new StyleColor(Color.clear);
                        _currentActionBar.style.height = actionScore * 100;
                    });
                    _considerationsContainer.hierarchy.Add(considerationLabel);
                }
            });
        }

        return _rootElement;
    }
}