using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace TrapLib.MP;

/// <summary>Multiplayer state helpers — all safe to call without KrokMP installed.</summary>
public static class MPSync
{
    private static Type _krokMPType;
    private static PropertyInfo _runningProp;
    private static PropertyInfo _serverProp;
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
                if (_krokMPType != null)
                {
                    _runningProp = _krokMPType.GetProperty("network_system_is_running");
                    _serverProp  = _krokMPType.GetProperty("is_server");
                }
                _krokMPChecked = true;
            }
            if (_runningProp == null || _serverProp == null) return false;
            try
            {
                var running = (bool)_runningProp.GetValue(null);
                var server  = (bool)_serverProp.GetValue(null);
                return running && !server;
            }
            catch (Exception ex)
            {
                TrapLibPlugin.Log?.LogWarning($"[MPSync] IsClient reflection failed (KrokMP API may have changed): {ex.Message}");
                return false;
            }
        }
    }

    /// <summary>
    /// Enumerate all living player bodies.
    /// In multiplayer uses NetPlayer.AllLivingPlayers (via reflection);
    /// in single-player falls back to PlayerCamera.main.body.
    /// Safe to call without KrokMP installed.
    /// </summary>
    public static IEnumerable<Body> AllPlayerBodies
    {
        get
        {
            Type netPlayerType = Type.GetType("KrokoshaCasualtiesMP.NetPlayer, KrokoshaCasualtiesMP");
            if (netPlayerType != null)
            {
                var allLivingField = netPlayerType.GetField("AllLivingPlayers",
                    BindingFlags.Public | BindingFlags.Static);
                if (allLivingField != null)
                {
                    var list = allLivingField.GetValue(null) as IList;
                    if (list != null)
                    {
                        var bodyField = netPlayerType.GetField("body",
                            BindingFlags.Public | BindingFlags.Instance);
                        if (bodyField != null)
                        {
                            foreach (var player in list)
                            {
                                var body = bodyField.GetValue(player) as Body;
                                if (body != null) yield return body;
                            }
                            yield break;
                        }
                    }
                }
            }
            var local = PlayerCamera.main?.body;
            if (local != null) yield return local;
        }
    }

    /// <summary>Invalidate cached reflection delegates.</summary>
    public static void Refresh() { _forceSync = null; _runningProp = null; _serverProp = null; _krokMPChecked = false; }

    /// <summary>
    /// Run an action after a delay, with server/SP guard and null safety.
    /// Safe to call from any context — the action only runs on server/SP,
    /// and only if the MonoBehaviour is still alive.
    /// </summary>
    public static void RunAfterDelay(MonoBehaviour host, float delay, System.Action action)
    {
        if (host == null) return;
        host.StartCoroutine(SafeDelayedAction(delay, action));
    }

    private static IEnumerator SafeDelayedAction(float delay, System.Action action)
    {
        yield return new WaitForSeconds(delay);
        if (IsClient) yield break; // server/SP only
        try { action?.Invoke(); }
        catch (System.Exception ex)
        {
            TrapLibPlugin.Log?.LogWarning($"[MPSync] Delayed action failed: {ex.Message}");
        }
    }

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
            catch (Exception ex)
            {
                TrapLibPlugin.Log?.LogWarning($"[MPSync] QueueHealthSync setup failed (KrokMP API may have changed): {ex.Message}");
                _forceSync = _ => { };
            }
            if (_forceSync == null) _forceSync = _ => { };
        }
        try { _forceSync(body); }
        catch (Exception ex)
        {
            TrapLibPlugin.Log?.LogWarning($"[MPSync] QueueHealthSync invoke failed: {ex.Message}");
        }
    }
}
