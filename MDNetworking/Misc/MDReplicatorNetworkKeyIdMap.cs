using System.Collections.Generic;

namespace MD
{
    /// <summary>
    /// A support class that maps networked nodes to ints we can send across the network
    /// </summary>
    class MDReplicatorNetworkKeyIdMap
    {
        private const string LOG_CAT = "LogReplicatorNetworkKeyIdMap";
        private Dictionary<uint, string> NetworkIDToKeyMap = new Dictionary<uint, string>();
        private Dictionary<string, uint> KeyToNetworkIdMap = new Dictionary<string, uint>();

        // First dictionary uint is the network key, the inner uint is the tick
        private Dictionary<uint, Dictionary<uint, object[]>> ClockedValueBuffer =
            new Dictionary<uint, Dictionary<uint, object[]>>();

        /// <summary>
        /// Add a new id/key pair to our map
        /// </summary>
        /// <param name="id">The ID to add</param>
        /// <param name="key">The key to add</param>
        public void AddNetworkKeyIdPair(uint id, string key)
        {
            if (NetworkIDToKeyMap.ContainsKey(id) == false)
            {
                NetworkIDToKeyMap.Add(id, key);
                KeyToNetworkIdMap.Add(key, id);
            }
            else
            {
                MDLog.Warn(LOG_CAT, $"Tried to add key {key} for id {id} but it already has key {NetworkIDToKeyMap[id]}");
            }
        }

        /// <summary>
        /// Get all uint keys we got in the map
        /// </summary>
        /// <returns>List of uints</returns>
        public IEnumerable<uint> GetKeys()
        {
            return NetworkIDToKeyMap.Keys;
        }

        /// <summary>
        /// Get the buffer for the given ID if we got one
        /// </summary>
        /// <param name="ID">The ID to get the buffer for</param>
        /// <returns>The buffer for the ID</returns>
        public Dictionary<uint, object[]> GetBufferForId(uint ID)
        {
            if (!ClockedValueBuffer.ContainsKey(ID))
            {
                ClockedValueBuffer.Add(ID, new Dictionary<uint, object[]>());
            }

            return ClockedValueBuffer[ID];
        }

        /// <summary>
        /// Add something to the buffer for the ID
        /// </summary>
        /// <param name="ID">The ID to add something for</param>
        /// <param name="Tick">The tick this happened</param>
        /// <param name="Parameters">The values to add to the buffer</param>
        public void AddToBuffer(uint ID, uint Tick, params object[] Parameters)
        {
            Dictionary<uint, object[]> buffer = GetBufferForId(ID);
            if (buffer.ContainsKey(Tick) == false)
            {
                buffer.Add(Tick, Parameters);
            }
        }

        /// <summary>
        /// Apply the buffer to a member if it exists
        /// </summary>
        /// <param name="ID">The ID to check the buffer for</param>
        /// <param name="Member">The member to apply this buffer to</param>
        public void CheckBuffer(uint ID, MDReplicatedMember Member)
        {
            if (!ClockedValueBuffer.ContainsKey(ID))
            {
                return;
            }
            
            Dictionary<uint, object[]> buffer = GetBufferForId(ID);
            foreach (uint tick in buffer.Keys)
            {
                object[] value = buffer[tick];
                MDLog.Trace(LOG_CAT, $"Updating value to {value} for {ID} on tick {tick}");
                Member.SetValues(tick, value);
            }

            buffer.Clear();
            ClockedValueBuffer.Remove(ID);
        }

        /// <summary>
        /// Remove the buffer for an ID
        /// </summary>
        /// <param name="ID">The ID to remove the buffer for</param>
        protected void RemoveBufferId(uint ID)
        {
            if (ClockedValueBuffer.ContainsKey(ID))
            {
                ClockedValueBuffer.Remove(ID);
            }
        }

        /// <summary>
        /// Get the string value for the uint id
        /// </summary>
        /// <param name="id">The uint id</param>
        /// <returns>The string key</returns>
        public string GetValue(uint id)
        {
            MDLog.CError(NetworkIDToKeyMap.ContainsKey(id) == false, LOG_CAT, $"NetworkIDToKeyMap does not contain id {id}");
            return NetworkIDToKeyMap.ContainsKey(id) ? NetworkIDToKeyMap[id] : null;
        }

        /// <summary>
        /// Get the uint id for a string key
        /// </summary>
        /// <param name="key">The key</param>
        /// <returns>The uint id</returns>
        public uint GetValue(string key)
        {
            MDLog.CError(KeyToNetworkIdMap.ContainsKey(key) == false, LOG_CAT, $"KeyToNetworkIdMap does not contain key {key}");
            return KeyToNetworkIdMap.ContainsKey(key) ? KeyToNetworkIdMap[key] : 0;
        }

        /// <summary>
        /// Get a count of how many pairs we got in the map
        /// </summary>
        /// <returns>The count</returns>
        public int GetCount()
        {
            return KeyToNetworkIdMap.Count;
        }

        /// <summary>
        /// Check if we got the string key in our map
        /// </summary>
        /// <param name="key">The string key</param>
        /// <returns>true if it exists, false if not</returns>
        public bool ContainsKey(string key)
        {
            return KeyToNetworkIdMap.ContainsKey(key);
        }

        /// <summary>
        /// Check if the uint id exists in our map
        /// </summary>
        /// <param name="id">The id to find</param>
        /// <returns>true if it exists, false if not</returns>
        public bool ContainsKey(uint id)
        {
            return NetworkIDToKeyMap.ContainsKey(id);
        }

        /// <summary>
        /// Remove the string key value from our map
        /// </summary>
        /// <param name="key">The string key</param>
        public void RemoveValue(string key)
        {
            if (KeyToNetworkIdMap.ContainsKey(key))
            {
                MDLog.Trace(LOG_CAT, $"Removing key [{key}]");
                NetworkIDToKeyMap.Remove(KeyToNetworkIdMap[key]);
                RemoveBufferId(KeyToNetworkIdMap[key]);
                KeyToNetworkIdMap.Remove(key);
            }
        }

        /// <summary>
        /// Remove the uint id from our map
        /// </summary>
        /// <param name="id">The id</param>
        public void RemoveValue(uint id)
        {
            if (NetworkIDToKeyMap.ContainsKey(id))
            {
                MDLog.Trace(LOG_CAT, $"Removing id [{id}]");
                KeyToNetworkIdMap.Remove(NetworkIDToKeyMap[id]);
                RemoveBufferId(id);
                NetworkIDToKeyMap.Remove(id);
            }
        }

        /// <summary>
        /// Remove the list of members from our map
        /// </summary>
        /// <param name="members">The member list</param>
        public void RemoveMembers(List<MDReplicatedMember> members)
        {
            foreach (MDReplicatedMember member in members)
            {
                RemoveValue(member.GetUniqueKey());
            }
        }
    }
}