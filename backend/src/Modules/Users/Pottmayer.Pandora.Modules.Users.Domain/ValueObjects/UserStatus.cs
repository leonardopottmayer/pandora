using Pottmayer.Pandora.Shared.Domain;

namespace Pottmayer.Pandora.Modules.Users.Domain.ValueObjects;

public sealed record UserStatus : IDomainValue<UserStatus>
{
    public string Value { get; }

    private UserStatus(string value) => Value = value;

    public static readonly UserStatus Active  = new("active");
    public static readonly UserStatus Blocked = new("blocked");

    public static UserStatus FromValue(string value) => value switch
    {
        "active"  => Active,
        "blocked" => Blocked,
        _         => new UserStatus(value)
    };

    public override string ToString() => Value;
}
