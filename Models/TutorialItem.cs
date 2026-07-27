using System.ComponentModel;

namespace VANTAGE.Models
{
    // One tutorial video from the S3 tutorials.json manifest. The list shows the
    // Name with Description underneath. Both are provided by Steve per video and
    // are independent of the filename.
    public class TutorialItem : INotifyPropertyChanged
    {
        // S3 object key of the MP4. Number-prefixed ("1 - Vantage-Intro.mp4") to
        // control list ordering; the number never shows in the UI.
        public string Key { get; set; } = string.Empty;

        // Name shown in the list. Provided by Steve per video; does not have to
        // match the filename.
        public string Name { get; set; } = string.Empty;

        // Description shown under the name. Provided by Steve per video.
        public string Description { get; set; } = string.Empty;

        // Whether this user has opened the video before. Drives the "Watched"
        // badge; set on load from UserSettings and flipped when the row is clicked.
        private bool _watched;
        public bool Watched
        {
            get => _watched;
            set
            {
                if (_watched == value) return;
                _watched = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Watched)));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }
}
