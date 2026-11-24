using Application.Persistence;
using Application.UseCases.EventBooking;
using Domain.Entities;
using Domain.Rules;
using Domain.Services;
using Infrastructure.Email;
using Infrastructure.Identity;
using Infrastructure.Persistence;
using Infrastructure.Persistence.Repositories;
using Infrastructure.UseCases;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Tests;

/// <summary>
/// Stage 1, signing and deposit (UC-03): the rules on their own (policy and entities, no database),
/// then the use cases end to end on an in-memory database, as the API calls them.
/// </summary>
public sealed class EventBookingTests : IDisposable
{
    private const int ManagerId = 2, OtherManagerId = 3;
    private static DateOnly Today => VietnamTime.GetVietnamToday();

    private readonly WpsmDbContext _db = TestFixtures.NewDb();
    private readonly TestFixtures.RecordingEmailOutbox _outbox = new();
    private readonly PasswordService _passwords = new(new PasswordHasher<User>());

    public EventBookingTests()
    {
        _db.Users.AddRange(TestFixtures.NewUser(ManagerId, "Manager", "manager@test.local", "Quản lý Minh"),
            TestFixtures.NewUser(OtherManagerId, "Manager", "other@test.local", "Quản lý Khác"));
        _db.Halls.Add(TestFixtures.NewHall());
        _db.Slots.Add(TestFixtures.NewSlot());
        _db.ServiceCatalogItems.AddRange(
            TestFixtures.NewCatalogItem(10, "Thuê sảnh", "Hall", "Gói", 30_000_000m),
            TestFixtures.NewCatalogItem(11, "Thực đơn", "Catering", "Bàn", 3_500_000m),
            TestFixtures.NewCatalogItem(12, "Pháo hoa", "Decor", "Gói", 5_000_000m, status: "Inactive"));
        _db.PriceFrames.Add(TestFixtures.NewFrame(11, 3_000_000m, 4_000_000m, updatedBy: ManagerId, from: Today.AddDays(-30)));
        _db.SaveChanges();
    }

    public void Dispose() => _db.Dispose();

    // ── Rules, no database ──────────────────────────────────────────────────

    [Theory]
    [InlineData("", "a@b.vn", "0912345678")]
    [InlineData("Ánh & Minh", "not-an-email", "0912345678")]
    [InlineData("Ánh & Minh", "a@b.vn", "12345")]
    public void Customer_details_are_checked(string name, string email, string phone) =>
        Assert.Throws<BusinessRuleException>(() => EventBookingPolicy.ValidateCustomer(name, email, phone));

    [Fact]
    public void Customer_details_are_trimmed_and_the_phone_keeps_digits()
    {
        var customer = EventBookingPolicy.ValidateCustomer("  Ánh & Minh ", " anh@test.vn ", "0912 345 678");
        Assert.Equal(new BookingCustomer("Ánh & Minh", "anh@test.vn", "0912345678"), customer);
    }

    [Fact]
    public void Services_must_be_chosen_once_each_with_a_positive_quantity_inside_their_price_frame()
    {
        Assert.Throws<BusinessRuleException>(() => EventBookingPolicy.ValidateLines([]));
        Assert.Throws<BusinessRuleException>(() => EventBookingPolicy.ValidateLines([new(1, null, 1, 1m), new(1, null, 2, 1m)]));
        Assert.Throws<BusinessRuleException>(() => EventBookingPolicy.ValidateLines([new(1, null, 0, 1m)]));
        Assert.Throws<BusinessRuleException>(() => EventBookingPolicy.EnsurePriceInRange("Thực đơn", 5_000_000m, new(3_000_000m, 4_000_000m)));
        EventBookingPolicy.EnsurePriceInRange("Thực đơn", 3_500_000m, new(3_000_000m, 4_000_000m));
    }

    [Fact]
    public void Deposit_is_at_least_the_policy_ratio_and_at_most_the_contract_value()
    {
        Assert.Equal(30_000_000m, EventBookingPolicy.Deposit(100_000_000m, 0, EventPolicy.Default));
        Assert.Equal(50_000_000m, EventBookingPolicy.Deposit(100_000_000m, 50_000_000m, EventPolicy.Default));
        Assert.Throws<BusinessRuleException>(() => EventBookingPolicy.Deposit(100_000_000m, 29_000_000m, EventPolicy.Default));
        Assert.Throws<BusinessRuleException>(() => EventBookingPolicy.Deposit(100_000_000m, 100_000_001m, EventPolicy.Default));
    }

