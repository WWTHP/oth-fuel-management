using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Code = FuelManagement.SiphonWire.Code;
using Packet = FuelManagement.SiphonWire.Packet;

namespace FuelManagement
{
    /// <summary>
    /// Siphoning: hold SiphonKey, stopped next to another player's car, and fuel flows from
    /// their tank into yours.
    ///
    /// Each tank lives only in its owner's game, so a transfer is a request/answer: every
    /// <see cref="Interval"/> s the receiver asks the donor for a chunk (<see cref="Net.Msg.SiphonRequest"/>),
    /// the donor's game checks that it sees the receiver's car close by, takes what it can spare
    /// (never below SiphonReserve), and answers with the amount (<see cref="Net.Msg.SiphonGive"/>).
    /// The receiver adds only what the donor actually gave, so fuel is never created or lost.
    /// Only one request is in flight at a time; late answers are still credited.
    ///
    /// Needs the mod on both players and on the host (clients only talk via the host).
    /// </summary>
    internal static class Siphon
    {
        private const float Interval = 0.5f;      // seconds between requests
        private const float Timeout = 2.5f;       // no answer by then = probably no mod on the other side
        private const float MaxSpeed = 2f;        // m/s: both cars must be (nearly) stopped
        private const float DonorSlack = 3f;      // m: donor-side distance tolerance (network lag)
        private const float StatusSeconds = 2f;   // how long a status line stays after letting go

        /// <summary>A short status line for the gauge while siphoning, or "" when there's nothing to say.</summary>
        public static string Status => Time.unscaledTime < _statusUntil ? _status : "";

        /// <summary>True while fuel is flowing into our tank.</summary>
        public static bool Receiving => Time.unscaledTime < _receivingUntil;

        /// <summary>True while someone is siphoning from us.</summary>
        public static bool BeingDrained => Time.unscaledTime < _drainedUntil;

        public static int ModdedPeers => Peers.Count;

        private static readonly HashSet<int> Peers = new();
        private static readonly Dictionary<uint, float> Outstanding = new(); // request id → sent time
        private static readonly HashSet<int> LoggedDonors = new(), LoggedReceivers = new();
        private static string _status = "";
        private static float _statusUntil, _receivingUntil, _drainedUntil, _nextSend;
        private static uint _nextId = 1;
        private static float _sessionReceived, _sessionGiven;
        private static IntPtr _runnerPtr;
        private static bool _helloSent;
        private static float _helloAgainAt = -1f;

        public static void Init() => Net.OnMessage = OnMessage;

        /// <summary>Called every frame (main thread).</summary>
        public static void Update()
        {
            SessionUpdate();
            ExpireOutstanding();
            if (!Settings.Enabled.Value || !Settings.Held(Settings.SiphonKey)) return;
            Request();
        }

        // ------------------------------------------------------------- session

        private static void SessionUpdate()
        {
            var run = Net.Runner();
            var ptr = run?.Pointer ?? IntPtr.Zero;
            if (ptr != _runnerPtr)
            {
                _runnerPtr = ptr;
                _helloSent = false;
                _helloAgainAt = -1f;
                Peers.Clear();
                Net.ClearPeers();
                Outstanding.Clear();
                LoggedDonors.Clear();
                LoggedReceivers.Clear();
                if (_sessionReceived > 0f || _sessionGiven > 0f)
                    Mod.Log.Msg($"Siphon totals last session: received {_sessionReceived * 100f:0}%, " +
                                $"gave {_sessionGiven * 100f:0}% of a tank.");
                _sessionReceived = _sessionGiven = 0f;
            }
            if (run == null || !Net.Ready) return;
            if (!_helloSent)
            {
                _helloSent = true;
                if (run.IsSinglePlayer) return;
                SendHello();
                _helloAgainAt = Time.unscaledTime + 5f;
            }
            else if (_helloAgainAt > 0f && Time.unscaledTime > _helloAgainAt)
            {
                _helloAgainAt = -1f;
                if (Peers.Count == 0) SendHello();
            }
        }

        private static void SendHello()
        {
            var w = Net.Begin(Net.Msg.Hello, out var ms);
            w.Write(SiphonWire.EncodeHello(false));
            Net.Broadcast(ms);
        }

        private static void ExpireOutstanding()
        {
            if (Outstanding.Count == 0) return;
            List<uint> old = null;
            foreach (var kv in Outstanding)
                if (Time.unscaledTime - kv.Value > 30f) (old ??= new List<uint>()).Add(kv.Key);
            if (old != null) foreach (var id in old) Outstanding.Remove(id);
        }

        // ------------------------------------------------------------ receiver

