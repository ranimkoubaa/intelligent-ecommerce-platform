using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using ProductManagement.Models.Repositories;
using ProductManagement.ViewModels;

namespace ProductManagement.Controllers
{
    public class AccountController : Controller
    {
        private readonly UserManager<IdentityUser> userManager;
        private readonly SignInManager<IdentityUser> signInManager;
        private readonly ICategorieRepository categorieRepository;

        public AccountController(
            UserManager<IdentityUser> userManager,
            SignInManager<IdentityUser> signInManager,
            ICategorieRepository categorieRepository)
        {
            this.userManager = userManager;
            this.signInManager = signInManager;
            this.categorieRepository = categorieRepository;
        }

        [HttpGet]
        public IActionResult Register()
        {
            ViewData["Categories"] = categorieRepository.GetAll();
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            if (ModelState.IsValid)
            {
                var user = new IdentityUser
                {
                    UserName = model.Email,
                    Email = model.Email
                };

                var result = await userManager.CreateAsync(
                    user,
                    model.Password);

                if (result.Succeeded)
                {
                    await signInManager.SignInAsync(
                        user,
                        isPersistent: false);

                    return RedirectToAction("Index", "Home");
                }

                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(
                        string.Empty,
                        error.Description);
                }
            }

            ViewData["Categories"] = categorieRepository.GetAll();
            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> Logout()
        {
            await signInManager.SignOutAsync();
            return RedirectToAction("Index", "Home");
        }

        [HttpGet]
        public IActionResult Login()
        {
            ViewData["Categories"] = categorieRepository.GetAll();
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl)
        {
            if (ModelState.IsValid)
            {
                var result = await signInManager.PasswordSignInAsync(
                    model.Email,
                    model.Password,
                    model.RememberMe,
                    false);

                if (result.Succeeded)
                {
                    if (!string.IsNullOrEmpty(returnUrl))
                    {
                        return LocalRedirect(returnUrl);
                    }
                    else
                    {
                        return RedirectToAction("Index", "Home");
                    }
                }

                ModelState.AddModelError(string.Empty, "Invalid Login Attempt");
            }

            ViewData["Categories"] = categorieRepository.GetAll();
            return View(model);
        }

        [AllowAnonymous]
        public IActionResult AccessDenied()
        {
            ViewData["Categories"] = categorieRepository.GetAll();
            return View();
        }
    }
}