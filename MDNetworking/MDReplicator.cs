using Godot;
using System;
using System.Reflection;
using System.Text;
using Generics = System.Collections.Generic;
using BytesList = System.Collections.Generic.List<byte[]>;
using ReplicatedNodeDict = System.Collections.Generic.Dictionary<string, ReplicatedNode>;
using ReplicatedStructDict = System.Collections.Generic.Dictionary<string, ReplicatedStruct>;
using ReplicatedFieldDict = System.Collections.Generic.Dictionary<string, ReplicatedField>;
using ReplicatedListDict = System.Collections.Generic.Dictionary<string, ReplicatedList>;
using ReplicatedDictDict = System.Collections.Generic.Dictionary<string, ReplicatedDict>;
using System.Diagnostics;

public class MDReplicator
{
    private ReplicatedNodeDict NodeList = new ReplicatedNodeDict();
    private const string LOG_CAT = "LogReplicator";

    public MDReplicator()
    {
        MDLog.AddLogCategoryProperties(LOG_CAT, new MDLogProperties(MDLogLevel.Info));
    }

    // Registers the given instance's fields marked with [MDReplicated()]
    public void RegisterReplication(Node Instance)
    {
        RegisterNode(Instance);
    }

    // Unregisters the given instance's fields marked with [MDReplicated()]
    public void UnregisterReplication(Node Instance)
    {
        UnregisterNode(Instance);
    }

    // Broadcasts out replicated modified variables if we're the server, propagates changes recieved from the server if client.
    public void TickReplication()
    {   
        BuildNodeDataAndSend();
    }

    // Updates fields on this client with changes received from server
    public void UpdateChanges(byte[] ReplicationData)
    {       
        /*
            Node data format:
            [Length of Node Name] int
            [Node Name data] string
            [Num Data] int - The number of sub objects we're replicating
            [Value data] byte[]
         */
        #if DEBUG
        using (MDProfiler Profiler = new MDProfiler("MDReplicator.UpdateChanges"))
        #endif
        {
            int ByteLocation = 0;
            int NameLength = MDSerialization.GetIntFromStartOfByteArray(ReplicationData);
            string NodeName = (string)MDSerialization.ConvertBytesToSupportedType(MDSerialization.Type_String, ReplicationData.SubArray(ByteLocation += 4, (ByteLocation += NameLength) - 1));

            MDLog.Debug(LOG_CAT, "Name Length: [{0}] | Node Name: [{1}] | ByteLocation: [{2}]", NameLength, NodeName, ByteLocation);
            
            if (!NodeList.ContainsKey(NodeName))
            {
                MDLog.Error(LOG_CAT, "Received replication info for non-registered node [{0}]", NodeName);
                return;
            }

            ReplicatedNode RepNode = NodeList[NodeName];
            RepNode.UpdateData(ReplicationData, ByteLocation);
        }
    }

