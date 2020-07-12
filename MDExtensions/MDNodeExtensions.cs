using Godot;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;

namespace MD
{
/*
 * MDNodeExtensions
 *
 * Extension class to provide useful framework methods
 */
    public static class MDNodeExtensions
    {
        /// <summary>
        /// Grabs the singleton game instance, doesn't rely on being in the tree
        /// </summary>
        public static MDGameInstance GetGameInstance(this Node Instance)
        {
            return MDStatics.GetGameInstance();
        }

        /// <summary>Grabs the GameSession from the GameInstance</summary>
        public static MDGameSession GetGameSession(this Node Instance)
        {
            MDGameInstance GI = Instance.GetGameInstance();
            return GI.GameSession;
        }

        /// <summary>Grabs the Configuration from the GameInstance</summary>
        public static MDConfiguration GetConfiguration(this Node Instance)
        {
            MDGameInstance GI = Instance.GetGameInstance();
            return GI.Configuration;
        }

        /// <summary>Grabs the GameSynchronizer from the GameInstance</summary>
        public static MDGameSynchronizer GetGameSynchronizer(this Node Instance)
        {
            MDGameInstance GI = Instance.GetGameInstance();
            return GI.GameSynchronizer;
        }

        /// <summary>Grabs the GameClock from the GameInstance</summary>
        public static MDGameClock GetGameClock(this Node Instance)
        {
            MDGameInstance GI = Instance.GetGameInstance();
            return GI.GameClock;
        }

        /// <summary>Gets the ping of the given peer</summary>
        public static int GetPlayerPing(this Node Instance, int PeerId)
        {
            MDGameInstance GI = Instance.GetGameInstance();
            return GI.GameSynchronizer.GetPlayerPing(PeerId);
        }

        /// <summary>Gets the estimated OS.GetTicksMsec of the given peer</summary>
        public static uint GetPlayerTicksMsec(this Node Instance, int PeerId)
        {
            MDGameInstance GI = Instance.GetGameInstance();
            return GI.GameSynchronizer.GetPlayerTicksMsec(PeerId);
        }

        public static MDInput GetInputState(this Node Instance)
        {
            return Instance.GetGameInstance().InputState;
        }

        public static T GetPlayerInfo<T>(this Node Instance, int PeerId) where T : MDPlayerInfo
        {
            MDGameSession Session = Instance.GetGameSession();
            MDPlayerInfo PlayerInfo = Session.GetPlayerInfo(PeerId);
            return PlayerInfo as T;
        }

        /// <param name="Instance">The Node from where function is called</param>
        /// <param name="NodeType">The type of node to spawn</param>
        /// <param name="NodeName">The name of the new node</param>
        /// <param name="NetworkMaster">The peer that should own this, default is server</param>
        /// <param name="SpawnPos">Where the spawn this node</param>
        public static Node SpawnNetworkedNode(this Node Instance, Type NodeType, string NodeName,
            int NetworkMaster = -1,
            Vector3? SpawnPos = null)
        {
            MDGameSession GameSession = Instance.GetGameSession();
            return GameSession.SpawnNetworkedNode(NodeType, Instance, NodeName, true, NetworkMaster, SpawnPos);
        }

        /// <param name="Instance">The Node from where function is called</param>
        /// <param name="NodeType">The type of node to spawn</param>
        /// <param name="NodeName">The name of the new node</param>
        /// <param name="UseRandomName">If set to true a random number will be added at the end of the node name</param>
        /// <param name="NetworkMaster">The peer that should own this, default is server</param>
        /// <param name="SpawnPos">Where the spawn this node</param>
        public static Node SpawnNetworkedNode(this Node Instance, Type NodeType, string NodeName, bool UseRandomName,
            int NetworkMaster = -1, Vector3? SpawnPos = null)
        {
            MDGameSession GameSession = Instance.GetGameSession();
            return GameSession.SpawnNetworkedNode(NodeType, Instance, NodeName, UseRandomName, NetworkMaster, SpawnPos);
        }

