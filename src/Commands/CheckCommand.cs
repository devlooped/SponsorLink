using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Devlooped.Sponsors;

[Description("Checks whether a given email/domain is sponsoring a given user/org.")]
public class CheckCommand : Command<CheckCommand.CheckSettings>
{
    // SponsorLink public key, used to verify the manifest signature.
    const string PublicKey = "MIIBCgKCAQEAo5bLk9Iim5twrxGVzJ4OxfxDuvy3ladTNvcFNv4Hm9/1No89SISKTXZ1bSABTnqH6z/DpklcHveGMSmsncEvUebrg7tX6+M3byVXU6Q/d82PtwgbDXT9d10A4lePS2ioJQqlHWQy/fuNwe7FjptV+yguf5IUxVRdZ77An1IyGUk9Cj6n4RuYIPrP5O0AmFPHOwEzywUWVaV1NHYRe0Th6i5/hyDV13K7+LP9VzwucnWEvzujtnL6ywZDeaKkwfeFsXZyYywHj6oJK9Obed/nu1e+69fmUqprtc0t/3A9uHc0G/0sDNLLAd83j2NSOS2IHJo17azOLFuhekka8dSKnQIDAQAB";

    public class CheckSettings(string email, string sponsorable) : CommandSettings
    {
        [Description("Email (or domain) to check for an active sponsorship to the sponsorable account.")]
        [CommandArgument(0, "<email>")]
        public string Email => email;

        [Description("GitHub account name of the sponsorable user or organization to check.")]
        [CommandArgument(1, "<sponsorable>")]
        public string Sponsorable => sponsorable;
    }

    public override int Execute([NotNull] CommandContext context, [NotNull] CheckSettings settings)
    {
#if NET6_0_OR_GREATER
        // NOTE: we have the Manifest class as well as convenience Variables and Constants classes
        // in this project, but we want this to serve as an example of how to perform the check from
        // anywhere that only has access to the environment variables and SponsorLink public key.

        if (Environment.GetEnvironmentVariable("SPONSORLINK_MANIFEST", EnvironmentVariableTarget.User) is not string manifest ||
            string.IsNullOrEmpty(manifest) ||
            Environment.GetEnvironmentVariable("SPONSORLINK_INSTALLATION", EnvironmentVariableTarget.User) is not string installation ||
            string.IsNullOrEmpty(installation))
        {
            AnsiConsole.MarkupLine("[red]x[/] No manifest found. Run [yellow]gh sponsors[/] first.");
            return -1;
        }

        var rsa = RSA.Create();
        rsa.ImportRSAPublicKey(Convert.FromBase64String(PublicKey), out _);

        var validation = new TokenValidationParameters
        {
            RequireExpirationTime = true,
            ValidAudience = "SponsorLink",
            ValidIssuer = "Devlooped",
            // NOTE: setting this to false allows checking sponsorships even when the manifest is expired. 
            // This might be useful if package authors want to extend the manifest lifetime beyond the default 
            // 30 days and issue a warning on expiration, rather than an error and a forced sync.
            // If this is not set (or true), the catch for SecurityTokenExpiredException will be hit instead.
            ValidateLifetime = false,
            IssuerSigningKey = new RsaSecurityKey(rsa)
        };

        try
        {
            var principal = new JwtSecurityTokenHandler().ValidateToken(manifest, validation, out var securityToken);

            if (securityToken.ValidTo < DateTime.UtcNow)
            {
                AnsiConsole.MarkupLine("$[red]x[/] The manifest expired on {securityToken.ValidTo:yyyy-MM-dd}. Run [yellow]gh sponsors[/] to refresh.");
                return -2;
            }

            var hash = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(installation + settings.Email + settings.Sponsorable)));
            if (principal.Claims.Any(c => c.Type == "hash" && c.Value == hash))
            {
                AnsiConsole.MarkupLine($"[green]✓[/] [green]{settings.Sponsorable}[/] has an active sponsorship from [blue]{settings.Email}[/].");
                return 0;
            }

            AnsiConsole.MarkupLine($"[orange1]![/] [yellow]{settings.Sponsorable}[/] is not sponsored by [blue]{settings.Email}[/].");
        }
        catch (SecurityTokenExpiredException)
        {
            AnsiConsole.MarkupLine("[red]x[/] The manifest has expired. Run [yellow]gh sponsors[/] to generate a new one.");
            return -2;
        }
        catch (SecurityTokenInvalidSignatureException)
        {
            AnsiConsole.MarkupLine("[red]x[/] The manifest signature is invalid. Run [yellow]gh sponsors[/] to generate a new one.");
            return -3;
        }
        catch (SecurityTokenException ex)
        {
            AnsiConsole.MarkupLine($"[red]x[/] The manifest is invalid. Run [yellow]gh sponsors[/] to generate a new one.");
            AnsiConsole.WriteException(ex);
            return -4;
        }

        return 0;
#else
        AnsiConsole.MarkupLine("[red]x[/] This command requires .NET 6 or newer.");
        return -1;
#endif
    }
}
