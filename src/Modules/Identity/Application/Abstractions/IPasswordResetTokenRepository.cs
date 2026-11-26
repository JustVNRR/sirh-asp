using Sirh.Modules.Identity.Domain.Entities;

namespace Sirh.Modules.Identity.Application.Abstractions;

public interface IPasswordResetTokenRepository
{
    Task<PasswordResetToken?> GetLatestByUserIdAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<PasswordResetToken?> GetLatestUsableByUserIdAsync(
        Guid userId,
        DateTime utcNow,
        CancellationToken cancellationToken = default);

    Task AddAsync(PasswordResetToken token, CancellationToken cancellationToken = default);

    Task InvalidateActiveForUserAsync(
        Guid userId,
        DateTime invalidatedAtUtc,
        CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
