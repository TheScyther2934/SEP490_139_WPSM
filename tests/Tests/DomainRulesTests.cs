using System;
using Domain.Rules;
using Domain.Services;
using Xunit;

namespace Tests;

/// <summary>Business rules tested on their own: no database, no EF Core, no web host.</summary>
public sealed class DomainRulesTests
{
    [Fact]
    public void CoordinationSheet_FreezesOnlyWhileTheEventIsLocked()
    {
        foreach (var status in new[] { EventStatus.Adjusting, EventStatus.Completed, EventStatus.Settled, EventStatus.Cancelled })
            Assert.Throws<BusinessRuleException>(() => CoordinationSheetPolicy.Freeze(status, null, null));
        Assert.Equal(1, CoordinationSheetPolicy.Freeze(EventStatus.Locked, null, null).Version);
    }

    [Fact]
    public void CoordinationSheet_FreezesTheDraftInPlace_AndReapprovalIssuesTheNextVersion()
    {
        var draft = CoordinationSheetPolicy.Freeze(EventStatus.Locked, 1, CoordinationSheetPolicy.Draft);
        Assert.False(draft.NewVersion);
        Assert.Equal(1, draft.Version);

        var reapproved = CoordinationSheetPolicy.Freeze(EventStatus.Locked, 2, CoordinationSheetPolicy.Stale);
        Assert.True(reapproved.NewVersion);
        Assert.Equal(3, reapproved.Version);
    }

    [Fact]
    public void CoordinationSheet_TurnsStaleOnlyWhenFrozen()
    {
        Assert.True(CoordinationSheetPolicy.TurnsStaleOnConfirmedAddon(CoordinationSheetPolicy.Frozen));
        Assert.False(CoordinationSheetPolicy.TurnsStaleOnConfirmedAddon(CoordinationSheetPolicy.Draft));
        Assert.False(CoordinationSheetPolicy.TurnsStaleOnConfirmedAddon(null));
    }

    [Fact]
    public void Addon_IsOnlyForLockedOrHeldEvents_BeforeTheSettlementIsPaid()
    {
        Assert.Throws<BusinessRuleException>(() => AddonContractPolicy.EnsureCanCreate(EventStatus.Adjusting, false));
        Assert.Throws<BusinessRuleException>(() => AddonContractPolicy.EnsureCanCreate(EventStatus.Settled, false));
        Assert.Throws<BusinessRuleException>(() => AddonContractPolicy.EnsureCanCreate(EventStatus.Completed, settlementPaid: true));
        AddonContractPolicy.EnsureCanCreate(EventStatus.Locked, false);
        AddonContractPolicy.EnsureCanCreate(EventStatus.Completed, false);
    }

    [Fact]
    public void Addon_ContentIsCheckedAndTrimmed()
    {
        Assert.Throws<BusinessRuleException>(() => AddonContractPolicy.ValidateContent("ab", [new(null, "Bàn", 1, 1m)]));
        Assert.Throws<BusinessRuleException>(() => AddonContractPolicy.ValidateContent("Thêm bàn", []));
        Assert.Throws<BusinessRuleException>(() => AddonContractPolicy.ValidateContent("Thêm bàn", [new(null, "Bàn", 0, 1m)]));
        var (title, lines) = AddonContractPolicy.ValidateContent("  Thêm 2 bàn  ", [new(11, "  Bàn thêm ", 2, 3_500_000m)]);
        Assert.Equal("Thêm 2 bàn", title);
        Assert.Equal("Bàn thêm", lines[0].Description);
        Assert.Equal(7_000_000m, AddonContractPolicy.Total(lines));
    }

    [Fact]
    public void Addon_ReminderAtMostOncePerHour()
    {
        var now = new DateTime(2026, 11, 20, 10, 0, 0, DateTimeKind.Utc);
        Assert.Throws<BusinessRuleException>(() => AddonContractPolicy.EnsureCanRemind(AddonContractPolicy.PendingCustomer, now.AddMinutes(-30), now));
        AddonContractPolicy.EnsureCanRemind(AddonContractPolicy.PendingCustomer, now.AddMinutes(-61), now);
        Assert.Throws<BusinessRuleException>(() => AddonContractPolicy.EnsureCanRemind(AddonContractPolicy.Confirmed, null, now));
    }

    [Fact]
    public void Addon_CancelNeedsAReason_AndOnlyBeforeConfirmation()
    {
        Assert.Throws<BusinessRuleException>(() => AddonContractPolicy.EnsureCanCancel(AddonContractPolicy.PendingCustomer, "  ok "));
        Assert.Throws<BusinessRuleException>(() => AddonContractPolicy.EnsureCanCancel(AddonContractPolicy.Confirmed, "Khách không lấy nữa"));
        Assert.Equal("Khách không lấy nữa", AddonContractPolicy.EnsureCanCancel(AddonContractPolicy.PendingCustomer, " Khách không lấy nữa "));
    }

    [Fact]
    public void Addon_ConfirmOnlyWhileWaiting_AndBeforeTheEventIsSettled()
    {
        Assert.Throws<BusinessRuleException>(() => AddonContractPolicy.EnsureCanConfirm(AddonContractPolicy.Cancelled, EventStatus.Locked));
        Assert.Throws<BusinessRuleException>(() => AddonContractPolicy.EnsureCanConfirm(AddonContractPolicy.PendingCustomer, EventStatus.Settled));
        AddonContractPolicy.EnsureCanConfirm(AddonContractPolicy.PendingCustomer, EventStatus.Locked);
    }
}
