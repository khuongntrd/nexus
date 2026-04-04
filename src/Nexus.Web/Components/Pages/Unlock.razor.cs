using Microsoft.AspNetCore.Components;

namespace Nexus.Web.Components.Pages;

public partial class Unlock : ComponentBase
{
    [SupplyParameterFromQuery]
    public string? ReturnUrl { get; set; }

    [SupplyParameterFromQuery]
    public string? Error { get; set; }

    private string SignInUrl
        => $"/auth/signin?returnUrl={Uri.EscapeDataString(string.IsNullOrWhiteSpace(ReturnUrl) ? "/" : ReturnUrl)}";
}
