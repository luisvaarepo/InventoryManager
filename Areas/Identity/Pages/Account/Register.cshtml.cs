using System.ComponentModel.DataAnnotations;
using System.Text;
using InventoryManagement.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;

namespace InventoryManagement.Areas.Identity.Pages.Account;

[AllowAnonymous]
public class RegisterModel : PageModel
{
    private readonly SignInManager<IdentityUser> _signInManager;
    private readonly UserManager<IdentityUser> _userManager;
    private readonly IUserStore<IdentityUser> _userStore;
    private readonly IUserEmailStore<IdentityUser> _emailStore;
    private readonly ILogger<RegisterModel> _logger;

    /// <summary>
    /// Initializes the register page model with identity services required for account creation.
    /// </summary>
    /// <param name="userManager">Identity user manager used to create users and assign roles.</param>
    /// <param name="userStore">Underlying user store used for username persistence.</param>
    /// <param name="signInManager">Sign-in manager used for post-registration authentication.</param>
    /// <param name="logger">Logger used for registration audit messages.</param>
    /// <remarks>
    /// Expected output: a page model ready to process local registration requests.
    /// Possible errors: may throw when email store support is unavailable.
    /// </remarks>
    public RegisterModel(
        UserManager<IdentityUser> userManager,
        IUserStore<IdentityUser> userStore,
        SignInManager<IdentityUser> signInManager,
        ILogger<RegisterModel> logger)
    {
        _userManager = userManager;
        _userStore = userStore;
        _emailStore = GetEmailStore();
        _signInManager = signInManager;
        _logger = logger;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public string? ReturnUrl { get; set; }

    public class InputModel
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        [StringLength(100, ErrorMessage = "The {0} must be at least {2} and at max {1} characters long.", MinimumLength = 6)]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        [DataType(DataType.Password)]
        [Display(Name = "Confirm password")]
        [Compare(nameof(Password), ErrorMessage = "The password and confirmation password do not match.")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }

    /// <summary>
    /// Handles GET requests for the registration page.
    /// </summary>
    /// <param name="returnUrl">Optional URL to redirect to after successful registration.</param>
    /// <remarks>
    /// Expected output: return URL preserved for the registration workflow.
    /// Possible errors: no custom exceptions are thrown by this handler.
    /// </remarks>
    public void OnGet(string? returnUrl = null)
    {
        ReturnUrl = returnUrl;
    }

    /// <summary>
    /// Validates registration input, creates an identity user, assigns default role, and signs in when allowed.
    /// </summary>
    /// <param name="returnUrl">Optional URL to redirect to after successful registration.</param>
    /// <returns>A page result on validation/creation failure or redirect result on success.</returns>
    /// <remarks>
    /// Expected output: user account created and assigned the staff role by default.
    /// Possible errors: identity operation failures are added to model state; store exceptions can propagate.
    /// </remarks>
    public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
    {
        returnUrl ??= Url.Content("~/");
        ReturnUrl = returnUrl;

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var user = CreateUser();
        await _userStore.SetUserNameAsync(user, Input.Email, CancellationToken.None);
        await _emailStore.SetEmailAsync(user, Input.Email, CancellationToken.None);

        var result = await _userManager.CreateAsync(user, Input.Password);
        if (result.Succeeded)
        {
            await _userManager.AddToRoleAsync(user, Roles.Manager);
            _logger.LogInformation("User created a new account with password.");

            var userId = await _userManager.GetUserIdAsync(user);
            var code = await _userManager.GenerateEmailConfirmationTokenAsync(user);
            code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));
            var callbackUrl = Url.Page(
                "/Account/ConfirmEmail",
                pageHandler: null,
                values: new { area = "Identity", userId, code, returnUrl },
                protocol: Request.Scheme);

            if (_userManager.Options.SignIn.RequireConfirmedAccount)
            {
                return RedirectToPage("RegisterConfirmation", new { email = Input.Email, returnUrl, callbackUrl });
            }

            await _signInManager.SignInAsync(user, isPersistent: false);
            return LocalRedirect(returnUrl);
        }

        foreach (var error in result.Errors)
        {
            ModelState.AddModelError(string.Empty, error.Description);
        }

        return Page();
    }

    /// <summary>
    /// Creates a new identity user instance for registration.
    /// </summary>
    /// <returns>A new <see cref="IdentityUser"/> instance.</returns>
    /// <remarks>
    /// Expected output: user instance suitable for identity persistence.
    /// Possible errors: throws <see cref="InvalidOperationException"/> when user creation fails.
    /// </remarks>
    private IdentityUser CreateUser()
    {
        try
        {
            return Activator.CreateInstance<IdentityUser>();
        }
        catch
        {
            throw new InvalidOperationException($"Can't create an instance of '{nameof(IdentityUser)}'.");
        }
    }

    /// <summary>
    /// Returns an email-capable user store required by the default identity UI.
    /// </summary>
    /// <returns>Email-capable user store implementation.</returns>
    /// <remarks>
    /// Expected output: user store cast that supports email operations.
    /// Possible errors: throws <see cref="NotSupportedException"/> when email support is unavailable.
    /// </remarks>
    private IUserEmailStore<IdentityUser> GetEmailStore()
    {
        if (!_userManager.SupportsUserEmail)
        {
            throw new NotSupportedException("The default UI requires a user store with email support.");
        }

        return (IUserEmailStore<IdentityUser>)_userStore;
    }
}
