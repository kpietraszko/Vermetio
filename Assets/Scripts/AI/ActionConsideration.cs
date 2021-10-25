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
    
    [Serializable]
    public class ActionConsideration : ISerializationCallbackReceiver
    {
        public ConsiderationInputType InputType;
        public CurveType CurveType;
        public float M = 1f;
        public float K = 1f;
        public float B = 0f;
        public float C = 0f;

        [SerializeField]
        private AnimationCurve _curve;

        private int _generatedCurveHash;

        public void GenerateCurvePreview()
        {
            _curve = new AnimationCurve();
            var steps = 512f;
            for (int i = 0; i < steps; i++)
            {
                var x = i / steps;
                _curve.AddKey(x, ScoreActionsSystem.ProcessWithCurve(x, CurveType, M, K, B, C));
            }
            _generatedCurveHash = GetHashCode();
        }

        public override int GetHashCode()
        {
            return CurveType.GetHashCode() ^ M.GetHashCode() ^ K.GetHashCode() ^ B.GetHashCode() ^ C.GetHashCode();
        }

        public void OnBeforeSerialize()
        {
            if (GetHashCode() == _generatedCurveHash)
                return;

            GenerateCurvePreview();
        }

        public void OnAfterDeserialize()
        {
        }
    }
}