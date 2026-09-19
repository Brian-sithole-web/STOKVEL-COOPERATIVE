using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Stokvel.Application.Common;
using Stokvel.Application.Dtos;
using Stokvel.Application.Services;
using Stokvel.Domain;
using Stokvel.Domain.Entities;
using Stokvel.Infrastructure.Email;
using Stokvel.Infrastructure.Identity;
using Stokvel.Infrastructure.Persistence;

namespace Stokvel.Infrastructure.Services;

public sealed class SetupService : AppServiceBase, ISetupService
{
    private readonly IConfiguration _config;
    private readonly RoleManager<ApplicationRole> _roles;
    private readonly TokenFactory _tokens;

    public SetupService(
        StokvelDbContext db,
        ICurrentUser current,
        UserManager<ApplicationUser> users,
        RoleManager<ApplicationRole> roles,
        IConfiguration config) : base(db, current, users)
    {
        _roles = roles;
        _config = config;
        _tokens = new TokenFactory(config);
    }

    public async Task<SetupStatusDto> GetStatusAsync(CancellationToken ct = default)
    {
        var settings = await Db.PlatformSettings.AsNoTracking().FirstOrDefaultAsync(ct);
        var adminExists = (await Users.GetUsersInRoleAsync(SystemRoles.PlatformAdmin)).Count > 0;
        var configured = settings?.SetupCompleted == true && adminExists;
        return new SetupStatusDto(configured, !configured);
    }

    public async Task<AuthResponse> InitializeAsync(InitializeSetupRequest request, CancellationToken ct = default)
    {
        var status = await GetStatusAsync(ct);
        if (status.IsConfigured)
            throw new BusinessRuleException("SETUP_COMPLETE", "The platform has already been configured.");

        ValidatePassword(request.Password);

        await EnsureRolesAsync();

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = request.Email.Trim(),
            Email = request.Email.Trim(),
            FullName = request.FullName.Trim(),
            EmailConfirmed = true,
            MustChangePassword = true
        };

        var created = await Users.CreateAsync(user, request.Password);
        if (!created.Succeeded)
            throw new BusinessRuleException("IDENTITY", string.Join(" ", created.Errors.Select(e => e.Description)));

        await Users.AddToRoleAsync(user, SystemRoles.PlatformAdmin);

        if (!await Db.PlatformSettings.AnyAsync(ct))
        {
            Db.PlatformSettings.Add(new PlatformSettings
            {
                SetupCompleted = true,
                SetupCompletedAt = DateTime.UtcNow,
                DefaultLendingSources = LendingSource.Both
            });
        }
        else
        {
            var settings = await Db.PlatformSettings.FirstAsync(ct);
            settings.SetupCompleted = true;
            settings.SetupCompletedAt = DateTime.UtcNow;
        }

        await AuditAsync("SETUP", $"Platform Administrator {user.FullName} was created through secure first-run setup.", ct: ct);
        await Db.SaveChangesAsync(ct);
        return await BuildAuthAsync(user, ct, _tokens);
    }

    private async Task EnsureRolesAsync()
    {
        foreach (var role in new[] { SystemRoles.PlatformAdmin, SystemRoles.User })
        {
            if (!await _roles.RoleExistsAsync(role))
                await _roles.CreateAsync(new ApplicationRole(role));
        }
    }

    internal static void ValidatePassword(string password)
    {
        if (string.IsNullOrWhiteSpace(password) || password.Length < 10
            || !password.Any(char.IsUpper) || !password.Any(char.IsLower)
            || !password.Any(char.IsDigit) || password.All(char.IsLetterOrDigit))
        {
            throw new BusinessRuleException("WEAK_PASSWORD",
                "Password must be at least 10 characters and include upper, lower, digit and symbol.");
        }
    }
}

