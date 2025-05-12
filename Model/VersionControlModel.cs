namespace FolderPorter.Model
{
    [Serializable]
    public class VersionControlModel
    {
        public string LastSuccessVersion { get; set; }
        public string Version { get; set; }

        public List<ValidVersionEntry> ValidVersionList { get; set; }

        public VersionControlModel()
        {
            ValidVersionList = new List<ValidVersionEntry>();
        }
    }
}