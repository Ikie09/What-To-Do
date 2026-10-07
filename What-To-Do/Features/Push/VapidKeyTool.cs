using System.Security.Cryptography;
using Microsoft.AspNetCore.WebUtilities;

namespace What_To_Do.Features.Push;

public static class VapidKeyTool
{
    // VAPID keys are a P-256 key pair: the public key is the 65-byte uncompressed point, the private key is the
    // 32-byte scalar, both base64url encoded (the format browsers and push libraries expect).
    public static (string PublicKey, string PrivateKey) GenerateKeys()
    {
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var parameters = ecdsa.ExportParameters(includePrivateParameters: true);

        var publicKey = new byte[65];
        publicKey[0] = 0x04;
        parameters.Q.X!.CopyTo(publicKey, 1);
        parameters.Q.Y!.CopyTo(publicKey, 33);

        return (WebEncoders.Base64UrlEncode(publicKey), WebEncoders.Base64UrlEncode(parameters.D!));
    }

    // Prints a new VAPID pair with the exact commands to store it in user-secrets (never commit these).
    public static void PrintNewKeys()
    {
        var (publicKey, privateKey) = GenerateKeys();

        Console.WriteLine("New VAPID keys for Web Push. Keep the private key secret.");
        Console.WriteLine();
        Console.WriteLine("Run these in the What-To-Do project folder (use your own email address):");
        Console.WriteLine();
        Console.WriteLine($"  dotnet user-secrets set \"Push:VapidPublicKey\" \"{publicKey}\"");
        Console.WriteLine($"  dotnet user-secrets set \"Push:VapidPrivateKey\" \"{privateKey}\"");
        Console.WriteLine("  dotnet user-secrets set \"Push:Subject\" \"mailto:you@example.com\"");
        Console.WriteLine();
        Console.WriteLine("Changing the keys later invalidates every existing push subscription (devices must turn push on again).");
    }
}
