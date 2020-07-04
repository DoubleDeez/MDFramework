using System;
using System.Collections.Generic;
using System.Reflection;
using Godot;

namespace MD
{
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public class MDBindNode : Attribute
    {
        private const string LOG_CAT = "LogBindNode";

        private string Path;

        // If PathToNode is left empty, we'll search the children based on the the member name
        public MDBindNode(string PathToNode = "")
        {
            Path = PathToNode;
        }

        public string GetNodePath(string DefaultPath)
        {
            return Path == "" ? DefaultPath : Path;
        }

        public static void PopulateBindNodes(Node Instance)
        {
            List<MemberInfo> Members = MDStatics.GetTypeMemberInfos(Instance);
            foreach (MemberInfo Member in Members)
            {
                MDBindNode BindAttr = Member.GetCustomAttribute(typeof(MDBindNode)) as MDBindNode;
                if (BindAttr == null)
                {
                    continue;
                }

                Type MemberType = null;
                FieldInfo Field = Member as FieldInfo;
                PropertyInfo Property = Member as PropertyInfo;
                if (Field != null)
                {
                    MemberType = Field.FieldType;
                }
                else if (Property != null)
                {
                    MemberType = Property.PropertyType;
                }

                if (!MDStatics.IsSameOrSubclass(MemberType, typeof(Node)))
                {
                    MDLog.Error(LOG_CAT,
                        "Not Node-Type field [{0}] on Node {1} with Type [{2}] was marked with [MDBindNode()]",
                        Member.Name, Instance.Name, Instance.GetType().Name);
                    continue;
                }

                string PathToNode = BindAttr.GetNodePath(Member.Name);
                Node BoundNode = FindNode(Instance, PathToNode);
                if (BoundNode == null)
                {
                    continue;
                }

                if (Field != null)
                {
                    Field.SetValue(Instance, BoundNode);
                }
                else if (Property != null)
                {
                    Property.SetValue(Instance, BoundNode);
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

            return null;
        }
    }
}