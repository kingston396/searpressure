using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using UnityEngine;

namespace SearPressure.UnityHost
{
    public static class StoreConfig
    {
        // The one-time product in Play Console (Monetise with Play → Products → One-time products).
        public const string RemoveAdsId = "remove_ads";
        public const string OwnedKey = "SearPressure.RemoveAds";   // cached on the device so ads never flash up at start-up
    }

#if UNITY_ANDROID && !UNITY_EDITOR
    // Google Play Billing (library 9.x, added to the Gradle build by Editor/SearPressureDependencies.xml),
    // called through JNI. Google calls us back on its own threads; everything that touches the game is
    // queued and run in Update on Unity's thread.
    public sealed class PlayBilling : MonoBehaviour, IStore
    {
        const string Api = "com.android.billingclient.api.";
        const int OK = 0, USER_CANCELED = 1, ITEM_ALREADY_OWNED = 7;
        const int PURCHASED = 1, PENDING = 2;

        readonly ConcurrentQueue<Action> main = new ConcurrentQueue<Action>();
        AndroidJavaObject activity, client, product;
        string offerToken, price;
        bool owned, connected, connecting;
        float retryAt; float retryDelay = 2;
        float connectingSince, productRetryAt, productRetryDelay = 5;
        Action<StoreResult> waitingBuy; float waitingBuyUntil;   // a tap that arrived before the store was ready
        float restoreUntil;
        Action<StoreResult> buyDone, restoreDone;

        public event Action<bool> OwnedChanged;
        // True once the start-up ownership check has answered (or failed). The host waits for it (briefly)
        // before starting ads, so a returning buyer on a new phone never sees an ad or the ad consent form.
        public bool Checked { get; private set; }
        public bool Owned => owned;
        public string Price => price;
        public bool Ready => connected && product != null;

        void Awake() { owned = PlayerPrefs.GetInt(StoreConfig.OwnedKey, 0) == 1; }

