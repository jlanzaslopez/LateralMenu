using System;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace LateralMenu.Models
{
    public enum AlarmState
    {
        Ok = 0,
        Warning = 1,
        Alarm = 2
    }

    public class TreeNode : INotifyPropertyChanged
    {
        // Identity-ish
        public Guid Id { get; } = Guid.NewGuid();

        // Display fields
        public string Title { get; set; }
        public string Name { get; set; }
        public string Path { get; set; }             // for Navigation nodes: real path; for Content nodes: synthesized "<parent>/<ContentName>"
        public string ParentPath { get; set; }       // used for Content activation (navigate parent)

        // Secondary/right-side display (does not change -> no notify)
        public string ParentTitle { get; set; }

        private string _value;
        public string Value
        {
            get => _value;
            set { if (_value != value) { _value = value; OnPropertyChanged(nameof(Value)); } }
        }

        // Hierarchy
        public ObservableCollection<TreeNode> Items { get; } = new ObservableCollection<TreeNode>();

        // Favorites
        private bool _isFavorite;
        public bool IsFavorite
        {
            get => _isFavorite;
            set { if (_isFavorite != value) { _isFavorite = value; OnPropertyChanged(nameof(IsFavorite)); } }
        }

        private DateTimeOffset? _favoritedAt;
        public DateTimeOffset? FavoritedAt
        {
            get => _favoritedAt;
            set { if (_favoritedAt != value) { _favoritedAt = value; OnPropertyChanged(nameof(FavoritedAt)); } }
        }

        private bool _isContent;                    // false = Navigation node, true = Content node
        public bool IsContent
        {
            get => _isContent;
            set { if (_isContent != value) { _isContent = value; OnPropertyChanged(nameof(IsContent)); } }
        }

        // Alarm state (updates in background -> must notify)
        private AlarmState _alarmState = AlarmState.Ok;
        public AlarmState AlarmState
        {
            get => _alarmState;
            set { if (_alarmState != value) { _alarmState = value; OnPropertyChanged(nameof(AlarmState)); } }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
