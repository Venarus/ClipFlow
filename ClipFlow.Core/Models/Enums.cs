using System;

namespace ClipFlow.Core.Models;

public enum ContentType
{
    Text,
    RichText,
    Html,
    Image,
    FileDrop,
    Url,
    Code,
    Color,
    Email,
    Phone
}

public enum SmartType
{
    None,
    Url,
    Email,
    HexColor,
    RgbColor,
    Date,
    Phone,
    Code,
    FilePath
}

public enum DateFilterOption
{
    All,
    Today,
    Yesterday,
    ThisWeek,
    ThisMonth
}

public enum SortOption
{
    Newest,
    Oldest,
    MostUsed,
    ContentType
}
