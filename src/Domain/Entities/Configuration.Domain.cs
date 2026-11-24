namespace Domain.Entities;

public partial class ServiceCatalogItem
{
    public const string Active = "Active";
    public const string Inactive = "Inactive";

    /// <summary>A new catalog service; name, unit and price already checked (ConfigurationPolicy.ValidateCatalogItem).</summary>
    public static ServiceCatalogItem Create(string name, string category, string unit, decimal price, bool active) => new()
    {
        Name = name, Category = category, Unit = unit, DefaultPrice = price, Status = active ? Active : Inactive
    };

    /// <summary>Signed service lines keep their own description and price, so editing the catalog never changes a signed contract.</summary>
    public void Edit(string name, string category, string unit, decimal price, bool active)
    {
        Name = name;
        Category = category;
        Unit = unit;
        DefaultPrice = price;
        SetActive(active);
    }

    public void SetActive(bool active) => Status = active ? Active : Inactive;
}

public partial class PriceFrame
{
    /// <summary>A price frame of a service from a date; frames of the past are history and never change.</summary>
    public static PriceFrame Create(int itemId, DateOnly effectiveFrom, decimal min, decimal max, int updatedBy, DateTime nowUtc) => new()
    {
        ItemId = itemId, EffectiveFrom = effectiveFrom, MinPrice = min, MaxPrice = max, UpdatedBy = updatedBy, UpdatedAt = nowUtc
    };

    public void Change(decimal min, decimal max, int updatedBy, DateTime nowUtc)
    {
        MinPrice = min;
        MaxPrice = max;
        UpdatedBy = updatedBy;
        UpdatedAt = nowUtc;
    }
}

public partial class PolicyConfig
{
    /// <summary>One policy value from a date (a new version; earlier versions stay for the events they applied to).</summary>
    public static PolicyConfig Create(string type, decimal value, DateOnly effectiveFrom, int updatedBy, DateTime nowUtc) => new()
    {
        PolicyType = type, Value = value, EffectiveFrom = effectiveFrom, UpdatedBy = updatedBy, UpdatedAt = nowUtc
    };

    public void Change(decimal value, int updatedBy, DateTime nowUtc)
    {
        Value = value;
        UpdatedBy = updatedBy;
        UpdatedAt = nowUtc;
    }
}

public partial class SystemSetting
{
    public const string HotlineKey = "Hotline";

    public static SystemSetting Create(string key, string value, int updatedBy, DateTime nowUtc) => new()
    {
        SettingKey = key, SettingValue = value, UpdatedBy = updatedBy, UpdatedAt = nowUtc
    };

    /// <summary>Only a different value counts as a change (keeps who changed it last meaningful).</summary>
    public void Change(string value, int updatedBy, DateTime nowUtc)
    {
        if (SettingValue == value) return;
        SettingValue = value;
        UpdatedBy = updatedBy;
        UpdatedAt = nowUtc;
    }
}
