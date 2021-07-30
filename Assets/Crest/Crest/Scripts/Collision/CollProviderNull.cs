// Crest Ocean System

// Copyright 2020 Wave Harmonic Ltd

using System.Collections.Generic;
using UnityEngine;

namespace Crest
{
    /// <summary>
    /// Gives a flat, still ocean.
    /// </summary>
    public class CollProviderNull : ICollProvider
    {
        public int Query(int i_ownerHash, float i_minSpatialLength, IList<Vector3> i_queryPoints,
            IList<Vector3> o_resultDisps, IList<Vector3> o_resultNorms, IList<Vector3> o_resultVels)
        {
            if (o_resultDisps != null)
            {
                for (int i = 0; i < o_resultDisps.Count; i++)
                {
                    o_resultDisps[i] = Vector3.zero;
                }
            }

            if (o_resultNorms != null)
            {
                for (int i = 0; i < o_resultNorms.Count; i++)
                {
                    o_resultNorms[i] = Vector3.up;
                }
            }

            if (o_resultVels != null)
            {
                for (int i = 0; i < o_resultVels.Count; i++)
                {
                    o_resultVels[i] = Vector3.zero;
                }
            }

            return 0;
        }

        public int Query(int i_ownerHash, float i_minSpatialLength, IList<Vector3> i_queryPoints,
            IList<float> o_resultHeights, IList<Vector3> o_resultNorms, IList<Vector3> o_resultVels)
        {
            if (o_resultHeights != null)
            {
                for (int i = 0; i < o_resultHeights.Count; i++)
                {
                    o_resultHeights[i] = 0f;
                }
            }

            if (o_resultNorms != null)
            {
                for (int i = 0; i < o_resultNorms.Count; i++)
                {
                    o_resultNorms[i] = Vector3.up;
                }
            }

            if (o_resultVels != null)
            {
                for (int i = 0; i < o_resultVels.Count; i++)
                {
                    o_resultVels[i] = Vector3.zero;
                }
            }

            return 0;
        }

        public bool RetrieveSucceeded(int queryStatus)
        {
            return true;
        }

        public void UpdateQueries()
        {
        }

        public void CleanUp()
        {
        }

        public readonly static CollProviderNull Instance = new CollProviderNull();
    }
}