    [Fact]
    public void Event_codes_follow_the_highest_code_of_the_year()
    {
        Assert.Equal("WPSM-2026-001", EventBookingPolicy.NextProjectCode(2026, []));
        Assert.Equal("WPSM-2026-013", EventBookingPolicy.NextProjectCode(2026, ["WPSM-2026-003", "WPSM-2026-012", "WPSM-2025-099"]));
    }

    [Fact]
    public void A_staff_email_cannot_be_a_customer_and_a_known_customer_password_is_reset_only_before_first_sign_in()
    {
        Assert.Throws<BusinessRuleException>(() => new User { Role = "Manager", Status = "Active" }.PrepareForNewBooking());
        var returning = new User { Role = "Customer", Status = "Inactive" };
        returning.PrepareForNewBooking();
        Assert.Equal("Active", returning.Status);

        Assert.Throws<BusinessRuleException>(() => new User { MustChangePassword = false }.ResetTemporaryPassword("hash"));
        var waiting = new User { MustChangePassword = true };
        waiting.ResetTemporaryPassword("new-hash");
        Assert.Equal("new-hash", waiting.PasswordHash);
    }

    [Fact]
    public void A_deposit_is_recorded_once()
    {
        var deposit = new Payment { Status = "Due" };
        deposit.RecordBankReceipt("wpsm/receipt.jpg", "bien-lai.jpg", "image/jpeg", ManagerId, Today);
        Assert.Equal(("Paid", "Bank", ManagerId), (deposit.Status, deposit.PaymentMethod, deposit.RecordedBy));
        Assert.Throws<BusinessRuleException>(() => deposit.RecordBankReceipt("x", "y", "image/jpeg", ManagerId, Today));
    }

    // ── Use cases on a database ─────────────────────────────────────────────

    private EventMessages Messages() => new(_db, _outbox, new EmailOptions { PublicBaseUrl = "http://test.local" });
    private BookEvent Book() => new(new EventBookingStore(_db), _passwords, Messages(), new UnitOfWork(_db), new ProjectRepository(_db, new ProjectReadModel(_db)));

    private static CreateProjectCommand Signing(string email = "anh@test.vn", string payment = "Cash", decimal deposit = 0, int daysAway = 40,
        params SpecificationLineCommand[] lines) =>
        new("Ánh & Minh", email, "0912345678", "Sảnh Hoàng Gia", "tối", Today.AddDays(daysAway), 0, deposit, payment, null,
            lines.Length > 0 ? lines : [new(10, "", 1, 30_000_000m), new(11, "Thực đơn chay", 20, 3_500_000m)]);

    [Fact]
    public async Task Signing_creates_the_customer_the_event_its_services_and_a_paid_cash_deposit()
    {
        var created = await Book().HandleAsync(Signing(), ManagerId);

        Assert.NotNull(created.TemporaryPassword);
        var project = _db.WeddingProjects.AsNoTracking().Include(p => p.Customer).Include(p => p.SpecItems).Include(p => p.Payments).Include(p => p.ProjectManagers).Single();
        Assert.Equal(created.Project.Id, project.ProjectCode);
        Assert.StartsWith($"WPSM-{DateTime.UtcNow.Year}-", project.ProjectCode);
        Assert.Equal(EventStatus.Adjusting, project.Status);
        Assert.Equal(100_000_000m, project.BaseContractValue);
        Assert.Equal(30_000_000m, project.DepositAmount);
        Assert.Equal(["Thuê sảnh", "Thực đơn chay"], project.SpecItems.OrderBy(s => s.CatalogItemId).Select(s => s.Description));
        Assert.Equal(("Paid", "Cash"), (project.Payments.Single().Status, project.Payments.Single().PaymentMethod));
        Assert.Equal(("Primary", ManagerId), (project.ProjectManagers.Single().Role, project.ProjectManagers.Single().ManagerId));
        Assert.True(project.Customer.MustChangePassword);
        Assert.True(_passwords.Verify(project.Customer.PasswordHash, created.TemporaryPassword!));
        Assert.Contains(_outbox.Messages, m => m.To == "anh@test.vn" && m.Kind == "Credentials" && m.ProjectId == project.ProjectId);
        Assert.Contains(_db.Notifications, n => n.RecipientId == project.CustomerId && n.Message.Contains("đã được xác nhận"));
    }

