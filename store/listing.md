# Sear Pressure: store listing

Everything to paste into Google Play Console and App Store Connect. Character limits are in brackets.

- **Developer:** Ragnarok Talent Partners LLC
- **Contact email:** b.kingston396@gmail.com
- **Privacy policy:** `store/privacy-policy.html`. It needs a public web address; see "Hosting the privacy policy" at the end.
- **Version:** 1.0.0 (build 1)
- **Package name:** `com.kingston.searpressure` (permanent once uploaded)
- **Step-by-step Google Play guide:** `store/google-play/GUIDE.md`

---

## Both stores

**App name** [30]
```
Sear Pressure
```

**Full description** [4000]
```
One food truck. Two chefs. A line out the window.

Sear Pressure is a frantic little cooking game about running a food truck on a road trip across America. Chop, cook, plate and serve before the tickets run out, and swap between your two chefs to be in two places at once.

COOK YOUR WAY ACROSS THE COUNTRY
Start at a hometown diner and drive the truck through Route 66 breakfasts, drive-in burgers, Texas smokehouses, Tex-Mex taco trucks, New Orleans jazz brunches, New York pizza, New England lobster shacks and the California coast, all the way to a Thanksgiving feast. That's more than 30 kitchens across 10 stops, with 40 dishes to master.

TWO CHEFS, ONE BRAIN
You control both chefs. Leave one stirring the soup while the other chops onions, then swap in an instant. Kitchens split down the middle, so you pass plates across the counter to keep things moving.

THINGS WILL GO WRONG
Pots boil over. Grease fires spread along the counter. Live lobsters hop off the counter. Grab the extinguisher, keep your cool and get that order out the window.

EARN STARS, UNLOCK THE ROAD
Serve fast for tips, earn up to three stars in every kitchen and unlock the next stop. Spend your coins on kitchen upgrades and chef gear, and dress your chefs in outfits from classic whites to a cowboy hat.

A NEW CHALLENGE EVERY DAY
The daily challenge picks one of your kitchens, adds a twist like Rush Hour, and gives you a quest worth bonus coins. It's the same challenge for everyone that day, and it works offline.

SECRETS ON THE ROAD
Some kitchens are hidden. Some roads lead somewhere unexpected. And somewhere out there, a very hard-to-please food critic is waiting.

• Play offline, anywhere
• Free to play. One optional purchase removes every ad (and makes revives free)
• Built-in tutorial: learn to cook in a couple of minutes
• Portrait or landscape, with left-handed controls
• Chunky pixel art and a sizzling soundtrack

Can you take the heat?
```

---

## Google Play

**Short description** [80]
```
Run a food truck across America. Two chefs, one kitchen, zero time to spare.
```

**Category:** Game › Casual
**Tags (pick up to 5 in Play Console):** Cooking, Time management, Casual, Pixel art, Offline
**Price:** Free  **Contains ads:** Yes (Google AdMob)  **In-app purchases:** Yes: "Remove ads", one-time, $4.99 (product ID `remove_ads`)

**Graphics** (in `store/`)
| Item | File |
|---|---|
| App icon 512×512 | `google-play/icon-512.png` |
| Feature graphic 1024×500 | `google-play/feature-graphic-1024x500.png` |
| Phone screenshots, 1080×2160 (upload 4–8) | `screenshots/google-play-phone/01…07` |

**Content rating questionnaire (IARC)**
- Violence: none. Kitchen fires are cartoon hazards; nobody gets hurt.
- Fear, sex, drugs, gambling, crude humour: none. The food critic is grumpy but never swears.
- Users interact or share content: **No** (no online features in this version).
- Shares location / personal info: **No**.
- Expected rating: Everyone / PEGI 3.

**Target audience:** choose **13 and over**. The game suits every age, but ticking under-13 age groups puts the app under Google's Families policy, which has extra requirements. You can change this later.

