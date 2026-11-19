namespace Infrastructure.Persistence;

internal static class WpsmCategoryNames
{
    private static readonly IReadOnlyDictionary<string, string> CategoryToDatabase = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["hall"] = "Hall", ["sảnh"] = "Hall", ["sảnh tiệc"] = "Hall",
        ["cuisine"] = "Catering", ["catering"] = "Catering", ["ẩm thực"] = "Catering",
        ["decor"] = "Decor", ["trang trí"] = "Decor",
        ["mc"] = "MC",
        ["av"] = "AV", ["âm thanh & ánh sáng"] = "AV", ["âm thanh ánh sáng"] = "AV",
        ["media"] = "Photo/Video", ["photo/video"] = "Photo/Video", ["quay chụp"] = "Photo/Video",
        ["costume"] = "Attire", ["attire"] = "Attire", ["trang phục"] = "Attire",
        ["makeup"] = "Makeup", ["trang điểm"] = "Makeup",
        ["transport"] = "Transport", ["vận chuyển"] = "Transport",
        ["ceremony"] = "Ceremony", ["bê lễ"] = "Ceremony", ["lễ vật"] = "Ceremony",
        ["performance"] = "Performance", ["văn nghệ"] = "Performance",
        ["furniture"] = "Furniture", ["nội thất"] = "Furniture"
    };

    internal static string Normalize(string category) => CategoryToDatabase.TryGetValue(category.Trim(), out var value)
        ? value : throw new InvalidOperationException("Danh mục dịch vụ không hợp lệ.");

    internal static string ToDisplay(string category) => category switch
    {
        "Hall" => "Sảnh", "Catering" => "Ẩm thực", "Decor" => "Trang trí", "MC" => "MC", "AV" => "Âm thanh & ánh sáng",
        "Photo/Video" => "Quay chụp", "Attire" => "Trang phục", "Makeup" => "Trang điểm", "Transport" => "Vận chuyển",
        "Ceremony" => "Bê lễ", "Performance" => "Văn nghệ", "Furniture" => "Nội thất", _ => category
    };
}
