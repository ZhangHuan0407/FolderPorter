namespace FolderPorter.Model
{
    [Serializable]
    public class ListFolderResponseModel
    {
        public string Folder { get; set; }
        public List<ValidVersionEntry> ValidVersionList { get; set; }
        public int ValidVersionCount { get; set; }

        public ListFolderResponseModel()
        {
        }

        public ListFolderResponseModel(string folder, IEnumerable<ValidVersionEntry> list, int validVersionCount)
        {
            Folder = folder;
            ValidVersionList = new List<ValidVersionEntry>(list);
            ValidVersionCount = validVersionCount;
        }
    }
}