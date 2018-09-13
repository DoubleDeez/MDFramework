using Godot;
using System;
using System.Text;
using TypeToMDTypeDict = System.Collections.Generic.Dictionary<System.Type, int>;
using MDTypeToTypeDict = System.Collections.Generic.Dictionary<int, System.Type>;
using ToByteConverterDict = System.Collections.Generic.Dictionary<System.Type, System.Func<object, byte[]>>;
using FromByteConverterDict = System.Collections.Generic.Dictionary<System.Type, System.Func<byte[], object>>;

// Types of data supported for replication, used to determine type size when [de]serializing packets
public static class MDSerialization
{
    private const string LOG_CAT = "LogMDSerialization";

    public const int TypeString = 0;
    public const int TypeBool = 1;
    public const int TypeByte = 2;
    public const int TypeChar = 3;
    public const int TypeFloat = 4;
    public const int TypeLong = 5;
    public const int TypeUlong = 6;
    public const int TypeInt = 7;
    public const int TypeUint = 8;
    public const int TypeShort = 9;
    public const int TypeUshort = 10;
    public const int TypeNode = 11;

    // Converts a Type to an MDType int for serialization
    public static int GetMDTypeFromType(Type InType)
    {
        if (InType.IsSubclassOf(typeof(Node)))
        {
            return TypeNode;
        }

        if (TypeToMDType.ContainsKey(InType))
        {
            return TypeToMDType[InType];
        }

        return -1;
    }

     // Converts an MDType int to a Type for deserialization
    public static Type GetTypeFromMDType(int MDType)
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
            Field Data Format:
            [Length of Name] int
            [Name data] string
            [Type data] int 
            [Num Data] int - This is only here for variable sized types (eg. string)
            [Value data] bytes
         */
        Type DataType = Data.GetType();
        int MDType = GetMDTypeFromType(DataType);
        if (MDType == -1)
        {
            MDLog.Error(LOG_CAT, "Attempting to serialize unsupported type {0}", DataType.Name);
            return null;
        }
        
        byte[] NameLengthBytes = ConvertSupportedTypeToBytes(Encoding.UTF8.GetByteCount(DataName));
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

        Func<object, byte[]> converter;
        if (!ToBytes.TryGetValue(DataType, out converter))
        {
            MDLog.Error(LOG_CAT, "Attempting to serialize unsupport type [{0}] for packet", Data.GetType().Name);
            return null;
        }

        return converter(Data);
    }

    // Convert a the first 4 bytes of a byte array to an int
    public static int GetIntFromStartOfByteArray(byte[] Data)
    {
        Func<byte[], object> converter;
        FromBytes.TryGetValue(typeof(int), out converter);

        return (int)converter(Data);
    }

    // Convert a byte array to the supported data type stored in the bytes (Format: [MDType as int][Optional Num][Data])
    public static object ConvertBytesToSupportedType(byte[] Data)
    {
        int MDType = GetIntFromStartOfByteArray(Data);
        return ConvertBytesToSupportedType(MDType, Data.SubArray(4));
    }

    // Convert a byte array to the supported data type
    public static object ConvertBytesToSupportedType(int MDType, byte[] Data)
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

    // Returns true if the type requires storing the number of values (eg. a list is unbound so we need to know how many values are serialized)
    public static bool DoesTypeRequireTrackingCount(int MDType)
    {
        return MDType == TypeString; // TODO - List/Dict
    }

    // Counts the number of values to store for serialization
    public static int NumValuesToSerialize(object Data, int MDType)
    {
        if (MDType == TypeString)
        {
            return Encoding.UTF8.GetByteCount((string)Data);
        }

        return 0;
    }

    public static bool DoesTypeRequireTrackingCount(Type SupportedType)
    {
        int MDType = GetMDTypeFromType(SupportedType);
        return DoesTypeRequireTrackingCount(MDType);
    }

    private static readonly TypeToMDTypeDict TypeToMDType = new TypeToMDTypeDict()
    {
        { typeof(string),   TypeString },
        { typeof(bool),     TypeBool },
        { typeof(byte),     TypeByte },
        { typeof(char),     TypeChar },
        { typeof(float),    TypeFloat },
        { typeof(long),     TypeLong },
        { typeof(ulong),    TypeUlong },
        { typeof(int),      TypeInt },
        { typeof(uint),     TypeUint },
        { typeof(short),    TypeShort },
        { typeof(ushort),   TypeUshort },
        { typeof(Node),     TypeNode },
    };

    private static readonly MDTypeToTypeDict MDTypeToType = new MDTypeToTypeDict()
    {
        { TypeString,   typeof(string) },
        { TypeBool,     typeof(bool) },
        { TypeByte,     typeof(byte) },
        { TypeChar,     typeof(char) },
        { TypeFloat,    typeof(float) },
        { TypeLong,     typeof(long) },
        { TypeUlong,    typeof(ulong) },
        { TypeInt,      typeof(int) },
        { TypeUint,     typeof(uint) },
        { TypeShort,    typeof(short) },
        { TypeUshort,   typeof(ushort) },
        { TypeNode,     typeof(Node) },
    };

    private static readonly ToByteConverterDict ToBytes = new ToByteConverterDict()
    {
        { typeof(string),   o => Encoding.UTF8.GetBytes((string) o) },
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
        { typeof(Node),     o => MDStatics.EmptyByteArray },
    };

    private static readonly FromByteConverterDict FromBytes = new FromByteConverterDict()
    {
        { typeof(string),   bytes => Encoding.UTF8.GetString(bytes) },
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
        { typeof(Node),     bytes => null },
    };
} 