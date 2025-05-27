namespace Voidstrap.Enums
{
    public enum CursorType
    {
        [EnumSort(Order = 1)]
        [EnumName(FromTranslation = "Common.Default")]
        Default,

        [EnumSort(Order = 5)]
        [EnumName(StaticName = "FPS Cursor (V1)")]
        FPSCursor,

        [EnumSort(Order = 4)]
        [EnumName(StaticName = "Clean Cursor")]
        CleanCursor,


        [EnumSort(Order = 3)]
        [EnumName(StaticName = "Dot Cursor")]
        DotCursor,

        [EnumSort(Order = 2)]
        [EnumName(StaticName = "Stoofs Cursor")]
        StoofsCursor,

        [EnumSort(Order = 6)]
        [EnumName(StaticName = "2006 Legacy Cursor")]
        From2006,

        [EnumSort(Order = 7)]
        [EnumName(StaticName = "2013 Legacy Cursor")]
        From2013,

        [EnumSort(Order = 8)]
        [EnumName(StaticName = "WhiteDotCursor")]
        WhiteDotCursor,

        [EnumSort(Order = 9)]
        [EnumName(StaticName = "ArceyPlayz Dot")]
        VerySmallWhiteDot
    }
}
