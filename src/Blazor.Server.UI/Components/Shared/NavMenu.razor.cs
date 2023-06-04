using Blazor.Server.UI.Pages.Documents;
using Blazor.Server.UI.Shared;
using CleanArchitecture.Blazor.Application.Constants.Permission;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;

namespace Blazor.Server.UI.Components.Shared;

public partial class NavMenu
{
    [EditorRequired] [Parameter] public bool IsDarkMode { get; set; }
    [EditorRequired] [Parameter] public bool SideMenuDrawerOpen { get; set; }
    [EditorRequired] [Parameter] public EventCallback ToggleSideMenuDrawer { get; set; }
    [EditorRequired] [Parameter] public EventCallback OpenCommandPalette { get; set; }
    [EditorRequired] [Parameter] public bool RightToLeft { get; set; }
    [EditorRequired] [Parameter] public EventCallback RightToLeftToggle { get; set; }
    [Parameter] public EventCallback<MouseEventArgs> OnSettingClick { get; set; }

    [CascadingParameter]
    private Task<AuthenticationState> AuthState { get; set; } = default!;

    private bool _canUpload;
    protected override async Task OnInitializedAsync()
    {
        var state = await AuthState;
        _canUpload = (await AuthService.AuthorizeAsync(state.User, Permissions.Images.Upload)).Succeeded;

    }
        private async Task OnUploadImages()
    {
        var parameters = new DialogParameters
            {
                
            };
        var options = new DialogOptions { CloseButton = true, MaxWidth = MaxWidth.Small, FullWidth = true };
        var dialog = DialogService.Show<UploadImagesDialog> ("Upload Images", parameters, options);
        var state = await dialog.Result;
    }

}