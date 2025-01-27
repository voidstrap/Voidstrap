namespace Hellstrap.Enums
{
    public enum CursorType
    {
        [EnumSort(Order = 1)]
        [EnumName(FromTranslation = "Common.Default")]
        Default,

        [EnumSort(Order = 4)]
        [EnumName(StaticName = "FPSCursor (V1)")]
        FPSCursor,

        [EnumSort(Order = 3)]
        From2006,

        [EnumSort(Order = 2)]
        From2013
    }
}
