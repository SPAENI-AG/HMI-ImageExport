namespace HMIImageExport.Core;

/// <summary>Ein Bild des Panels als BGRA32 (4 Byte pro Pixel, Zeile = Width*4 Byte).</summary>
public sealed record FrameSnapshot(int Width, int Height, byte[] Bgra);

/// <summary>
/// Schnittstelle zur VNC-Bibliothek. Die UI kennt nur diese Schnittstelle,
/// damit die Bibliothek (oder ein eigener Client) austauschbar bleibt.
/// </summary>
public interface IVncSession : IAsyncDisposable
{
    bool IsConnected { get; }
    event EventHandler? FrameUpdated;
    event EventHandler<string?>? Disconnected;

    Task ConnectAsync(string host, int port, string password, CancellationToken ct);
    Task DisconnectAsync();

    /// <summary>Touch-Tap: Maustaste links drücken und loslassen an Panel-Pixel (x, y).</summary>
    void Tap(int x, int y);

    /// <summary>Aktuelles Bild in Originalauflösung, oder null ohne Verbindung.</summary>
    FrameSnapshot? GetSnapshot();
}
