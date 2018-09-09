using Godot;
using System;
using System.Reflection;
using Generics = System.Collections.Generic;
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
    public void TickReplication(float DeltaTime)
    {

    }

    // Broadcasts out replicated modified variables
    private void BroadcastChanges()
    {

    }

    // Updates fields on this client with changes received from server
    private void UpdateChanges()
    {

    }

    // Registers a subnode field to the provided replicated node
    private bool RegisterSubNode(Node SubNode, ReplicatedNodeDict RepNodeList)
    {
        bool HasReplicatedFields = false;
        ReplicatedNode SubRepNode = new ReplicatedNode();
        FieldInfo[] Fields = SubNode.GetType().GetFields();
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

                }
                // Is this a dictionary
                else if (FieldType.IsGenericType && FieldType.GetGenericTypeDefinition() == typeof(Generics.Dictionary<,>))
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
}

// Data of a registered replicated Node
public class ReplicatedNode : ReplicatedObject
{
    public WeakRef WeakNode;
}

// Data of a replicated non-Node class or struct
public class ReplicatedNonNode : ReplicatedObject
{
    public WeakReference<object> WeakObject;

    public FieldInfo Field;
}

// Data of a replicated POD or string field
public class ReplicatedField
{
    public FieldInfo Field;

    public object CachedValue;
}

// Data of a replicated List or array
public class ReplicatedList
{
    public FieldInfo Field;

    // TODO
}

// Data of a replicated Dictionary
public class ReplicatedDict
{
    public FieldInfo Field;

    // TODO
}