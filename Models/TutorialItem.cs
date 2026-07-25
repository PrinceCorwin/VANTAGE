namespace VANTAGE.Models
{
    // One tutorial video from the S3 tutorials.json manifest. The list shows the
    // filename without extension as the name, with Description underneath.
    public class TutorialItem
    {
        // S3 object key of the MP4. Number-prefixed ("1 - Vantage-Intro.mp4") to
        // control ordering.
        public string Key { get; set; } = string.Empty;

        // Description shown under the name. Provided by Steve per video.
        public string Description { get; set; } = string.Empty;

        // Name shown in the list: the filename without its .mp4 extension.
        public string DisplayName => System.IO.Path.GetFileNameWithoutExtension(Key);
    }
}
