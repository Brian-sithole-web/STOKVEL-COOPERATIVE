using Stokvel.Application.Dtos;

namespace Stokvel.Application.Common;

public record AuthUser(
    Guid Id,
    string Email,
    string FullName,
    bool IsPlatformAdmin,
    bool MustChangePassword,
    IReadOnlyList<GroupMembershipDto> Memberships);

public record PagedResult<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize);

public interface ICurrentUser
{
    Guid UserId { get; }
    string Email { get; }
    string FullName { get; }
    bool IsPlatformAdmin { get; }
    bool IsAuthenticated { get; }
}
