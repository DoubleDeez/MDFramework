using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace MD
{
    /// <summary>
    /// Contains static methods for the MDFramework
    /// </summary>
    public static class MDStatics
    {
        private static readonly Dictionary<string, MemberInfo> MemberInfoCache = new Dictionary<string, MemberInfo>();

        private static readonly Dictionary<string, MethodInfo> MethodInfoCache = new Dictionary<string, MethodInfo>();

        private static readonly Dictionary<Type, IMDDataConverter> DataConverterCache = new Dictionary<Type, IMDDataConverter>();

        public static BindingFlags BindFlagsAllMembers =
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

        // MDStatics needs a reference to a Godot object to really be useful, so the GameInstance sets a reference to itself here
        public static MDGameInstance GI { get; set; }

        // Useful to return instead of null
        public static byte[] EmptyByteArray { get; } = new byte[0];

        /// <summary>
        /// Allows retrieving the game instance without relying on the tree
        /// </summary>
        public static MDGameInstance GetGameInstance()
        {
            return GI;
        }

        ///<summary>Get the global game session from the game instance</summary>
        public static MDGameSession GetGameSession()
        {
            return GI.GameSession;
        }

        ///<summary>Get the global game synchronizer from the game instance</summary>
        public static MDGameSynchronizer GetGameSynchronizer()
        {
            return GI.GameSynchronizer;
        }

        /// <summary>
        /// Get the replicator from the game instance
        /// </summary>
        public static MDReplicator GetReplicator()
        {
            return GI.Replicator;
        }

        /// <summary>
        /// Helper to construct a subarray
        /// </summary>
        /// <param name="data">The array to construct from</param>
        /// <param name="StartIndex">Starting index</param>
        /// <param name="EndIndex">Ending index</param>
        /// <typeparam name="T">Type of the array</typeparam>
        /// <returns>The new sub array</returns>
        public static T[] SubArray<T>(this T[] data, int StartIndex, int EndIndex)
        {
            int NewLength = 1 + (EndIndex - StartIndex);
            if (NewLength == data.Length)
            {
                return data;
            }

            T[] result = new T[NewLength];
            Array.Copy(data, StartIndex, result, 0, NewLength);
            return result;
        }

        /// <summary>
        /// Helper to trim the beginning of an array
        /// </summary>
        /// <param name="data">The array to trim</param>
        /// <param name="StartIndex">The index to keep from</param>
        /// <typeparam name="T">The type of the array</typeparam>
        /// <returns>New array without the beginning</returns>
        public static T[] SubArray<T>(this T[] data, int StartIndex)
        {
            return data.SubArray(StartIndex, data.Length - 1);
        }

        /// <summary>
        /// Helper to append any number of byte arrays
        /// </summary>
        /// <param name="ByteArrays">The byte arrays to append</param>
        /// <returns>A joined byte array</returns>
        public static byte[] JoinByteArrays(params byte[][] ByteArrays)
        {
            // Count the total number of bytes
            int TotalBytes = ByteArrays.Sum(ByteArray => ByteArray.Length);

            // Copy all the bytes into the new array
            byte[] JoinedArray = new byte[TotalBytes];

            int NumBytesCopied = 0;
            foreach (var ByteArray in ByteArrays)
            {
                ByteArray.CopyTo(JoinedArray, NumBytesCopied);
                NumBytesCopied += ByteArray.Length;
            }

            return JoinedArray;
        }

        /// <summary>
        /// Gets the peer ID from the game session, 1 for server or 0 for standalone
        /// </summary>
        /// <returns>The peer id</returns>
        public static int GetPeerId()
        {
            SceneTree Tree = GetTree();
            if (Tree != null && Tree.HasNetworkPeer())
            {
                return GetTree().GetNetworkUniqueId();
            }

            return 0;
        }

        /// <summary>
        /// Checks if the gameclock is active
        /// </summary>
        /// <returns>True if it is, false if not</returns>
        public static bool IsGameClockActive()
        {
            return GetGameSynchronizer() != null && GI.IsGameClockActive();
        }

        /// <summary>
        /// Is the game session started in non-standalone?
        /// </summary>
        /// <returns>True if network is active, false if not</returns>
        public static bool IsNetworkActive()
        {
            return GetPeerId() != 0;
        }

        /// <summary>
        /// Is the game session the server?
        /// </summary>
        /// <returns>Returns true if we are or if network is inactive, false if not</returns>
        public static bool IsServer()
        {
            if (!IsNetworkActive())
            {
                return true;
            }
            return GetPeerId() == GetServerId();
        }

        /// <summary>
        /// Check if we are a client
        /// </summary>
        /// <returns>True if we are, false if not or if network is not active</returns>
        public static bool IsClient()
        {
            return IsNetworkActive() && !IsServer();
        }

        /// <summary>
        /// Get the PeerId of the server
        /// </summary>
        /// <returns>The PeerID of the server</returns>
        public static int GetServerId()
        {
            return GetGameSession().GetNetworkMaster();
        }

        /// <summary>
        /// Get how many players are in the game
        /// </summary>
        public static int GetPlayersCount()
        {
            return GetGameSession().PlayersCount;
        }

        /// <summary>
        /// Gets the net mode of the local client
        /// </summary>
        public static MDNetMode GetNetMode()
        {
            return GI?.GetNetMode() ?? MDNetMode.Standalone;
        }

        /// <summary>
        /// Gets the SceneTree from the GameInstance
        /// </summary>
        public static SceneTree GetTree()
        {
            return GI != null && GI.IsInsideTree() ? GI.GetTree() : null;
        }

        /// <summary>
        /// Check if subclass is a subclass of the base 
        /// </summary>
        /// <param name="SubClass">The sube class</param>
        /// <param name="Base">The base class</param>
        /// <returns>true if the types are equal or is SubClass is a a subclass of Base</returns>
        public static bool IsSameOrSubclass(Type SubClass, Type Base)
        {
            return SubClass.IsSubclassOf(Base) || SubClass == Base;
        }

        /// <summary>
        /// Returns the attribute object for the specified type, 
        /// climbing the hierarchy until Node is reached or the attribute is found
        /// </summary>
        /// <param name="InstanceType">The type to search</param>
        /// <typeparam name="T">The type to find</typeparam>
        /// <returns>The attribute object for the specified type or null if not found</returns>
        public static T FindClassAttribute<T>(Type InstanceType) where T : Attribute
        {
            if (IsSameOrSubclass(InstanceType, typeof(Node)) == false)
            {
                return null;
            }

            T FoundAtr = Attribute.GetCustomAttribute(InstanceType, typeof(T)) as T;
            if (FoundAtr != null)
            {
                return FoundAtr;
            }

            if (InstanceType != typeof(Node))
            {
                return FindClassAttribute<T>(InstanceType.BaseType);
            }

            return null;
        }

        /// <summary>
        /// Get type member infos for the object
        /// </summary>
        /// <param name="Object">The object to find member infos for</param>
        /// <returns>List of member infos</returns>
        public static List<MemberInfo> GetTypeMemberInfos(object Object)
        {
            return GetTypeMemberInfos(Object.GetType());
        }

        /// <summary>
        /// Returns a list of all the unique members for a Node, including the hierarchy
        /// </summary>
        /// <param name="ObjectType">The object type to find for</param>
        /// <returns>List of members</returns>
        public static List<MemberInfo> GetTypeMemberInfos(Type ObjectType)
        {
            List<MemberInfo> Members = new List<MemberInfo>();
            while (ObjectType != null && ObjectType != typeof(Node))
            {
                Members.AddRange(ObjectType.GetFields(BindFlagsAllMembers));
                Members.AddRange(ObjectType.GetProperties(BindFlagsAllMembers));
                ObjectType = ObjectType.BaseType;
            }

            List<MemberInfo> DeDupedMembers = new List<MemberInfo>();
            foreach (MemberInfo Member in Members)
            {
                bool IsUnique = DeDupedMembers.All(
                    DeDupedMember =>
                        DeDupedMember.DeclaringType != Member.DeclaringType || DeDupedMember.Name != Member.Name);

                if (IsUnique)
                {
                    DeDupedMembers.Add(Member);
                }
            }

            return DeDupedMembers;
        }

        /// <summary>
        /// Find a specific member by name
        /// </summary>
        /// <param name="CurNode">The node to search</param>
        /// <param name="Name">Name of the member</param>
        /// <returns>The MemberInfo or null if not found</returns>
        private static MemberInfo GetMemberByName(Node CurNode, string Name)
        {
            Type NodeType = CurNode.GetType();
            MemberInfo Member = NodeType.GetField(Name, BindFlagsAllMembers);
            if (Member == null)
            {
                Member = NodeType.GetProperty(Name, BindFlagsAllMembers);
            }

            return Member;
        }

        /// <summary>
        /// Get the RPC type of a member
        /// </summary>
        /// <param name="Node">The node the member belongs to</param>
        /// <param name="MemberName">The name of the member</param>
        /// <returns>The rpc type as an enum</returns>
        public static MDRemoteMode GetMemberRpcType(Node Node, string MemberName)
        {
            MemberInfo Info = GetMemberInfo(Node, MemberName);

            if (Info == null)
            {
                return MDRemoteMode.Unknown;
            }

            if (Info.GetCustomAttribute(typeof(RemoteAttribute)) != null)
            {
                return MDRemoteMode.Remote;
            }

            if (Info.GetCustomAttribute(typeof(RemoteSyncAttribute)) != null)
            {
                return MDRemoteMode.RemoteSync;
            }

            if (Info.GetCustomAttribute(typeof(PuppetAttribute)) != null)
            {
                return MDRemoteMode.Puppet;
            }

            if (Info.GetCustomAttribute(typeof(PuppetSyncAttribute)) != null)
            {
                return MDRemoteMode.PuppetSync;
            }

            if (Info.GetCustomAttribute(typeof(MasterAttribute)) != null)
            {
                return MDRemoteMode.Master;
            }

            if (Info.GetCustomAttribute(typeof(MasterSyncAttribute)) != null)
            {
                return MDRemoteMode.MasterSync;
            }

            return MDRemoteMode.Unknown;
        }

        /// <summary>
        /// Get rpc type of a method
        /// </summary>
        /// <param name="Node">The node to check</param>
        /// <param name="Method">The method name</param>
        /// <param name="Parameters">The parameters the method takes</param>
        /// <returns>The rpc type</returns>
        public static MDRemoteMode GetMethodRpcType(Node Node, string Method, params object[] Parameters)
        {
            MethodInfo Info = GetMethodInfo(Node, Method, Parameters);

            if (Info == null)
            {
                return MDRemoteMode.Unknown;
            }

            if (Info.GetCustomAttribute(typeof(RemoteAttribute)) != null)
            {
                return MDRemoteMode.Remote;
            }

            if (Info.GetCustomAttribute(typeof(RemoteSyncAttribute)) != null)
            {
                return MDRemoteMode.RemoteSync;
            }

            if (Info.GetCustomAttribute(typeof(PuppetAttribute)) != null)
            {
                return MDRemoteMode.Puppet;
            }

            if (Info.GetCustomAttribute(typeof(PuppetSyncAttribute)) != null)
            {
                return MDRemoteMode.PuppetSync;
            }

            if (Info.GetCustomAttribute(typeof(MasterAttribute)) != null)
            {
                return MDRemoteMode.Master;
            }

            if (Info.GetCustomAttribute(typeof(MasterSyncAttribute)) != null)
            {
                return MDRemoteMode.MasterSync;
            }

            return MDRemoteMode.Unknown;
        }

        /// <summary>
        /// Looks up the MethodInfo in our cache, if it does not exist it is resolved
        /// </summary>
        /// <param name="Node">The node to look this up for</param>
        /// <param name="Method">The name of the method</param>
        /// <param name="Parameters">The parameters you intend to send to the method</param>
        /// <returns>The MethodInfo or null if it does not exist</returns>
        public static MethodInfo GetMethodInfo(Node Node, String Method, params object[] Parameters)
        {
            Type nodeType = Node.GetType();
            List<Type> Signature = MDStatics.GetSignatureFromParameters(Parameters);
            StringBuilder SignatureString = new StringBuilder();
            Signature.ForEach(type => SignatureString.Append(type.ToString()));

            String key = $"{nodeType.ToString()}#{Method}#{SignatureString.ToString()}";

            if (!MethodInfoCache.ContainsKey(key))
            {
                MethodInfo newInfo = nodeType.GetMethod(Method,
                    MDStatics.BindFlagsAllMembers, null, Signature.ToArray(), null);
                MethodInfoCache.Add(key, newInfo);
            }

            return MethodInfoCache[key];
        }

        /// <summary>
        /// Looks up the MemberInfo from our cache, if it does not exist it is resolved
        /// </summary>
        /// <param name="Node">The node to look this up for</param>
        /// <param name="Name">The name of the member</param>
        /// <returns>The MemberInfo or null if it does not exist</returns>
        public static MemberInfo GetMemberInfo(Node Node, String Name)
        {
            String key = $"{Node.GetType().ToString()}#{Name}";
            if (!MemberInfoCache.ContainsKey(key))
            {
                MemberInfo newMember = MDStatics.GetMemberByName(Node, Name);
                MemberInfoCache.Add(key, newMember);
            }
            
            return MemberInfoCache[key];
        }

        /// <summary>
        /// Gets a list of parameter types from a parameter array
        /// </summary>
        /// <param name="Parameters">Array of parameters</param>
        /// <returns>A list containing parameter types in order, or empty list if no parameters</returns>
        public static List<Type> GetSignatureFromParameters(params object[] Parameters)
        {
            List<Type> Signature = new List<Type>();
            foreach (object obj in Parameters)
            {
                Signature.Add(obj.GetType());
            }
            return Signature;
        }

        /// <summary>
        /// Converts a size in bytes such as the one from OS.GetStaticMemoryUsage() into a more human readable format
        /// </summary>
        /// <param name="len">The value to convert</param>
        /// <returns>A human readable string</returns>
        public static string HumanReadableSize(double len)
        {
            string[] sizes = {"B", "KB", "MB", "GB", "TB"};
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len /= 1024;
            }

            return string.Format("{0:0.##} {1}", len, sizes[order]);
        }

        /// <summary>
        /// Get the data converter for the given type
        /// </summary>
        /// <param name="Type">The type to get the data converter for</param>
        /// <returns>The default data converter for this type</returns>
        public static IMDDataConverter GetConverterForType(Type Type)
        {
            if (!DataConverterCache.ContainsKey(Type))
            {
                DataConverterCache.Add(Type, _InternalGetConverterForType(Type));
            }
            return DataConverterCache[Type];
        }

        private static IMDDataConverter _InternalGetConverterForType(Type Type)
        {
            String NameSpace = Type.Namespace;
            if (Type.GetInterface(nameof(IMDDataConverter)) != null)
            {
                return Activator.CreateInstance(Type) as IMDDataConverter;
            }
            else if (Type.IsEnum)
            {
                Type constructedType = typeof(MDEnumDataConverter<>).MakeGenericType(Type);
                return (IMDDataConverter)Activator.CreateInstance(constructedType);
            }
            else if (NameSpace == null || (NameSpace != "System" && NameSpace != "Godot"
                    && NameSpace.StartsWith("Godot.") == false && NameSpace.StartsWith("System.") == false))
            {
                // Custom class converter
                Type constructedType = typeof(MDCustomClassDataConverter<>).MakeGenericType(Type);
                return (IMDDataConverter)Activator.CreateInstance(constructedType);
            }
            else
            {
                // Set our default converter
                return new MDObjectDataConverter();
            }
        }
    }
}