namespace FolderPorter
{
    [Flags]
    [Serializable]
    public enum EncryptedTransmission
    {
        SimplePassword = 0x01,
        AES_CBC = 0x20,
    }
}