    // Sends tracked replicated fields for the specified node to the specified Peer. Generally used to update a peer when they join.
    public void BuildNodeDataAndSendToPeer(int PeerID, string NodeName)
    {
        if (NodeList.ContainsKey(NodeName))
        {
            MDGameSession GameSession = MDStatics.GetGameSession();
            byte[] NodeData = NodeList[NodeName].BuildData(false);
            if (NodeData != null && GameSession != null)
            {
                GameSession.SendPacket(PeerID, MDPacketType.Replication, NodeData);
            }
        }
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
                GameSession.BroadcastPacket(MDPacketType.Replication, NodeData);
            }
        }
    }

    // Registers a replicated node
    private void RegisterNode(Node Instance)
    {
        string NodeName = Instance.GetName();
        if (NodeList.ContainsKey(NodeName))
        {
            MDLog.Warn(LOG_CAT, "Attempting to register already registered Node [{0}]", NodeName);
            return;
        }

        bool HasReplicatedFields = false;
        ReplicatedNode RepNode = new ReplicatedNode();
        RepNode.WeakNode = new WeakReference(Instance);
        FieldInfo[] Fields = Instance.GetType().GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        foreach(FieldInfo Field in Fields)
        {
            MDReplicated RepAttribute = Field.GetCustomAttribute(typeof(MDReplicated)) as MDReplicated;
            if (RepAttribute != null)
            {
                Type FieldType = Field.FieldType;
                if (RegisterSubNodeOrStringOrPOD(Instance, Field, RepNode.ReplicatedFields))
                {
                    HasReplicatedFields = true;
                }
                else if (typeof(Generics.IList<>).IsAssignableFrom(FieldType))
                {
                    // TODO
                }
                else if (FieldType.IsGenericType && FieldType.GetGenericTypeDefinition() == typeof(Generics.Dictionary<,>))
                {
                    // TODO
                }
                else if (FieldType.IsValueType)
                {
                    // TODO - Pretty sure this is guaranteed to be a struct now
                }
                else
                {
                    MDLog.Error(LOG_CAT, "Attempting to register unsupport type ({0}) for replication.", Field.ToString());
                }
            }
        }

        if (HasReplicatedFields)
        {
            NodeList.Add(NodeName, RepNode);

            // If were a client, we should request up-to-date values from the server
            if (MDStatics.GetNetMode() == MDNetMode.Client)
            {
                MDGameSession GameSession = MDStatics.GetGameSession();
                GameSession.SendPacket(MDPacketType.Replication, MDSerialization.ConvertSupportedTypeToBytes(NodeName));
            }
        }
    }

    // Unregisters a replicated node
    private void UnregisterNode(Node Instance)
    {
        string NodeName = Instance.GetName();
        NodeList.Remove(NodeName);
    }

    // Registers strings (or sub node paths) and POD types to the replicated object
    private bool RegisterSubNodeOrStringOrPOD(object FieldOwner, FieldInfo Field, ReplicatedFieldDict ReplicatedFields)
    {
        Type FieldType = Field.FieldType;
        bool IsString = FieldType == typeof(string);
        bool IsSubNode = MDStatics.IsSameOrSubclass(FieldType, typeof(Node));
        if (IsString || IsSubNode || FieldType.IsPrimitive || FieldType.IsEnum)
        {
            ReplicatedField RepField = new ReplicatedField();
            RepField.Field = Field;

            if (IsString)
            {
                string CurrentValue = Field.GetValue(FieldOwner) as string;
                RepField.CachedValue = CurrentValue != null ? string.Copy(CurrentValue) : null;
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
    public ReplicatedFieldDict ReplicatedFields = new ReplicatedFieldDict();

    public ReplicatedStructDict ReplicatedNonNodes = new ReplicatedStructDict();

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
                case MDSerialization.Type_Node:
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

                case MDSerialization.Type_Invalid:
                default:
                    MDLog.Error(LOG_CAT, "Attempting to deserialize invalid object type [{0}]", (int)ObjectType);
                    break;
            }
        }

        return NumBytesDeserialized - StartIndex;
    }

    protected BytesList BuildChildData(object Container, bool ChangedValuesOnly = true)
    {
        BytesList TempList = new BytesList();
        BuildFieldData(Container, TempList, ChangedValuesOnly);
        return TempList;
    }

    private void BuildFieldData(object Container, BytesList DataList, bool ChangedValuesOnly = true)
    {
        foreach(ReplicatedField RepField in ReplicatedFields.Values)
        {
            byte[] FieldData = RepField.BuildData(Container, ChangedValuesOnly);
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

    public byte[] BuildData(bool ChangedValuesOnly = true)
    {
        /*
            Node data format:
            [Length of Node Name] int
            [Node Name data] string
            [Num Data] int - The number of sub objects we're replicating
            [Value data] byte[]
         */
        Node NodeRef = WeakNode.Target as Node;
        if (NodeRef == null)
        {
            return null;
        }

        BytesList ChildData = BuildChildData(NodeRef, ChangedValuesOnly);
        int NumRepObjects = ChildData.Count;

        if (NumRepObjects > 0)
        {
            // Node is a special case (non-subnode) so we have to serialize it manually
            string NodeName = NodeRef.GetName();
            BytesList Data = new BytesList();
            byte[] NameBytes = MDSerialization.ConvertSupportedTypeToBytes(NodeName);
            byte[] NumObjectsBytes = MDSerialization.ConvertSupportedTypeToBytes(NumRepObjects);
            Data.Add(NameBytes);
            Data.Add(NumObjectsBytes);
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

// Data of a replicated struct
public class ReplicatedStruct : ReplicatedObject
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

    public byte[] BuildData(object Container, bool ChangedValuesOnly = true)
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
        if (!ChangedValuesOnly || (CurrentValue == null && CachedValue != null) || !CurrentValue.Equals(CachedValue))
        {
            CachedValue = CurrentValue;
            return MDSerialization.SerializeSupportedTypeToBytes(Field.Name, CachedValue);
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
        if ((ReplicatedValue == null && CachedValue != null) || !ReplicatedValue.Equals(CachedValue))
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