namespace MarketplaceApi.Services
{
    public interface IPasswordHasher
    {
        string Hash(string password);
        bool Verify(string password, string hash);
    }

    public class BcryptPasswordHasher : IPasswordHasher
    {
        // Work factor 12 -> good balance of security/performance in 2026.
        private const int WorkFactor = 12;

        public string Hash(string password) =>
            BCrypt.Net.BCrypt.HashPassword(password, workFactor: WorkFactor);

        public bool Verify(string password, string hash) =>
            BCrypt.Net.BCrypt.Verify(password, hash);
    }
}