        /// <param name="Instance">The Node from where function is called</param>
        /// <param name="Scene">The packed scene to spawn</param>
        /// <param name="NodeName">The name of the new node</param>
        /// <param name="NetworkMaster">The peer that should own this, default is server</param>
        /// <param name="SpawnPos">Where the spawn this node</param>
        public static Node SpawnNetworkedNode(this Node Instance, PackedScene Scene, string NodeName,
            int NetworkMaster = -1, Vector3? SpawnPos = null)
        {
            MDGameSession GameSession = Instance.GetGameSession();
            return GameSession.SpawnNetworkedNode(Scene, Instance, NodeName, NetworkMaster, SpawnPos);
        }

        /// <param name="Instance">The Node from where function is called</param>
        /// <param name="Scene">The packed scene to spawn</param>
        /// <param name="NodeName">The name of the new node</param>
        /// <param name="UseRandomName">If set to true a random number will be added at the end of the node name</param>
        /// <param name="NetworkMaster">The peer that should own this, default is server</param>
        /// <param name="SpawnPos">Where the spawn this node</param>
        public static Node SpawnNetworkedNode(this Node Instance, PackedScene Scene, string NodeName,
            bool UseRandomName,
            int NetworkMaster = -1, Vector3? SpawnPos = null)
        {
            MDGameSession GameSession = Instance.GetGameSession();
            return GameSession.SpawnNetworkedNode(Scene, Instance, NodeName, UseRandomName, NetworkMaster, SpawnPos);
        }

        /// <param name="Instance">The Node from where function is called</param>
        /// <param name="ScenePath">The path to the scene</param>
        /// <param name="NodeName">The name of the new node</param>
        /// <param name="NetworkMaster">The peer that should own this, default is server</param>
        /// <param name="SpawnPos">Where the spawn this node</param>
        public static Node SpawnNetworkedNode(this Node Instance, string ScenePath, string NodeName,
            int NetworkMaster = -1,
            Vector3? SpawnPos = null)
        {
            MDGameSession GameSession = Instance.GetGameSession();
            return GameSession.SpawnNetworkedNode(ScenePath, Instance, NodeName, NetworkMaster, SpawnPos);
        }

        // Shortcut for GetTree().GetRoot().AddChild()
        public static void AddNodeToRoot(this Node Instance, Node Child, bool Deferred = false)
        {
            if (Deferred)
            {
                Instance.GetTree().Root.CallDeferred("add_child", Child);
            }
            else
            {
                Instance.GetTree().Root.AddChild(Child);
            }
        }

        // Helper to mark input as handled
        public static void SetInputHandled(this Node Instance)
        {
            Instance.GetTree().SetInputAsHandled();
        }

        // Helper to register commands
        public static void RegisterCommandAttributes(this Node Instance)
        {
            MDCommands.RegisterCommandAttributes(Instance);
        }

        // Helper to unregister commands
        public static void UnregisterCommandAttributes(this Node Instance)
        {
            MDCommands.UnregisterCommandAttributes(Instance);
        }

        // Helper to register replication
        public static void RegisterReplicatedAttributes(this Node Instance)
        {
            Instance.GetGameSession().Replicator.RegisterReplication(Instance);
        }

        // Helper to unregister replication
        public static void UnregisterReplicatedAttributes(this Node Instance)
        {
            Instance.GetGameSession().Replicator.UnregisterReplication(Instance);
        }

        // Helper to populate members marked with [MDBindNode()]
        public static void PopulateBindNodes(this Node Instance)
        {
            MDBindNode.PopulateBindNodes(Instance);
        }

        // Returns true if this application can set replicated variables, call client RPCs, and broadcast RPCs
        public static bool IsServer(this Node Instance)
        {
            return Instance.GetNetMode() < MDNetMode.Client;
        }

