using Godot;
using System;
using System.Text;
using TypeToMDTypeDict = System.Collections.Generic.Dictionary<System.Type, byte>;
using MDTypeToTypeDict = System.Collections.Generic.Dictionary<byte, System.Type>;
using TypeToSizeDict = System.Collections.Generic.Dictionary<System.Type, int>;
using ToByteConverterDict = System.Collections.Generic.Dictionary<System.Type, System.Func<object, byte[]>>;
using FromByteConverterDict = System.Collections.Generic.Dictionary<System.Type, System.Func<byte[], object>>;
using ByteArrayList = System.Collections.Generic.List<byte[]>;

// Types of data supported for replication, used to determine type size when [de]serializing packets
public static class MDSerialization
{
    private const string LOG_CAT = "LogMDSerialization";

    public const byte Type_String = 0;
    public const byte Type_Bool = 1;
    public const byte Type_Byte = 2;
    public const byte Type_Char = 3;
    public const byte Type_Float = 4;
    public const byte Type_Long = 5;
    public const byte Type_Ulong = 6;
    public const byte Type_Int = 7;
    public const byte Type_Uint = 8;
    public const byte Type_Short = 9;
    public const byte Type_Ushort = 10;
    public const byte Type_Node = 11;

    public const byte Type_Invalid = 255;

    // Converts a Type to an MDType byte for serialization
    public static byte GetMDTypeFromType(Type InType)
    {
        if (InType.IsSubclassOf(typeof(Node)))
        {
            return Type_Node;
        }

        if (InType.IsEnum)
        {
            return Type_Int;
        }

        if (TypeToMDType.ContainsKey(InType))
        {
            return TypeToMDType[InType];
        }

        return Type_Invalid;
    }

     // Converts an MDType int to a Type for deserialization
    public static Type GetTypeFromMDType(byte MDType)
    {
        if (MDTypeToType.ContainsKey(MDType))
        {
            return MDTypeToType[MDType];
        }

        return null;
    }

    // Convert a supported type to byte array, prefixing with the name and serialized type int
    public static byte[] SerializeSupportedTypeToBytes(string DataName, object Data)
    {
        /*
            Data Format:
            [Length of Name] int
            [Name data] string
            [Type data] byte 
            [Num Data] int - This is only here for variable sized types (eg. string)
            [Value data] bytes
         */
        Type DataType = Data.GetType();
        byte MDType = GetMDTypeFromType(DataType);
        if (MDType == Type_Invalid)
        {
            MDLog.Error(LOG_CAT, "Attempting to serialize unsupported type {0}", DataType.Name);
            return null;
        }
        
        byte[] NameLengthBytes = ConvertSupportedTypeToBytes(Encoding.Unicode.GetByteCount(DataName));
        byte[] NameBytes = ConvertSupportedTypeToBytes(DataName);
        byte[] TypeBytes = ConvertSupportedTypeToBytes(MDType);
        byte[] DataBytes = ConvertSupportedTypeToBytes(Data);
        if (DoesTypeRequireTrackingCount(DataType))
        {
            byte[] CountBytes = ConvertSupportedTypeToBytes(NumValuesToSerialize(Data, MDType));
            return MDStatics.JoinByteArrays(NameLengthBytes, NameBytes, TypeBytes, CountBytes, DataBytes);
        }
        else
        {
            return MDStatics.JoinByteArrays(NameLengthBytes, NameBytes, TypeBytes, DataBytes);
        }
    }
    
    // Convert a supported type to byte array
    public static byte[] ConvertSupportedTypeToBytes(object Data)
    {
        Type DataType = Data.GetType();
        if (DataType.IsSubclassOf(typeof(Node)))
        {
            DataType = typeof(Node);
        }
        else if (DataType.IsEnum)
        {
            DataType = typeof(int);
        }

        Func<object, byte[]> converter;
        if (!ToBytes.TryGetValue(DataType, out converter))
        {
            MDLog.Error(LOG_CAT, "Attempting to serialize unsupported type [{0}] for packet", DataType.Name);
            return MDStatics.EmptyByteArray;
        }

        return converter(Data);
    }