**Data safety form** (the game uses Google AdMob; Google's own guide for this form: "Google Mobile Ads SDK: data disclosure")
- Does your app collect or share any of the required user data types? **Yes.**
- Is all collected data encrypted in transit? **Yes.**  Can users request deletion? **No** (we hold no data ourselves; point people to the contact email).
- Declare these, each **collected and shared**, **processed ephemerally: no**, **required** (users can't turn it off), purposes **Advertising or marketing**, **Analytics** and **Fraud prevention, security and compliance**:
  - **Location → Approximate location** (from the IP address)
  - **Device or other IDs → Device or other IDs** (advertising ID)
  - **App activity → Other user-generated content? No**; **App interactions: Yes** (ads viewed and tapped)
  - **App info and performance → Crash logs** and **Diagnostics**
- **Financial info → Purchase history:** the "Remove ads" purchase goes through Google Play Billing. Check Google's current data-safety guidance for Play Billing; declare it only if it says to.
- Progress is saved only on the device and never collected. The Unity engine's anonymous device info falls under Diagnostics above.
- **Advertising ID declaration** (App content → Advertising ID): **Yes**, used for **Advertising or marketing** and **Analytics**.

**New personal developer accounts:** before production access, Google requires a **closed test with at least 12 testers opted in for 14 days in a row**. Upload build 1 to Closed testing first and invite friends by email or Google Group.

---

## Apple App Store

**Subtitle** [30]
```
Food truck cooking frenzy
```

**Promotional text** [170]
```
Two chefs, one food truck and a road trip across America. Chop, cook and swap chefs to beat the rush, and try a new daily challenge every day. Free to play.
```

**Keywords** [100, comma-separated, no spaces needed]
```
cooking,chef,kitchen,food truck,restaurant,burger,time management,diner,pixel,offline,pizza,bbq
```
(95 characters. Don't repeat words from the app name.)

**Primary category:** Games › Casual  **Secondary category:** Games › Simulation
**Age rating questionnaire:** everything "None", except **Cartoon or Fantasy Violence: None** (fires only). Expected rating **4+**.
**App Privacy ("nutrition label"):** because of AdMob, declare **Data Used to Track You: Device ID**, and **Data Linked/Not Linked to You** for Identifiers (Device ID), Location (Coarse), Usage Data (Advertising Data, Product Interaction) and Diagnostics, with purposes Third-Party Advertising and Analytics. On iOS the game should also show Apple's tracking prompt (App Tracking Transparency) before personalised ads; set its message in Assets → Google Mobile Ads → Settings → "User Tracking Usage Description" when you build for iPhone.
**Export compliance:** the game uses no encryption beyond Apple's own, so answer **No** to "uses non-exempt encryption". To skip the question on every upload, add `ITSAppUsesNonExemptEncryption = NO` to the Xcode project's Info.plist.
**Support URL:** required. The privacy-policy page's address works, or any page with your contact email.

**Screenshots** (in `store/screenshots/`)
| Device | Size | Folder |
|---|---|---|
| iPhone 6.9" (required) | 1320×2868 | `iphone-6.9/` |
| iPhone 6.5" | 1284×2778 | `iphone-6.5/` |
| iPad 13" (required if the app runs on iPad) | 2064×2752 | `ipad-13/` |

**App icon:** `app-store/icon-1024.png` (Unity also puts the icon in the Xcode project automatically).

---

## Hosting the privacy policy

Both stores need the policy at a public web address. The easiest free options:
1. **GitHub Pages:** in the repo's Settings → Pages, choose "Deploy from a branch", branch `main`, folder `/ (root)`. The page is then at `https://kingston396.github.io/searpressure/store/privacy-policy.html`. Private repos need a paid GitHub plan for Pages.
2. **Google Sites** (free with your Gmail): make a one-page site and paste the policy text.
3. **Netlify Drop** (free): drag the `store` folder onto app.netlify.com/drop.

## Regenerating the art

The icon, feature graphic, splash logo and screenshots are drawn by the test harness from the game itself:
```
cd SearPressureUnity/Tools/Harness && dotnet build -v q
dotnet run --no-build -- storeart   /tmp/art     # icons, splash logo, feature graphic
dotnet run --no-build -- storeshots /tmp/shots   # every screenshot set
```
