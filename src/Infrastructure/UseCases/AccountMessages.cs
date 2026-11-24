using Application.Persistence;
using Application.UseCases.Accounts;
using Domain.Entities;
using Infrastructure.Email;

namespace Infrastructure.UseCases;

/// <summary>Account emails through the outbox: the row commits with the account change and JOB-07 sends it.</summary>
public sealed class AccountMessages(IEmailOutbox emailOutbox, EmailOptions emailOptions) : IAccountMessages
{
    public void StaffCredentials(User user, string temporaryPassword, bool reset) =>
        emailOutbox.Enqueue(user.Email, reset ? "Cấp lại mật khẩu WPSM" : "Tài khoản WPSM của bạn",
            EmailTemplates.AccountCredentials(user.FullName, user.Email, temporaryPassword, user.Role, reset, emailOptions.PublicBaseUrl), "Credentials", null);
}
