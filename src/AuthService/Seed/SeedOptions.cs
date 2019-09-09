using IdentityServer4.Models;

namespace AuthService.Seed
{
    public class SeedOptions
    {
        public string AdminRole { get; set; } = "Administrator";
        public AdminUserDefinition[] AdminUsers { get; set; } = new AdminUserDefinition[0];
        public Client[] Clients { get; set; } = new Client[0];

        public IdentityResource[] IdentityResources { get; set; } =
        {
            new IdentityResources.OpenId(),
            new IdentityResources.Profile(),
            new IdentityResources.Email(),
        };
        public ApiResource[] ApiResources { get; set; } = new ApiResource[0];
    }
}
