namespace Hellstrap.Enums
{
    public enum CursorType
    {
        // Default cursor
        [EnumSort(Order = 1)]
        [EnumName(FromTranslation = "Common.Default")]
        Default,

        // FPS cursor for a first-person shooter experience
        [EnumSort(Order = 5)]
        [EnumName(StaticName = "FPS Cursor (V1)")]
        FPSCursor,

        // Clean and minimalist design cursor
        [EnumSort(Order = 4)]
        [EnumName(StaticName = "Clean Cursor")]
        CleanCursor,


        [EnumSort(Order = 3)]
        [EnumName(StaticName = "Dot Cursor")]
        DotCursor,

        // A more stylized "Stoofs" cursor
        [EnumSort(Order = 2)]
        [EnumName(StaticName = "Stoofs Cursor")]
        StoofsCursor,

        // Legacy cursor from the year 2006
        [EnumSort(Order = 6)]
        [EnumName(StaticName = "2006 Legacy Cursor")]
        From2006,

        // Legacy cursor from the year 2013
        [EnumSort(Order = 7)]
        [EnumName(StaticName = "2013 Legacy Cursor")]
        From2013
    }
}
