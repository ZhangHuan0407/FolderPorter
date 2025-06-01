using System;
using System.IO.Enumeration;

namespace FolderPorter.Model
{
    [Serializable]
    public class IgnoreModel
    {
        public List<string> ItemList { get; set; }

        public IgnoreModel(string[] lines)
        {
            ItemList = new List<string>();
            for (int i = 0; i < lines.Length; i++)
            {
                lines[i] = lines[i].Trim();
                if (string.IsNullOrEmpty(lines[i]))
                    continue;
                if (lines[i][0] == '#')
                    continue;
                ItemList.Add(lines[i]);
            }
        }

        public bool ShouldIgnore(string fileRelativePath)
        {
            bool ignoreCase = OperatingSystem.IsWindows();
            for (int i = 0; i < ItemList.Count; i++)
            {
                if (FileSystemName.MatchesSimpleExpression(ItemList[i], fileRelativePath, ignoreCase))
                    return true;
            }
            return false;
        }
    }
}