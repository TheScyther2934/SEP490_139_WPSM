using Domain.Rules;
using Domain.Services;

namespace Domain.Entities;

public partial class WeddingProject
{
    /// <summary>
    /// Stage 1 (UC-03): the signed event with its services, the creator as Primary operator and the deposit.
    /// Paid on the spot in cash, or due until the Manager records the bank receipt.
    /// Lines and deposit must already be checked with <see cref="EventBookingPolicy"/>.
    /// </summary>
    public static WeddingProject Book(string projectCode, User customer, int managerId, int hallId, int slotId, DateOnly eventDate,
        IReadOnlyList<(BookingLine Line, string Description)> lines, decimal deposit, string paymentMethod, DateTime nowUtc, DateOnly todayVn)
    {
        var contractValue = EventBookingPolicy.ContractValue(lines.Select(x => x.Line).ToList());
        var project = new WeddingProject
        {
            ProjectCode = projectCode,
            Customer = customer,
            ManagerId = managerId,
            CreatedBy = managerId,
            HallId = hallId,
            SlotId = slotId,
            EventDate = eventDate,
            BaseContractValue = contractValue,
            DepositAmount = deposit,
            // Signed in stage 1, adjustable in stage 2 until the lock date; CurrentTotal follows the service lines.
            CurrentTotal = contractValue,
            Status = EventStatus.Adjusting,
            CreatedAt = nowUtc
        };
        // The creator is the Primary operator; co-managers are added later from M-04 or by an Admin.
        project.ProjectManagers.Add(new ProjectManager { ManagerId = managerId, Role = "Primary", AssignedBy = managerId, AssignedAt = nowUtc, Reason = "Người tạo sự kiện" });
        foreach (var (line, description) in lines)
            project.SpecItems.Add(new SpecItem { CatalogItemId = line.CatalogItemId, Description = description, Quantity = line.Quantity, UnitPrice = line.UnitPrice });

        var paidInCash = paymentMethod == EventBookingPolicy.Cash;
        project.Payments.Add(new Payment
        {
            PaymentType = "Deposit",
            Status = paidInCash ? "Paid" : "Due",
            DueDate = todayVn,
            Amount = deposit,
            PaymentMethod = paidInCash ? EventBookingPolicy.Cash : null,
            PaidDate = paidInCash ? todayVn : null,
            RecordedBy = paidInCash ? managerId : null
        });
        return project;
    }
}
