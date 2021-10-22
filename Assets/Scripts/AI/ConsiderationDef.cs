using Unity.Collections;

namespace Vermetio.AI
{
    public struct ConsiderationDef
    {
        // public FixedString64 ConsiderationName; // should only be used for debugging
        public ConsiderationInputType InputType;
        public ConsiderationCurve Curve;
    }
}