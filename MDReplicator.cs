using Godot;
using System;
using System.Reflection;
using Generics = System.Collections.Generic;
using BytesList = System.Collections.Generic.List<byte[]>;
using ReplicatedNodeDict = System.Collections.Generic.Dictionary<string, ReplicatedNode>;
using ReplicatedNonNodeDict = System.Collections.Generic.Dictionary<string, ReplicatedNonNode>;
using ReplicatedFieldDict = System.Collections.Generic.Dictionary<string, ReplicatedField>;
using ReplicatedListDict = System.Collections.Generic.Dictionary<string, ReplicatedList>;
using ReplicatedDictDict = System.Collections.Generic.Dictionary<string, ReplicatedDict>;
using System.Diagnostics;

public class MDReplicator
{
    private ReplicatedNodeDict NodeList = new ReplicatedNodeDict();
    private const string LOG_CAT = "LogReplicator";

    // Registers the given instance's fields marked with [MDReplicated()]
    public void RegisterReplication(Node Instance)
    {
        if (!RegisterSubNode(Instance, NodeList))
        {
            MDLog.Warn(LOG_CAT, "Attempting to register node ({0}) that doesn't have any replicated fields or subfields.", Instance.GetPath());
        }
    }

    // Broadcasts out replicated modified variables if we're the server, propagates changes recieved from the server if client.
    public void TickReplication()
    {
        BuildNodeDataAndSend();
    }

    // Updates fields on this client with changes received from server
    public void UpdateChanges(byte[] ReplicationData)
    {
        int ByteLocation = 0;
        int NameLength = MDSerialization.GetIntFromStartOfByteArray(ReplicationData);
        string NodeName = (string)MDSerialization.ConvertBytesToSupportedType(MDSerialization.Type_String, ReplicationData.SubArray(ByteLocation += 4, (ByteLocation += NameLength) - 1));
        byte NodeDataType = ReplicationData[ByteLocation++];
        
        if (!NodeList.ContainsKey(NodeName))
        {
            MDLog.Error(LOG_CAT, "Received replication info for non-registered node [{0}]", NodeName);
            return;
        }

        ReplicatedNode RepNode = NodeList[NodeName];
        RepNode.UpdateData(ReplicationData, ByteLocation);
    }

    // Iterate over NodeList finding nodes with updated values
    private void BuildNodeDataAndSend()
    {
        MDGameSession GameSession = MDStatics.GetGameSession();
        foreach(ReplicatedNode RepNode in NodeList.Values)
        {
            byte[] NodeData = RepNode.BuildData();
            if (NodeData != null)
            {
                GameSession.SendPacket(MDPacketType.Replication, NodeData);
            }
        }
    }

    // Registers a subnode field to the provided replicated node
    private bool RegisterSubNode(Node SubNode, ReplicatedNodeDict RepNodeList)
    {
        bool HasReplicatedFields = false;
        ReplicatedNode SubRepNode = new ReplicatedNode();
        SubRepNode.WeakNode = new WeakReference(SubNode);
        FieldInfo[] Fields = SubNode.GetType().GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        foreach(FieldInfo Field in Fields)
        {
            MDReplicated RepAttribute = Field.GetCustomAttribute(typeof(MDReplicated)) as MDReplicated;
            if (RepAttribute != null)
            {
                Type FieldType = Field.FieldType;
                // Is this a sub Node?
                if (FieldType.IsSubclassOf(typeof(Node)))
                {
                    HasReplicatedFields |= RegisterSubNode(Field.GetValue(SubNode) as Node, SubRepNode.ReplicatedNodes);
                }
                // Is this a string or POD type?
                else if (RegisterStringOrPOD(SubNode, Field, SubRepNode.ReplicatedFields))
                {
                    HasReplicatedFields = true;
                }
                // Is this a List/Array?
                else if (typeof(Generics.IList<>).IsAssignableFrom(FieldType))
                {
                    // TODO
                }
                // Is this a dictionary
                else if (FieldType.IsGenericType && FieldType.GetGenericTypeDefinition() == typeof(Generics.Dictionary<,>))
                {
                    // TODO
                }
                else if (false /* How to detect Non-node class/structs? */)
                {

                }
                else
                {
                    MDLog.Error(LOG_CAT, "Attempting to register unsupport type ({0}) for replication.", Field.ToString());
                }
            }
        }

        if (HasReplicatedFields)
        {
            RepNodeList.Add(SubNode.GetName(), SubRepNode);
        }

        return HasReplicatedFields;
    }

