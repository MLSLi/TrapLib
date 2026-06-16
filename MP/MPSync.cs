using System;
using UnityEngine;

namespace TrapLib.MP;

/// <summary>Multiplayer state helpers — all safe to call without KrokMP installed.</summary>
public static class MPSync
{
    private static Type _krokMPType;
    private static bool _krokMPChecked;

    /// <summary>True when running single-player or as the MP host/server.</summary>
    public static bool IsServerOrSP => !IsClient;

    /// <summary>True when this instance is a pure KrokMP client (network up, not server).</summary>
    public static bool IsClient
    {
        get
        {
            if (!_krokMPChecked)
            {
                _krokMPType = Type.GetType("KrokoshaCasualtiesMP.KrokoshaScavMultiplayer, KrokoshaCasualtiesMP");
                _krokMPChecked = true;
            }
            if (_krokMPType == null) return false;
            try
            {
                var running = (bool)_krokMPType.GetProperty("network_system_is_running").GetValue(null);
                var server  = (bool)_krokMPType.GetProperty("is_server").GetValue(null);
                return running && !server;
            }
            catch { return false; }
        }
    }

    /// <summary>Invalidate cached reflection delegates.</summary>
    public static void Refresh() { _forceSync = null; }

    // ---- Force health sync ----

    private static Action<Body> _forceSync;

    /// <summary>
    /// Force KrokMP to immediately sync body state to clients.
    /// Safe to call without KrokMP installed — degrades to no-op.
    /// </summary>
    public static void QueueHealthSync(Body body)
    {
        if (IsClient) return; // Server-only — silently ignore on pure clients
        if (_forceSync == null)
        {
            try
            {
                var medicalSync = Type.GetType("KrokoshaCasualtiesMP.MedicalSync, KrokoshaCasualtiesMP");
                var netBodyType = Type.GetType("KrokoshaCasualtiesMP.NetBody, KrokoshaCasualtiesMP");
                if (medicalSync != null && netBodyType != null)
                {
                    var queueMethod = medicalSync.GetMethod("Server_QueueSendCharacterHealth");
                    _forceSync = b =>
                    {
                        var nb = b.GetComponent(netBodyType);
                        if (nb != null) queueMethod.Invoke(null, new object[] { nb, true });
                    };
                }
            }
            catch { _forceSync = _ => { }; }
            if (_forceSync == null) _forceSync = _ => { };
        }
        try { _forceSync(body); } catch { /* defence: MP detection may race with network init */ }
    }
}
