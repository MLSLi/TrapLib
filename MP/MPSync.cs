using System;

namespace TrapLib.MP;

/// <summary>Multiplayer state helpers — all safe to call without KrokMP installed.</summary>
public static class MPSync
{
    private static bool? _isClient;

    /// <summary>True when running single-player or as the MP host/server.</summary>
    public static bool IsServerOrSP => !IsClient;

    /// <summary>True when this instance is a pure KrokMP client (network up, not server).</summary>
    public static bool IsClient
    {
        get
        {
            if (_isClient.HasValue) return _isClient.Value;
            var t = Type.GetType("KrokoshaCasualtiesMP.KrokoshaScavMultiplayer, KrokoshaCasualtiesMP");
            if (t == null) { _isClient = false; return false; }
            try
            {
                var running = (bool)t.GetProperty("network_system_is_running").GetValue(null);
                var server  = (bool)t.GetProperty("is_server").GetValue(null);
                _isClient = running && !server;
            }
            catch { _isClient = false; }
            return _isClient.Value;
        }
    }

    /// <summary>Call once per session to invalidate the cached MP state.</summary>
    public static void Refresh()
    {
        _isClient = null;
    }
}
