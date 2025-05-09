namespace FolderPorter.Model
{
    [Serializable]
    public class FileAnchor
    {
        public int FileIndex { get; set; }
        public int SliceIndex { get; set; }

        public FileAnchor(int fileIndex, int sliceIndex)
        {
            FileIndex = fileIndex;
            SliceIndex = sliceIndex;
        }
    }
}