        // Returns true if the local peer is the network master of the node or we're not networking
        public static bool IsMaster(this Node Instance)
        {
            return MDStatics.IsNetworkActive() == false || Instance.GetNetworkMaster() == MDStatics.GetPeerId();
        }

        ///<summary>Returns true if we are the client on a network</summary>
        public static bool IsClient(this Node Instance)
        {
            return MDStatics.IsClient();
        }

        // Returns the net mode of the game session
        public static MDNetMode GetNetMode(this Node Instance)
        {
            if (Instance.GetTree().HasNetworkPeer())
            {
                return Instance.GetTree().IsNetworkServer() ? MDNetMode.Server : MDNetMode.Client;
            }

            return MDNetMode.Standalone;
        }

        // Removes this node from its parent and frees it
        public static void RemoveAndFree(this Node Instance)
        {
            if (Godot.Object.IsInstanceValid(Instance))
            {
                if (Godot.Object.IsInstanceValid(Instance.GetParent()))
                {
                    Instance.GetParent().RemoveChild(Instance);
                }

                Instance.QueueFree();
            }
        }

        // Same as Rpc except it checks if the network is activate first and takes game clock into account
        public static object MDRpc(this Node Instance, string Method, params object[] Args)
        {
            if (!MDStatics.IsNetworkActive())
            {
                return null;
            }

            if (!MDStatics.IsGameClockActive())
            {
                return Instance.Rpc(Method, Args);
            }

            // Send through replicator
            MDStatics.GetReplicator().SendClockedRpc(-1, MDReliability.Reliable, Instance, Method, Args);
            return null;
        }

        // Same as RpcId except it checks if the network is activate first and takes game clock into account
        public static object MDRpcId(this Node Instance, int PeerId, string Method, params object[] Args)
        {
            if (!MDStatics.IsNetworkActive())
            {
                return null;
            }

            if (!MDStatics.IsGameClockActive())
            {
                return Instance.RpcId(PeerId, Method, Args);
            }

            // Send through replicator
            MDStatics.GetReplicator().SendClockedRpc(PeerId, MDReliability.Reliable, Instance, Method, Args);
            return null;
        }

        // Same as RpcUnreliable except it checks if the network is activate first and takes game clock into account
        public static object MDRpcUnreliable(this Node Instance, string Method, params object[] Args)
        {
            if (!MDStatics.IsNetworkActive())
            {
                return null;
            }

            if (!MDStatics.IsGameClockActive())
            {
                return Instance.RpcUnreliable(Method, Args);
            }

            // Send through replicator
            MDStatics.GetReplicator().SendClockedRpc(-1, MDReliability.Unreliable, Instance, Method, Args);
            return null;
        }

        // Same as RpcUnreliableId except it checks if the network is activate first and takes game clock into account
        public static object MDRpcUnreliableId(this Node Instance, int PeerId, string Method, params object[] Args)
        {
            if (!MDStatics.IsNetworkActive())
            {
                return null;
            }

            if (!MDStatics.IsGameClockActive())
            {
                return Instance.RpcUnreliableId(PeerId, Method, Args);
            }

            // Send through replicator
            MDStatics.GetReplicator().SendClockedRpc(PeerId, MDReliability.Unreliable, Instance, Method, Args);
            return null;
        }

        // Same as Rset except it checks if the network is activate first
        public static void MDRset(this Node Instance, string Property, object Value)
        {
            if (!MDStatics.IsNetworkActive())
            {
                return;
            }

            if (MDStatics.IsGameClockActive())
            {
                // Send through replicator
                MDStatics.GetReplicator().SendClockedRset(-1, MDReliability.Reliable, Instance, Property, Value);
                return;
            }

            Instance.Rset(Property, Value);
        }

        // Same as RsetId except it checks if the network is activate first
        public static void MDRsetId(this Node Instance, int PeerId, string Property, object Value)
        {
            if (MDStatics.IsNetworkActive())
            {
                if (MDStatics.IsGameClockActive())
                {
                    // Send through replicator
                    MDStatics.GetReplicator()
                        .SendClockedRset(PeerId, MDReliability.Reliable, Instance, Property, Value);
                    return;
                }

                Instance.RsetId(PeerId, Property, Value);
            }
        }

