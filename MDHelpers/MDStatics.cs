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
        private const string LOG_CAT = "LogMDStatics";
        private const string SEPARATOR = "#";
        private static readonly Dictionary<string, MemberInfo> MemberInfoCache = new Dictionary<string, MemberInfo>();

        private static readonly Dictionary<string, MethodInfo> MethodInfoCache = new Dictionary<string, MethodInfo>();

        private static readonly Dictionary<Type, IList<MethodInfo>> NumberedMethodInfoCache = new Dictionary<Type, IList<MethodInfo>>();

        private static readonly Dictionary<Type, IMDDataConverter> DataConverterCache = new Dictionary<Type, IMDDataConverter>();

        private static readonly Dictionary<Type, IMDDataConverter> DataConverterTypeCache = new Dictionary<Type, IMDDataConverter>();

        public static BindingFlags BindFlagsAll = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.FlattenHierarchy | BindingFlags.Static;

        public static BindingFlags BindFlagsAllIgnoreCase = BindFlagsAll | BindingFlags.IgnoreCase;
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
        
        /// <summary>Grabs the Interface Manager from the GameInstance</summary>
        public static MDInterfaceManager GetInterfaceManager()
        {
            return GI.InterfaceManager;
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
        public static T FindClassAttributeInNode<T>(Type InstanceType) where T : Attribute
        {
            return MDReflectionCache.FindClassAttributeInNode<T>(InstanceType);
        }

        /// <summary>
        /// Returns the attribute object for the specified type, 
        /// climbing the hierarchy until we got no parent or the attribute is found
        /// </summary>
        /// <param name="InstanceType">The type to search</param>
        /// <typeparam name="T">The type to find</typeparam>
        /// <returns>The attribute object for the specified type or null if not found</returns>
        public static T FindClassAttribute<T>(Type InstanceType) where T : Attribute
        {
            T FoundAtr = Attribute.GetCustomAttribute(InstanceType, typeof(T)) as T;
            if (FoundAtr != null)
            {
                return FoundAtr;
            }

            if (InstanceType != typeof(Node) && InstanceType.BaseType != null)
            {
                return FindClassAttribute<T>(InstanceType.BaseType);
            }

            return null;
        }

        /// <summary>
        /// Check if a type is in the godot namespace
        /// </summary>
        public static bool IsInGodotNamespace(Type type)
        {
            if (type.Namespace != null && 
               (type.Namespace == "Godot" || type.Namespace.StartsWith("Godot.")))
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Get type member infos for the object
        /// </summary>
        /// <param name="Object">The object to find member infos for</param>
        /// <returns>List of member infos</returns>
        public static IList<MemberInfo> GetTypeMemberInfos(object Object)
        {
            return Object.GetType().GetMemberInfos();
        }

        /// <summary>
        /// Creates an instance of the type based on the base class T
        /// </summary>
        /// <param name="Type">The type to instantiate</param>
        /// <returns>The instance created or null if it fails</returns>
        public static T CreateTypeInstance<T>(Type Type) where T : class
        {
            if (!IsSameOrSubclass(Type, typeof(T)))
            {
                MDLog.Error(LOG_CAT, $"Type [{Type.Name}] is not a subclass of [{typeof(T).Name}]");
                return null;
            }

            return Activator.CreateInstance(Type) as T;
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
            return NodeType.GetMemberRecursive(Name);
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

            if (MDReflectionCache.GetCustomAttribute<RemoteAttribute>(Info) != null)
            {
                return MDRemoteMode.Remote;
            }

            if (MDReflectionCache.GetCustomAttribute<RemoteSyncAttribute>(Info) != null)
            {
                return MDRemoteMode.RemoteSync;
            }

            if (MDReflectionCache.GetCustomAttribute<PuppetAttribute>(Info) != null)
            {
                return MDRemoteMode.Puppet;
            }

            if (MDReflectionCache.GetCustomAttribute<PuppetSyncAttribute>(Info) != null)
            {
                return MDRemoteMode.PuppetSync;
            }

            if (MDReflectionCache.GetCustomAttribute<MasterAttribute>(Info) != null)
            {
                return MDRemoteMode.Master;
            }

            if (MDReflectionCache.GetCustomAttribute<MasterSyncAttribute>(Info) != null)
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

            if (MDReflectionCache.GetCustomAttribute<RemoteAttribute>(Info) != null)
            {
                return MDRemoteMode.Remote;
            }

            if (MDReflectionCache.GetCustomAttribute<RemoteSyncAttribute>(Info) != null)
            {
                return MDRemoteMode.RemoteSync;
            }

            if (MDReflectionCache.GetCustomAttribute<PuppetAttribute>(Info) != null)
            {
                return MDRemoteMode.Puppet;
            }

            if (MDReflectionCache.GetCustomAttribute<PuppetSyncAttribute>(Info) != null)
            {
                return MDRemoteMode.PuppetSync;
            }

            if (MDReflectionCache.GetCustomAttribute<MasterAttribute>(Info) != null)
            {
                return MDRemoteMode.Master;
            }

            if (MDReflectionCache.GetCustomAttribute<MasterSyncAttribute>(Info) != null)
            {
                return MDRemoteMode.MasterSync;
            }

            return MDRemoteMode.Unknown;
        }

        /// <summary>
        /// Looks up the MethodInfo in our cache, if it does not exist it is resolved
        /// </summary>
        /// <param name="Node">The node to look this up for</param>
        /// <param name="MethodNumber">The number of the method</param>
        /// <returns>The MethodInfo or null if it does not exist</returns>
        public static MethodInfo GetMethodInfo(Node Node, int MethodNumber)
        {
            Type nodeType = Node.GetType();
            if (!NumberedMethodInfoCache.ContainsKey(nodeType))
            {
                NumberedMethodInfoCache.Add(nodeType, nodeType.GetMethodInfos());
            }

            if (MethodNumber < NumberedMethodInfoCache[nodeType].Count)
            {
                return NumberedMethodInfoCache[nodeType][MethodNumber];
            }

            return null;
        }

        /// <summary>
        /// Gets the number of a method
        /// </summary>
        /// <param name="Node">The node to look this up for</param>
        /// <param name="MethodNumber">The name of the method</param>
        /// <param name="Parameters">The parameters you intend to send to the method</param>
        /// <returns>The MethodInfo or -1 if it is not found</returns>
        public static int GetMethodNumber(Node Node, string Method, params object[] Parameters)
        {
            Type nodeType = Node.GetType();
            if (!NumberedMethodInfoCache.ContainsKey(nodeType))
            {
                NumberedMethodInfoCache.Add(nodeType, nodeType.GetMethodInfos());
            }

            MethodInfo info = GetMethodInfo(Node, Method, Parameters);

            if (info != null)
            {
                IList<MethodInfo> methodInfos = NumberedMethodInfoCache[nodeType];
                for (int i = 0; i < methodInfos.Count; i++)
                {
                    if (methodInfos[i].Equals(info))
                    {
                        return i;
                    }
                }
            }

            MDLog.Warn(LOG_CAT, $"Method number could not be found for {nodeType.ToString()}#{Method}({GetParametersAsString(Parameters)})");
            return -1;
        }
        
        

        /// <summary>
        /// Looks up the MethodInfo in our cache, if it does not exist it is resolved
        /// </summary>
        /// <param name="Node">The node to look this up for</param>
        /// <param name="Method">The name of the method</param>
        /// <param name="Parameters">The parameters you intend to send to the method</param>
        /// <returns>The MethodInfo or null if it does not exist</returns>
        public static MethodInfo GetMethodInfo(Node Node, string Method, params object[] Parameters)
        {
            Type nodeType = Node.GetType();
            List<Type> Signature = MDStatics.GetSignatureFromParameters(Parameters);
            StringBuilder SignatureString = new StringBuilder();
            Signature.ForEach(type => SignatureString.Append(type.ToString()));

            String key = $"{nodeType.ToString()}#{Method}#{SignatureString.ToString()}";

            if (!MethodInfoCache.ContainsKey(key))
            {
                MethodInfo newInfo = nodeType.GetMethodRecursive(Method, Signature.ToArray());
                if (newInfo != null)
                {
                    MethodInfoCache.Add(key, newInfo);
                }
                else
                {
                    // Couldn't find anything with direct compare so we will search for the method manually and cache it if found
                    IList<MethodInfo> Methods = nodeType.GetMethodInfos();
                    foreach (MethodInfo CandidateMethod in Methods)
                    {
                        if (CandidateMethod.Name != Method)
                        {
                            continue;
                        }

                        ParameterInfo[] CandidateParams = CandidateMethod.GetParameters();
                        if (CandidateParams.Count() != Signature.Count)
                        {
                            continue;
                        }

                        List<Type> CandidateSignature = new List<Type>();
                        CandidateParams.ToList().ForEach(param => CandidateSignature.Add(param.ParameterType));

                        MDLog.Debug(LOG_CAT, $"Evaluating {CandidateMethod.Name} ({GetParametersAsString(CandidateSignature.ToArray())})");

                        bool IsCompatible = true;

                        for (int i = 0; i < Signature.Count; ++i)
                        {
                            Type SignatureType = Signature[i];
                            Type CandidateType = CandidateSignature[i];
                            bool isNullable = CandidateType.IsNullable();
                            if ((CandidateType.IsNullable() && (Parameters == null || Parameters[i] == null)) == false
                                && (SignatureType.IsCastableTo(CandidateType) == false))
                            {
                                MDLog.Debug(LOG_CAT, $"CandidateMethod.Name: {CandidateMethod.Name} SignatureType: {SignatureType.ToString()} does not cast to {CandidateType.ToString()}");
                                IsCompatible = false;
                                break;
                            }
                        }

                        if (IsCompatible)
                        {
                            MDLog.Debug(LOG_CAT, $"Adding compatible method key {key}");
                            MethodInfoCache.Add(key, CandidateMethod);
                        }
                    }
                }
            }

            if (MethodInfoCache.ContainsKey(key))
            {
                return MethodInfoCache[key];
            }
            return null;
        }

        /// <summary>
        /// Converts the parameters for sending
        /// </summary>
        /// <param name="MethodInfo">The method the parameters are for</param>
        /// <param name="Parameters">The parameters</param>
        /// <returns>List of converted parameters</returns>
        public static object[] ConvertParametersForSending(MethodInfo MethodInfo, params object[] Parameters)
        {
            ParameterInfo[] CandidateParams = MethodInfo.GetParameters();

            List<object> NewParams = new List<object>();
            for (int i = 0; i < CandidateParams.Length; i++)
            {
                IMDDataConverter Converter = GetConverterForType(CandidateParams[i].ParameterType);
                object[] paramsForSending = new object[] { null };
                if (Parameters != null && i < Parameters.Length)
                {
                    paramsForSending = Converter.ConvertForSending(Parameters[i], true);
                    NewParams.Add($"{i}{SEPARATOR}{paramsForSending.Length}");
                }
                else
                {
                    NewParams.Add($"{i}{SEPARATOR}{1}");
                }
                NewParams.AddRange(paramsForSending);
            }

            return NewParams.ToArray();
        }

        /// <summary>
        /// Converts a set of parameters that were sent back to an object
        /// </summary>
        /// <param name="MethodInfo">The method info to convert for</param>
        /// <param name="Parameters">The parameters</param>
        /// <returns></returns>
        public static object[] ConvertParametersBackToObject(MethodInfo MethodInfo, params object[] Parameters)
        {
            if (Parameters == null || Parameters.Length == 0)
            {
                return Parameters;
            }

            ParameterInfo[] CandidateParams = MethodInfo.GetParameters();

            List<object> ConvertedParams = new List<object>();
            for (int i = 0; i < Parameters.Length; i++)
            {
                // key0 = index, key1 = length
                object[] keys = Parameters[i].ToString().Split(SEPARATOR);
                int index = Convert.ToInt32(keys[0].ToString());
                int length = Convert.ToInt32(keys[1].ToString());

                // Extract parameters and use data converter
                object[] converterParams = Parameters.SubArray(i+1, i+length);
                IMDDataConverter Converter = GetConverterForType(CandidateParams[index].ParameterType);
                object convertedValue = Converter.ConvertBackToObject(null, converterParams);

                // Add the value to our list
                ConvertedParams.Add(convertedValue);
                i += length;
            }

            return ConvertedParams.ToArray();
        }        

        /// <summary>
        /// Looks up the MemberInfo from our cache, if it does not exist it is resolved
        /// </summary>
        /// <param name="Node">The node to look this up for</param>
        /// <param name="Name">The name of the member</param>
        /// <returns>The MemberInfo or null if it does not exist</returns>
        public static MemberInfo GetMemberInfo(Node Node, string Name)
        {
            string key = $"{Node.GetType().ToString()}#{Name}";
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
            if (Parameters == null)
            {
                Signature.Add(typeof(object));
            }
            else
            {
                foreach (object obj in Parameters)
                {
                    if (obj == null)
                    {
                        Signature.Add(typeof(object));
                    }
                    else
                    {
                        Signature.Add(obj.GetType());
                    }
                }
            }
            return Signature;
        }

        /// <summary>
        /// Gets a string containing the parameter values
        /// </summary>
        /// <param name="Parameters">Array of parameters</param>
        /// <returns>A string containing all parameters and their values</returns>
        public static string GetParametersAsString(params object[] Parameters)
        {
            if (Parameters == null)
            {
                return "null";
            }
            string ReturnString = "";
            foreach (object obj in Parameters)
            {
                string toAdd = obj == null ? "null" : obj.ToString();
                ReturnString += ReturnString == "" ? toAdd : $", {toAdd}";
            }
            return ReturnString;
        }

        /// <summary>
        /// Converts a size in bytes such as the one from OS.GetStaticMemoryUsage() into a more human readable format
        /// </summary>
        /// <param name="len">The value to convert</param>
        /// <returns>A human readable string</returns>
        public static string HumanReadableMemorySize(double len)
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
        /// Adds a data converter to the cache for the given type. This converter will be returned when using 
        /// MDstatics.GetConverterForType() once it is in the cache.
        /// </summary>
        /// <param name="Type">The type to add the converter for, include generic typing</param>
        /// <param name="DataConverterType">The type of the data converter</param>
        /// <returns>True if added, false if not</returns>
        public static bool AddDataConverterForTypeToCache(Type Type, Type DataConverterType)
        {
            if (Type == null || DataConverterType == null)
            {
                return false;
            }

            if (DataConverterType.IsAssignableFrom(typeof(IMDDataConverter)) 
                && !DataConverterCache.ContainsKey(Type))
            {
                IMDDataConverter converter = Activator.CreateInstance(DataConverterType) as IMDDataConverter;
                DataConverterCache.Add(Type, converter);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Adds a data converter to the cache for the given type. This converter will be returned when using 
        /// MDstatics.GetConverterForType() once it is in the cache.
        /// </summary>
        /// <param name="Type">The type to add the converter for, include generic typing</param>
        /// <param name="DataConverter">The data converter</param>
        /// <returns>True if added, false if not</returns>
        public static bool AddDataConverterForTypeToCache(Type Type, IMDDataConverter DataConverter)
        {
            if (Type == null || DataConverter == null)
            {
                return false;
            }

            if (!DataConverterCache.ContainsKey(Type))
            {
                DataConverterCache.Add(Type, DataConverter);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Create a data converter of the given type or get it from the buffer if it already exists
        /// </summary>
        /// <param name="ConverterType">The data converter type to create</param>
        /// <returns>The data convert for that type or null if it is not a valid data converter</returns>
        public static IMDDataConverter CreateConverterOfType(Type DataConverterType)
        {
            if (DataConverterType != null && DataConverterType.IsAssignableFrom(typeof(IMDDataConverter)))
            {
                if (!DataConverterTypeCache.ContainsKey(DataConverterType))
                {
                    IMDDataConverter converter = Activator.CreateInstance(DataConverterType) as IMDDataConverter;
                    if (converter.AllowCachingOfConverter())
                    {
                        DataConverterTypeCache.Add(DataConverterType, converter);
                    }
                    else
                    {
                        return converter;
                    }
                }
                return DataConverterTypeCache[DataConverterType];
            }
            return null;
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
                IMDDataConverter converter = _InternalGetConverterForType(Type);
                if (converter.AllowCachingOfConverter())
                {
                    DataConverterCache.Add(Type, converter);
                }
                else
                {
                    return converter;
                }
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
            else if (Type.GetInterface(nameof(IMDCommandReplicator)) != null)
            {
                Type constructedType = typeof(MDCommandReplicatorDataConverter<>).MakeGenericType(Type);
                return Activator.CreateInstance(constructedType) as IMDDataConverter;
            }
            else if (Type == typeof(Double) || Type == typeof(Decimal))
            {
                Type constructedType = typeof(MDSendAsStringDataConverter<>).MakeGenericType(Type);
                return Activator.CreateInstance(constructedType) as IMDDataConverter;
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

    /// <summary>
    /// Find all types in the current assembly that implements the attribute
    /// </summary>
    /// <typeparam name="T">The attribute you want to look for</typeparam>
    /// <returns>A list of types that implements the attribute or an empty list</returns>
    public static List<Type> FindAllScriptsImplementingAttribute<T>() where T : Attribute
    {
        List<Type> List = new List<Type>();
        Assembly assembly = Assembly.GetExecutingAssembly();
        foreach (Type type in assembly.GetTypes())
        {
            if (FindClassAttribute<T>(type) != null)
            {
                List.Add(type);
            }
        }
        return List;
    }

#region LOAD SCENES MATCHING COMPARATOR

    /// <summary>
    /// Find all PackedScenes in your project that matches the comparator.
    /// Example: List<PackedScene> TestList = MDStatics.LoadScenes((Node n) => n.GetType().GetInterface("IAutomaticTest") != null);
    /// This would find all scenes that implements the IAutomaticTest interface
    /// </summary>
    /// <param name="CompareFunc">A compare function used to check if we want this scene or not</param>
    /// <returns>A list of PackedScenes or an empty list if none were found</returns>
    public static List<PackedScene> LoadScenes(Func<Node, bool> CompareFunc)
    {
        List<PackedScene> List = new List<PackedScene>();
        LoadScenes("res://", List, CompareFunc);
        return List;
    }

    private static void LoadScenes(string Path, List<PackedScene> List, Func<Node, bool> CompareFunc)
    {
        Directory dir = new Directory();
        dir.Open(Path);
        dir.ListDirBegin(true, true);
        while (true)
        {
            String filePath = dir.GetNext();
            if (filePath == "")
            {
                break;
            }
            if (dir.CurrentIsDir())
            {
                // Go into all subfolder except ignore folder
                LoadScenes(Path + filePath + "/", List, CompareFunc);
            }
            else if (filePath.ToLower().EndsWith(".tscn"))
            {
                PackedScene Scene = LoadPackedScene(Path + filePath);
                Node instance = Scene.Instance();
                if (CompareFunc(instance))
                {
                    List.Add(Scene);
                }
                instance.QueueFree();
            }
        }
        dir.ListDirEnd();
    }

    private static PackedScene LoadPackedScene(String path)
    {
        String full_path = path;
        if (!ResourceLoader.Exists(full_path))
        {
            MDLog.Warn(LOG_CAT, $"Can't find: {full_path}");
        }
        return (PackedScene)ResourceLoader.Load(full_path);
    }
#endregion

    }
}