    // Converts a string to [length of string][string data]
    public static byte[] ConvertSupportedTypeToBytes(string String)
    {
        byte[] StringLengthBytes = ConvertSupportedTypeToBytes(Encoding.Unicode.GetByteCount(String));
        byte[] StringBytes = ConvertSupportedTypeToBytes((object)String);
        return MDStatics.JoinByteArrays(StringLengthBytes, StringBytes);
    }

    // Converts an array of support objects
    public static byte[] ConvertObjectsToBytes(params object[] args)
    {
        ByteArrayList ByteList = new ByteArrayList();
        foreach (object obj in args)
        {
            if (obj.GetType() == typeof(string))
            {
                ByteList.Add(ConvertSupportedTypeToBytes((string)obj));
            }
            else
            {
                ByteList.Add(ConvertSupportedTypeToBytes(obj));
            }
        }

        return MDStatics.JoinByteArrays(ByteList.ToArray());
    }

    // Convert the first 4 bytes of a byte array to an int
    public static int GetIntFromStartOfByteArray(byte[] Data)
    {
        Func<byte[], object> converter;
        FromBytes.TryGetValue(typeof(int), out converter);

        return (int)converter(Data);
    }

    // Convert a byte array to the supported data type stored in the bytes (Format: [MDType as byte][Optional Num][Data])
    public static object ConvertBytesToSupportedType(byte[] Data)
    {
        byte MDType = Data[0];
        return ConvertBytesToSupportedType(MDType, Data.SubArray(1));
    }

    // Convert a byte array to the supported data type
    public static object ConvertBytesToSupportedType(byte MDType, byte[] Data)
    {
        Type SupportedType = GetTypeFromMDType(MDType);
        return ConvertBytesToSupportedType(SupportedType, Data);
    }

    // Convert a byte array to the supported data type
    public static object ConvertBytesToSupportedType(Type SupportedType, byte[] Data)
    {
        Func<byte[], object> converter;
        if (FromBytes.TryGetValue(SupportedType, out converter))
        {
            return converter(Data);
        }

        MDLog.Error(LOG_CAT, "Attempting the deserialize unsupport type [{0}]", SupportedType.Name);
        return null;
    }

    // Convert a byte array to the supported data type
    public static T ConvertBytesToSupportedType<T>(byte[] Data)
    {
        return (T)ConvertBytesToSupportedType(typeof(T), Data);
    }

    // Returns true if the type requires storing the number of values (eg. a list is unbound so we need to know how many values are serialized)
    public static bool DoesTypeRequireTrackingCount(byte MDType)
    {
        return MDType == Type_String; // TODO - List/Dict
    }

    // Returns true if the type requires storing the number of values (eg. a list is unbound so we need to know how many values are serialized)
    public static bool DoesTypeRequireTrackingCount(Type SupportedType)
    {
        byte MDType = GetMDTypeFromType(SupportedType);
        return DoesTypeRequireTrackingCount(MDType);
    }

    // Counts the number of values to store for serialization
    public static int NumValuesToSerialize(object Data, byte MDType)
    {
        if (MDType == Type_String)
        {
            return Encoding.Unicode.GetByteCount((string)Data);
        }

        return 0;
    }

    // Get the size in bytes of a primitive type
    public static int GetSizeInBytes(Type InType)
    {
        if (TypeSize.ContainsKey(InType))
        {
            return TypeSize[InType];
        }

        return 0;
    }

    // Get the size in bytes of a primitive type
    public static int GetSizeInBytes(byte MDType)
    {
        Type LookupType = GetTypeFromMDType(MDType);
        return GetSizeInBytes(LookupType);
    }

