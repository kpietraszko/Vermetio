using System;
using System.Collections;
using UnityEngine;

namespace Vermetio.AI
{
    [Serializable]
    public enum CurveType
    {
        LinearOrCubic = 1,
        SCurve = 2
    }
    
    [CreateAssetMenu(fileName = "ActionConsideration", menuName = "AI/Consideration")]
    [Serializable]
    public class ActionConsideration : ExpandableGenericScriptableObject
    {
        public ConsiderationInputType InputType;
        public CurveType CurveType;
        public float CurveM;
        public float CurveK;
        public float CurveB;
        public float CurveC;

        [SerializeField]
        private AnimationCurve _curve;

        private void OnValidate()
        {
            _curve = new AnimationCurve();
        }
    }
}