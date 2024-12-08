using System;
using UnityEngine;
using UnityEngine.Splines;

namespace SplineRoads
{
    [Serializable]
    public class SplineSpan
    {
        public int Index = 0;

        public Vector2 Range = new Vector2(0, 1);

        public void Validate(SplineContainer container)
        {
            Range.x = Mathf.Clamp01(Range.x);
            Range.y = Mathf.Clamp01(Range.y);
        }
    }
}
