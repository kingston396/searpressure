namespace SearPressure.UnityHost
{
    // Where online play connects: the relay server in SearPressureUnity/Server (see its README).
    public static class NetConfig
    {
        // Set this to your deployed relay, e.g. "wss://sear-pressure-relay.onrender.com".
        public static string RelayUrl = "ws://127.0.0.1:8080";

        // Release builds must use wss:// (phones block plain ws:// to the internet).
        // WebGL builds have no ClientWebSocket, so online play is off there.
        public static INetTransport Create()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return null;
#else
            if (string.IsNullOrEmpty(RelayUrl)) return null;
            return new WsRelayTransport(RelayUrl);
#endif
        }
    }
}
