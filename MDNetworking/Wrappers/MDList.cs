using Godot;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

public class MDList<T>
{
    protected const String LOG_CAT = "LogMDList";

    private class ListActionRecord
    {
        public ListActionRecord(uint Number, MDListActions Type, object[] Parameters)
        {
            this.CommandNumber = Number;
            this.Type = Type;
            this.Parameters = Parameters;
        }

        public uint CommandNumber = 0;
        public MDListActions Type = MDListActions.UNKOWN;
        public object[] Parameters;
    }
    
    private List<T> RealList = new List<T>();

    private Queue<ListActionRecord> CommandHistory = new Queue<ListActionRecord>();

    private List<ListActionRecord> CommandQueue = new List<ListActionRecord>();

    private uint CommandCounter = 0;

    private uint ListId = 0;

    private IMDListDataConverter<T> DataConverter;

    private MDReplicator Replicator;

    public MDList(IMDListDataConverter<T> DataConverter, uint ListId, MDReplicator Replicator)
    {
        MDLog.AddLogCategoryProperties(LOG_CAT, new MDLogProperties(MDLogLevel.Info));
        this.DataConverter = DataConverter;
        this.ListId = ListId;
        this.Replicator = Replicator;
    }

#region PUBLIC METHODS

    ///<summary>Do not call this! This should only be used by the MDReplicator</summary>
    public void MDSendActions()
    {
        // For now not much error handling
        while (CommandHistory.Count > 0)
        {
            SendAction(CommandHistory.Dequeue());
        }
    }

    ///<summary>Do not call this! This should only be used by the MDReplicator</summary>
    public void MDProcessAction(uint Number, MDListActions Type, params object[] Parameters)
    {
        if (Number > CommandCounter)
        {
            CommandQueue.Add(new ListActionRecord(Number, Type, Parameters));
            return;
        }
        else if (Number < CommandCounter)
        {
            // This should not happen
            MDLog.Error(LOG_CAT, "Recieved a command with number {0} when our internal CommandCounter is {1}", Number, CommandCounter);
            return;
        }

        // Process incoming command
        switch (Type)
        {
            case MDListActions.MODIFICATION:
                RealList[(int)Parameters[0]] = ConvertFromObject(Parameters.SubArray(1));
                break;
            case MDListActions.ADD:
                RealList.Add(ConvertFromObject(Parameters));
                break;
            case MDListActions.INSERT:
                RealList.Insert((int)Parameters[0],ConvertFromObject(Parameters.SubArray(1)));
                break;
            case MDListActions.REMOVE_AT:
                RealList.RemoveAt((int)Parameters[0]);
                break;
            case MDListActions.REMOVE_RANGE:
                RealList.RemoveRange((int)Parameters[0], (int)Parameters[1]);
                break;
            case MDListActions.REVERSE_INDEX:
                RealList.Reverse((int)Parameters[0], (int)Parameters[1]);
                break;
            case MDListActions.REVERSE:
                RealList.Reverse();
                break;
            case MDListActions.CLEAR:
                RealList.Clear();
                break;

        }

        // Increase counter and check if next command is queued
        CommandCounter++;
        ListActionRecord NextCommand = GetCurrentCommandFromQueue();
        if (NextCommand != null)
        {
            MDProcessAction(NextCommand.CommandNumber, NextCommand.Type, NextCommand.Parameters);
        }
    }

#endregion

#region PRIVATE METHODS

    private void SendAction(ListActionRecord ActionRecord)
    {
        Replicator.SendListData(ListId, ActionRecord.CommandNumber, ActionRecord.Type, ParseParameters(ActionRecord));
    }

    private object[] ParseParameters(ListActionRecord ActionRecord)
    {
        // Any ListAction that has a T object in it needs to be converted, for anything else just pass along the parameters
        switch (ActionRecord.Type)
        {
            case MDListActions.ADD:
                return ConvertToObject(ActionRecord.Parameters[0]);
            case MDListActions.MODIFICATION:
            case MDListActions.INSERT:
                return new object[] { ActionRecord.Parameters[0], ConvertToObject(ActionRecord.Parameters[1]) };
            default:
                return ActionRecord.Parameters;
        }
    }

    // Just for convenience
    private object[] ConvertToObject(object item)
    {
        return DataConverter.ConvertToObject((T)item);
    }

    // Just for convenience
    private T ConvertFromObject(object[] Parameters)
    {
        return DataConverter.ConvertFromObject(Parameters);
    }

    // Get the current command from the queue and remove it if it exists
    private ListActionRecord GetCurrentCommandFromQueue()
    {
        ListActionRecord ActionRecord = null;
        foreach (ListActionRecord record in CommandQueue)
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

    private void RecordAction(MDListActions Type, params object[] Parameters)
    {
        CommandHistory.Enqueue(new ListActionRecord(GetActionNumber(), Type, Parameters));
    }

    private uint GetActionNumber()
    {
        CommandCounter++;
        return CommandCounter-1;
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
        // This may not be accurate, may have to do this in reverse to get order the same as normal insert range?
        foreach (T item in collection)
        {
            Insert(index, item);
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
    public IEnumerator<T> GetEnumerator()
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

#region NOT SUPPORTED METHODS

    public void Sort()
    {
        // Passing a comparer over the network is too difficult
        // Maybe we can record the order before / after but it would be a pain
        // For now not supported
        throw new NotSupportedException("Sort is not supported on MDList");
    }

    public void Sort(int index, int count, IComparer<T> comparer)
    {
        Sort();
    }
    
    public void Sort(Comparison<T> comparison)
    {
        Sort();
    }
    
    public void Sort(IComparer<T> comparer)
    {
        Sort();
    }

#endregion
}

public interface IMDListDataConverter<T>
{
    object[] ConvertToObject(T item);

    T ConvertFromObject(object[] Parameters);
}