    // Registers strings and POD types to the replicated object
    private bool RegisterStringOrPOD(object FieldOwner, FieldInfo Field, ReplicatedFieldDict ReplicatedFields)
    {
        Type FieldType = Field.FieldType;
        if (FieldType == typeof(string) || FieldType.IsPrimitive || FieldType.IsEnum)
        {
            ReplicatedField RepField = new ReplicatedField();
            RepField.Field = Field;

            if (FieldType == typeof(string))
            {
                RepField.CachedValue = string.Copy(Field.GetValue(FieldOwner) as string);
            }
            else
            {
                RepField.CachedValue = Field.GetValue(FieldOwner);
            }

            ReplicatedFields.Add(Field.Name, RepField);
            return true;
        }

        return false;
    }
}

// Base class for replicated class/struct types
public class ReplicatedObject
{
    public ReplicatedNodeDict ReplicatedNodes = new ReplicatedNodeDict();

    public ReplicatedFieldDict ReplicatedFields = new ReplicatedFieldDict();

    public ReplicatedNonNodeDict ReplicatedNonNodes = new ReplicatedNonNodeDict();

    public ReplicatedListDict ReplicatedLists = new ReplicatedListDict();

    public ReplicatedDictDict ReplicatedDicts = new ReplicatedDictDict();

    private const string LOG_CAT = "LogReplicatedObject";

    // Updates the subobjects and returns the number of bytes that were deserialized
    protected int UpdateData(object Container, byte[] Data, int StartIndex)
    {
        /*
            Data format at this point:
            [Num Data] int - The number of sub objects we're replicating
            [Value data] byte[]
         */
        int NumBytesDeserialized = StartIndex;
        int NumSubObjects = MDSerialization.GetIntFromStartOfByteArray(Data.SubArray(NumBytesDeserialized, (NumBytesDeserialized += 4) - 1));

        for (int i = 0; i < NumSubObjects; ++i)
        {
            /*
                Each object Format:
                [Length of Name] int
                [Name data] string
                [Type data] byte 
                [Num Data] int - This is only here for variable sized types (eg. string)
                [Value data] byte[]
            */
            int NameLength = MDSerialization.GetIntFromStartOfByteArray(Data.SubArray(NumBytesDeserialized, (NumBytesDeserialized += 4) - 1));
            string ObjectName = (string)MDSerialization.ConvertBytesToSupportedType(MDSerialization.Type_String, Data.SubArray(NumBytesDeserialized, (NumBytesDeserialized += NameLength) - 1));
            byte ObjectType = Data[NumBytesDeserialized++];
            
            switch(ObjectType)
            {
                case MDSerialization.Type_String:
                case MDSerialization.Type_Bool:
                case MDSerialization.Type_Byte:
                case MDSerialization.Type_Char:
                case MDSerialization.Type_Float:
                case MDSerialization.Type_Long:
                case MDSerialization.Type_Ulong:
                case MDSerialization.Type_Int:
                case MDSerialization.Type_Uint:
                case MDSerialization.Type_Short:
                case MDSerialization.Type_Ushort:
                    if (!ReplicatedFields.ContainsKey(ObjectName))
                    {
                        MDLog.Error(LOG_CAT, "Received replication info for non-replicated field [{0}] of type [{1}]", ObjectName, (int)ObjectType);
                    }
                    else
                    {
                        ReplicatedField RepField = ReplicatedFields[ObjectName];
                        NumBytesDeserialized += RepField.UpdateData(Container, ObjectType, Data, NumBytesDeserialized);
                    }
                    break;

                case MDSerialization.Type_Node:
                    if (!ReplicatedNodes.ContainsKey(ObjectName))
                    {
                        MDLog.Error(LOG_CAT, "Received replication info for non-replicated subnode [{0}]", ObjectName);
                    }
                    else
                    {
                        ReplicatedNode RepNode = ReplicatedNodes[ObjectName];
                        NumBytesDeserialized += RepNode.UpdateData(Data, NumBytesDeserialized);
                    }
                    break;

                case MDSerialization.Type_Invalid:
                default:
                    MDLog.Error(LOG_CAT, "Attempting to deserialize invalid object type [{0}]", (int)ObjectType);
                    break;
            }
        }

        return NumBytesDeserialized - StartIndex;
    }

