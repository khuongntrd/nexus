using Microsoft.AspNetCore.Components;

namespace Nexus.Web.Components.Pages;

public partial class Connectors
{
    [Inject]
    private NavigationManager Navigation { get; set; } = default!;

    protected override void OnInitialized()
    {
        Navigation.NavigateTo("/settings", true);
    }
}
