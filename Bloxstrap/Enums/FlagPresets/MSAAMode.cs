﻿namespace Voidstrap.Enums.FlagPresets
{
    public enum MSAAMode
    {
        [EnumName(FromTranslation = "Common.Automatic")]
        Default,
        [EnumName(StaticName = "1x")]
        x1,
        [EnumName(StaticName = "2x")]
        x2,
        [EnumName(StaticName = "4x")]
        x4,
        [EnumName(StaticName = "8x")]
        x8
    }
}
