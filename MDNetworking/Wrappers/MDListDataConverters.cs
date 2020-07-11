using Godot;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace MD
{
    public interface IMDDataConverter
    {
        /// <summary>
        /// Convert this object into an object array that can be converted back later
        /// </summary>
        /// <param name="Item">The item to be converted</param>
        /// <param name="Complete">If you support partial replication then do full replication if complete is true</param>
        /// <returns>An array of objects to be sent over the network</returns>
        object[] ConvertForSending(object Item, bool Complete);

        /// <summary>
        /// The list of parameters may be longer than the amount of parameters the conversion consumes
        /// </summary>
        /// <param name="CurrentObject">The object that we are about to replace</param>
        /// <param name="Parameters">The parameters for conversion</param>
        /// <returns>The new object</returns>
        object CovertBackToObject(object CurrentObject, object[] Parameters);

        ///<Summary>Return how many parameters were used by the last ConvertFromObject call</summary>
        int GetParametersConsumedByLastConversion();

        /// <summary>
        /// Check if the object has changed. If your object is a class or struct you need to track when you change it yourself.
        /// You should reset the changed status each time this method is called
        /// </summary>
        /// <param name="LastValue">The last value of the object</param>
        /// <param name="CurrentValue">The new value of the object</param>
        /// <returns></returns>
        bool ShouldObjectBeReplicated(object LastValue, object CurrentValue);
    }

    ///<summary> This default implementation should work on any godot base type that can be sent with rpc calls</summary>
    public class MDObjectDataConverter : IMDDataConverter
    {
        public object[] ConvertForSending(object Item, bool Complete)
        {
            return new object[] { Item };
        }

        public object CovertBackToObject(object CurrentObject, object[] Parameters)
        {
            return Parameters[0];
        }

        public int GetParametersConsumedByLastConversion()
        {
            return 1;
        }

        public bool ShouldObjectBeReplicated(object LastValue, object CurrentValue)
        {
            return Equals(LastValue, CurrentValue) == false;
        }
    }

    public class MDCustomClassDataConverter<T> : IMDDataConverter
    {
        private List<MemberInfo> Members = null;

        private List<object> LastValues = new List<object>();

        private void ExtractMembers()
        {
            if (Members == null)
            {
                Members = new List<MemberInfo>();
                List<MemberInfo> MemberInfos = MDStatics.GetTypeMemberInfos(typeof(T));
                foreach (MemberInfo Member in MemberInfos)
                {
                    MDReplicated RepAttribute = Member.GetCustomAttribute(typeof(MDReplicated)) as MDReplicated;
                    if (RepAttribute == null)
                    {
                        continue;
                    }
                    Members.Add(Member);
                }
            }
        }

        public object[] ConvertForSending(object Item, bool Complete)
        {
            ExtractMembers();
            if (Item == null)
            {
                return null;
            }

            List<object> ObjectArray = new List<object>();
            List<object> newLastValues = new List<object>();

            for (int i = 0; i < Members.Count; i++)
            {
                object value = Members[i].GetValue(Item);
                if (Complete || LastValues.Count == 0 || Equals(LastValues[i], value) == false)
                {
                    ObjectArray.Add(i);
                    ObjectArray.Add(value);
                }
                newLastValues.Add(value);
            }

            LastValues = newLastValues;
            return ObjectArray.ToArray();
        }

        public object CovertBackToObject(object CurrentObject, object[] Parameters)
        {
            ExtractMembers();

            T obj;
            if (CurrentObject != null)
            {
                // Replace values in existing object
                GD.Print("We got one");
                obj = (T)CurrentObject;
            }
            else
            {
                // Return a new object
                GD.Print("Replacing");
                obj = (T)Activator.CreateInstance(typeof(T));
            }

            for (int i = 0; i < Parameters.Length; i = i + 2)
            {
                GD.Print($"Converting back {Members[(int)Parameters[i]].Name} - {Parameters[i+1]}");
                Members[(int)Parameters[i]].SetValue(obj, Parameters[i+1]);
            }
            return obj;
        }

        public int GetParametersConsumedByLastConversion()
        {
            ExtractMembers();
            return Members.Count;
        }

        public bool ShouldObjectBeReplicated(object LastValue, object CurrentValue)
        {
            ExtractMembers();
            if (LastValues.Count == 0 && CurrentValue != null)
            {
                return true;
            } 
            else if (CurrentValue != LastValue)
            {
                return true;
            }
            else if (LastValue == null && CurrentValue == null)
            {
                return false;
            }

            for (int i = 0; i < Members.Count; i++)
            {
                object value = Members[i].GetValue(CurrentValue);
                if (Equals(LastValues[i], value) == false)
                {
                    return true;
                }
            }
            
            return false;
        }
    }
}