using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;

namespace VANTAGE.Utilities
{
    /// <summary>
    /// ObservableCollection that supports bulk operations without firing notifications for each item
    /// </summary>
    public class BulkObservableCollection<T> : ObservableCollection<T>
    {
        private bool _suppressNotification = false;

        /// <summary>
        /// Add multiple items at once with a single notification
        /// </summary>
        public void AddRange(IEnumerable<T> items)
        {
            if (items == null) return;

            _suppressNotification = true;

            foreach (var item in items)
            {
                Add(item);
            }

            _suppressNotification = false;
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }

        protected override void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
        {
            if (!_suppressNotification)
                base.OnCollectionChanged(e);
        }
    }
}