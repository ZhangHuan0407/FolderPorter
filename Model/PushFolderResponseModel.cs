namespace FolderPorter.Model
{
    [Serializable]
    public class PushFolderResponseModel
    {
        public List<FileAnchor> NeedSyncList { get; set; }

        public int DeleteFilesCount { get; set; }

        public bool TransferFinish { get; set; }

        public PushFolderResponseModel()
        {
            NeedSyncList = new List<FileAnchor>();
        }
    }
}