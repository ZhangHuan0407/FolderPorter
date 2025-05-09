namespace FolderPorter.Model
{
    [Serializable]
    public class PushFolderRequestModel
    {
        public string Folder { get; set; }

        public List<FileSliceHashModel> FileSliceHashList { get; set; }

        public List<FileAnchor> BytesTransferList { get; set; }

        public bool TransferFinish { get; set; }

        public PushFolderRequestModel()
        {
        }
        public PushFolderRequestModel(string folder)
        {
            Folder = folder;
            FileSliceHashList = new List<FileSliceHashModel>();
            BytesTransferList = new List<FileAnchor>();
            TransferFinish = false;
        }
    }
}