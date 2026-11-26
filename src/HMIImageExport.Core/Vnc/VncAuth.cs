using System.Security.Cryptography;
using System.Text;

namespace HMIImageExport.Core.Vnc;

public sealed class VncException : Exception
{
    public VncException(string message) : base(message) { }
    public VncException(string message, Exception inner) : base(message, inner) { }
}

/// <summary>VNC-Authentifizierung (RFB Sicherheitsart 2): DES-Challenge/Response, Passwort max. 8 Zeichen.</summary>
public static class VncAuth
{
    public static byte[] Response(byte[] challenge, string? password)
    {
        if (challenge.Length != 16) throw new ArgumentException("Challenge muss 16 Byte lang sein.", nameof(challenge));

        // VNC-Eigenheit: jedes Schlüsselbyte wird bitweise gespiegelt; Passwort wird auf 8 Byte gekürzt/mit 0 aufgefüllt.
        var key = new byte[8];
        var pw = Encoding.Latin1.GetBytes(password ?? "");
        for (var i = 0; i < 8 && i < pw.Length; i++) key[i] = ReverseBits(pw[i]);

        using var des = DES.Create();
        des.Mode = CipherMode.ECB;
        des.Padding = PaddingMode.None;
        try { des.Key = key; }
        catch (CryptographicException ex)
        {
            throw new VncException("Das Passwort ergibt einen unzulässigen DES-Schlüssel (leeres oder ungewöhnliches Passwort).", ex);
        }
        using var enc = des.CreateEncryptor();
        return enc.TransformFinalBlock(challenge, 0, 16);
    }

    private static byte ReverseBits(byte b)
    {
        byte r = 0;
        for (var i = 0; i < 8; i++) { r = (byte)((r << 1) | (b & 1)); b >>= 1; }
        return r;
    }
}