    [Fact]
    public async Task A_returning_customer_gets_no_new_password_and_the_slot_cannot_be_booked_twice()
    {
        await Book().HandleAsync(Signing(), ManagerId);
        var again = await Assert.ThrowsAsync<BusinessRuleException>(() => Book().HandleAsync(Signing(), ManagerId));
        Assert.Contains("đã được đặt", again.Message);

        var second = await Book().HandleAsync(Signing(daysAway: 41, payment: "Bank"), ManagerId);
        Assert.Null(second.TemporaryPassword);
        Assert.Equal(1, _db.Users.Count(u => u.Role == "Customer"));
        Assert.Single(_outbox.Messages, m => m.Kind == "Credentials");
        Assert.Equal("Due", _db.Payments.Single(p => p.Project.ProjectCode == second.Project.Id).Status);
    }

    [Theory]
    [InlineData(10, 0, "Pháo hoa")]          // inactive service
    [InlineData(14, 0, null)]                 // outside the booking window
    [InlineData(40, 1_000_000, null)]         // deposit below 30%
    public async Task Invalid_signings_are_refused_and_nothing_is_saved(int daysAway, decimal deposit, string? inactive)
    {
        SpecificationLineCommand[] lines = inactive is null ? [] : [new(12, "", 1, 5_000_000m)];
        await Assert.ThrowsAsync<BusinessRuleException>(() => Book().HandleAsync(Signing(daysAway: daysAway, deposit: deposit, lines: lines), ManagerId));
        Assert.Empty(_db.WeddingProjects);
        Assert.Equal(0, _db.Users.Count(u => u.Role == "Customer"));
    }

    [Fact]
    public async Task A_price_outside_its_frame_is_refused()
    {
        var refused = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            Book().HandleAsync(Signing(lines: [new(10, "", 1, 30_000_000m), new(11, "", 20, 5_000_000m)]), ManagerId));
        Assert.Contains("Thực đơn", refused.Message);
    }

    [Fact]
    public async Task Credentials_are_resent_until_the_customer_signs_in_and_only_by_an_operator()
    {
        var created = await Book().HandleAsync(Signing(), ManagerId);
        var resend = new ResendCustomerCredentials(new EventBookingStore(_db), _passwords, Messages(), new UnitOfWork(_db));

        Assert.Null(await resend.HandleAsync(created.Project.Id, OtherManagerId));
        var password = await resend.HandleAsync(created.Project.Id, ManagerId);
        Assert.NotNull(password);
        Assert.NotEqual(created.TemporaryPassword, password);
        Assert.Equal(2, _outbox.Messages.Count(m => m.Kind == "Credentials"));

        _db.Users.Single(u => u.Role == "Customer").MustChangePassword = false;
        _db.SaveChanges();
        await Assert.ThrowsAsync<BusinessRuleException>(() => resend.HandleAsync(created.Project.Id, ManagerId));
    }

    [Fact]
    public async Task The_creator_records_the_bank_receipt_once()
    {
        var created = await Book().HandleAsync(Signing(payment: "Bank"), ManagerId);
        var record = new RecordDepositReceipt(new EventBookingStore(_db), Messages(), new UnitOfWork(_db));
        var receipt = new StoredFileView(Guid.NewGuid(), "DepositReceipt", "bien-lai.jpg", "image/jpeg", 1000, DateTime.UtcNow) { StorageKey = "wpsm/receipt.jpg" };

        Assert.False(await record.HandleAsync(created.Project.Id, OtherManagerId, receipt));
        Assert.True(await record.HandleAsync(created.Project.Id, ManagerId, receipt));
        var deposit = _db.Payments.AsNoTracking().Single();
        Assert.Equal(("Paid", "wpsm/receipt.jpg"), (deposit.Status, deposit.ReceiptFilePath));
        Assert.Contains(_db.Notifications, n => n.Message.Contains("Biên nhận tiền đặt cọc"));
        await Assert.ThrowsAsync<BusinessRuleException>(() => record.HandleAsync(created.Project.Id, ManagerId, receipt));
    }
}
