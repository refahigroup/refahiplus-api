namespace Identity.Domain.Aggregates;

public class UserAggregate
{
    public string Id { get; private set; }
    public string Username { get; private set; }
    public string PasswordHash { get; private set; }
    public string Roles { get; private set; }

    public UserAggregate(string id, string username, string passwordHash, string roles)
    {
        Id = id;
        Username = username;
        PasswordHash = passwordHash;
        Roles = roles;
    }

    public static UserAggregate Create(string id, string username, string plainPassword, string roles)
    {
        var hash = BCrypt.Net.BCrypt.HashPassword(plainPassword);
        return new UserAggregate(id, username, hash, roles);
    }

    public bool VerifyPassword(string plainPassword)
    {
        return BCrypt.Net.BCrypt.Verify(plainPassword, PasswordHash);
    }

    public void SetPassword(string plainPassword)
    {
        PasswordHash = BCrypt.Net.BCrypt.HashPassword(plainPassword);
    }
}
