using Microsoft.EntityFrameworkCore;

namespace Stokvel.Infrastructure.Persistence;

public static class SchemaPatcher
{
    public static async Task ApplyAsync(StokvelDbContext db, CancellationToken cancellationToken = default)
    {
        await db.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS "CardPaymentSessions" (
                "Id" TEXT NOT NULL CONSTRAINT "PK_CardPaymentSessions" PRIMARY KEY,
                "GroupId" TEXT NOT NULL,
                "ContributionId" TEXT NOT NULL,
                "MemberId" TEXT NOT NULL,
                "UserId" TEXT NOT NULL,
                "Amount" TEXT NOT NULL,
                "Status" INTEGER NOT NULL,
                "ChallengeToken" TEXT NOT NULL,
                "ExpiresAt" TEXT NOT NULL,
                "ChallengedAt" TEXT NULL,
                "CompletedAt" TEXT NULL,
                "IssuerName" TEXT NULL,
                "CreatedAt" TEXT NOT NULL,
                "CreatedBy" TEXT NULL,
                "UpdatedAt" TEXT NULL,
                "UpdatedBy" TEXT NULL
            );
            """,
            cancellationToken);

        await db.Database.ExecuteSqlRawAsync(
            """
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_CardPaymentSessions_ChallengeToken"
            ON "CardPaymentSessions" ("ChallengeToken");
            """,
            cancellationToken);

        if (!await ColumnExistsAsync(db, "Notifications", "InvitationId", cancellationToken))
        {
            await db.Database.ExecuteSqlRawAsync(
                """ALTER TABLE "Notifications" ADD COLUMN "InvitationId" TEXT NULL;""",
                cancellationToken);
        }
    }

    private static async Task<bool> ColumnExistsAsync(
        StokvelDbContext db,
        string tableName,
        string columnName,
        CancellationToken cancellationToken)
    {
        var connection = db.Database.GetDbConnection();
        var shouldClose = connection.State != System.Data.ConnectionState.Open;
        if (shouldClose)
            await connection.OpenAsync(cancellationToken);

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = $"PRAGMA table_info(\"{tableName}\")";
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                if (string.Equals(reader.GetString(1), columnName, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }
        finally
        {
            if (shouldClose)
                await connection.CloseAsync();
        }
    }
}
