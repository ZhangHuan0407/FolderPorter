using System.Text.Json.Serialization;

namespace FolderPorter.Model
{
    [Serializable]
    public class OptionRequestModel
    {
        public string TypeStr { get; set; }
        [JsonIgnore]
        public WorkingMode Type
        {
            get
            {
                Enum.TryParse(TypeStr, true, out WorkingMode result);
                return result;
            }
        }

        public string Folder { get; set; }
        public int? SpecificVersion { get; set; }
        public string User { get; set; }

        public OptionRequestModel()
        {
        }
        public OptionRequestModel(WorkingMode type, string folder, string user)
        {
            TypeStr = type.ToString();
            Folder = folder;
            User = user;
        }
    }
}