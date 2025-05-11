namespace FolderPorter.Model
{
    [Serializable]
    public class OptionRequestModel
    {
        public string Type { get; set; }
        public string Folder { get; set; }

        public OptionRequestModel()
        {
        }
        public OptionRequestModel(string type, string folder)
        {
            Type = type;
            Folder = folder;
        }
    }
}