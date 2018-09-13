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

    private byte[] ConstructReplicatedPacketData(ReplicatedNode RepNode)
    {
        /*
            Packet format:
            DataArray {
                [DataType] : int
                [NumData] : int - only if DataType is string, Array, List, Dictionary, Struct, or Class
                [TypeNameLength] : int - only if Struct or Class
                [TypeName] : string - only if Struct or Class
                [Data] : byte[] - Length determined by type or NumData, only if NOT Struct, or Class
                [Data] : DataArray - (Recursive) Only if Struct or Class
            }
         */
        

        return null;
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
            [Type data] int 
            [Num Data] int - This is only here for variable sized types (eg. string)
            [Value data] bytes
         */
        object CurrentValue = Field.GetValue(Container);
        if (!CurrentValue.Equals(CachedValue))
        {
            CachedValue = CurrentValue;

            return MDStatics.JoinByteArrays(MDSerialization.SerializeSupportedTypeToBytes(Field.Name, CachedValue));
        }

        return null;
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