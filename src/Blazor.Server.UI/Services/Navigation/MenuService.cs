using Blazor.Server.UI.Models.SideMenu;
using CleanArchitecture.Blazor.Application.Constants.Role;

namespace Blazor.Server.UI.Services.Navigation;

public class MenuService : IMenuService
{
    private readonly List<MenuSectionModel> _features = new()
    {
        new MenuSectionModel
        {
            Title = "Application",
            SectionItems = new List<MenuSectionItemModel>
            {
                new() { Title = "Home", Icon = Icons.Material.Filled.Home, Href = "/" },
                new() { Title = "Images", Icon = Icons.Material.Filled.PhotoLibrary, Href = "/pages/Images" },
                new() { Title = "Samples", Icon = Icons.Material.Filled.SwitchAccount, Href = "/pages/Samples" },
                 
                 
            }
        },
        new MenuSectionModel
        {
            Title = "MANAGEMENT",
            Roles = new[] { RoleName.Administrator },
            SectionItems = new List<MenuSectionItemModel>
            {
                new()
                {
                    IsParent = true,
                    Title = "Authorization",
                    Icon = Icons.Material.Filled.ManageAccounts,
                    MenuItems = new List<MenuSectionSubItemModel>
                    {
                        new()
                        {
                            Title = "Multi-Tenant",
                            Href = "/system/tenants",
                            PageStatus = PageStatus.Completed
                        },
                        new()
                        {
                            Title = "Users",
                            Href = "/identity/users",
                            PageStatus = PageStatus.Completed
                        },
                        new()
                        {
                            Title = "Roles",
                            Href = "/identity/roles",
                            PageStatus = PageStatus.Completed
                        },
                        new()
                        {
                            Title = "Profile",
                            Href = "/user/profile",
                            PageStatus = PageStatus.Completed
                        }
                    }
                },
                new()
                {
                    IsParent = true,
                    Title = "System",
                    Icon = Icons.Material.Filled.Devices,
                    MenuItems = new List<MenuSectionSubItemModel>
                    {
                        new()
                        {
                            Title = "Folders",
                            Href = "/pages/Folders",
                            PageStatus = PageStatus.Completed
                        },
                        new()
                        {
                            Title = "Picklist",
                            Href = "/system/picklist",
                            PageStatus = PageStatus.Completed
                        },
                        new()
                        {
                            Title = "Audit Trails",
                            Href = "/system/audittrails",
                            PageStatus = PageStatus.Completed
                        },
                        new()
                        {
                            Title = "Log",
                            Href = "/system/logs",
                            PageStatus = PageStatus.Completed
                        },
                        new()
                        {
                            Title = "Jobs",
                            Href = "/jobs",
                            PageStatus = PageStatus.Completed,
                            Target = "_blank"
                        }
                    }
                }
            }
        }
    };

    public IEnumerable<MenuSectionModel> Features => _features;
}