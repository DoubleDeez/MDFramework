using System.Collections.Generic;

namespace MD
{
    class MDReplicatorNetworkKeyIdMap
    {
        private Dictionary<uint, string> NetworkIDToKeyMap = new Dictionary<uint, string>();
        private Dictionary<string, uint> KeyToNetworkIdMap = new Dictionary<string, uint>();

        // First dictionary uint is the network key, the inner uint is the tick
        private Dictionary<uint, Dictionary<uint, object>> ClockedValueBuffer =
            new Dictionary<uint, Dictionary<uint, object>>();

        public void AddNetworkKeyIdPair(uint id, string key)
        {
            NetworkIDToKeyMap.Add(id, key);
            KeyToNetworkIdMap.Add(key, id);
        }

        public IEnumerable<uint> GetKeys()
        {
            return NetworkIDToKeyMap.Keys;
        }

        public Dictionary<uint, object> GetBufferForId(uint ID)
        {
            if (!ClockedValueBuffer.ContainsKey(ID))
            {
                ClockedValueBuffer.Add(ID, new Dictionary<uint, object>());
            }

            return ClockedValueBuffer[ID];
        }

        public void AddToBuffer(uint ID, uint Tick, object Value)
        {
            Dictionary<uint, object> buffer = GetBufferForId(ID);
            buffer.Add(Tick, Value);
        }

        public void AddToBuffer(uint ID, uint Tick, params object[] Parameters)
        {
            Dictionary<uint, object> buffer = GetBufferForId(ID);
            buffer.Add(Tick, Parameters);
        }

        public void CheckBuffer(uint ID, MDReplicatedMember Member)
        {
            if (!ClockedValueBuffer.ContainsKey(ID))
            {
                return;
            }
            Dictionary<uint, object> buffer = GetBufferForId(ID);
            foreach (uint tick in buffer.Keys)
            {
                object value = buffer[tick];
                if (value.GetType().IsArray)
                {
                    Member.SetValues(tick, value);
                }
                else
                {
                    Member.SetValue(tick, value);
                }
            }

            buffer.Clear();
            ClockedValueBuffer.Remove(ID);
        }

        protected void RemoveBufferId(uint ID)
        {
            if (ClockedValueBuffer.ContainsKey(ID))
            {
                ClockedValueBuffer.Remove(ID);
            }
        }

        public string GetValue(uint id)
        {
            return NetworkIDToKeyMap.ContainsKey(id) ? NetworkIDToKeyMap[id] : null;
        }

        public uint GetValue(string key)
        {
            return KeyToNetworkIdMap.ContainsKey(key) ? KeyToNetworkIdMap[key] : 0;
        }

        public int GetCount()
        {
            return KeyToNetworkIdMap.Count;
        }

        public bool ContainsKey(string key)
        {
            return KeyToNetworkIdMap.ContainsKey(key);
        }

        public bool ContainsKey(uint id)
        {
            return NetworkIDToKeyMap.ContainsKey(id);
        }

        public void RemoveValue(string key)
        {
            if (KeyToNetworkIdMap.ContainsKey(key))
            {
                NetworkIDToKeyMap.Remove(KeyToNetworkIdMap[key]);
                RemoveBufferId(KeyToNetworkIdMap[key]);
                KeyToNetworkIdMap.Remove(key);
            }
        }

        public void RemoveValue(uint id)
        {
            if (NetworkIDToKeyMap.ContainsKey(id))
            {
                KeyToNetworkIdMap.Remove(NetworkIDToKeyMap[id]);
                RemoveBufferId(id);
                NetworkIDToKeyMap.Remove(id);
            }
        }

        public void RemoveMembers(List<MDReplicatedMember> members)
        {
            foreach (MDReplicatedMember member in members)
            {
                RemoveValue(member.GetUniqueKey());
            }
        }
    }
}