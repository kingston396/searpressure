using System;
using GoogleMobileAds.Api;
using GoogleMobileAds.Ump.Api;
using UnityEngine;

namespace SearPressure.UnityHost
{
    // AdMob for the game: an adaptive banner along the bottom, interstitials and rewarded ads.
    // Starts with Google's consent flow (UMP), which asks players in the EEA/UK and does nothing elsewhere.
    public sealed class AdMobAds : MonoBehaviour, IAds
    {
        BannerView banner;
        InterstitialAd interstitial;
        RewardedAd rewarded;
        bool started, fullScreen;
        float retryInterstitial, retryRewarded, retryBanner;

        // Banner height in screen pixels (0 until one has loaded). The host keeps the game above it.
        public float BannerHeightPx { get; private set; }

        public void Begin()
        {
            MobileAds.RaiseAdEventsOnUnityMainThread = true;
            // A previous session may already have consent: start straight away, and refresh consent too.
            if (ConsentInformation.CanRequestAds()) StartAds();
            var request = new ConsentRequestParameters { TagForUnderAgeOfConsent = false };
            ConsentInformation.Update(request, updateError =>
            {
                if (updateError != null) { Debug.LogWarning("Sear Pressure ads: consent info failed: " + updateError.Message); if (ConsentInformation.CanRequestAds()) StartAds(); return; }
                ConsentForm.LoadAndShowConsentFormIfRequired(formError =>
                {
                    if (formError != null) Debug.LogWarning("Sear Pressure ads: consent form failed: " + formError.Message);
                    if (ConsentInformation.CanRequestAds()) StartAds();
                });
            });
        }

        void StartAds()
        {
            if (started) return;
            started = true;
            if (AdsConfig.UsingTestIds) Debug.Log("Sear Pressure ads: using Google's TEST ad IDs (see AdsConfig.cs).");
            MobileAds.Initialize(_ =>
            {
                LoadBanner();
                LoadInterstitial();
                LoadRewarded();
            });
        }

        void Update()
        {
            if (!started) return;
            float t = Time.unscaledTime;
            if (retryBanner > 0 && t > retryBanner) { retryBanner = 0; LoadBanner(); }
            if (retryInterstitial > 0 && t > retryInterstitial) { retryInterstitial = 0; LoadInterstitial(); }
            if (retryRewarded > 0 && t > retryRewarded) { retryRewarded = 0; LoadRewarded(); }
        }

        // ---- banner ----
        void LoadBanner()
        {
            banner?.Destroy();
            var size = AdSize.GetCurrentOrientationAnchoredAdaptiveBannerAdSizeWithWidth(MobileAds.Utils.GetDeviceSafeWidth());
            banner = new BannerView(AdsConfig.Banner, size, AdPosition.Bottom);
            banner.OnBannerAdLoaded += () => { BannerHeightPx = banner.GetHeightInPixels(); if (fullScreen) banner.Hide(); };
            banner.OnBannerAdLoadFailed += err =>
            {
                Debug.LogWarning("Sear Pressure ads: banner failed: " + err.GetMessage());
                BannerHeightPx = 0; retryBanner = Time.unscaledTime + 30;
            };
            banner.LoadAd(new AdRequest());
        }

        // Reload the banner at the new width after the phone rotates.
        int lastW, lastH;
        void LateUpdate()
        {
            if (!started || banner == null) return;
            if (Screen.width != lastW || Screen.height != lastH)
            {
                bool first = lastW == 0;
                lastW = Screen.width; lastH = Screen.height;
                if (!first) LoadBanner();
            }
        }

        // ---- interstitial ----
        void LoadInterstitial()
        {
            interstitial?.Destroy(); interstitial = null;
            InterstitialAd.Load(AdsConfig.Interstitial, new AdRequest(), (ad, err) =>
            {
                if (err != null || ad == null) { retryInterstitial = Time.unscaledTime + 30; return; }
                interstitial = ad;
            });
        }

        public bool InterstitialReady => interstitial != null && interstitial.CanShowAd() && !fullScreen;

        public void ShowInterstitial(Action done)
        {
            if (!InterstitialReady) { done?.Invoke(); return; }
            var ad = interstitial; interstitial = null;
            bool finished = false;
            void Finish() { if (finished) return; finished = true; EndFullScreen(); done?.Invoke(); LoadInterstitial(); }
            ad.OnAdFullScreenContentClosed += Finish;
            ad.OnAdFullScreenContentFailed += _ => Finish();
            BeginFullScreen();
            ad.Show();
        }

        // ---- rewarded ----
        void LoadRewarded()
        {
            rewarded?.Destroy(); rewarded = null;
            RewardedAd.Load(AdsConfig.Rewarded, new AdRequest(), (ad, err) =>
            {
                if (err != null || ad == null) { retryRewarded = Time.unscaledTime + 30; return; }
                rewarded = ad;
            });
        }

        public bool RewardedReady => rewarded != null && rewarded.CanShowAd() && !fullScreen;

        public void ShowRewarded(Action<bool> done)
        {
            if (!RewardedReady) { done?.Invoke(false); return; }
            var ad = rewarded; rewarded = null;
            bool earned = false, finished = false;
            void Finish() { if (finished) return; finished = true; EndFullScreen(); done?.Invoke(earned); LoadRewarded(); }
            ad.OnAdFullScreenContentClosed += Finish;
            ad.OnAdFullScreenContentFailed += _ => Finish();
            BeginFullScreen();
            ad.Show(_ => earned = true);
        }

        // Game sound off and the banner hidden while a full-screen ad is up.
        void BeginFullScreen() { fullScreen = true; AudioListener.pause = true; banner?.Hide(); }
        void EndFullScreen() { fullScreen = false; AudioListener.pause = false; banner?.Show(); }

        // ---- consent ----
        public bool PrivacyOptionsRequired =>
            ConsentInformation.PrivacyOptionsRequirementStatus == PrivacyOptionsRequirementStatus.Required;

        public void ShowPrivacyOptions()
        {
            ConsentForm.ShowPrivacyOptionsForm(err =>
            {
                if (err != null) Debug.LogWarning("Sear Pressure ads: privacy options failed: " + err.Message);
                if (ConsentInformation.CanRequestAds()) StartAds();
            });
        }

        void OnDestroy()
        {
            banner?.Destroy(); interstitial?.Destroy(); rewarded?.Destroy();
        }
    }
}
