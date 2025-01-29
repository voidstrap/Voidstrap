namespace Hellstrap.Enums
{
    public enum CursorType
    {
        [EnumSort(Order = 1)]
        [EnumName(FromTranslation = "Common.Default")]
        Default,

        [EnumSort(Order = 3)]
        [EnumName(StaticName = "FPS Cursor (V1)")]
        FPSCursor,

        [EnumSort(Order = 2)]
        [EnumName(StaticName = "Clean Cursor")]
        CleanCursor,

        [EnumSort(Order = 5)]
        From2006,

        [EnumSort(Order = 4)]
        From2013
    }
}
