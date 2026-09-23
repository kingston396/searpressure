using System;
using System.Collections.Generic;

namespace SearPressure
{
    // Placeholder: online co-op and versus are ported in this file.
    public interface INetTransport { }
    public sealed class RemoteInput { public double mx, my; public bool held, grab, chop; }
    public sealed class Guest { public int slot; public RemoteInput remote = new RemoteInput(); public string fit = "classic"; public List<string> gear = new List<string>(); }
    public sealed class NetState
    {
        public string role, mode = "coop", code = "";
        public int slot = 1;
        public List<string> fits = new List<string>();
        public List<List<string>> gears = new List<List<string>>();
        public List<Guest> guests = new List<Guest>();
        public List<object[]> events = new List<object[]>();
    }

    public sealed partial class Game
    {
        public readonly NetState NET = new NetState();
        void netTick(double dt) { }
        void updateGuest(double dt) { }
        void drawVsBoard() { }
        public int netPlayers() => 1;
        void netSend(object msg) { }
        void sendFits() { }
        string vsSummary() => "";
        void setPaused(bool on, bool tell) { paused = on; }
        void enterOnlineMenu() { }
        void netLeave(bool notify = true) { }
        void setMode(string m) { }
        void netStartVersus() { }
        void netStartGuests(int i, bool versus, double seed) { }
        void hostGame() { }
        void joinGame() { }
        void netFitChanged() { }
    }
}
