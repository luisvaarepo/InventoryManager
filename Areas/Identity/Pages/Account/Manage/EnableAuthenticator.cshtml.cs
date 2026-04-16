using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace InventoryManagement.Areas.Identity.Pages.Account.Manage;

public class EnableAuthenticatorModel : PageModel
{
    private readonly UserManager<IdentityUser> _userManager;
    private readonly ILogger<EnableAuthenticatorModel> _logger;
    private readonly UrlEncoder _urlEncoder;

    private const string AuthenticatorUriFormat = "otpauth://totp/{0}:{1}?secret={2}&issuer={0}&digits=6";

    /// <summary>
    /// Initializes the page model used to configure an authenticator app for two-factor authentication.
    /// </summary>
    /// <param name="userManager">Identity user manager used for key reset, token verification, and 2FA enablement.</param>
    /// <param name="logger">Logger used for audit and diagnostic events in the 2FA setup flow.</param>
    /// <param name="urlEncoder">URL encoder used to safely format the authenticator URI payload.</param>
    /// <remarks>
    /// Expected output: model instance ready to load setup data and process authenticator verification.
    /// Possible errors: dependency injection failures can occur when required identity services are unavailable.
    /// </remarks>
    public EnableAuthenticatorModel(
        UserManager<IdentityUser> userManager,
        ILogger<EnableAuthenticatorModel> logger,
        UrlEncoder urlEncoder)
    {
        _userManager = userManager;
        _logger = logger;
        _urlEncoder = urlEncoder;
    }

    [TempData]
    public string? StatusMessage { get; set; }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public string SharedKey { get; private set; } = string.Empty;

    public string AuthenticatorUri { get; private set; } = string.Empty;

    public class InputModel
    {
        [Required]
        [StringLength(7, ErrorMessage = "The {0} must be at least {2} and at max {1} characters long.", MinimumLength = 6)]
        [DataType(DataType.Text)]
        [Display(Name = "Verification Code")]
        public string Code { get; set; } = string.Empty;
    }

    /// <summary>
    /// Loads the current user authenticator setup key and QR URI for initial page rendering.
    /// </summary>
    /// <returns>A page result with key and URI data, or a not-found result when the user is missing.</returns>
    /// <remarks>
    /// Expected output: <see cref="SharedKey"/> and <see cref="AuthenticatorUri"/> are populated for display.
    /// Possible errors: identity data access exceptions can propagate during key retrieval.
    /// </remarks>
    public async Task<IActionResult> OnGetAsync()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null)
        {
            return NotFound($"Unable to load user with ID '{_userManager.GetUserId(User)}'.");
        }

        await LoadSharedKeyAndQrCodeUriAsync(user);
        return Page();
    }

    /// <summary>
    /// Verifies the authenticator app code and enables two-factor authentication for the current user.
    /// </summary>
    /// <returns>A page result when validation fails, or redirect to 2FA settings after successful enablement.</returns>
    /// <remarks>
    /// Expected output: two-factor authentication is enabled and recovery code generation is initiated.
    /// Possible errors: user lookup failures return not-found; identity operations can fail and update model state.
    /// </remarks>
    public async Task<IActionResult> OnPostAsync()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null)
        {
            return NotFound($"Unable to load user with ID '{_userManager.GetUserId(User)}'.");
        }

        if (!ModelState.IsValid)
        {
            await LoadSharedKeyAndQrCodeUriAsync(user);
            return Page();
        }

        var verificationCode = Input.Code.Replace(" ", string.Empty, StringComparison.Ordinal)
            .Replace("-", string.Empty, StringComparison.Ordinal);

        var is2FaTokenValid = await _userManager.VerifyTwoFactorTokenAsync(
            user,
            _userManager.Options.Tokens.AuthenticatorTokenProvider,
            verificationCode);

        if (!is2FaTokenValid)
        {
            ModelState.AddModelError("Input.Code", "Verification code is invalid.");
            await LoadSharedKeyAndQrCodeUriAsync(user);
            return Page();
        }

        await _userManager.SetTwoFactorEnabledAsync(user, true);
        var userId = await _userManager.GetUserIdAsync(user);
        _logger.LogInformation("User with ID '{UserId}' has enabled 2FA with an authenticator app.", userId);

        StatusMessage = "Your authenticator app has been verified.";
        return RedirectToPage("./GenerateRecoveryCodes");
    }

    /// <summary>
    /// Retrieves or resets the authenticator key and prepares formatted key and QR code URI values.
    /// </summary>
    /// <param name="user">Current identity user for whom setup data is generated.</param>
    /// <returns>A task representing asynchronous key retrieval and URI generation.</returns>
    /// <remarks>
    /// Expected output: setup values are available for the 2FA configuration page.
    /// Possible errors: identity store operations can propagate exceptions when retrieval or reset fails.
    /// </remarks>
    private async Task LoadSharedKeyAndQrCodeUriAsync(IdentityUser user)
    {
        var unformattedKey = await _userManager.GetAuthenticatorKeyAsync(user);
        if (string.IsNullOrWhiteSpace(unformattedKey))
        {
            await _userManager.ResetAuthenticatorKeyAsync(user);
            unformattedKey = await _userManager.GetAuthenticatorKeyAsync(user);
        }

        SharedKey = FormatKey(unformattedKey ?? string.Empty);

        var email = await _userManager.GetEmailAsync(user) ?? await _userManager.GetUserNameAsync(user) ?? user.Id;
        var issuer = _userManager.Options.Tokens.AuthenticatorIssuer ?? "InventoryManagement";
        AuthenticatorUri = GenerateQrCodeUri(issuer, email, unformattedKey ?? string.Empty);
    }

    /// <summary>
    /// Formats a raw authenticator key into readable 4-character groups.
    /// </summary>
    /// <param name="unformattedKey">Raw secret key emitted by the identity authenticator provider.</param>
    /// <returns>Grouped key string suitable for manual entry in authenticator apps.</returns>
    /// <remarks>
    /// Expected output: a lowercase grouped key such as xxxx xxxx xxxx.
    /// Possible errors: no custom exceptions are thrown; empty input produces an empty output string.
    /// </remarks>
    private static string FormatKey(string unformattedKey)
    {
        var result = new StringBuilder();
        var currentPosition = 0;

        while (currentPosition + 4 < unformattedKey.Length)
        {
            result.Append(unformattedKey.AsSpan(currentPosition, 4)).Append(' ');
            currentPosition += 4;
        }

        if (currentPosition < unformattedKey.Length)
        {
            result.Append(unformattedKey.AsSpan(currentPosition));
        }

        return result.ToString().ToLower(CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Generates the `otpauth` URI used by authenticator apps and QR code renderers.
    /// </summary>
    /// <param name="issuer">Authenticator issuer label shown in the app account list.</param>
    /// <param name="email">Account identifier displayed by the authenticator app.</param>
    /// <param name="unformattedKey">Raw secret key used for TOTP generation.</param>
    /// <returns>URL-encoded `otpauth` URI that can be embedded into a QR code.</returns>
    /// <remarks>
    /// Expected output: standards-compliant TOTP URI accepted by major authenticator apps.
    /// Possible errors: no custom exceptions are thrown by this formatter.
    /// </remarks>
    private string GenerateQrCodeUri(string issuer, string email, string unformattedKey)
    {
        return string.Format(
            CultureInfo.InvariantCulture,
            AuthenticatorUriFormat,
            _urlEncoder.Encode(issuer),
            _urlEncoder.Encode(email),
            unformattedKey);
    }
}
