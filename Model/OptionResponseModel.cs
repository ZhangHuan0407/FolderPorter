namespace FolderPorter.Model
{
    [Serializable]
    public class OptionResponseModel
    {
        public bool FindFolder { get; set; }
        public bool Refause { get; set; }
        public bool PleaseWaiting { get; set; }

        public Version RemoteVersion { get; set; }

        public OptionResponseModel()
        {
        }
        public OptionResponseModel(Version serverVersion)
        {
            RemoteVersion = serverVersion;
        }
    }
}