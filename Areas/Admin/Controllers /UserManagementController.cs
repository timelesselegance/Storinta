using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HumanBodyWeb.Models;

namespace HumanBodyWeb.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class UserManagementController : Controller
    {
        private readonly UserManager<ApplicationUser> _userMgr;
        private readonly RoleManager<IdentityRole> _roleMgr;

        public UserManagementController(
            UserManager<ApplicationUser> userMgr,
            RoleManager<IdentityRole> roleMgr)
        {
            _userMgr = userMgr;
            _roleMgr = roleMgr;
        }

        // GET: /Admin/UserManagement
        public async Task<IActionResult> Index()
        {
            var users = _userMgr.Users.ToList();
            var model = new List<UserRolesViewModel>();

            foreach (var u in users)
            {
                var roles = await _userMgr.GetRolesAsync(u);
                model.Add(new UserRolesViewModel
                {
                    UserId = u.Id,
                    Email  = u.Email!,
                    Roles = roles
                });
            }

            return View(model);
        }

        // GET: /Admin/UserManagement/EditRoles/{userId}
        public async Task<IActionResult> EditRoles(string userId)
        {
            var user = await _userMgr.FindByIdAsync(userId);
            if (user == null)
                return NotFound();

            var allRoles = _roleMgr.Roles.Select(r => r.Name!).ToList();
            var userRoles = await _userMgr.GetRolesAsync(user);

            var vm = new EditUserRolesViewModel
            {
                UserId = user.Id,
                Email = user.Email!,
                Roles = allRoles.Select(r => new RoleCheckbox
                {
                    RoleName = r,
                    IsSelected = userRoles.Contains(r)
                }).ToList()
            };

            return View(vm);
        }

        // POST: /Admin/UserManagement/EditRoles/{userId}
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditRoles(EditUserRolesViewModel vm)
        {
            var user = await _userMgr.FindByIdAsync(vm.UserId);
            if (user == null)
                return NotFound();

            var currentRoles = await _userMgr.GetRolesAsync(user);
            // Remove all
            await _userMgr.RemoveFromRolesAsync(user, currentRoles);

            // Add selected
            var selectedRoles = vm.Roles.Where(r => r.IsSelected).Select(r => r.RoleName);
            await _userMgr.AddToRolesAsync(user, selectedRoles);

            return RedirectToAction(nameof(Index));
        }
    }

    // ViewModel: Kullanıcı ve mevcut roller
    public class UserRolesViewModel
    {
        public string UserId { get; set; } = null!;
        public string Email { get; set; } = null!;
        public IList<string> Roles { get; set; } = null!;
    }

    // ViewModel: Edit Roles sayfası için
    public class EditUserRolesViewModel
    {
        public string UserId { get; set; } = null!;
        public string Email { get; set; } = null!;
        public List<RoleCheckbox> Roles { get; set; } = null!;
    }

    // Role checkbox
    public class RoleCheckbox
    {
        public string RoleName { get; set; } = null!;
        public bool IsSelected { get; set; }
    }
}