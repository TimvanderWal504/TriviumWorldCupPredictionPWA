using TriviumWorldCup.Api.Domain;

public static class UserProfilesData
{
    public static IReadOnlyList<UserProfile> All
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
                    Id = adminUserId,
                    DisplayName = "Tim",
                    CountryCode = "NL",                    
                }
            ];
        }
    }
}