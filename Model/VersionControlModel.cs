namespace FolderPorter.Model
{
    [Serializable]
    public class VersionControlModel
    {
        public int LastSuccessVersion { get; set; }
        public int Version { get; set; }

        public List<ValidVersionEntry> ValidVersionList { get; set; }

        public VersionControlModel()
        {
            ValidVersionList = new List<ValidVersionEntry>();
        }
    }
}