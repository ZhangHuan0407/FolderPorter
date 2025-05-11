namespace FolderPorter.Model
{
    [Serializable]
    public class VersionControlModel
    {
        public int LastSuccessVersion { get; set; }
        public int Version { get; set; }

        public List<string> TransferLog { get; set; }

        public VersionControlModel()
        {
            TransferLog = new List<string>();
        }
    }
}