using Godot;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace MD
{
    public class MDDisposableList<T> : List<T>, IDisposable
    {
        protected MDList<T> MDListRef;

        public MDDisposableList(IEnumerable<T> collection, MDList<T> ListRef) : base(collection)
        {
            MDListRef = ListRef;
        }

        public void Dispose()
        {
            MDListRef.DoFullResynch(this);
        }
    }
    public static class MDList
    {
        // This class is just here for the enum since you can't reference enums / statics inside a generic class.
        public enum Settings
        {
            COMPARATOR,
            UNSAFE_MODE
        }
    }
    public class MDList<T> : IMDCommandReplicator
    {
        public enum MDListActions
        {
            UNKOWN,
            SET_CURRENT_COMMAND_ID,
            MODIFICATION,
            ADD,
            INSERT,
            REMOVE_AT,
            REMOVE_RANGE,
            REVERSE_INDEX,
            REVERSE,
            CLEAR,
            SORT,
            SORT_INDEX,
            SORT_COMPARATOR,
            RESYNCH_START
        }

        protected const String LOG_CAT = "LogMDList";

        protected class ListCommandRecord
        {
            public ListCommandRecord(int Number, MDListActions Type, object[] Parameters)
            {
                this.CommandNumber = Number;
                this.Type = Type;
                this.Parameters = Parameters;
            }

            public int CommandNumber = 0;
            public MDListActions Type = MDListActions.UNKOWN;
            public object[] Parameters;
        }
        
        protected List<T> RealList = new List<T>();

        protected Queue<ListCommandRecord> CommandHistory = new Queue<ListCommandRecord>();

        protected List<ListCommandRecord> CommandQueue = new List<ListCommandRecord>();

        protected List<IComparer<T>> Comparators = new List<IComparer<T>>();

        protected int CommandCounter = 0;

        protected IMDDataConverter DataConverter;

        protected MDReplicator Replicator;

        protected bool UnsafeMode = false;

        protected bool FullResynch = false;

        protected bool CompleteMode = false;

        public MDList()
        {
            MDLog.AddLogCategoryProperties(LOG_CAT, new MDLogProperties(MDLogLevel.Info));
            this.Replicator = MDStatics.GetReplicator();
        }

    #region PUBLIC METHODS

        public List<object[]> MDGetCommandsForNewPlayer()
        {
            int CurrentAction = 0;
            ListCommandRecord record = null;
            List<object[]> Commands = new List<object[]>();

            // Enable complete replication mode
            CompleteMode = true;

            // Add all items to the command list
            foreach (T item in RealList)
            {
                record = new ListCommandRecord(CurrentAction, MDListActions.ADD, new object[] {item});
                Commands.Add(AsObjectArray(record));
                CurrentAction++;
            }

            CompleteMode = false;

            // Add current command we are at
            record = new ListCommandRecord(CurrentAction, MDListActions.SET_CURRENT_COMMAND_ID, new object[] {CommandCounter});
            Commands.Add(AsObjectArray(record));

            return Commands;
        }

        ///<summary>Do not call this! This should only be used by the MDReplicator</summary>
        public void MDSetSettings(MDReplicatedSetting[] Settings)
        {
            SetConverter(Settings);
            LoadSettings(Settings);
        }

        ///<summary>Do not call this! This should only be used by the MDReplicator</summary>
        public List<object[]> MDGetCommands()
        {
            List<object[]> Commands = new List<object[]>();

            // Prepare for full resynch
            if (FullResynch)
            {
                RecordAction(MDListActions.RESYNCH_START);
            }

            // For now not much error handling
            while (CommandHistory.Count > 0)
            {
                Commands.Add(AsObjectArray(CommandHistory.Dequeue()));
            }

            // Do full resynch
            if (FullResynch)
            {
                FullResynch = false;
                Commands.AddRange(MDGetCommandsForNewPlayer());
            }

            return Commands;
        }

        ///<summary>Do not call this! This should only be used by the MDReplicator</summary>
        public void MDProcessCommand(params object[] Params)
        {
            // Parse input
            int CmdNumber = Convert.ToInt32(Params[0]);
            MDListActions Type = (MDListActions)Enum.Parse(typeof(MDListActions), Params[1].ToString());
            object[] Parameters = Params.SubArray(2);

            MDLog.Trace(LOG_CAT, $"Recieved command [{CmdNumber.ToString()}] {Type.ToString()}");
            foreach(object obj in Parameters)
            {
                MDLog.Trace(LOG_CAT, $"Parameter: {obj.ToString()}");
            }

            if (CmdNumber > CommandCounter)
            {
                CommandQueue.Add(new ListCommandRecord(CmdNumber, Type, Parameters));
                return;
            }
            else if (CmdNumber < CommandCounter)
            {
                // This should not happen
                MDLog.Error(LOG_CAT, $"Recieved a command with number {CmdNumber} when our internal CommandCounter is {CommandCounter}");
                return;
            }

            // Increase command counter
            CommandCounter++;

            // Process incoming command
            switch (Type)
            {
                case MDListActions.SET_CURRENT_COMMAND_ID:
                    CommandCounter = Convert.ToInt32(Parameters[0]);
                    break;
                case MDListActions.MODIFICATION:
                    int index = Convert.ToInt32(Parameters[0]);
                    RealList[index] = ConvertFromObject(RealList[index], Parameters.SubArray(1));
                    break;
                case MDListActions.ADD:
                    RealList.Add(ConvertFromObject(null, Parameters));
                    break;
                case MDListActions.INSERT:
                    RealList.Insert(Convert.ToInt32(Parameters[0]), ConvertFromObject(null, Parameters.SubArray(1)));
                    break;
                case MDListActions.REMOVE_AT:
                    RealList.RemoveAt(Convert.ToInt32(Parameters[0]));
                    break;
                case MDListActions.REMOVE_RANGE:
                    RealList.RemoveRange(Convert.ToInt32(Parameters[0]), Convert.ToInt32(Parameters[1]));
                    break;
                case MDListActions.REVERSE_INDEX:
                    RealList.Reverse(Convert.ToInt32(Parameters[0]), Convert.ToInt32(Parameters[1]));
                    break;
                case MDListActions.REVERSE:
                    RealList.Reverse();
                    break;
                case MDListActions.CLEAR:
                    RealList.Clear();
                    break;
                case MDListActions.SORT:
                    RealList.Sort();
                    break;
                case MDListActions.SORT_COMPARATOR:
                    RealList.Sort(GetComparatorByIndex(Convert.ToInt32(Parameters[0])));
                    break;
                case MDListActions.SORT_INDEX:
                    RealList.Sort(Convert.ToInt32(Parameters[0]), Convert.ToInt32(Parameters[1]), 
                                    GetComparatorByIndex(Convert.ToInt32(Parameters[2])));
                    break;
                case MDListActions.RESYNCH_START:
                    RealList.Clear();
                    CommandCounter = 0;
                    break;

            }

            // Check if next command is queued
            ListCommandRecord NextCommand = GetCurrentCommandFromQueue();
            if (NextCommand != null)
            {
                MDProcessCommand(NextCommand.CommandNumber, NextCommand.Type, NextCommand.Parameters);
            }
        }

    #endregion

    #region PROTECTED METHODS

        protected IComparer<T> GetComparatorByIndex(int Index)
        {
            return Comparators[Index];
        }

        protected IComparer<T> GetComparatorByType(Type ComparatorType)
        {
            foreach (IComparer<T> comparator in Comparators)
            {
                if (comparator.GetType() == ComparatorType)
                {
                    return comparator;
                }
            }
            return null;
        }

        protected int GetComparatorIndexFromType(Type ComparatorType)
        {
            IComparer<T> comparator = GetComparatorByType(ComparatorType);
            if (comparator != null)
            {
                return Comparators.IndexOf(comparator);
            }

            return -1;
        }

        protected void LoadSettings(MDReplicatedSetting[] Settings)
        {
            MDReplicatedSetting[] ComparatorList = MDReplicator.ParseParameters(typeof(MDList.Settings), Settings);
            foreach (MDReplicatedSetting setting in ComparatorList)
            {
                switch ((MDList.Settings)setting.Key)
                {
                    case MDList.Settings.COMPARATOR:
                        if (!(setting.Value is Type))
                        {
                            MDLog.Warn(LOG_CAT, $"{setting.Value.ToString()} is not a type, use typeof(class) as value when using MDReplicatedSetting of type MDComparators");
                            continue;
                        }
                        Type settingType = (Type)setting.Value;
                        IComparer<T> comparer = Activator.CreateInstance(settingType) as IComparer<T>;
                        if (comparer != null)
                        {
                            AddComparer(comparer);
                        }
                        else
                        {
                            MDLog.Error(LOG_CAT, $"{settingType.ToString()} is not an IComparer<T>.");
                        }
                        break;
                    case MDList.Settings.UNSAFE_MODE:
                        if (setting.Value.GetType() == typeof(Boolean))
                        {
                            UnsafeMode = (bool)setting.Value;
                        }
                        else
                        {
                            UnsafeMode = Boolean.Parse(setting.Value.ToString());
                        }
                        break;
                }
            }
        }

        protected void AddComparer(IComparer<T> Comparator)
        {
            IComparer<T> FoundComparator = GetComparatorByType(Comparator.GetType());
            if (FoundComparator != null)
            {
                MDLog.Warn(LOG_CAT, $"Comparator with key {Comparator.GetType().ToString()} has already been registered");
                return;
            }

            Comparators.Add(Comparator);
        }

        protected void SetConverter(MDReplicatedSetting[] Settings)
        {
            Type DataConverterType = GetConverterType(MDReplicator.ParseParameters(typeof(MDReplicatedCommandReplicator.Settings), Settings));
            if (DataConverterType != null && DataConverterType.IsAssignableFrom(typeof(IMDDataConverter)))
            {
                DataConverter = Activator.CreateInstance(DataConverterType) as IMDDataConverter;
                return;
            }
            
            if (typeof(T).GetInterface(nameof(IMDDataConverter)) != null)
            {
                DataConverter = Activator.CreateInstance(typeof(T)) as IMDDataConverter;
            }
            else
            {
                // Set our default converter
                DataConverter = new MDObjectDataConverter();
            }
        }

        protected Type GetConverterType(MDReplicatedSetting[] Settings)
        {
            foreach (MDReplicatedSetting setting in Settings)
            {
                if ((MDReplicatedCommandReplicator.Settings)setting.Key == MDReplicatedCommandReplicator.Settings.Converter)
                {
                    if (setting.Value == null)
                    {
                        return null;
                    }
                    return Type.GetType(setting.Value.ToString());
                }
            }
            return null;
        }

        protected object[] AsObjectArray(ListCommandRecord ActionRecord)
        {
            List<object> ObjectList = new List<object>();
            ObjectList.Add(ActionRecord.CommandNumber);
            ObjectList.Add((int)ActionRecord.Type);
            ObjectList.AddRange(ParseParameters(ActionRecord));
            return ObjectList.ToArray();
        }

        protected object[] ParseParameters(ListCommandRecord ActionRecord)
        {
            // Any ListAction that has a T object in it needs to be converted, for anything else just pass along the parameters
            switch (ActionRecord.Type)
            {
                case MDListActions.ADD:
                    return ConvertToObject(ActionRecord.Parameters[0]);
                case MDListActions.MODIFICATION:
                case MDListActions.INSERT:
                    List<object> ObjectList = new List<object>();
                    ObjectList.Add(ActionRecord.Parameters[0]);
                    ObjectList.AddRange(ConvertToObject(ActionRecord.Parameters[1]));
                    return ObjectList.ToArray();
                default:
                    return ActionRecord.Parameters;
            }
        }

        // Just for convenience
        protected object[] ConvertToObject(object Item)
        {
            return DataConverter.ConvertForSending(Item, CompleteMode);
        }
        
        // Just for convenience
        protected T ConvertFromObject(object CurrentObject, object[] Parameters)
        {
            return (T)DataConverter.CovertBackToObject(CurrentObject, Parameters);
        }

        // Get the current command from the queue and remove it if it exists
        protected ListCommandRecord GetCurrentCommandFromQueue()
        {
            ListCommandRecord ActionRecord = null;
            foreach (ListCommandRecord record in CommandQueue)
            {
                if (record.CommandNumber == CommandCounter)
                {
                    ActionRecord = record;
                    break;
                }
            }

            if (ActionRecord != null)
            {
                CommandQueue.Remove(ActionRecord);
            }

            return ActionRecord;
        }

        protected void RecordAction(MDListActions Type, params object[] Parameters)
        {
            CommandHistory.Enqueue(new ListCommandRecord(GetActionNumber(), Type, Parameters));
        }

        protected int GetActionNumber()
        {
            CommandCounter++;
            return CommandCounter-1;
        }

        protected void CheckIfUnsafeMode()
        {
            if (!UnsafeMode)
            {
                String message = "Attempted to access MDList.GetRawList() without configuring first, please read the wiki!";
                MDLog.Error(LOG_CAT, message);
                throw new AccessViolationException(message);
            }
        }

    #endregion

    #region MODIFYING METHODS
        public T this[int index] 
        { 
            get
            {
                return RealList[index];
            } 
            set
            {
                RecordAction(MDListActions.MODIFICATION, index, value);
                RealList[index] = value;
            } 
        }

        public void Add(T item)
        {
            RecordAction(MDListActions.ADD, item);
            RealList.Add(item);
        }
        
        public void AddRange(IEnumerable<T> collection)
        {
            foreach (T item in collection)
            {
                Add(item);
            }
        }

        public void Clear()
        {
            RecordAction(MDListActions.CLEAR);
            RealList.Clear();
        }

        public void Insert(int index, T item)
        {
            RecordAction(MDListActions.INSERT, index, item);
            RealList.Insert(index, item);
        }
        
        public void InsertRange(int index, IEnumerable<T> collection)
        {
            // Simple way of reversing order
            int indexOffset = 0;
            foreach (T item in collection)
            {
                Insert(index + indexOffset, item);
                indexOffset++;
            }
        }

        public bool Remove(T item)
        {
            // We always remove by index internally since this is easier to pass across the network
            int index = RealList.FindIndex((T x) => x.Equals(item));
            if (index >= 0)
            {
                RemoveAt(index);
                return true;
            }

            return false;
        }
        
        public int RemoveAll(Predicate<T> match)
        {
            List<T> items = RealList.FindAll(match);
            items.ForEach(item => Remove(item));
            return items.Count;
        }
        
        public void RemoveAt(int index)
        {
            RealList.RemoveAt(index);
            RecordAction(MDListActions.REMOVE_AT, index);
        }
        
        public void RemoveRange(int index, int count)
        {
            RealList.RemoveRange(index, count);
            RecordAction(MDListActions.REMOVE_RANGE, index, count);
        }
        
        public void Reverse(int index, int count)
        {
            RealList.Reverse(index, count);
            RecordAction(MDListActions.REVERSE_INDEX, index, count);
        }
        
        public void Reverse()
        {
            RealList.Reverse();
            RecordAction(MDListActions.REVERSE);
        }

        public void Sort()
        {
            RealList.Sort();
            RecordAction(MDListActions.SORT);
        }

        public void Sort(int index, int count, Type ComparatorType)
        {
            IComparer<T> Comparer = GetComparatorByType(ComparatorType);
            if (Comparer == null)
            {
                MDLog.Error(LOG_CAT, $"Attempted to use comparator [{ComparatorType.ToString()}] that is not registered for the MDList");
                return;
            }

            RealList.Sort(index, count, Comparer);
            RecordAction(MDListActions.SORT_INDEX, index, count, GetComparatorIndexFromType(ComparatorType));
        }
        
        public void Sort(Type ComparatorType)
        {
            IComparer<T> Comparer = GetComparatorByType(ComparatorType);
            if (Comparer == null)
            {
                MDLog.Error(LOG_CAT, $"Attempted to use comparator [{ComparatorType.ToString()}] that is not registered for the MDList");
                return;
            }

            RealList.Sort(Comparer);
            RecordAction(MDListActions.SORT_COMPARATOR, GetComparatorIndexFromType(ComparatorType));
        }

        /// <summary>
        /// Get a copy of the list that you can modify as you wish, once the list is Disposed the MDList will update all clients.
        /// Before using this method please read the wiki.
        /// </summary>
        /// <returns>A disposable list</returns>
        public MDDisposableList<T> GetRawList()
        {
            CheckIfUnsafeMode();
            return new MDDisposableList<T>(RealList, this);
        }

        /// <summary>
        /// Do not call this method directly, this is called by MDDisposableList.Dispose()
        /// </summary>
        /// <param name="NewList">The new list to replace this list with</param>
        public void DoFullResynch(List<T> NewList)
        {
            CheckIfUnsafeMode();
            RealList = new List<T>(NewList);
            FullResynch = true;
        }

    #endregion

    #region READ ONLY METHODS    
        public int Count { get { return RealList.Count; } }
        public int Capacity { get { return RealList.Capacity; } }

        public ReadOnlyCollection<T> AsReadOnly()
        {
            return RealList.AsReadOnly();
        }
        
        public int BinarySearch(int index, int count, T item, IComparer<T> comparer)
        {
            return RealList.BinarySearch(index, count, item, comparer);
        }

        public int BinarySearch(T item)
        {
            return RealList.BinarySearch(item);
        }
        
        public int BinarySearch(T item, IComparer<T> comparer)
        {
            return RealList.BinarySearch(item, comparer);
        }
        
        public bool Contains(T item)
        {
            return RealList.Contains(item);
        }
        
        public List<TOutput> ConvertAll<TOutput>(Converter<T, TOutput> converter)
        {
            return RealList.ConvertAll(converter);
        }
        
        public void CopyTo(T[] array, int arrayIndex)
        {
            RealList.CopyTo(array, arrayIndex);
        }
        
        public void CopyTo(int index, T[] array, int arrayIndex, int count)
        {
            RealList.CopyTo(index, array, arrayIndex, count);
        }
        
        public void CopyTo(T[] array)
        {
            RealList.CopyTo(array);
        }
        
        public bool Exists(Predicate<T> match)
        {
            return RealList.Exists(match);
        }
        
        public T Find(Predicate<T> match)
        {
            return RealList.Find(match);
        }
        
        public List<T> FindAll(Predicate<T> match)
        {
            return RealList.FindAll(match);
        }
        
        public int FindIndex(Predicate<T> match)
        {
            return RealList.FindIndex(match);
        }
        
        public int FindIndex(int startIndex, Predicate<T> match)
        {
            return RealList.FindIndex(startIndex, match);
        }
        
        public int FindIndex(int startIndex, int count, Predicate<T> match)
        {
            return RealList.FindIndex(startIndex, count, match);
        }
        
        public T FindLast(Predicate<T> match)
        {
            return RealList.FindLast(match);
        }
        
        public int FindLastIndex(Predicate<T> match)
        {
            return RealList.FindLastIndex(match);
        }
        
        public int FindLastIndex(int startIndex, Predicate<T> match)
        {
            return RealList.FindLastIndex(startIndex, match);
        }
        
        public int FindLastIndex(int startIndex, int count, Predicate<T> match)
        {
            return RealList.FindLastIndex(startIndex, count, match);
        }
        
        ///<summary>Be careful not to modify the list itself from this method</summary>
        public void ForEach(Action<T> action)
        {
            RealList.ForEach(action);
        }
        
        ///<summary>Be careful not to modify the list itself from this method</summary>
        public IEnumerable<T> GetEnumerator()
        {
            foreach (T item in RealList)
            {
                yield return item;
            }
        }
        
        public List<T> GetRange(int index, int count)
        {
            return RealList.GetRange(index, count);
        }
        
        public int IndexOf(T item, int index, int count)
        {
            return RealList.IndexOf(item, index, count);
        }
        
        public int IndexOf(T item, int index)
        {
            return RealList.IndexOf(item, index);
        }
        
        public int IndexOf(T item)
        {
            return RealList.IndexOf(item);
        }
        
        public int LastIndexOf(T item)
        {
            return RealList.LastIndexOf(item);
        }
        
        public int LastIndexOf(T item, int index)
        {
            return RealList.LastIndexOf(item, index);
        }
        
        public int LastIndexOf(T item, int index, int count)
        {
            return RealList.LastIndexOf(item, index, count);
        }
        
        public T[] ToArray()
        {
            return RealList.ToArray();
        }
        
        public void TrimExcess()
        {
            // Won't bother to network this, doesn't matter
            RealList.TrimExcess();
        }
        
        public bool TrueForAll(Predicate<T> match)
        {
            return RealList.TrueForAll(match);
        }

    #endregion
    }
}

