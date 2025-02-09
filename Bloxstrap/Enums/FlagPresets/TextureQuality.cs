namespace Hellstrap.Enums.FlagPresets
{
    public enum TextureQuality
    {
        [EnumName(FromTranslation = "Common.Automatic")]
        Default, // Automatically adjusts texture quality

        [EnumName(StaticName = "Max (Blurry icons)")]
        Level1,

        [EnumName(StaticName = "Ultra")]
        Level2,

        [EnumName(StaticName = "Very High")]
        Level3,

        [EnumName(StaticName = "High")]
        Level4,

        [EnumName(StaticName = "Low")]
        Level5,

        [EnumName(StaticName = "Medium")]
        Level6,

        [EnumName(StaticName = "Very Low")]
        Level7,

        [EnumName(StaticName = "Lowest (Removes textures)")]
        Level8
    }
}
