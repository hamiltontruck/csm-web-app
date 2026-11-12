using System.Reflection;
using System.Security.Claims;
using CSM.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CSM.Infrastructure.Tests;

public sealed class FinanceOfficerInvoiceLineApiTests
{
    [Theory]
    [InlineData("GetByInvoice", true)] [InlineData("GetByInvoice", false)]
    [InlineData("GetById", true)] [InlineData("GetById", false)]
    [InlineData("Create", true)] [InlineData("Create", false)]
    [InlineData("Update", true)] [InlineData("Update", false)]
    [InlineData("Delete", true)] [InlineData("Delete", false)]
    public async Task Officer_invoice_line_HTTP_boundary_requires_the_current_invoice_grant(string action, bool granted)
    {
        var registrations = new ServiceCollection().AddLogging().AddGrantedBackendPermissions("FinanceOfficer",
            granted ? [GranularPermission.Finance_Invoice_View] : []);
        using var services = registrations.BuildServiceProvider();
        var controller = typeof(CSM.Api.Controllers.InvoiceLinesController);
        var metadata = controller.GetCustomAttributes<AuthorizeAttribute>()
            .Concat(controller.GetMethod(action)!.GetCustomAttributes<AuthorizeAttribute>());
        var policy = await AuthorizationPolicy.CombineAsync(services.GetRequiredService<IAuthorizationPolicyProvider>(), metadata);
        var principal = BackendAuthorizationFixture.WithGrantedIdentity(new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.Role, "FinanceOfficer")], "test")));
        var result = await services.GetRequiredService<IAuthorizationService>().AuthorizeAsync(principal, null, policy!);
        Assert.Equal(granted, result.Succeeded);
    }
}