    protected BytesList BuildChildData(object Container)
    {
        BytesList TempList = new BytesList();
        BuildFieldData(Container, TempList);
        return TempList;
    }

    private void BuildFieldData(object Container, BytesList DataList)
    {
        foreach(ReplicatedField RepField in ReplicatedFields.Values)
        {
            byte[] FieldData = RepField.BuildData(Container);
            if (FieldData != null)
            {
                DataList.Add(FieldData);
            }
        }
    }
}

// Data of a registered replicated Node
public class ReplicatedNode : ReplicatedObject
{
    public WeakReference WeakNode;

    public byte[] BuildData()
    {
        /*
            Node data format:
            [Length of Node Name] int
            [Node Name data] string
            [Type data] byte (MDSerialization.Type_Node)
            [Num Data] int - The number of sub objects we're replicating
            [Value data] byte[]
         */
        Node NodeRef = WeakNode.Target as Node;
        if (NodeRef == null)
        {
            return null;
        }

        BytesList ChildData = BuildChildData(NodeRef);
        int NumRepObjects = ChildData.Count;

        if (NumRepObjects > 0)
        {
            string NodeName = NodeRef.GetName();
            BytesList Data = new BytesList();
            Data.Add(MDSerialization.SerializeSupportedTypeToBytes(NodeName, NodeRef));
            Data.Add(MDSerialization.ConvertSupportedTypeToBytes(NumRepObjects));
            Data.AddRange(ChildData);

            return MDStatics.JoinByteArrays(Data.ToArray());
        }
        
        return null;
    }

    public int UpdateData(byte[] Data, int StartIndex)
    {
        /*
            Data format at this point:
            [Num Data] int - The number of sub objects we're replicating
            [Value data] byte[]
         */
        Node NodeRef = WeakNode.Target as Node;
        return UpdateData(NodeRef, Data, StartIndex);
    }
}

// Data of a replicated non-Node class or struct
public class ReplicatedNonNode : ReplicatedObject
{
    public FieldInfo Field;

    public byte[] BuildData(object Container)
    {
        return null;
    }
}

// Data of a replicated POD or string field
public class ReplicatedField
{
    public FieldInfo Field;

    public object CachedValue;

    public byte[] BuildData(object Container)
    {
        /*
            Field Data Format:
            [Length of Name] int
            [Name data] string
            [Type data] byte 
            [Num Data] int - This is only here for variable sized types (eg. string)
            [Value data] byte[]
        */
        object CurrentValue = Field.GetValue(Container);
        if (!CurrentValue.Equals(CachedValue))
        {
            CachedValue = CurrentValue;

            return MDStatics.JoinByteArrays(MDSerialization.SerializeSupportedTypeToBytes(Field.Name, CachedValue));
        }

        return null;
    }

    // Updates the value and returns the number of bytes deserialized
    public int UpdateData(object Container, byte ObjectType, byte[] Data, int StartIndex)
    {
        /*
            Field Data Format:
            [Num Data] int - This is only here for variable sized types (eg. string)
            [Value data] byte[]
        */

        int NumBytesDeserialized = StartIndex;
        int ValueLength = 0;
        if (MDSerialization.DoesTypeRequireTrackingCount(ObjectType))
        {
            ValueLength = MDSerialization.GetIntFromStartOfByteArray(Data.SubArray(NumBytesDeserialized, (NumBytesDeserialized += 4) - 1));
        }
        else
        {
            ValueLength = MDSerialization.GetSizeInBytes(ObjectType);
        }

        object ReplicatedValue = MDSerialization.ConvertBytesToSupportedType(ObjectType, Data.SubArray(NumBytesDeserialized, (NumBytesDeserialized += ValueLength) - 1));
        if (ReplicatedValue != null && !ReplicatedValue.Equals(CachedValue))
        {
            CachedValue = ReplicatedValue;
            Field.SetValue(Container, CachedValue);
        }

        return NumBytesDeserialized - StartIndex;
    }
}

// Data of a replicated List or array
public class ReplicatedList
{
    public FieldInfo Field;

    // TODO

    public byte[] BuildData()
    {
        return null;
    }
}

// Data of a replicated Dictionary
public class ReplicatedDict
{
    public FieldInfo Field;

    // TODO

    public byte[] BuildData()
    {
        return null;
    }
}