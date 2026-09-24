// Compiled only while the Google Mobile Ads package is installed (SEARPRESSURE_ADS comes from the asmdef's
// version defines). See store/ADMOB.md.
#if SEARPRESSURE_ADS
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

        bool suppressed;
        bool bannerWanted = true;   // the game allows a banner right now (menus/results, not during play)

        bool reapply;   // after a full-screen ad the host decides again (the game may have moved on to a new service)

        public void SetBannerVisible(bool on)
        {
            if (bannerWanted == on && !reapply) return;
            reapply = false;
            bannerWanted = on;
            if (banner == null) return;
            if (on && !fullScreen) banner.Show(); else banner.Hide();
        }

        // "Remove ads" was bought: drop the banner and stop loading full-screen ads.
        public void Suppress()
        {
            suppressed = true;
            banner?.Destroy(); banner = null; BannerHeightPx = 0;
            interstitial?.Destroy(); interstitial = null;
            rewarded?.Destroy(); rewarded = null;
        }

        public void Begin()
        {
            if (suppressed) { suppressed = false; if (started) { LoadBanner(); LoadInterstitial(); LoadRewarded(); } }
            MobileAds.RaiseAdEventsOnUnityMainThread = true;
            // A previous session may already have consent: start straight away, and refresh consent too.
            if (ConsentInformation.CanRequestAds()) StartAds();
            var request = new ConsentRequestParameters { TagForUnderAgeOfConsent = false };
            ConsentInformation.Update(request, updateError =>
            {
                if (updateError != null)
                {
                    Debug.LogWarning("Sear Pressure ads: consent info failed: " + updateError.Message);
                    // Consent info unavailable (no privacy message published in AdMob yet, offline, ...): start
                    // ads anyway, with no consent string Google serves only what its rules allow. Retry the
                    // consent check later so a form still shows once one is set up and the phone is online.
                    StartAds();
                    if (!ConsentInformation.CanRequestAds()) consentRetryAt = Time.unscaledTime + 60;
                    return;
                }
                if (suppressed) return;   // bought "Remove ads" meanwhile: no ad consent form
                ConsentForm.LoadAndShowConsentFormIfRequired(formError =>
                {
                    if (formError != null) Debug.LogWarning("Sear Pressure ads: consent form failed: " + formError.Message);
                    if (ConsentInformation.CanRequestAds()) StartAds();
                });
            });
        }

        void StartAds()
        {
            if (started || suppressed) return;   // never start the ads SDK for a "Remove ads" owner
            started = true;
            if (AdsConfig.UsingTestIds) Debug.Log("Sear Pressure ads: using Google's TEST ad IDs (see AdsConfig.cs).");
            MobileAds.Initialize(_ =>
            {
                LoadBanner();
                LoadInterstitial();
                LoadRewarded();
            });
        }

        float consentRetryAt;
        void Update()
        {
            if (!started)
            {
                return;
            }
            if (consentRetryAt > 0 && Time.unscaledTime > consentRetryAt && !suppressed) { consentRetryAt = 0; Begin(); }
            float t = Time.unscaledTime;
            if (retryBanner > 0 && t > retryBanner) { retryBanner = 0; LoadBanner(); }
            if (retryInterstitial > 0 && t > retryInterstitial) { retryInterstitial = 0; LoadInterstitial(); }
            if (retryRewarded > 0 && t > retryRewarded) { retryRewarded = 0; LoadRewarded(); }
        }

        // ---- banner ----
        void LoadBanner()
        {
            if (suppressed) return;
            banner?.Destroy();
            var size = AdSize.GetCurrentOrientationAnchoredAdaptiveBannerAdSizeWithWidth(MobileAds.Utils.GetDeviceSafeWidth());
            banner = new BannerView(AdsConfig.Banner, size, AdPosition.Bottom);
            var bv = banner;
            bv.OnBannerAdLoaded += () => { if (bv != banner) return; BannerHeightPx = bv.GetHeightInPixels(); if (fullScreen || !bannerWanted) bv.Hide(); else bv.Show(); };
            bv.OnBannerAdLoadFailed += err =>
            {
                Debug.LogWarning("Sear Pressure ads: banner failed: " + err.GetMessage());
                BannerHeightPx = 0; retryBanner = Time.unscaledTime + 30;
            };
            // The plugin shows a new banner as soon as it loads; hide it first if it mustn't be seen now (during play).
            if (!bannerWanted || fullScreen) bv.Hide();
            bv.LoadAd(new AdRequest());
        }

        // Reload the banner at the new width after the phone rotates.
        int lastW, lastH;
        void LateUpdate()
        {
            if (!started || banner == null || suppressed) return;
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
            if (suppressed) return;
            interstitial?.Destroy(); interstitial = null;
            InterstitialAd.Load(AdsConfig.Interstitial, new AdRequest(), (ad, err) =>
            {
                if (err != null || ad == null) { retryInterstitial = Time.unscaledTime + 30; return; }
                if (suppressed) { ad.Destroy(); return; }   // bought "Remove ads" while it was loading
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
            if (suppressed) return;
            rewarded?.Destroy(); rewarded = null;
            RewardedAd.Load(AdsConfig.Rewarded, new AdRequest(), (ad, err) =>
            {
                if (err != null || ad == null) { retryRewarded = Time.unscaledTime + 30; return; }
                if (suppressed) { ad.Destroy(); return; }
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
        void EndFullScreen() { fullScreen = false; AudioListener.pause = false; reapply = true; }

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
#endif
