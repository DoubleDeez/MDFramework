using Godot;
using System;
using System.Collections.Generic;

namespace MD
{
    public interface IMDDataConverter
    {
        ///<summary>Convert this object into an object array that can be converted back later</summary>
        object[] ConvertToObjectArray(object item);

        ///<summary>The list of parameters may be longer than the amount of parameters the conversion consumes</summary>
        object ConvertFromObjectArray(object[] Parameters);

        ///<Summary>Return how many parameters were used by the last ConvertFromObject call</summary>
        int GetParametersConsumedByLastConversion();
    }

    ///<summary> This default implementation should work on any godot base type that can be sent with rpc calls</summary>
    public class MDObjectDataConverter : IMDDataConverter
    {
        public object[] ConvertToObjectArray(object item)
        {
            return new object[] { item };
        }

        public object ConvertFromObjectArray(object[] Parameters)
        {
            return Parameters[0];
        }

        public int GetParametersConsumedByLastConversion()
        {
            return 1;
        }
    }

    /// <summary>This data converter a list of something using another data converter</summary>
    /*
        You have to implement this class yourself if you wish to use it.

        For example you could do something like:

            public class MyListConverter : MDListDataConverter<String>
            {
                public MyListConverter() : base(new MDObjectDataConverter())
                {
                    
                }
            }

        This would allow you to use MyListConverter for an MDList that contains List<String>

            [MDReplicated]
            [MDReplicatedSetting(MDReplicatedCommandReplicator.Settings.Converter, typeof(MyListConverter))]
            protected MDList<List<String>> MyMDList;

        Now you can wrap this even further if you want a List<List<String>> then you add another converter

            public class MyListListConverter : MDListDataConverter<List<String>>
            {
                public MyListListConverter() : base(new MyListConverter())
                {
                    
                }
            }

        This will allow you now to have a MDList<List<List<String>>, so in theory you can wrap as many of these as you want.

            [MDReplicated]
            [MDReplicatedSetting(MDReplicatedCommandReplicator.Settings.Converter, typeof(MyListListConverter))]
            protected MDList<List<List<String>>> MyMDList;

        Just beware that wrapping a list inside an MDList will cause every value in the list to be sent whenever any value of the list is changed.
        In other words, it is not recommended.

    */
    public abstract class MDListDataConverter<T> : IMDDataConverter
    {
        protected IMDDataConverter Converter;

        protected int ConsumedByLastCall = 0;

        public MDListDataConverter(IMDDataConverter Converter)
        {
            this.Converter = Converter;
        }

        public object[] ConvertToObjectArray(object item)
        {
            List<object> ReturnList = new List<object>();
            List<T> list = (List<T>)item;
            foreach (T listItem in list)
            {
                ReturnList.AddRange(Converter.ConvertToObjectArray(listItem));
            }
            return ReturnList.ToArray();
        }

        public object ConvertFromObjectArray(object[] Parameters)
        {
            List<object> ConversionList = new List<object>(Parameters);
            List<T> NewList = new List<T>();

            // We are going to consume all the parameters
            ConsumedByLastCall = Parameters.Length;

            while (ConversionList.Count > 0)
            {
                T item = (T)Converter.ConvertFromObjectArray(ConversionList.ToArray());
                NewList.Add(item);
                for (int i=0; i < Converter.GetParametersConsumedByLastConversion(); i++)
                {
                    ConversionList.RemoveAt(0);
                }
            }
            return NewList;
        }

        public int GetParametersConsumedByLastConversion()
        {
            return ConsumedByLastCall;
        }
    }
}