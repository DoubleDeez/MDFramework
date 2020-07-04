using Godot;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

public class MDList<T>
{
    private class ListActionRecord
    {
        public ListActionRecord(uint Number, ListActions Type, object[] Parameters)
        {
            this.ActionNumber = Number;
            this.ActionType = Type;
            this.Parameters = Parameters;
        }

        public uint ActionNumber = 0;
        public ListActions ActionType = ListActions.UNKOWN;
        public object[] Parameters;

        public void Send()
        {
            // TODO: Implement
        }
    }
    public enum ListActions
    {
        UNKOWN,
        MODIFICATION,
        ADD,
        ADD_AT_INDEX,
        REMOVE_INDEX,
        REMOVE_RANGE,
        REVERSE_INDEX,
        REVERSE,
        CLEAR
    }
    private List<T> RealList = new List<T>();

    private Queue<ListActionRecord> CommandHistory = new Queue<ListActionRecord>();

    private uint CommandCounter = 0;

    public MDList()
    {

    }

    public void SendActions()
    {
        // For now not much error handling
        while (CommandHistory.Count > 0)
        {
            CommandHistory.Dequeue().Send();
        }
    }

    public void ProcessAction(uint Number, ListActions Type, params object[] Parameters)
    {
        // TODO: Implement this, this is for clients when they recieve a command.
    }

    protected void RecordAction(ListActions Type, params object[] Parameters)
    {
        CommandHistory.Enqueue(new ListActionRecord(GetActionNumber(), Type, Parameters));
    }

    private uint GetActionNumber()
    {
        CommandCounter++;
        return CommandCounter-1;
    }

#region MODIFYING METHODS
    public T this[int index] 
    { 
        get
        {
            return RealList[index];
        } 
        set
        {
            RecordAction(ListActions.MODIFICATION, index, value);
            RealList[index] = value;
        } 
    }

    public void Add(T item)
    {
        RecordAction(ListActions.ADD, item);
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
        RecordAction(ListActions.CLEAR);
        RealList.Clear();
    }

    public void Insert(int index, T item)
    {
        RecordAction(ListActions.ADD_AT_INDEX, index, item);
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
        RecordAction(ListActions.REMOVE_INDEX, index);
    }
    
    public void RemoveRange(int index, int count)
    {
        RealList.RemoveRange(index, count);
        RecordAction(ListActions.REMOVE_RANGE, index, count);
    }
    
    public void Reverse(int index, int count)
    {
        RealList.Reverse(index, count);
        RecordAction(ListActions.REVERSE_INDEX, index, count);
    }
    
    public void Reverse()
    {
        RealList.Reverse();
        RecordAction(ListActions.REVERSE);
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