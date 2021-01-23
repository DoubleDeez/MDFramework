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
            IList<MemberInfo> Members = MDStatics.GetTypeMemberInfos(Instance);
            foreach (MemberInfo Member in Members)
            {
                MDBindNode BindAttr = MDReflectionCache.GetCustomAttribute<MDBindNode>(Member) as MDBindNode;
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
                        $"Not Node-Type field [{Member.Name}] on Node {Instance.Name} with Type [{Instance.GetType().Name}] was marked with [MDBindNode()]");
                    continue;
                }

                string PathToNode = BindAttr.GetNodePath(Member.Name);
                Node BoundNode = Instance.MDFindNode(PathToNode);
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
    }
}