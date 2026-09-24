namespace SearPressure.UnityHost
{
    // AdMob IDs. The Android ones are Sear Pressure's real app and ad units; Development builds and the editor
    // swap in Google's public test units automatically. The iOS ones are still Google's test IDs (no iOS release yet).
    // Never tap your own live ads, or AdMob can suspend the account. See store/ADMOB.md.
    public static class AdsConfig
    {
        // Free with ads; buying "Remove ads" (PlayBilling.cs) switches them off. SEARPRESSURE_ADS is defined
        // automatically while the Google Mobile Ads package is installed (see SearPressure.Unity.asmdef).
        public static bool Enabled = true;

        // App IDs (with a "~"). The editor copies these into Assets/GoogleMobileAds/Resources/GoogleMobileAdsSettings.asset.
        public const string AndroidAppId = "ca-app-pub-2822796427144413~6568766943";   // Sear Pressure (Android) in AdMob
        public const string IosAppId = "ca-app-pub-3940256099942544~1458002511";

        // Ad unit IDs (with a "/").
        // Sear Pressure's real Android ad units (AdMob → Apps → Sear Pressure → Ad units).
        public const string AndroidBanner = "ca-app-pub-2822796427144413/2544520785";
        public const string AndroidInterstitial = "ca-app-pub-2822796427144413/3140426310";
        public const string AndroidRewarded = "ca-app-pub-2822796427144413/4261847002";
        // Google's test units: used automatically in Development builds (Build Profiles → Development Build),
        // so testing on your own phone never serves or taps real ads.
        const string TestAndroidBanner = "ca-app-pub-3940256099942544/9214589741";
        const string TestAndroidInterstitial = "ca-app-pub-3940256099942544/1033173712";
        const string TestAndroidRewarded = "ca-app-pub-3940256099942544/5224354917";
        public const string IosBanner = "ca-app-pub-3940256099942544/2435281174";
        public const string IosInterstitial = "ca-app-pub-3940256099942544/4411468910";
        public const string IosRewarded = "ca-app-pub-3940256099942544/1712485313";

        public const string TestPublisher = "ca-app-pub-3940256099942544";
        public static bool UsingTestIds => AndroidAppId.StartsWith(TestPublisher) || AndroidBanner.StartsWith(TestPublisher)
            || AndroidInterstitial.StartsWith(TestPublisher) || AndroidRewarded.StartsWith(TestPublisher);

#if UNITY_IOS
        public static string Banner => IosBanner;          // test IDs until an iOS release is set up
        public static string Interstitial => IosInterstitial;
        public static string Rewarded => IosRewarded;
#else
        static bool Test => UnityEngine.Debug.isDebugBuild;
        public static string Banner => Test ? TestAndroidBanner : AndroidBanner;
        public static string Interstitial => Test ? TestAndroidInterstitial : AndroidInterstitial;
        public static string Rewarded => Test ? TestAndroidRewarded : AndroidRewarded;
#endif
    }
}
