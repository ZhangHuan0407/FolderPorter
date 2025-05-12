using System;
using System.Collections.Generic;

namespace FolderPorter
{
    [Serializable]
    public enum WorkingMode
    {
        Unknown,
        Server = 1,
        Push = 2,
        Pull = 3,
        List = 4,
        Help = 9,
    }
}