public sealed class AuthService : AppServiceBase, IAuthService
{
    private readonly TokenFactory _tokens;
    private readonly RoleManager<ApplicationRole> _roles;
    private readonly IEmailSender _emailSender;
    private readonly IConfiguration _config;
    private readonly IHostEnvironment _environment;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        StokvelDbContext db,
        ICurrentUser current,
        UserManager<ApplicationUser> users,
        RoleManager<ApplicationRole> roles,
        IConfiguration config,
        IEmailSender emailSender,
        IHostEnvironment environment,
        ILogger<AuthService> logger) : base(db, current, users)
    {
        _roles = roles;
        _config = config;
        _tokens = new TokenFactory(config);
        _emailSender = emailSender;
        _environment = environment;
        _logger = logger;
    }

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken ct = default)
    {
        SetupService.ValidatePassword(request.Password);
        if (!await _roles.RoleExistsAsync(SystemRoles.User))
            await _roles.CreateAsync(new ApplicationRole(SystemRoles.User));

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = request.Email.Trim(),
            Email = request.Email.Trim(),
            FullName = request.FullName.Trim(),
            EmailConfirmed = false
        };

        var created = await Users.CreateAsync(user, request.Password);
        if (!created.Succeeded)
            throw new BusinessRuleException("IDENTITY", string.Join(" ", created.Errors.Select(e => e.Description)));

        await Users.AddToRoleAsync(user, SystemRoles.User);
        await AuditAsync("REGISTER", $"{user.FullName} registered on STOKVEL COOPERATIVE.", ct: ct);
        await Db.SaveChangesAsync(ct);
        return await BuildAuthAsync(user, ct, _tokens);
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        var user = await Users.FindByEmailAsync(request.Email.Trim())
                   ?? throw new ForbiddenException("Invalid email or password.");
        if (!await Users.CheckPasswordAsync(user, request.Password))
            throw new ForbiddenException("Invalid email or password.");
        return await BuildAuthAsync(user, ct, _tokens);
    }

    public async Task<AuthResponse> MeAsync(CancellationToken ct = default)
    {
        var user = await Users.FindByIdAsync(Current.UserId.ToString())
                   ?? throw new ForbiddenException("Not authenticated.");
        return await BuildAuthAsync(user, ct, _tokens);
    }

    public async Task ChangePasswordAsync(ChangePasswordRequest request, CancellationToken ct = default)
    {
        SetupService.ValidatePassword(request.NewPassword);
        var user = await Users.FindByIdAsync(Current.UserId.ToString())
                   ?? throw new ForbiddenException("Not authenticated.");
        var result = await Users.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
        if (!result.Succeeded)
            throw new BusinessRuleException("IDENTITY", string.Join(" ", result.Errors.Select(e => e.Description)));
        user.MustChangePassword = false;
        await Users.UpdateAsync(user);
    }

    public async Task<AuthResponse> AcceptInviteAsync(AcceptInviteRequest request, CancellationToken ct = default)
    {
        var invite = await Db.GroupInvitations.FirstOrDefaultAsync(i => i.Token == request.Token, ct)
                     ?? throw new NotFoundException("Invitation not found.");
        if (invite.Status != InvitationStatus.Pending || invite.ExpiresAt < DateTime.UtcNow)
            throw new BusinessRuleException("INVITE_INVALID", "This invitation is no longer valid.");

        var user = await Users.FindByEmailAsync(invite.Email);
        SetupService.ValidatePassword(request.Password);
        if (user is null)
        {
            if (!await _roles.RoleExistsAsync(SystemRoles.User))
                await _roles.CreateAsync(new ApplicationRole(SystemRoles.User));
            user = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                UserName = invite.Email,
                Email = invite.Email,
                FullName = request.FullName.Trim(),
                EmailConfirmed = true
            };
            var created = await Users.CreateAsync(user, request.Password);
            if (!created.Succeeded)
                throw new BusinessRuleException("IDENTITY", string.Join(" ", created.Errors.Select(e => e.Description)));
            await Users.AddToRoleAsync(user, SystemRoles.User);
        }
        else
        {
            if (!string.IsNullOrWhiteSpace(request.FullName))
                user.FullName = request.FullName.Trim();
            var resetToken = await Users.GeneratePasswordResetTokenAsync(user);
            var reset = await Users.ResetPasswordAsync(user, resetToken, request.Password);
            if (!reset.Succeeded)
                throw new BusinessRuleException("IDENTITY", string.Join(" ", reset.Errors.Select(e => e.Description)));
            await Users.UpdateAsync(user);
        }

        var groups = new GroupService(Db, Current, Users);
        await groups.JoinWithInviteTokenAsync(invite, user, ct);
        return await BuildAuthAsync(user, ct, _tokens);
    }

    public async Task<InvitePreviewDto> GetInvitePreviewAsync(string token, CancellationToken ct = default)
    {
        var invite = await Db.GroupInvitations.AsNoTracking().FirstOrDefaultAsync(row => row.Token == token, ct)
                     ?? throw new NotFoundException("Invitation not found.");
        if (invite.Status != InvitationStatus.Pending || invite.ExpiresAt < DateTime.UtcNow)
            throw new BusinessRuleException("INVITE_INVALID", "This invitation is no longer valid.");
        var group = await Db.StokvelGroups.AsNoTracking().FirstOrDefaultAsync(row => row.Id == invite.GroupId, ct)
                    ?? throw new NotFoundException("Stokvel not found.");
        return new InvitePreviewDto(invite.Email, group.Name, invite.ExpiresAt);
    }

    public async Task<ForgotPasswordResultDto> ForgotPasswordAsync(ForgotPasswordRequest request, CancellationToken ct = default)
    {
        var email = request.Email?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(email))
            throw new BusinessRuleException("EMAIL_REQUIRED", "Enter the email address for your account.");

        const string genericMessage = "If that email is registered, we sent a link to reset your password.";
        var user = await Users.FindByEmailAsync(email);
        if (user is null)
            return new ForgotPasswordResultDto(genericMessage, false, null);

        var token = await Users.GeneratePasswordResetTokenAsync(user);
        var resetUrl = MailLink.PasswordReset(
            MailLink.ResolveAppBaseUrl(_config, request.ClientOrigin),
            user.Email ?? email,
            token);

        try
        {
            await _emailSender.SendAsync(
                user.Email ?? email,
                "Reset your pkvela password",
                PasswordResetEmail.BuildHtml(user.FullName, resetUrl),
                ct);
            return new ForgotPasswordResultDto(genericMessage, true, null);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not email password reset to {Email}", user.Email);
            var developmentLink = _environment.IsDevelopment() ? resetUrl : null;
            var message = developmentLink is null
                ? genericMessage
                : $"{genericMessage} The email could not be sent from this machine. Use this link to reset your password.";
            return new ForgotPasswordResultDto(message, false, developmentLink);
        }
    }

    public async Task<AuthResponse> ResetPasswordAsync(ResetPasswordRequest request, CancellationToken ct = default)
    {
        SetupService.ValidatePassword(request.Password);
        var email = request.Email?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(request.Token))
            throw new BusinessRuleException("RESET_INVALID", "This password reset link is incomplete. Request a new one from the sign-in page.");

        var user = await Users.FindByEmailAsync(email)
                   ?? throw new BusinessRuleException("RESET_INVALID", "This password reset link is no longer valid.");
        var reset = await Users.ResetPasswordAsync(user, request.Token, request.Password);
        if (!reset.Succeeded)
            throw new BusinessRuleException("RESET_INVALID", "This password reset link is no longer valid. Request a new one from the sign-in page.");

        user.MustChangePassword = false;
        await Users.UpdateAsync(user);
        await AuditAsync("PASSWORD_RESET", $"{user.Email} reset their password.", ct: ct);
        await Db.SaveChangesAsync(ct);
        return await BuildAuthAsync(user, ct, _tokens);
    }
}