    private static readonly TypeToMDTypeDict TypeToMDType = new TypeToMDTypeDict()
    {
        { typeof(string),   Type_String },
        { typeof(bool),     Type_Bool },
        { typeof(byte),     Type_Byte },
        { typeof(char),     Type_Char },
        { typeof(float),    Type_Float },
        { typeof(long),     Type_Long },
        { typeof(ulong),    Type_Ulong },
        { typeof(int),      Type_Int },
        { typeof(uint),     Type_Uint },
        { typeof(short),    Type_Short },
        { typeof(ushort),   Type_Ushort },
        { typeof(Node),     Type_Node },
    };

    private static readonly MDTypeToTypeDict MDTypeToType = new MDTypeToTypeDict()
    {
        { Type_String,   typeof(string) },
        { Type_Bool,     typeof(bool) },
        { Type_Byte,     typeof(byte) },
        { Type_Char,     typeof(char) },
        { Type_Float,    typeof(float) },
        { Type_Long,     typeof(long) },
        { Type_Ulong,    typeof(ulong) },
        { Type_Int,      typeof(int) },
        { Type_Uint,     typeof(uint) },
        { Type_Short,    typeof(short) },
        { Type_Ushort,   typeof(ushort) },
        { Type_Node,     typeof(Node) },
    };

    private static readonly ToByteConverterDict ToBytes = new ToByteConverterDict()
    {
        { typeof(string),   o => Encoding.Unicode.GetBytes((string) o) },
        { typeof(bool),     o => BitConverter.GetBytes((bool) o) },
        { typeof(byte),     o => new byte[1]{(byte)o} },
        { typeof(char),     o => BitConverter.GetBytes((char) o) },
        { typeof(float),    o => BitConverter.GetBytes((float) o) },
        { typeof(long),     o => BitConverter.GetBytes((long) o) },
        { typeof(ulong),    o => BitConverter.GetBytes((ulong) o) },
        { typeof(int),      o => BitConverter.GetBytes((int) o) },
        { typeof(uint),     o => BitConverter.GetBytes((uint) o) },
        { typeof(short),    o => BitConverter.GetBytes((short) o) },
        { typeof(ushort),   o => BitConverter.GetBytes((ushort) o) },
        { typeof(Node),     o => MDStatics.EmptyByteArray }, // Node serialization is handled in MDReplcator
    };

    private static readonly FromByteConverterDict FromBytes = new FromByteConverterDict()
    {
        { typeof(string),   bytes => Encoding.Unicode.GetString(bytes) },
        { typeof(bool),     bytes => BitConverter.ToBoolean(bytes, 0) },
        { typeof(byte),     bytes => bytes[0] },
        { typeof(char),     bytes => BitConverter.ToChar(bytes, 0) },
        { typeof(float),    bytes => BitConverter.ToSingle(bytes, 0) },
        { typeof(long),     bytes => BitConverter.ToInt64(bytes, 0) },
        { typeof(ulong),    bytes => BitConverter.ToUInt64(bytes, 0) },
        { typeof(int),      bytes => BitConverter.ToInt32(bytes, 0) },
        { typeof(uint),     bytes => BitConverter.ToUInt32(bytes, 0) },
        { typeof(short),    bytes => BitConverter.ToInt16(bytes, 0) },
        { typeof(ushort),   bytes => BitConverter.ToUInt16(bytes, 0) },
        { typeof(Node),     bytes => null }, // Node deserialization is handled in MDReplcator
    };

    private static readonly TypeToSizeDict TypeSize = new TypeToSizeDict()
    {
        { typeof(bool),     1 },
        { typeof(byte),     1 },
        { typeof(char),     2 },
        { typeof(float),    4 },
        { typeof(long),     8 },
        { typeof(ulong),    8 },
        { typeof(int),      4 },
        { typeof(uint),     4 },
        { typeof(short),    2 },
        { typeof(ushort),   2 },
    };
} 