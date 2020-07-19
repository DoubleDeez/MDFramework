using Godot;
using System.Collections.Generic;
using System.Reflection;

namespace MD
{
    /// <summary>
    /// Clocked interpolated float
    /// </summary>
    public class MDCRMInterpolatedFloat : MDCRMInterpolatedValue<float>
    {
        public MDCRMInterpolatedFloat(MemberInfo Member, bool Reliable, MDReplicatedType ReplicatedType,
            WeakRef NodeRef, MDReplicatedSetting[] Settings)
            : base(Member, Reliable, ReplicatedType, NodeRef, Settings)
        {
        }
        
        protected override float LinearInterpolate(float LastValue, float Value, float Alpha)
        {
            return Mathf.Lerp(LastValue, Value, Alpha);
        }
        
        protected override bool HasValueChanged(float CurValue, float NewValue)
        {
            return Mathf.IsEqualApprox(CurValue, NewValue) == false;
        }
    }
}