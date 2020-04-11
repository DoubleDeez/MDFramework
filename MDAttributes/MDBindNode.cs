using System;
using System.Reflection;
using Godot;

[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
public class MDBindNode : Attribute
{
    private const string LOG_CAT = "LogBindNode";

    // If PathToNode is left empty, we'll search the children based on the the member name
    public MDBindNode(string PathToNode = "")
    {
        Path = PathToNode;
    }

    public string GetNodePath(string DefaultPath)
    {
        return Path == "" ? DefaultPath : Path;
    }

    private string Path;

    public static void PopulateBindNodes(Node Instance)
    {
        FieldInfo[] Fields = Instance.GetType().GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        foreach(FieldInfo Field in Fields)
        {
            MDBindNode BindAttr = Field.GetCustomAttribute(typeof(MDBindNode)) as MDBindNode;
            if (BindAttr != null)
            {
                if (!MDStatics.IsSameOrSubclass(Field.FieldType, typeof(Node)))
                {
                    MDLog.Error(LOG_CAT, "Not Node-Type field [{0}] on Node Type [{0}] was marked with [MDBindNode()]", Field.Name, Instance.GetType().Name);
                    continue;
                }

                string PathToNode = BindAttr.GetNodePath(Field.Name);
                Node BoundNode = FindNode(Instance, PathToNode);
                if (BoundNode != null)
                {
                    Field.SetValue(Instance, BoundNode);
                }
            }
        }

        PropertyInfo[] Props = Instance.GetType().GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        foreach(PropertyInfo Prop in Props)
        {
            MDBindNode BindAttr = Prop.GetCustomAttribute(typeof(MDBindNode)) as MDBindNode;
            if (BindAttr != null)
            {
                if (!MDStatics.IsSameOrSubclass(Prop.PropertyType, typeof(Node)))
                {
                    MDLog.Error(LOG_CAT, "Non Node-Type property [{0}] on Node Type [{0}] was marked with [MDBindNode()]", Prop.Name, Instance.GetType().Name);
                    continue;
                }

                string PathToNode = BindAttr.GetNodePath(Prop.Name);
                Node BoundNode = FindNode(Instance, PathToNode);
                if (BoundNode != null)
                {
                    Prop.SetValue(Instance, BoundNode);
                }
            }
        }
    }

    private static Node FindNode(Node Instance, string PathToNode)
    {
        // First, if we have an explicit node path, try that
        Node BoundNode = Instance.GetNodeOrNull(PathToNode);
        if (BoundNode != null)
        {
            return BoundNode;
        }

        // Check if we have a child with the same name
        Godot.Collections.Array Children = Instance.GetChildren();
        foreach (Node Child in Children)
        {
            if (Child != null && Child.Name == PathToNode)
            {
                return Child;
            }
        }

        // Then check if a child has the node instead
        foreach (Node Child in Children)
        {
            BoundNode = FindNode(Child, PathToNode);
            if (BoundNode != null)
            {
                return BoundNode;
            }
        }

        // If it's still not found, log an error
        MDLog.Error(LOG_CAT, "Failed to find BindNode with path [{0}] on Node [{1}] of type [{2}]", PathToNode, Instance.Name, Instance.GetType().Name);
        return null;
    }
}