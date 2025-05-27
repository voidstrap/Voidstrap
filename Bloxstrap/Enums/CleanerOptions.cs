namespace Voidstrap.Enums
{
    public enum CleanerOptions
    {
        [EnumName(StaticName = "Never")]
        Never,
        [EnumName(StaticName = "After 1 Day")]
        OneDay,
        [EnumName(StaticName = "After 1 Week")]
        OneWeek,
        [EnumName(StaticName = "After 1 Month")]
        OneMonth,
        [EnumName(StaticName = "After 2 Months")]
        TwoMonths
    }
}