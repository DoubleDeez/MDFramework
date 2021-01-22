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
        object ConvertBackToObject(object CurrentObject, object[] Parameters);

        /// <summary>
        /// Check if the object has changed. If your object is a class or struct you need to track when you change it yourself.
        /// You should reset the changed status each time this method is called
        /// </summary>
        /// <param name="LastValue">The last value of the object</param>
        /// <param name="CurrentValue">The new value of the object</param>
        /// <returns></returns>
        bool ShouldObjectBeReplicated(object LastValue, object CurrentValue);

        /// <summary>
        /// This decides if we allow caching of this converter.
        /// Only reason not to allow caching is if the converter holds a state for the particular object
        /// </summary>
        /// <returns>True if we allow caching, false if not</returns>
        bool AllowCachingOfConverter();
    }

    ///<summary> This default implementation should work on any godot base type that can be sent with rpc calls</summary>
    public class MDObjectDataConverter : IMDDataConverter
    {
        public object[] ConvertForSending(object Item, bool Complete)
        {
            return new object[] { Item };
        }

        public object ConvertBackToObject(object CurrentObject, object[] Parameters)
        {
            return Parameters[0];
        }

        public bool ShouldObjectBeReplicated(object LastValue, object CurrentValue)
        {
            return Equals(LastValue, CurrentValue) == false;
        }

        public bool AllowCachingOfConverter()
        {
            return true;
        }
    }

    ///<summary> Data converter for enums</summary>
    public class MDEnumDataConverter<T> : IMDDataConverter where T : Enum
    {
        public object[] ConvertForSending(object Item, bool Complete)
        {
            return new object[] { (int)Item };
        }

        public object ConvertBackToObject(object CurrentObject, object[] Parameters)
        {
            return (T)Parameters[0];
        }

        public bool ShouldObjectBeReplicated(object LastValue, object CurrentValue)
        {
            if (LastValue == null && CurrentValue == null)
            {
                return false;
            }
            else if (LastValue == null || CurrentValue == null)
            {
                return true;
            }
            return Equals((int)LastValue, (int)CurrentValue) == false;
        }

        public bool AllowCachingOfConverter()
        {
            return true;
        }
    }

    ///<summary> Data converter for types such as double or decimal</summary>
    public class MDSendAsStringDataConverter<T> : IMDDataConverter
    {
        public object[] ConvertForSending(object Item, bool Complete)
        {
            if (Item == null)
            {
                return new object[] { null };
            }
            return new object[] { Item.ToString() };
        }

        public object ConvertBackToObject(object CurrentObject, object[] Parameters)
        {
            if (Parameters[0] == null)
            {
                return null;
            }
            return (T)Convert.ChangeType(Parameters[0], typeof(T));
        }

        public bool ShouldObjectBeReplicated(object LastValue, object CurrentValue)
        {
            if (LastValue == null && CurrentValue == null)
            {
                return false;
            }
            else if (LastValue == null || CurrentValue == null)
            {
                return true;
            }
            return Equals(LastValue, CurrentValue) == false;
        }

        public bool AllowCachingOfConverter()
        {
            return true;
        }
    }

    ///<summary> Data converter for IMDCommandReplicator</summary>
    public class MDCommandReplicatorDataConverter<T> : IMDDataConverter where T : IMDCommandReplicator
    {
        private const string LOG_CAT = "LogCommandReplicatorDataConverter";

        public object[] ConvertForSending(object Item, bool Complete)
        {
            if (Item == null)
            {
                return new object[] { null };
            }

            // Get commands
            T CReplicator = (T)Item;
            List<object[]> commands = Complete ? CReplicator.MDGetCommandsForNewPlayer() : CReplicator.MDGetCommands();

            List<object> ReturnList = new List<object>();

            foreach (object[] command in commands)
            {
                // Add length
                ReturnList.Add(command.Length);
                ReturnList.AddRange(command);
            }

            MDLog.Trace(LOG_CAT, $"MDCommandReplicator converting for sending ({MDStatics.GetParametersAsString(ReturnList.ToArray())})");

            return ReturnList.ToArray();
        }

        public object ConvertBackToObject(object CurrentObject, object[] Parameters)
        {
            MDLog.Trace(LOG_CAT, $"MDCommandReplicator converting back ({MDStatics.GetParametersAsString(Parameters)})");

            if (Parameters.Length == 1 && Parameters[0] == null)
            {
                return null;
            }

            T obj;
            if (CurrentObject != null)
            {
                // Replace values in existing object
                obj = (T)CurrentObject;
            }
            else
            {
                // Return a new object
                obj = (T)Activator.CreateInstance(typeof(T));
            }
            
            for (int i=0; i < Parameters.Length; i++)
            {
                // Get the length of the data
                int length = Convert.ToInt32(Parameters[i].ToString());

                // Extract parameters and apply to the command replicator
                object[] converterParams = Parameters.SubArray(i+1, i+length);
                obj.MDProcessCommand(converterParams);
                i += length;
            }

            return (T)obj;
        }

        public bool ShouldObjectBeReplicated(object LastValue, object CurrentValue)
        {
            if (LastValue == null && CurrentValue == null)
            {
                return false;
            }
            else if (LastValue == null || CurrentValue == null)
            {
                MDLog.Trace(LOG_CAT, "We should be replicated because something is null");
                return true;
            }
            else if (((T)CurrentValue).MDShouldBeReplicated())
            {
                MDLog.Trace(LOG_CAT, "We should be replicated because we have updates");
                return true;
            }

            return false;
        }

        public bool AllowCachingOfConverter()
        {
            return true;
        }
    }

    /// <summary>
    /// Data converter for custom classes
    /// </summary>
    /// <typeparam name="T">The custom class type</typeparam>
    public class MDCustomClassDataConverter<T> : IMDDataConverter
    {
        private const string LOG_CAT = "LogCustomClassDataConverter";
        private const String SEPARATOR = "#";
        private List<MemberInfo> Members = null;

        private List<IMDDataConverter> DataConverters = null;

        private List<object> LastValues = new List<object>();

        private void ExtractMembers()
        {
            if (Members == null)
            {
                Members = new List<MemberInfo>();
                DataConverters = new List<IMDDataConverter>();
                IList<MemberInfo> MemberInfos = typeof(T).GetMemberInfos();
                foreach (MemberInfo Member in MemberInfos)
                {
                    MDReplicated RepAttribute = MDReflectionCache.GetCustomAttribute<MDReplicated>(Member) as MDReplicated;
                    if (RepAttribute == null)
                    {
                        continue;
                    }
                    Members.Add(Member);
                    DataConverters.Add(MDStatics.GetConverterForType(Member.GetUnderlyingType()));
                }
            }
        }

        public object[] ConvertForSending(object Item, bool Complete)
        {
            ExtractMembers();
            if (Item == null)
            {
                return new object[] { null };
            }

            List<object> ObjectArray = new List<object>();
            List<object> newLastValues = new List<object>();

            for (int i = 0; i < Members.Count; i++)
            {
                object value = Members[i].GetValue(Item);
                IMDDataConverter Converter = DataConverters[i];
                if (Complete || LastValues.Count == 0 || Converter.ShouldObjectBeReplicated(LastValues[i], value))
                {
                    object[] dataArray = Converter.ConvertForSending(value, Complete);
                    if (dataArray != null)
                    {
                        ObjectArray.Add($"{i}{SEPARATOR}{dataArray.Length}");
                        ObjectArray.AddRange(dataArray);
                    }
                    else
                    {
                        ObjectArray.Add($"{i}{SEPARATOR}{1}");
                        ObjectArray.Add(null);
                    }
                }
                newLastValues.Add(value);
            }

            MDLog.Trace(LOG_CAT, $"MDCustomClass converting for sending ({MDStatics.GetParametersAsString(ObjectArray.ToArray())})");

            LastValues = newLastValues;
            return ObjectArray.ToArray();
        }

        public object ConvertBackToObject(object CurrentObject, object[] Parameters)
        {
            ExtractMembers();
            MDLog.Trace(LOG_CAT, $"MDCustomClass converting back ({MDStatics.GetParametersAsString(Parameters)})");

            if (Parameters.Length == 1 && Parameters[0] == null)
            {
                return null;
            }

            T obj;
            if (CurrentObject != null)
            {
                // Replace values in existing object
                obj = (T)CurrentObject;
            }
            else
            {
                // Return a new object
                obj = (T)Activator.CreateInstance(typeof(T));
            }

            for (int i = 0; i < Parameters.Length; i++)
            {
                // key0 = index, key1 = length
                object[] keys = Parameters[i].ToString().Split(SEPARATOR);
                int index = Convert.ToInt32(keys[0].ToString());
                int length = Convert.ToInt32(keys[1].ToString());

                // Extract parameters and use data converter
                object[] converterParams = Parameters.SubArray(i+1, i+length);
                object currentValue = Members[index].GetValue(obj);
                object convertedValue = DataConverters[index].ConvertBackToObject(currentValue, converterParams);

                // Set the value and increase i based on length of data
                Members[index].SetValue(obj, convertedValue);
                i += length;
            }
            return obj;
        }

        public bool ShouldObjectBeReplicated(object LastValue, object CurrentValue)
        {
            ExtractMembers();
            if (LastValues.Count == 0 && CurrentValue != null)
            {
                MDLog.Trace(LOG_CAT, "We haven't ever replicated");
                return true;
            } 
            else if (Equals(CurrentValue, LastValue) == false)
            {
                MDLog.Trace(LOG_CAT, "The values are different");
                return true;
            }
            else if (LastValue == null && CurrentValue == null)
            {
                return false;
            }

            for (int i = 0; i < Members.Count; i++)
            {
                object value = Members[i].GetValue(CurrentValue);
                if (DataConverters[i].ShouldObjectBeReplicated(LastValues[i], value))
                {
                    MDLog.Trace(LOG_CAT, "We should be replicated");
                    return true;
                }
            }

            return false;
        }

        public bool AllowCachingOfConverter()
        {
            return false;
        }
    }
}