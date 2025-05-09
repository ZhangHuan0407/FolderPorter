namespace FolderPorter.Model
{
    [Serializable]
    public class FileSliceHashModel
    {
        public List<string> CRCList { get; set; }
        public int FileIndex { get; set; }
        public string FileRelativePath { get; set; }
        public long FileTotalLength { get; set; }

        public FileSliceHashModel()
        {
        }
        public FileSliceHashModel(int fileIndex, string fileRelativePath, FileInfo fileInfo, ref byte[]? buffer)
        {
            CRCList = new List<string>();
            FileIndex = fileIndex;
            FileTotalLength = fileInfo.Length;
            FileRelativePath = fileRelativePath;
            using (FileStream fileStream = fileInfo.OpenRead())
            {
                for (long start = 0; start < FileTotalLength; start += Program.SliceLength)
                {
                    long length = Math.Min(Program.SliceLength, FileTotalLength - start);
                    string crc32 = CRC32.ComputeStream(fileStream, start, length, ref buffer).CRC32Str;
                    CRCList.Add(crc32);
                }
            }
        }
    }
}