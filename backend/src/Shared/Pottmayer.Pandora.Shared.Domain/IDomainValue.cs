namespace Pottmayer.Pandora.Shared.Domain;

public interface IDomainValue<TSelf> where TSelf : IDomainValue<TSelf>
{
    string Value { get; }
    static abstract TSelf FromValue(string value);
}
