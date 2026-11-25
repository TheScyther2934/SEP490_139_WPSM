using Application.Persistence;
using Application.UseCases.Configuration;
using Domain.Entities;
using Domain.Rules;
using Domain.Services;
using Infrastructure.Persistence;
using Infrastructure.UseCases;
using Xunit;

namespace Tests;

/// <summary>Bugs reported by testers on 22–24/11/2026 (Discord #sep-code): phone numbers, catalog supplier flag, reopening tasks, removing evidence.</summary>
public sealed class BugReportFixTests
{
    private readonly WpsmDbContext _db = TestFixtures.NewDb();

    [Theory]
    [InlineData("0912345678", "0912345678")]
    [InlineData("0912 345 678", "0912345678")]
    [InlineData("+84 912 345 678", "0912345678")]
    [InlineData("84912345678", "0912345678")]
    [InlineData("02438123456", "02438123456")]
    public void Vietnamese_phone_numbers_are_kept_as_digits_starting_with_0(string input, string expected) =>
        Assert.Equal(expected, PhonePolicy.Require(input));

    [Theory]
    [InlineData("1185028924")]   // reported: 10 digits but no leading 0
    [InlineData("091234567")]    // 9 digits
    [InlineData("091234567890")] // 12 digits
    [InlineData("")]
    public void Phone_numbers_without_a_leading_0_or_of_the_wrong_length_are_refused(string input)
    {
        Assert.Throws<BusinessRuleException>(() => PhonePolicy.Require(input));
        Assert.Throws<BusinessRuleException>(() => EventBookingPolicy.ValidateCustomer("Ánh & Minh", "anh@test.vn", input));
    }

    [Fact]
    public void An_account_phone_is_optional_but_checked_when_given()
    {
        Assert.Null(AccountPolicy.ValidateDetails("Minh", "minh@test.vn", " ").Phone);
        Assert.Throws<BusinessRuleException>(() => AccountPolicy.ValidateDetails("Minh", "minh@test.vn", "1185028924"));
    }

    [Fact]
    public async Task The_admin_sets_and_keeps_whether_a_service_needs_an_outside_supplier()
    {
        _db.ServiceCatalogItems.Add(TestFixtures.NewCatalogItem(17, "Áo dài cưới", "Attire", "Bộ", 3_000_000m));
        await _db.SaveChangesAsync();
        var store = new ConfigurationStore(_db);

        var on = await new UpdateCatalogItem(store, new UnitOfWork(_db)).HandleAsync(17, new CatalogItemCommand("Áo dài cưới", "Attire", "Bộ", 3_000_000m, true, true));
        var kept = await new UpdateCatalogItem(store, new UnitOfWork(_db)).HandleAsync(17, new CatalogItemCommand("Áo dài cưới", "Attire", "Bộ", 3_200_000m, true));
        var added = await new AddCatalogItem(store, new UnitOfWork(_db)).HandleAsync(new CatalogItemCommand("Xe hoa VIP", "Transport", "Chiếc", 5_000_000m, true, true));

        Assert.True(on!.RequiresSupplier);
        Assert.True(kept!.RequiresSupplier); // not sent: unchanged
        Assert.True(added.RequiresSupplier);
        Assert.True(_db.ServiceCatalogItems.Single(x => x.ItemId == 17).RequiresSupplier);
    }

    [Fact]
    public void A_task_completed_by_mistake_is_reopened_and_keeps_its_proof()
    {
        var task = new DayOfTask { Status = "Pending", TaskName = "Nhận nguyên liệu", Department = "Kitchen", DeadlineTime = DateTime.UtcNow.AddDays(3) };
        task.ExecutionPhotos.Add(new ExecutionPhoto());
        task.Complete(5, DateTime.UtcNow, 1);

        task.Reopen();

        Assert.Equal(("Pending", null, null), (task.Status, task.CompletedAt, task.CompletedBy));
        Assert.Single(task.ExecutionPhotos);
        Assert.Throws<BusinessRuleException>(task.Reopen);
    }

    [Theory]
    [InlineData(EventStatus.Adjusting, false)]
    [InlineData(EventStatus.Locked, true)]
    [InlineData(EventStatus.Completed, true)]
    [InlineData(EventStatus.Settled, false)]
    public void Supplier_evidence_is_removed_only_between_the_lock_and_the_settlement(string status, bool allowed)
    {
        var error = Record.Exception(() => EventStagePolicy.EnsureCanRemoveEvidence(status));
        Assert.Equal(allowed, error is null);
    }
}
