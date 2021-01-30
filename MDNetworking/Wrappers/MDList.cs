using Godot;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace MD
{
    public class MDDisposableList<T> : List<T>, IDisposable
    {
        protected IMDCommandReplicator MDListRef;

        public MDDisposableList(IEnumerable<T> collection, IMDCommandReplicator ListRef) : base(collection)
        {
            MDListRef = ListRef;
        }

        public void Dispose()
        {
            MDListRef.MDDoFullResynch(this);
        }
    }
    public static class MDList
    {
        // This class is just here for the enum since you can't reference enums / statics inside a generic class.
        public enum Settings
        {
            COMPARATOR,
            UNSAFE_MODE,
            MANUAL_CHECK_FOR_UPDATE_MODE
        }
    }
    public class MDList<T> : IMDCommandReplicator
    {
        internal class CustomComparer : IComparer<KeyValuePair<T, IMDDataConverter>>
        {
            private IComparer<T> Comparer;

            public CustomComparer(IComparer<T> comparer)
            {
                this.Comparer = comparer;
            }

            public int Compare(KeyValuePair<T, IMDDataConverter> x, KeyValuePair<T, IMDDataConverter> y)
            {
                if (Comparer == null)
                {
                    return x.Key.ToString().CompareTo(y.Key.ToString());
                }
                return Comparer.Compare(x.Key, y.Key);
            }
        }

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
        
        protected List<KeyValuePair<T, IMDDataConverter>> RealList = new List<KeyValuePair<T, IMDDataConverter>>();

        protected Queue<ListCommandRecord> CommandHistory = new Queue<ListCommandRecord>();

        protected List<ListCommandRecord> CommandQueue = new List<ListCommandRecord>();

        protected List<IComparer<T>> Comparators = new List<IComparer<T>>();

        protected int CommandCounter = 0;

        protected Type DataConverterType = null;

        protected MDReplicator Replicator;

        protected bool UnsafeMode = false;

        protected bool ManualCheckForUpdateMode = false;

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

            // Add a resynch start command
            record = new ListCommandRecord(CurrentAction, MDListActions.RESYNCH_START, new object[] {});
            Commands.Add(AsObjectArray(record));

            // Add all items to the command list
            foreach (KeyValuePair<T, IMDDataConverter> item in RealList)
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

        public bool MDShouldBeReplicated()
        {
            if (CommandHistory.Count > 0)
            {
                return true;
            }

            if (RealList.Count == 0)
            {
                return false;
            }
            
            return ManualCheckForUpdateMode ? false : MDCheckForUpdates();
        }

        /// <summary>
        /// Can be manually called to check for updates in case you got a list containing another list or custom classes.
        /// </summary>
        /// <returns>True if update was found, false if not</returns>
        public bool MDCheckForUpdates()
        {
            Type ConverterType = RealList[0].Value.GetType();
            bool hasChanges = false;

            if (ConverterType.IsGenericType && 
                (ConverterType.GetGenericTypeDefinition() == typeof(MDCommandReplicatorDataConverter<>)
                || ConverterType.GetGenericTypeDefinition() == typeof(MDCustomClassDataConverter<>)))
            {
                // We also need to check each item in case they are custom classes or another MDList
                for (int index = 0; index < RealList.Count; index++)
                {
                    T item = RealList[index].Key;
                    IMDDataConverter Converter = RealList[index].Value;
                    if (Converter.ShouldObjectBeReplicated(item, item))
                    {
                        MDLog.Trace(LOG_CAT, $"List has changed, sending modification");
                        hasChanges = true;
                        RecordAction(MDListActions.MODIFICATION, index, RealList[index]);
                    }
                }
            }

            return hasChanges;
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

            MDLog.Trace(LOG_CAT, $"Received command [{CmdNumber.ToString()}] {Type.ToString()} ({MDStatics.GetParametersAsString(Parameters)})");

            // Resynch start always goes through
            if (Type != MDListActions.RESYNCH_START)
            {
                if (CmdNumber > CommandCounter)
                {
                    MDLog.Trace(LOG_CAT, $"Added command to queue since counter is only {CommandCounter}");
                    CommandQueue.Add(new ListCommandRecord(CmdNumber, Type, Parameters));
                    return;
                }
                else if (CmdNumber < CommandCounter)
                {
                    // This should not happen
                    MDLog.Error(LOG_CAT, $"Recieved a command with number {CmdNumber} when our internal CommandCounter is {CommandCounter}");
                    return;
                }
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
                    RealList.Add(CreateNewObject(Parameters));
                    break;
                case MDListActions.INSERT:
                    RealList.Insert(Convert.ToInt32(Parameters[0]), CreateNewObject(Parameters.SubArray(1)));
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
                    RealList.Sort(new CustomComparer(null));
                    break;
                case MDListActions.SORT_COMPARATOR:
                    RealList.Sort(new CustomComparer(GetComparatorByIndex(Convert.ToInt32(Parameters[0]))));
                    break;
                case MDListActions.SORT_INDEX:
                    IComparer<T> comparer = GetComparatorByIndex(Convert.ToInt32(Parameters[2]));
                    RealList.Sort(Convert.ToInt32(Parameters[0]), Convert.ToInt32(Parameters[1]), 
                                    new CustomComparer(comparer));
                    break;
                case MDListActions.RESYNCH_START:
                    RealList.Clear();
                    CommandQueue.Clear();
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
                    case MDList.Settings.MANUAL_CHECK_FOR_UPDATE_MODE:
                        if (setting.Value.GetType() == typeof(Boolean))
                        {
                            ManualCheckForUpdateMode = (bool)setting.Value;
                        }
                        else
                        {
                            ManualCheckForUpdateMode = Boolean.Parse(setting.Value.ToString());
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
                this.DataConverterType = DataConverterType;
            }
        }

        protected IMDDataConverter GetNewConverter()
        {
            if (DataConverterType != null)
            {
                return MDStatics.CreateConverterOfType(DataConverterType);
            }
            
            return MDStatics.GetConverterForType(typeof(T));
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
            KeyValuePair<T, IMDDataConverter> pair;
            // Any ListAction that has a T object in it needs to be converted, for anything else just pass along the parameters
            switch (ActionRecord.Type)
            {
                case MDListActions.ADD:
                    pair = (KeyValuePair<T, IMDDataConverter>)ActionRecord.Parameters[0];
                    return pair.Value.ConvertForSending(pair.Key, CompleteMode);
                case MDListActions.MODIFICATION:
                case MDListActions.INSERT:
                    List<object> ObjectList = new List<object>();
                    ObjectList.Add(ActionRecord.Parameters[0]);
                    pair = (KeyValuePair<T, IMDDataConverter>)ActionRecord.Parameters[1];
                    ObjectList.AddRange(pair.Value.ConvertForSending(pair.Key, CompleteMode));
                    return ObjectList.ToArray();
                default:
                    return ActionRecord.Parameters;
            }
        }

        // Just for convenience
        protected object[] ConvertToObject(KeyValuePair<T, IMDDataConverter> Item)
        {
            return Item.Value.ConvertForSending(Item.Key, CompleteMode);
        }
        
        // Just for convenience
        protected KeyValuePair<T, IMDDataConverter> ConvertFromObject(KeyValuePair<T, IMDDataConverter> CurrentObject, object[] Parameters)
        {
            MDLog.Trace(LOG_CAT, $"Our values are {CurrentObject.Key} and {CurrentObject.Value.GetType().ToString()} ({MDStatics.GetParametersAsString(Parameters)})");
            return CreateNewItem((T)CurrentObject.Value.ConvertBackToObject(CurrentObject.Key, Parameters), CurrentObject.Value);
        }

        protected KeyValuePair<T, IMDDataConverter> CreateNewObject(object[] Parameters)
        {
            IMDDataConverter Converter = GetNewConverter();
            return CreateNewItem((T)Converter.ConvertBackToObject(null, Parameters), Converter);
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

        protected KeyValuePair<T, IMDDataConverter> CreateNewItem(T value)
        {
            return new KeyValuePair<T, IMDDataConverter>(value, GetNewConverter());
        }

        protected KeyValuePair<T, IMDDataConverter> CreateNewItem(T value, IMDDataConverter Converter)
        {
            return new KeyValuePair<T, IMDDataConverter>(value, Converter);
        }

    #endregion

    #region MODIFYING METHODS
        public T this[int index] 
        { 
            get
            {
                return RealList[index].Key;
            } 
            set
            {
                KeyValuePair<T, IMDDataConverter> pair = CreateNewItem(value, RealList[index].Value);
                RecordAction(MDListActions.MODIFICATION, index, pair);
                RealList[index] = pair;
            } 
        }

        public void Add(T item)
        {
            KeyValuePair<T, IMDDataConverter> pair = CreateNewItem(item, GetNewConverter());
            RecordAction(MDListActions.ADD, pair);
            RealList.Add(pair);
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
            KeyValuePair<T, IMDDataConverter> pair = CreateNewItem(item);
            RecordAction(MDListActions.INSERT, index, pair);
            RealList.Insert(index, pair);
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
            int index = RealList.FindIndex((KeyValuePair<T, IMDDataConverter> x) => x.Key.Equals(item));
            if (index >= 0)
            {
                RemoveAt(index);
                return true;
            }

            return false;
        }
        
        public int RemoveAll(Predicate<T> match)
        {
            List<KeyValuePair<T, IMDDataConverter>> items = RealList.FindAll((KeyValuePair<T, IMDDataConverter> x) => match.Invoke(x.Key));
            items.ForEach(item => Remove(item.Key));
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
            RealList.Sort(new CustomComparer(null));
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

            RealList.Sort(index, count, new CustomComparer(Comparer));
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

            RealList.Sort(new CustomComparer(Comparer));
            RecordAction(MDListActions.SORT_COMPARATOR, GetComparatorIndexFromType(ComparatorType));
        }

        /// <summary>
        /// Get a copy of the list that you can modify as you wish, once the list is Disposed the MDList will update all clients.
        /// Before using this method please read the wiki.
        /// </summary>
        /// <returns>A disposable list</returns>
        public MDDisposableList<KeyValuePair<T, IMDDataConverter>> GetRawList()
        {
            CheckIfUnsafeMode();
            return new MDDisposableList<KeyValuePair<T, IMDDataConverter>>(RealList, this);
        }

        /// <summary>
        /// Do not call this method directly, this is called by MDDisposableList.Dispose()
        /// </summary>
        /// <param name="NewList">The new list to replace this list with</param>
        public void MDDoFullResynch(object NewList)
        {
            CheckIfUnsafeMode();
            if (NewList is List<KeyValuePair<T, IMDDataConverter>>)
            {
                List<KeyValuePair<T, IMDDataConverter>> asList = (List<KeyValuePair<T, IMDDataConverter>>)NewList;
                RealList = new List<KeyValuePair<T, IMDDataConverter>>(asList);
                FullResynch = true;
            }
        }

    #endregion

    #region READ ONLY METHODS    
        public int Count { get { return RealList.Count; } }
        public int Capacity { get { return RealList.Capacity; } }

        public ReadOnlyCollection<T> AsReadOnly()
        {
            return AsList().AsReadOnly();
        }

        protected List<T> AsList()
        {
            List<T> ReadOnlyList = new List<T>();
            RealList.ForEach((KeyValuePair<T, IMDDataConverter> Pair) => ReadOnlyList.Add(Pair.Key));
            return ReadOnlyList;
        }
        
        public int BinarySearch(int index, int count, T item, IComparer<T> comparer)
        {
            return RealList.BinarySearch(index, count, CreateNewItem(item), new CustomComparer(comparer));
        }

        public int BinarySearch(T item)
        {
            return RealList.BinarySearch(CreateNewItem(item), new CustomComparer(null));
        }
        
        public int BinarySearch(T item, IComparer<T> comparer)
        {
            return RealList.BinarySearch(CreateNewItem(item), new CustomComparer(comparer));
        }
        
        public bool Contains(T item)
        {
            foreach (KeyValuePair<T, IMDDataConverter> listItem in RealList)
            {
                if (listItem.Key == null)
                {
                    // We can't do these two if's in one line.
                    // If listItem.Key == null and item != null then the .Equals check in the else if would crash
                    if (item == null)
                    {
                        return true;
                    }
                }
                else if (listItem.Key.Equals(item))
                {
                    return true;
                }
            }
            return false;
        }
        
        public List<TOutput> ConvertAll<TOutput>(Converter<T, TOutput> converter)
        {
            return this.AsList().ConvertAll(converter);
        }
        public void CopyTo(T[] array, int arrayIndex)
        {
            this.AsList().CopyTo(array, arrayIndex);
        }
        
        public void CopyTo(int index, T[] array, int arrayIndex, int count)
        {
            this.AsList().CopyTo(index, array, arrayIndex, count);
        }
        
        public void CopyTo(T[] array)
        {
            this.AsList().CopyTo(array);
        }
        
        public bool Exists(Predicate<T> match)
        {
            return this.AsList().Exists(match);
        }
        
        public T Find(Predicate<T> match)
        {
            return this.AsList().Find(match);
        }
        
        public List<T> FindAll(Predicate<T> match)
        {
            return this.AsList().FindAll(match);
        }
        
        public int FindIndex(Predicate<T> match)
        {
            return this.AsList().FindIndex(match);
        }
        
        public int FindIndex(int startIndex, Predicate<T> match)
        {
            return this.AsList().FindIndex(startIndex, match);
        }
        
        public int FindIndex(int startIndex, int count, Predicate<T> match)
        {
            return this.AsList().FindIndex(startIndex, count, match);
        }
        
        public T FindLast(Predicate<T> match)
        {
            return this.AsList().FindLast(match);
        }
        
        public int FindLastIndex(Predicate<T> match)
        {
            return this.AsList().FindLastIndex(match);
        }
        
        public int FindLastIndex(int startIndex, Predicate<T> match)
        {
            return this.AsList().FindLastIndex(startIndex, match);
        }
        
        public int FindLastIndex(int startIndex, int count, Predicate<T> match)
        {
            return this.AsList().FindLastIndex(startIndex, count, match);
        }
        
        ///<summary>Be careful not to modify the list itself from this method</summary>
        public void ForEach(Action<T> action)
        {
            this.AsList().ForEach(action);
        }
        
        ///<summary>Be careful not to modify the list itself from this method</summary>
        public IEnumerable<T> GetEnumerator()
        {
            foreach (KeyValuePair<T, IMDDataConverter> item in RealList)
            {
                yield return item.Key;
            }
        }
        
        public List<T> GetRange(int index, int count)
        {
            return this.AsList().GetRange(index, count);
        }
        
        public int IndexOf(T item, int index, int count)
        {
            return this.AsList().IndexOf(item, index, count);
        }
        
        public int IndexOf(T item, int index)
        {
            return this.AsList().IndexOf(item, index);
        }
        
        public int IndexOf(T item)
        {
            return this.AsList().IndexOf(item);
        }
        
        public int LastIndexOf(T item)
        {
            return this.AsList().LastIndexOf(item);
        }
        
        public int LastIndexOf(T item, int index)
        {
            return this.AsList().LastIndexOf(item, index);
        }
        
        public int LastIndexOf(T item, int index, int count)
        {
            return this.AsList().LastIndexOf(item, index, count);
        }
        
        public T[] ToArray()
        {
            return this.AsList().ToArray();
        }
        
        public void TrimExcess()
        {
            // Won't bother to network this, doesn't matter
            RealList.TrimExcess();
        }
        
        public bool TrueForAll(Predicate<T> match)
        {
            return this.AsList().TrueForAll(match);
        }

    #endregion

    #region LINQ METHODS
    public bool Any(Func<T, bool> predicate)
    {
        return this.AsList().Any(predicate);
    }

    public T Single(Func<T, bool> predicate)
    {
        return this.AsList().Single(predicate);
    }

    #endregion
    }
}

