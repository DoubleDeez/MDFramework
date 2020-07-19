using Godot;
using System.Collections.Generic;
using System.Reflection;

namespace MD
{
    /// <summary>
    /// Clocked interpolated vector3
    /// </summary>
    public class MDCRMInterpolatedVector3 : MDCRMInterpolatedValue<Vector3>
    {
        public MDCRMInterpolatedVector3(MemberInfo Member, bool Reliable, MDReplicatedType ReplicatedType,
            WeakRef NodeRef, MDReplicatedSetting[] Settings)
            : base(Member, Reliable, ReplicatedType, NodeRef, Settings)
        {
        }
        
        protected override Vector3 LinearInterpolate(Vector3 LastValue, Vector3 Value, float Alpha)
        {
            return LastValue.LinearInterpolate(Value, Alpha);
        }
        
        protected override bool HasValueChanged(Vector3 CurValue, Vector3 NewValue)
        {
            return CurValue.IsEqualApprox(NewValue) == false;
        }
    }
}