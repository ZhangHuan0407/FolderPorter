namespace FolderPorter.Model
{
    [Serializable]
    public class ValidVersionEntry
    {
        public string Version { get; set; }
        public DateTime DateTime { get; set; }
        public string RemoteUser { get; set; }

        public ValidVersionEntry(string version, DateTime dateTime, string remoteUser)
        {
            Version = version;
            DateTime = dateTime;
            RemoteUser = remoteUser;
        }
    }
}