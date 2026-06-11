using TriviumWorldCup.Api.Auth.Link;

public static class InviteUsersData
{
    public static IReadOnlyList<InviteUser> All
    {
        get
        {
            var adminUserId = Environment.GetEnvironmentVariable("ADMIN_USER_ID");
            
            if (string.IsNullOrWhiteSpace(adminUserId))
            {
                throw new InvalidOperationException("ADMIN_USER_ID environment variable is not configured.");
            }

            return
            [
                new()
                {
                    Id          = adminUserId,
                    DisplayName = "Tim",
                    Email       = "tim.vanderwal@trivium-esolutions.com",
                    CreatedAt   = DateTimeOffset.UtcNow,
                    Roles       = ["user", "admin"],
                }
            ];
        }
    }
}