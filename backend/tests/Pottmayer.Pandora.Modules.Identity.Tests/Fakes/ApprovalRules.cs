using Microsoft.Extensions.Options;
using Pottmayer.Pandora.Modules.Identity.Application.Options;
using Pottmayer.Pandora.Modules.Identity.Application.Services;

namespace Pottmayer.Pandora.Modules.Identity.Tests.Fakes;

internal static class ApprovalRules
{
    /// <summary>Approval switched off: what every install has by default.</summary>
    public static AccountApprovalRule Off { get; } = new(Options.Create(new AccountApprovalOptions()));

    public static AccountApprovalRule Required(params string[] preApproved) =>
        new(Options.Create(new AccountApprovalOptions { Required = true }), new FakeAccountApprovalPolicy(preApproved));
}