        // Same as RsetUnreliable except it checks if the network is activate first
        public static void MDRsetUnreliable(this Node Instance, string Property, object Value)
        {
            if (MDStatics.IsNetworkActive())
            {
                if (MDStatics.IsGameClockActive())
                {
                    // Send through replicator
                    MDStatics.GetReplicator().SendClockedRset(-1, MDReliability.Unreliable, Instance, Property, Value);
                    return;
                }

                Instance.RsetUnreliable(Property, Value);
            }
        }

        // Same as RsetUnreliable except it checks if the network is activate first
        public static void MDRsetUnreliableId(this Node Instance, int PeerId, string Property, object Value)
        {
            if (MDStatics.IsNetworkActive())
            {
                if (MDStatics.IsGameClockActive())
                {
                    // Send through replicator
                    MDStatics.GetReplicator()
                        .SendClockedRset(PeerId, MDReliability.Unreliable, Instance, Property, Value);
                    return;
                }

                Instance.RsetUnreliableId(PeerId, Property, Value);
            }
        }

        // Sends the RPC to the server only
        public static object MDServerRpc(this Node Instance, string Method, params object[] Args)
        {
            int ServerId = Instance.GetGameSession().GetNetworkMaster();
            return Instance.MDRpcId(ServerId, Method, Args);
        }

        // Sends the unreliable RPC to the server only
        public static object MDServerRpcUnreliable(this Node Instance, string Method, params object[] Args)
        {
            int ServerId = Instance.GetGameSession().GetNetworkMaster();
            return Instance.MDRpcUnreliableId(ServerId, Method, Args);
        }

        public static bool Invoke(this Node Instance, String Method, params object[] Parameters)
        {
            MethodInfo Info = MDStatics.GetMethodInfo(Instance, Method, Parameters);
            if (Info != null)
            {
                Info.Invoke(Instance, Parameters);
                return true;
            }

            return false;
        }

        public static bool SetMemberValue(this Node Instance, String Name, object Value)
        {
            MemberInfo Member = MDStatics.GetMemberInfo(Instance, Name);
            if (Member != null)
            {
                Member.SetValue(Instance, Value);
                return true;
            }

            return false;
        }

        /// <summary>Creates a timer as a child of the current node</summary>
        /// <param name="Instance">The Instance from where this function called</param>
        /// <param name="Name">The name of the timer</param>
        /// <param name="OneShot">Is this a one shot timer</param>
        /// <param name="WaitTime">Duration of the timer</param>
        /// <param name="TimerAsFirstArgument">Should we pass the timer as the first argument to the timeout method?</param>
        /// <param name="ConnectionTarget">The object to attach the timeout method to</param>
        /// <param name="MethodName">The name of the timeout method</param>
        /// <param name="Parameters">Array of parameters to pass to the timeout function</param>
        public static Timer CreateTimer(this Node Instance, String Name, bool OneShot, float WaitTime,
            bool TimerAsFirstArgument, Godot.Object ConnectionTarget, String MethodName, params object[] Parameters)
        {
            Timer timer = new Timer
            {
                Name = Name,
                OneShot = OneShot,
                WaitTime = WaitTime
            };
            List<object> parameters = new List<object>();
            if (TimerAsFirstArgument)
            {
                parameters.Add(timer);
            }

            parameters.AddRange(Parameters);

            timer.Connect("timeout", ConnectionTarget, MethodName, new Godot.Collections.Array(parameters));
            Instance.AddChild(timer);
            return timer;
        }

        public static void ChangeNetworkMaster(this Node Instance, int NewNetworkMaster)
        {
            MDGameSession GameSession = Instance.GetGameSession();
            GameSession.ChangeNetworkMaster(Instance, NewNetworkMaster);

        }
    }
}