        public void Begin()
        {
            try
            {
                using (var up = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                    activity = up.GetStatic<AndroidJavaObject>("currentActivity");
                BuildClient();
                Reconnect();
            }
            catch (Exception e) { Debug.LogWarning("Sear Pressure store: couldn't start Google Play Billing: " + e.Message); Checked = true; }
        }

        void BuildClient()
        {
            using var ppb = new AndroidJavaClass(Api + "PendingPurchasesParams").CallStatic<AndroidJavaObject>("newBuilder");
            ppb.Call<AndroidJavaObject>("enableOneTimeProducts").Dispose();
            using var pending = ppb.Call<AndroidJavaObject>("build");
            using var b = new AndroidJavaClass(Api + "BillingClient").CallStatic<AndroidJavaObject>("newBuilder", activity);
            b.Call<AndroidJavaObject>("setListener", new PurchasesUpdated(this)).Dispose();
            b.Call<AndroidJavaObject>("enablePendingPurchases", pending).Dispose();
            b.Call<AndroidJavaObject>("enableAutoServiceReconnection").Dispose();
            client = b.Call<AndroidJavaObject>("build");
        }

        // A connection stuck in CONNECTING can't be restarted on the same client: close it and build a new one.
        void RebuildClient()
        {
            try { client?.Call("endConnection"); } catch (Exception) { }
            client?.Dispose(); client = null;
            connected = false; connecting = false;
            try { BuildClient(); } catch (Exception e) { Debug.LogWarning("Sear Pressure store: rebuild failed: " + e.Message); }
            retryAt = Time.unscaledTime + 1;
        }

        void Update()
        {
            while (main.TryDequeue(out var a)) { try { a(); } catch (Exception e) { Debug.LogException(e); } }
            float t = Time.unscaledTime;
            if (connecting && t - connectingSince > 20) RebuildClient();   // a setup callback that never came
            if (restoreDone != null && !connected && t > restoreUntil) FinishRestore(StoreResult.Unavailable);
            if (retryAt > 0 && t > retryAt) { retryAt = 0; Reconnect(); }
            if (productRetryAt > 0 && t > productRetryAt) { productRetryAt = 0; if (connected && product == null) QueryProduct(); }
            // A tap that came in while connecting: go ahead once ready, or give up after a few seconds.
            if (waitingBuy != null)
            {
                if (owned || Ready) { var d = waitingBuy; waitingBuy = null; Buy(d); }   // Buy answers AlreadyOwned first
                else if (t > waitingBuyUntil) { var d = waitingBuy; waitingBuy = null; d(StoreResult.Unavailable); }
            }
        }

        void OnApplicationFocus(bool focus)
        {
            if (!focus) return;
            // Back from the Play Store or another app: a pending payment may have completed, or a refund landed.
            if (!connected) { Reconnect(); return; }
            QueryPurchases(null);
            if (product == null) QueryProduct();
        }

        // ---- set-up ----
        // (Re)connect when the client is DISCONNECTED (state 0); never while it's already connecting.
        // Handles every client state: DISCONNECTED (0) → connect; CONNECTING (1) → check again shortly;
        // CONNECTED (2, e.g. the library reconnected by itself) → treat as set up.
        void Reconnect()
        {
            if (client == null || connected || connecting) return;
            try
            {
                int state = client.Call<int>("getConnectionState");
                if (state == 2) { OnSetup(OK); return; }
                if (state == 1)
                {
                    // Connecting on its own (auto-reconnection): check again soon; if it stays stuck, rebuild.
                    if (connectingSince <= 0) connectingSince = Time.unscaledTime;
                    if (Time.unscaledTime - connectingSince > 20) { connectingSince = 0; RebuildClient(); return; }
                    retryAt = Time.unscaledTime + 2; return;
                }
                if (state == 3) { RebuildClient(); return; }   // CLOSED
                connecting = true; connectingSince = Time.unscaledTime;
                client.Call("startConnection", new StateListener(this));
            }
            catch (Exception e)
            {
                connecting = false; Debug.LogWarning("Sear Pressure store: reconnect failed: " + e.Message);
                retryAt = Time.unscaledTime + retryDelay; retryDelay = Mathf.Min(60, retryDelay * 2);
                FinishRestore(StoreResult.Unavailable);
            }
        }

        void OnSetup(int code)
        {
            connecting = false; connectingSince = 0;
            connected = code == OK;
            if (!connected)
            {
                Debug.LogWarning("Sear Pressure store: billing setup failed, code " + code);
                Checked = true;
                FinishRestore(Map(code) == StoreResult.Purchased ? StoreResult.Error : Map(code));
                // Try again by itself: 2, 4, 8 … up to 60 seconds apart.
                retryAt = Time.unscaledTime + retryDelay; retryDelay = Mathf.Min(60, retryDelay * 2);
                return;
            }
            retryDelay = 2;
            QueryProduct();
            QueryPurchases(null);
        }

        void OnDisconnected()
        {
            connected = false; connecting = false;
            retryAt = Time.unscaledTime + 2;
        }

        void QueryProduct()
        {
            try
            {
                using var pb = new AndroidJavaClass(Api + "QueryProductDetailsParams$Product").CallStatic<AndroidJavaObject>("newBuilder");
                pb.Call<AndroidJavaObject>("setProductId", StoreConfig.RemoveAdsId).Dispose();
                pb.Call<AndroidJavaObject>("setProductType", "inapp").Dispose();
                using var p = pb.Call<AndroidJavaObject>("build");
                using var list = new AndroidJavaObject("java.util.ArrayList");
                list.Call<bool>("add", p);
                using var qb = new AndroidJavaClass(Api + "QueryProductDetailsParams").CallStatic<AndroidJavaObject>("newBuilder");
                qb.Call<AndroidJavaObject>("setProductList", list).Dispose();
                using var q = qb.Call<AndroidJavaObject>("build");
                client.Call("queryProductDetailsAsync", q, new ProductDetailsListener(this));
            }
            catch (Exception e)
            {
                Debug.LogWarning("Sear Pressure store: product query failed: " + e.Message);
                productRetryAt = Time.unscaledTime + productRetryDelay; productRetryDelay = Mathf.Min(60, productRetryDelay * 2);
            }
        }

        void OnProduct(AndroidJavaObject details, string fmtPrice, string token)
        {
            if (details == null)
            {
                // Keep a product we already have (a later query can fail); otherwise try again, 5 → 60 s apart.
                if (product == null)
                {
                    Debug.LogWarning($"Sear Pressure store: product '{StoreConfig.RemoveAdsId}' not available yet (offline, or not created/activated in Play Console).");
                    productRetryAt = Time.unscaledTime + productRetryDelay; productRetryDelay = Mathf.Min(60, productRetryDelay * 2);
                }
                return;
            }
            product?.Dispose();
            product = details; price = fmtPrice; offerToken = token; productRetryDelay = 5;
        }

        // ---- buying ----
        public void Buy(Action<StoreResult> done)
        {
            if (owned) { done?.Invoke(StoreResult.AlreadyOwned); return; }
            if (client == null) { done?.Invoke(StoreResult.Unavailable); return; }
            if (!Ready)
            {
                // Give the store up to 6 seconds to connect / load the product, then carry on with this tap.
                if (waitingBuy != null) { done?.Invoke(StoreResult.Unavailable); return; }
                waitingBuy = done ?? (_ => { }); waitingBuyUntil = Time.unscaledTime + 6;
                if (!connected) { retryAt = 0; Reconnect(); }
                else if (product == null) QueryProduct();
                return;
            }
            buyDone = done;
            // launchBillingFlow must run on Android's UI thread.
            activity.Call("runOnUiThread", new AndroidJavaRunnable(() =>
            {
                try
                {
                    using var dpb = new AndroidJavaClass(Api + "BillingFlowParams$ProductDetailsParams").CallStatic<AndroidJavaObject>("newBuilder");
                    dpb.Call<AndroidJavaObject>("setProductDetails", product).Dispose();
                    if (!string.IsNullOrEmpty(offerToken)) dpb.Call<AndroidJavaObject>("setOfferToken", offerToken).Dispose();
                    using var dp = dpb.Call<AndroidJavaObject>("build");
                    using var list = new AndroidJavaObject("java.util.ArrayList");
                    list.Call<bool>("add", dp);
                    using var fb = new AndroidJavaClass(Api + "BillingFlowParams").CallStatic<AndroidJavaObject>("newBuilder");
                    fb.Call<AndroidJavaObject>("setProductDetailsParamsList", list).Dispose();
                    using var flow = fb.Call<AndroidJavaObject>("build");
                    using var res = client.Call<AndroidJavaObject>("launchBillingFlow", activity, flow);
                    int code = res.Call<int>("getResponseCode");
                    // OK means the Play sheet is up; the outcome arrives in onPurchasesUpdated.
                    if (code != OK) main.Enqueue(() => FinishBuy(code == ITEM_ALREADY_OWNED ? StoreResult.AlreadyOwned : Map(code)));
                }
                catch (Exception e)
                {
                    Debug.LogWarning("Sear Pressure store: launchBillingFlow failed: " + e.Message);
                    main.Enqueue(() => FinishBuy(StoreResult.Error));
                }
            }));
        }

        void FinishBuy(StoreResult r)
        {
            if (r == StoreResult.AlreadyOwned) QueryPurchases(null);   // confirm, and acknowledge if needed
            var d = buyDone; buyDone = null; d?.Invoke(r);
        }

        static StoreResult Map(int code) => code switch
        {
            OK => StoreResult.Purchased,
            USER_CANCELED => StoreResult.Cancelled,
            ITEM_ALREADY_OWNED => StoreResult.AlreadyOwned,
            -3 or -1 or 2 or 3 or 12 => StoreResult.Unavailable,   // timeout, disconnected, service/billing unavailable, network
            _ => StoreResult.Error,
        };

        struct Snap { public string token; public int state; public bool acknowledged; public bool mine; }

        // Read what we need while the Java objects are still valid (called on Google's thread).
        static List<Snap> ReadPurchases(AndroidJavaObject list)
        {
            var outL = new List<Snap>();
            if (list == null) return outL;
            int n = list.Call<int>("size");
            for (int i = 0; i < n; i++)
            {
                using var p = list.Call<AndroidJavaObject>("get", i);
                bool mine = false;
                using (var ids = p.Call<AndroidJavaObject>("getProducts"))
                {
                    int m = ids.Call<int>("size");
                    for (int k = 0; k < m; k++) if (ids.Call<string>("get", k) == StoreConfig.RemoveAdsId) mine = true;
                }
                outL.Add(new Snap { token = p.Call<string>("getPurchaseToken"), state = p.Call<int>("getPurchaseState"), acknowledged = p.Call<bool>("isAcknowledged"), mine = mine });
            }
            return outL;
        }

        void OnPurchasesUpdated(int code, List<Snap> purchases)
        {
            NoteCode(code);
            if (code == OK)
            {
                var r = Apply(purchases, false);
                FinishBuy(r ?? StoreResult.Error);
            }
            else if (code == ITEM_ALREADY_OWNED) FinishBuy(StoreResult.AlreadyOwned);
            else FinishBuy(Map(code));
        }

        // Owned if any "remove_ads" purchase is PURCHASED; acknowledge it (Google refunds unacknowledged ones after 3 days).
        StoreResult? Apply(List<Snap> purchases, bool fullList)
        {
            bool bought = false, pending = false;
            foreach (var p in purchases)
            {
                if (!p.mine) continue;
                if (p.state == PURCHASED) { bought = true; if (!p.acknowledged) Acknowledge(p.token); }
                else if (p.state == PENDING) pending = true;
            }
            if (bought) { SetOwned(true); return StoreResult.Purchased; }
            if (fullList) SetOwned(false);   // a complete, successful query with no purchase: refunded or never bought
            if (pending) return StoreResult.Pending;
            return fullList ? StoreResult.NotOwned : (StoreResult?)null;
        }

        void Acknowledge(string token)
        {
            try
            {
                using var ab = new AndroidJavaClass(Api + "AcknowledgePurchaseParams").CallStatic<AndroidJavaObject>("newBuilder");
                ab.Call<AndroidJavaObject>("setPurchaseToken", token).Dispose();
                using var ap = ab.Call<AndroidJavaObject>("build");
                client.Call("acknowledgePurchase", ap, new AckListener());
            }
            catch (Exception e) { Debug.LogWarning("Sear Pressure store: acknowledge failed (will retry next start): " + e.Message); }
        }

        void SetOwned(bool v)
        {
            if (owned == v) return;
            owned = v;
            PlayerPrefs.SetInt(StoreConfig.OwnedKey, v ? 1 : 0); PlayerPrefs.Save();
            OwnedChanged?.Invoke(v);
        }

        // ---- restoring / checking ----
        public void Restore(Action<StoreResult> done)
        {
            if (client == null) { done?.Invoke(StoreResult.Unavailable); return; }
            if (!connected) { restoreDone = done; restoreUntil = Time.unscaledTime + 8; retryAt = 0; Reconnect(); return; }   // answered by OnSetup's query, its failure, or the 8 s deadline
            QueryPurchases(done);
        }

        void QueryPurchases(Action<StoreResult> done)
        {
            if (done != null) restoreDone = done;
            try
            {
                using var qb = new AndroidJavaClass(Api + "QueryPurchasesParams").CallStatic<AndroidJavaObject>("newBuilder");
                qb.Call<AndroidJavaObject>("setProductType", "inapp").Dispose();
                using var q = qb.Call<AndroidJavaObject>("build");
                client.Call("queryPurchasesAsync", q, new PurchasesResponse(this));
            }
            catch (Exception e) { Debug.LogWarning("Sear Pressure store: purchase query failed: " + e.Message); FinishRestore(StoreResult.Error); }
        }

        // Any call answering SERVICE_DISCONNECTED (-1) means our "connected" flag is stale.
        void NoteCode(int code) { if (code == -1 && connected) OnDisconnected(); }

        void OnPurchasesQueried(int code, List<Snap> purchases)
        {
            Checked = true;
            NoteCode(code);
            if (code != OK) { FinishRestore(Map(code) == StoreResult.Purchased ? StoreResult.Error : Map(code)); return; }
            var r = Apply(purchases, true) ?? StoreResult.NotOwned;
            FinishRestore(r == StoreResult.Purchased ? StoreResult.AlreadyOwned : r);
        }

        void FinishRestore(StoreResult r) { var d = restoreDone; restoreDone = null; d?.Invoke(r); }

        // ---- Java callbacks (on Google's threads) ----
        sealed class StateListener : AndroidJavaProxy
        {
            readonly PlayBilling b;
            public StateListener(PlayBilling b) : base(Api + "BillingClientStateListener") { this.b = b; }
            public void onBillingSetupFinished(AndroidJavaObject result)
            {
                int code = result.Call<int>("getResponseCode");
                b.main.Enqueue(() => b.OnSetup(code));
            }
            public void onBillingServiceDisconnected() { b.main.Enqueue(b.OnDisconnected); }
        }

        sealed class ProductDetailsListener : AndroidJavaProxy
        {
            readonly PlayBilling b;
            public ProductDetailsListener(PlayBilling b) : base(Api + "ProductDetailsResponseListener") { this.b = b; }
            public void onProductDetailsResponse(AndroidJavaObject result, AndroidJavaObject details)
            {
                int code = result.Call<int>("getResponseCode");
                AndroidJavaObject found = null; string fmt = null, token = null;
                if (code == OK && details != null)
                {
                    using var list = details.Call<AndroidJavaObject>("getProductDetailsList");
                    if (list != null && list.Call<int>("size") > 0)
                    {
                        found = list.Call<AndroidJavaObject>("get", 0);   // our own reference: safe to keep
                        using var offer = found.Call<AndroidJavaObject>("getOneTimePurchaseOfferDetails");
                        if (offer != null) { fmt = offer.Call<string>("getFormattedPrice"); token = offer.Call<string>("getOfferToken"); }
                    }
                }
                b.main.Enqueue(() => b.OnProduct(found, fmt, token));
            }
        }

        sealed class PurchasesUpdated : AndroidJavaProxy
        {
            readonly PlayBilling b;
            public PurchasesUpdated(PlayBilling b) : base(Api + "PurchasesUpdatedListener") { this.b = b; }
            public void onPurchasesUpdated(AndroidJavaObject result, AndroidJavaObject purchases)
            {
                int code = result.Call<int>("getResponseCode");
                var snap = ReadPurchases(purchases);
                b.main.Enqueue(() => b.OnPurchasesUpdated(code, snap));
            }
        }

        sealed class PurchasesResponse : AndroidJavaProxy
        {
            readonly PlayBilling b;
            public PurchasesResponse(PlayBilling b) : base(Api + "PurchasesResponseListener") { this.b = b; }
            public void onQueryPurchasesResponse(AndroidJavaObject result, AndroidJavaObject purchases)
            {
                int code = result.Call<int>("getResponseCode");
                var snap = ReadPurchases(purchases);
                b.main.Enqueue(() => b.OnPurchasesQueried(code, snap));
            }
        }

        sealed class AckListener : AndroidJavaProxy
        {
            public AckListener() : base(Api + "AcknowledgePurchaseResponseListener") { }
            public void onAcknowledgePurchaseResponse(AndroidJavaObject result)
            {
                int code = result.Call<int>("getResponseCode");
                if (code != OK) Debug.LogWarning("Sear Pressure store: acknowledge returned " + code + " (retried next start)");
            }
        }

        void OnDestroy()
        {
            try { client?.Call("endConnection"); } catch (Exception) { }
            product?.Dispose(); client?.Dispose(); activity?.Dispose();
        }
    }
#endif

#if UNITY_EDITOR
    // In the editor there's no Google Play: pretend, so the flow can be tried in Play mode.
    // Sear Pressure → Testing → Reset "Remove Ads" clears it.
    public sealed class EditorStore : MonoBehaviour, IStore
    {
        bool owned;
        public event Action<bool> OwnedChanged;
        public bool Owned => owned;
        public string Price => "$4.99";
        public bool Ready => true;
        public bool Checked => true;
        void Awake() { owned = PlayerPrefs.GetInt(StoreConfig.OwnedKey, 0) == 1; }
        public void Begin() { }
        public void Buy(Action<StoreResult> done)
        {
            if (owned) { done?.Invoke(StoreResult.AlreadyOwned); return; }
            Debug.Log("Sear Pressure (editor): pretending to buy \"" + StoreConfig.RemoveAdsId + "\".");
            owned = true; PlayerPrefs.SetInt(StoreConfig.OwnedKey, 1); PlayerPrefs.Save();
            OwnedChanged?.Invoke(true);
            done?.Invoke(StoreResult.Purchased);
        }
        public void Restore(Action<StoreResult> done) => done?.Invoke(owned ? StoreResult.AlreadyOwned : StoreResult.NotOwned);
    }
#endif
}
