#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

namespace LM.App.Wpf.Common.Collections
{
    /// <summary>
    /// Observable collection with a helper to append multiple items in a single change notification.
    /// </summary>
    /// <typeparam name="T">Item type.</typeparam>
    internal sealed class RangeObservableCollection<T> : ObservableCollection<T>
    {
        public RangeObservableCollection()
        {
        }

        public RangeObservableCollection(IEnumerable<T> collection)
            : base(collection)
        {
        }

        public RangeObservableCollection(List<T> list)
            : base(list)
        {
        }

        public void AddRange(IEnumerable<T> items)
        {
            if (items is null)
                throw new ArgumentNullException(nameof(items));

            var newItems = new List<T>();
            foreach (var item in items)
            {
                newItems.Add(item);
            }

            if (newItems.Count == 0)
                return;

            CheckReentrancy();

            var startIndex = Count;
            foreach (var item in newItems)
            {
                Items.Add(item);
            }

            OnPropertyChanged(new PropertyChangedEventArgs(nameof(Count)));
            OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
            base.OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, newItems, startIndex));
        }
    }
}