        private static void Request()
        {
            if (!Car.Valid || Car.Player <= 0) { Say("no car"); return; }
            var run = Net.Runner();
            if (run == null || run.IsSinglePlayer) { Say("siphoning needs another player"); return; }
            if (!Net.Ready) { Say("multiplayer link: " + Net.Status); return; }
            if (Fuel.Tank.Level >= 0.999f) { Say("your tank is full"); return; }
            if (Car.Speed > MaxSpeed) { Say("stop next to the other car"); return; }
            float radius = Validate.InRange(Settings.SiphonRadius.Value, 1f, 30f) ? Settings.SiphonRadius.Value : 6f;
            if (!Car.NearestOther(radius, out int donor, out _)) { Say($"no car within {radius:0} m"); return; }

            // One request in flight at a time. Unanswered ones stay in Outstanding (for 30 s) so a
            // late answer is still credited; after Timeout we try again, but slower.
            float newest = NewestOutstanding();
            if (newest >= 0f && Time.unscaledTime - newest < Timeout) return;
            bool unanswered = newest >= 0f;
            if (unanswered) Say("no answer: the other driver (and the host) need the fuel mod");
            if (Time.unscaledTime < _nextSend) return;
            _nextSend = Time.unscaledTime + (unanswered ? Timeout : Interval);

            float rate = Validate.InRange(Settings.SiphonRate.Value, 0.01f, 0.5f) ? Settings.SiphonRate.Value : 0.1f;
            float amount = SiphonMath.RequestSize(Fuel.Tank.Level, rate * Interval);
            if (amount <= 0f) { Say("your tank is full"); return; }

            uint id = _nextId++;
            if (_nextId == 0) _nextId = 1;
            Outstanding[id] = Time.unscaledTime;
            Send(Net.Msg.SiphonRequest, new Packet(id, donor, Car.Player, amount, Code.Ok));
            if (LoggedDonors.Add(donor)) Mod.Log.Msg($"Siphoning from player {donor} at {Fuel.Tank.Level * 100f:0}%.");
        }

        private static float NewestOutstanding()
        {
            float newest = -1f;
            foreach (var t in Outstanding.Values) newest = Math.Max(newest, t);
            return newest;
        }

        private static void OnGive(Packet p)
        {
            if (p.Receiver != Car.Player || !Outstanding.Remove(p.Id)) return; // not ours, or a duplicate
            float added = Fuel.Tank.Refuel(p.Amount);
            _sessionReceived += added;
            switch (p.Code)
            {
                case Code.Ok when added > 0f:
                    _receivingUntil = Time.unscaledTime + Interval * 2f;
                    Say($"siphoning from player {p.Donor}");
                    break;
                case Code.Ok:
                    Say("your tank is full");
                    break;
                case Code.Reserve:
                    if (added > 0f) _receivingUntil = Time.unscaledTime + Interval * 2f;
                    Say("their tank is down to their reserve");
                    break;
                case Code.Refused:
                    Say("that driver doesn't allow siphoning");
                    break;
                case Code.TooFar:
                    Say("too far apart (from their side)");
                    break;
                default:
                    Say("the other driver has no car");
                    break;
            }
        }

        // --------------------------------------------------------------- donor

        private static void OnRequest(Packet p)
        {
            if (p.Donor != Car.Player || Car.Player <= 0) return; // addressed to someone else
            var (amount, code) = Decide(p);
            if (amount > 0f)
            {
                Fuel.Tank.Set(Fuel.Tank.Level - amount);
                _sessionGiven += amount;
                _drainedUntil = Time.unscaledTime + Interval * 2f;
            }
            if (LoggedReceivers.Add(p.Receiver))
                Mod.Log.Msg($"Player {p.Receiver} is siphoning from us ({code}, tank {Fuel.Tank.Level * 100f:0}%).");
            Send(Net.Msg.SiphonGive, new Packet(p.Id, p.Donor, p.Receiver, amount, code));
        }

        private static (float amount, Code code) Decide(Packet p)
        {
            if (!Settings.Enabled.Value || !Settings.AllowSiphon.Value) return (0f, Code.Refused);
            if (!Car.Valid) return (0f, Code.NoCar);
            float radius = Validate.InRange(Settings.SiphonRadius.Value, 1f, 30f) ? Settings.SiphonRadius.Value : 6f;
            // Our own view of where their car is: a far-away player can't drain us.
            if (Car.DistanceTo(p.Receiver) > radius + DonorSlack) return (0f, Code.TooFar);
            float amount = SiphonMath.Grant(Fuel.Tank.Level, p.Amount, Settings.SiphonReserve.Value);
            return amount < p.Amount ? (amount, Code.Reserve) : (amount, Code.Ok);
        }

        // ------------------------------------------------------------ messages

        private static void Send(Net.Msg type, Packet p)
        {
            var w = Net.Begin(type, out var ms);
            w.Write(SiphonWire.Encode(p));
            // Broadcast reaches only modded peers (via the host on a client); the packet names
            // its donor and receiver and everyone else ignores it.
            Net.Broadcast(ms);
        }

        private static void OnMessage(Net.Msg type, int sender, BinaryReader r)
        {
            if (sender > 0) Peers.Add(sender);
            var data = r.ReadBytes((int)(r.BaseStream.Length - r.BaseStream.Position));
            switch (type)
            {
                case Net.Msg.Hello:
                {
                    if (!SiphonWire.TryParseHello(data, out bool reply)) { Bad(type, sender); return; }
                    if (!reply && Net.IsAuthority(Net.Runner()))
                    {
                        // Tell the newcomer the host has the mod, so its packets get sent.
                        var w = Net.Begin(Net.Msg.Hello, out var ms);
                        w.Write(SiphonWire.EncodeHello(true));
                        Net.SendTo(sender, ms);
                    }
                    Mod.Log.Msg($"Player {sender} has the fuel mod{(reply ? " (host)" : "")}.");
                    break;
                }
                case Net.Msg.SiphonRequest:
                    if (SiphonWire.TryParse(data, out var req)) OnRequest(req);
                    else Bad(type, sender);
                    break;
                case Net.Msg.SiphonGive:
                    if (SiphonWire.TryParse(data, out var give)) OnGive(give);
                    else Bad(type, sender);
                    break;
                default:
                    Bad(type, sender);
                    break;
            }
        }

        private static void Bad(Net.Msg type, int sender) =>
            Mod.Log.Warning($"Ignored a malformed {type} packet from player {sender}.");

        private static void Say(string s)
        {
            _status = s;
            _statusUntil = Time.unscaledTime + StatusSeconds;
        }
    }
}
