namespace WPSM.API;

public sealed class WpsmPortalStore
{
    private readonly List<WeddingProjectDto> _projects =
    [
        new("WPSM-2026-014", "Minh Anh & Thu Hà", "Sảnh Hoàng Gia", "Evening", new DateOnly(2026, 12, 20), 185_000_000m, 18_500_000m, "Active Baseline", 72, 2, "Frozen", 42_000_000m)
    ];

    private readonly List<CatalogItemDto> _catalog =
    [
        new("SVC-HALL", "Thuê sảnh tiệc", "Địa điểm", "Gói", 25_000_000m, true),
        new("SVC-MENU", "Thực đơn tiệc cưới", "Ẩm thực", "Bàn", 3_500_000m, true),
        new("SVC-DECOR", "Trang trí sân khấu", "Trang trí", "Gói", 12_000_000m, true),
        new("SVC-MC", "MC chương trình", "Nhân sự", "Người", 4_000_000m, true),
        new("SVC-AV", "Âm thanh và ánh sáng", "Thiết bị", "Gói", 8_000_000m, true)
    ];

    private readonly List<PriceFrameDto> _priceFrames =
    [
        new("PF-HALL", "Thuê sảnh tiệc", "Sảnh Hoàng Gia", 25_000_000m, 45_000_000m, "VND"),
        new("PF-MENU", "Thực đơn tiệc cưới", "Gói tiêu chuẩn", 2_500_000m, 5_500_000m, "VND"),
        new("PF-DECOR", "Trang trí sân khấu", "Gói sân khấu", 8_000_000m, 25_000_000m, "VND")
    ];

    private PolicyDto _policies = new(10, 40, 50, 14, 5, 10, 3);
    private ProfileDto _profile = new("Minh Anh", "minhanh@example.com", "+84 912 345 678", "Customer");
    private int _nextProjectNumber = 15;
    private string _activeProjectId = "WPSM-2026-014";

    public IReadOnlyList<WeddingProjectDto> Projects => _projects;
    public IReadOnlyList<CatalogItemDto> Catalog => _catalog;
    public IReadOnlyList<PriceFrameDto> PriceFrames => _priceFrames;
    public PolicyDto Policies => _policies;
    public ProfileDto Profile => _profile;

    public object GetBootstrap()
    {
        var project = _projects.First(item => item.Id == _activeProjectId);
        return new
        {
            project,
            projects = _projects,
            catalog = _catalog,
            priceFrames = _priceFrames,
            policies = _policies,
            profile = _profile,
            roadmap = new[]
            {
                new RoadmapTaskDto("T-90", "Chốt danh sách dịch vụ", new DateOnly(2026, 9, 21), "Completed"),
                new RoadmapTaskDto("T-30", "Xác nhận số lượng khách", new DateOnly(2026, 11, 20), "In progress"),
                new RoadmapTaskDto("T-7", "Đóng băng BEO", new DateOnly(2026, 12, 13), "Upcoming"),
                new RoadmapTaskDto("Event Day", "Điều phối ngày tiệc", project.EventDate, "Upcoming")
            }
        };
    }

    public WeddingProjectDto CreateProject(CreateProjectRequest request)
    {
        var project = new WeddingProjectDto(
            $"WPSM-{request.EventDate.Year}-{_nextProjectNumber++:000}", request.CoupleName, request.Hall,
            request.Slot, request.EventDate, request.BaseValue, request.NrdAmount, "Active Baseline", 0, 0, "Not started", request.BaseValue - request.NrdAmount);
        _projects.Add(project);
        _activeProjectId = project.Id;
        return project;
    }

    public WeddingProjectDto? UpdateSpecification(string id, SpecificationRequest request)
    {
        var index = _projects.FindIndex(project => project.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
        if (index < 0) return null;
        var existing = _projects[index];
        _projects[index] = existing with { SpecificationSummary = request.Summary, GuestCount = request.GuestCount };
        return _projects[index];
    }

    public CatalogItemDto AddCatalogItem(CatalogItemRequest request)
    {
        var item = new CatalogItemDto($"SVC-{Guid.NewGuid():N}"[..12].ToUpperInvariant(), request.Name, request.Category, request.Unit, request.BasePrice, request.Active);
        _catalog.Add(item);
        return item;
    }

    public PriceFrameDto? UpdatePriceFrame(string id, PriceFrameRequest request)
    {
        var index = _priceFrames.FindIndex(frame => frame.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
        if (index < 0) return null;
        _priceFrames[index] = _priceFrames[index] with { MinPrice = request.MinPrice, MaxPrice = request.MaxPrice };
        return _priceFrames[index];
    }

    public PolicyDto UpdatePolicy(PolicyRequest request) =>
        _policies = new PolicyDto(request.NrdRatio, request.T30Ratio, request.FinalRatio, request.CrPenaltyDays, request.CrPenaltyPercent, request.LatePenaltyDays, request.LatePenaltyPercent);

    public ProfileDto UpdateProfile(ProfileRequest request) =>
        _profile = _profile with { FullName = request.FullName, Phone = request.Phone };
}

public sealed record WeddingProjectDto(string Id, string CoupleName, string Hall, string Slot, DateOnly EventDate, decimal BaseValue, decimal NrdAmount, string Status, int RoadmapProgress, int OpenChangeRequests, string BeoStatus, decimal OutstandingBalance, string SpecificationSummary = "Chưa nhập đặc tả", int GuestCount = 0);
public sealed record CatalogItemDto(string Id, string Name, string Category, string Unit, decimal BasePrice, bool Active);
public sealed record PriceFrameDto(string Id, string ServiceName, string Variant, decimal MinPrice, decimal MaxPrice, string Currency);
public sealed record PolicyDto(decimal NrdRatio, decimal T30Ratio, decimal FinalRatio, int CrPenaltyDays, decimal CrPenaltyPercent, int LatePenaltyDays, decimal LatePenaltyPercent);
public sealed record ProfileDto(string FullName, string Email, string Phone, string Role);
public sealed record RoadmapTaskDto(string Milestone, string Title, DateOnly DueDate, string Status);
public sealed record CreateProjectRequest(string CoupleName, string Hall, string Slot, DateOnly EventDate, decimal BaseValue, decimal NrdAmount);
public sealed record SpecificationRequest(string Summary, int GuestCount);
public sealed record CatalogItemRequest(string Name, string Category, string Unit, decimal BasePrice, bool Active);
public sealed record PriceFrameRequest(decimal MinPrice, decimal MaxPrice);
public sealed record PolicyRequest(decimal NrdRatio, decimal T30Ratio, decimal FinalRatio, int CrPenaltyDays, decimal CrPenaltyPercent, int LatePenaltyDays, decimal LatePenaltyPercent);
public sealed record ProfileRequest(string FullName, string Phone);
public sealed record LoginRequest(string Email, string Password);
public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword, string ConfirmPassword);
