namespace SearPressure.UnityHost
{
    // AdMob IDs. Until you paste in your own (AdMob → Apps → Sear Pressure → App settings / Ad units),
    // these are Google's public TEST IDs: they show "Test Ad" banners and never pay out.
    // Never tap your own live ads, or AdMob can suspend the account. See store/ADMOB.md.
    public static class AdsConfig
    {
        public static bool Enabled = true;

        // App IDs (with a "~"). The editor copies these into Assets/GoogleMobileAds/Resources/GoogleMobileAdsSettings.asset.
        public const string AndroidAppId = "ca-app-pub-3940256099942544~3347511713";
        public const string IosAppId = "ca-app-pub-3940256099942544~1458002511";

        // Ad unit IDs (with a "/").
        public const string AndroidBanner = "ca-app-pub-3940256099942544/9214589741";
        public const string AndroidInterstitial = "ca-app-pub-3940256099942544/1033173712";
        public const string AndroidRewarded = "ca-app-pub-3940256099942544/5224354917";
        public const string IosBanner = "ca-app-pub-3940256099942544/2435281174";
        public const string IosInterstitial = "ca-app-pub-3940256099942544/4411468910";
        public const string IosRewarded = "ca-app-pub-3940256099942544/1712485313";

        public const string TestPublisher = "ca-app-pub-3940256099942544";
        public static bool UsingTestIds => AndroidAppId.StartsWith(TestPublisher) || AndroidBanner.StartsWith(TestPublisher)
            || AndroidInterstitial.StartsWith(TestPublisher) || AndroidRewarded.StartsWith(TestPublisher);

#if UNITY_IOS
        public static string Banner => IosBanner;
        public static string Interstitial => IosInterstitial;
        public static string Rewarded => IosRewarded;
#else
        public static string Banner => AndroidBanner;
        public static string Interstitial => AndroidInterstitial;
        public static string Rewarded => AndroidRewarded;
#endif
    }
}
