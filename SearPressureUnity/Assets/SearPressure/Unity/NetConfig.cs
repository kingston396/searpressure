namespace SearPressure.UnityHost
{
    // Where online play connects: the relay server in SearPressureUnity/Server (see its README).
    public static class NetConfig
    {
        // Set this to your deployed relay, e.g. "wss://sear-pressure-relay.onrender.com".
        public static string RelayUrl = "ws://127.0.0.1:8787";

        public static INetTransport Create() => null;
    }
}
