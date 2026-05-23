# Google OAuth verification playbook

> Phase K Wave 7 — Bishop (Backend).

This playbook covers the operator-side work needed to move the
Mahjong-Autotable Google OAuth client from **Testing** state (≤ 100
external users, "unverified app" warning screen) to **In production**
state (no warnings, no user cap). Verification is run by Google's
**OAuth API Verification team** and typically takes **4–6 weeks**
from initial submission to approval; the most common reason for
rejection is missing scope justification or an incomplete privacy
policy — this playbook checklists both up-front.

## 1. Prerequisites

Before opening the verification submission in the Google Cloud
Console:

| # | Requirement | Where it lives |
| - | ----------- | -------------- |
| 1 | **Privacy policy URL** | `https://api.{env}.mahjong-autotable.com/privacy` — served by [`PrivacyController`](../src/backend/src/Mahjong.Autotable.Api/Controllers/PrivacyController.cs) (Wave-2 deliverable). MUST be reachable WITHOUT authentication. |
| 2 | **Terms of Service URL** | `https://api.{env}.mahjong-autotable.com/terms` — same controller, separate action. |
| 3 | **Homepage URL** | `https://mahjong-autotable.com` — production landing page. |
| 4 | **Application logo** (120×120 PNG, ≤ 1 MB, no transparency) | `assets/branding/google-oauth-logo-120.png`. |
| 5 | **Authorized domain** | `mahjong-autotable.com` — proven via Google Search Console verification (DNS TXT or HTML meta). See §3. |
| 6 | **Redirect URIs** (must EXACTLY match the running app) | Production: `https://api.prod.mahjong-autotable.com/api/auth/oauth/google/callback`. Staging: `https://api.staging.mahjong-autotable.com/api/auth/oauth/google/callback`. Local dev URIs SHOULD be removed before submission to avoid "low trust" findings. |
| 7 | **Demo video** (90 s, public YouTube unlisted) | See §5. |
| 8 | **Scope justification** (≤ 500 chars per scope) | See §4. |

## 2. Scope inventory

The Wave 4 OAuth flow requests **only the minimal identity scopes**:

| Scope | Purpose | Verification tier |
| ----- | ------- | ----------------- |
| `openid` | OIDC base; required for `id_token`. | **Non-sensitive** — no verification required, but listed for transparency. |
| `https://www.googleapis.com/auth/userinfo.email` | Confirms the user's verified email so duplicate-account merge works. | **Non-sensitive**. |
| `https://www.googleapis.com/auth/userinfo.profile` | Reads `name` + `picture` so the in-game lobby can show the player's display name + avatar without a separate upload. | **Non-sensitive**. |

The app **DOES NOT** request any of the following — explicitly
calling this out in the verification submission body short-circuits
the most common "scope reduction" follow-up questions:

* `…/auth/drive*` — no Drive access.
* `…/auth/calendar*` — no Calendar access.
* `…/auth/gmail*` — no Gmail access.
* `…/auth/contacts*` — no Contacts access.
* `…/auth/spreadsheets*` — no Sheets access.
* Any `restricted` or `sensitive` scope per
  [Google's scope sensitivity matrix](https://developers.google.com/identity/protocols/oauth2/scopes).

Because all three scopes are **non-sensitive**, verification
turnaround is typically the **fast track (3–5 business days)** —
NOT the 4–6-week brand-verification cycle that sensitive/restricted
scopes attract. The 4–6-week timeline above is the conservative
upper bound that assumes Google bounces the submission once for
clarification.

## 3. Authorized-domain verification

Verify `mahjong-autotable.com` (the apex domain — NOT a subdomain;
Google's verification is apex-only) via Google Search Console:

```bash
# Option A — DNS TXT record (preferred; persistent).
# In your DNS provider, add:
#   _google-site-verification.mahjong-autotable.com  TXT  "google-site-verification=<token>"
# Where <token> is the value Google issues in the Search Console
# console after you click "Add property".

# Verify the record propagated:
dig _google-site-verification.mahjong-autotable.com TXT +short
```

Once Search Console reports the property as verified, the domain
automatically appears in the **Authorized domains** dropdown of the
OAuth consent screen configuration.

## 4. Scope justification copy

Paste the following into the **"Why does your app need to request
access to sensitive scopes?"** field, even though our scopes are
**non-sensitive** — Google's submission form requires some
justification text and a thorough up-front explanation prevents
follow-up questions:

> Mahjong-Autotable is a real-time multiplayer mahjong table that
> requires player identity to (a) prevent the same Google account
> from claiming two seats at the same table (cheating prevention),
> (b) persist a player's match history across sessions, and (c)
> display the player's name + avatar in the lobby UI.
>
> We request the three lowest-tier identity scopes: `openid`,
> `userinfo.email`, and `userinfo.profile`. We DO NOT request
> Drive, Calendar, Gmail, Sheets, Contacts, or any other scope
> classified as sensitive or restricted by Google.
>
> Email is used only to merge duplicate accounts when a player
> signs in via a different OAuth provider with the same email.
> Profile (name + picture) is used only in the lobby UI; we do not
> store the picture URL beyond the user's session.
>
> User data is stored in our own PostgreSQL database hosted in
> AWS us-east-1, encrypted at rest with AWS KMS. We never sell,
> rent, or share user data with third parties. Our privacy policy
> at https://api.prod.mahjong-autotable.com/privacy documents the
> full data lifecycle including the user's right to delete their
> account (which cascade-deletes all stored profile data within
> 30 days).

## 5. Demo video script (90 s)

Google requires a public (unlisted is fine) YouTube video showing
**the OAuth consent screen + the post-auth flow** for each
requested scope. Keep it under 90 seconds — anything longer is
correlated with rejection per Google's own guidance.

| Time | Beat | Voiceover |
| ---- | ---- | --------- |
| 0–5 s | App homepage. | "This is Mahjong-Autotable, a multiplayer mahjong table." |
| 5–15 s | Click **Sign in with Google**. | "We use Google sign-in for identity only — no Drive, Calendar, or Gmail access." |
| 15–35 s | Google consent screen (CRITICAL: must show the FULL list of requested scopes — pause for 5 s). | "The consent screen requests three scopes: OpenID, email, and basic profile information." |
| 35–55 s | Redirect back to app + show the lobby UI. | "After consent, the user lands in the lobby. Their Google display name and avatar appear in the top-right corner — that's the only profile data we use." |
| 55–75 s | Show the user's settings page → **Delete my account** button. | "Users can delete their account at any time. Deletion is final and cascade-removes all stored profile data within 30 days." |
| 75–90 s | Show the privacy policy URL + terms URL. | "Our privacy policy and terms of service are linked at every step of the sign-in flow." |

Upload the rendered MP4 to YouTube as **Unlisted**. Paste the
URL into the **Demo video** field of the verification submission.

## 6. Submission checklist

In Google Cloud Console → APIs & Services → OAuth consent screen:

- [ ] **App information** populated: name, user support email, app logo (120×120).
- [ ] **App domain** populated: homepage, privacy policy, terms of service. All three MUST resolve over HTTPS without redirects.
- [ ] **Authorized domains** lists `mahjong-autotable.com` (apex only — subdomains are auto-allowed).
- [ ] **Developer contact** has TWO email addresses on `mahjong-autotable.com` (Google rejects single-address submissions).
- [ ] **Scopes** lists exactly: `openid`, `userinfo.email`, `userinfo.profile` — nothing else.
- [ ] **Test users** — empty for the production submission (the test-user list is only for **Testing** state).
- [ ] **App state** — **In production** selected.
- [ ] **Submit for verification** clicked.

After submission, the **App verification** tab shows the queue
status. Google sends progress emails to the developer contacts;
typical responses arrive within 3–5 business days for our
non-sensitive scope set.

## 7. Common rejection reasons + fixes

| Rejection reason | Root cause | Fix |
| ---------------- | ---------- | --- |
| "Homepage / privacy / terms URL doesn't match authorized domain" | URL points to a domain NOT in the Authorized Domains list. | Ensure all three URLs are on `*.mahjong-autotable.com`; verify the apex via Search Console (§3). |
| "Privacy policy doesn't disclose how Google user data is used" | The `/privacy` page is missing the Google-OAuth-specific paragraph. | Add a "Google OAuth data" section to [`PrivacyController`](../src/backend/src/Mahjong.Autotable.Api/Controllers/PrivacyController.cs) covering email + profile usage. |
| "Demo video doesn't show the consent screen" | Video cuts to the post-auth lobby too fast. | Re-record with a 5-second pause on the consent screen — see §5 timing. |
| "Redirect URI mismatch" | Local-dev redirect URIs left in the OAuth client config. | Remove `http://localhost:*` URIs before submission. They can be re-added on a SEPARATE OAuth client used for local dev. |
| "Scope reduction request" | Submission did not pre-empt the reduction question. | Re-submit with the §4 scope-justification copy verbatim. |

## 8. Post-approval operations

Once verified, the **In production** state is permanent unless
Google revokes the app for policy violation. Verification status
is preserved across:

* OAuth client secret rotations (Apone owns the
  `Auth__Providers__Google__ClientSecret` SSM parameter rotation —
  see [`docs/secret-rotation.md`](secret-rotation.md)).
* Redirect URI additions (no re-verification required).
* Scope reductions (auto-approved; expansions trigger
  re-verification).

Scope expansions DO trigger re-verification. If a future wave needs
a sensitive or restricted scope (e.g., Drive for replay export),
budget **6–8 weeks** + **a third-party security audit (CASA)** for
the re-verification cycle. The CASA audit fee is ~$15 000 USD as
of 2024 — Apone should budget this in the Wave-9 financial review
if any sensitive scope is added.

## 9. Cross-references

* [`docs/oauth-google.md`](oauth-google.md) — code-side OAuth client integration (if shipped; otherwise this is the canonical Google OAuth doc).
* [`docs/secret-management.md`](secret-management.md) — OAuth client-secret rotation.
* [`docs/secret-rotation.md`](secret-rotation.md) — cadence for the Google OAuth client secret.
* [`src/backend/src/Mahjong.Autotable.Api/Auth/AuthOptions.cs`](../src/backend/src/Mahjong.Autotable.Api/Auth/AuthOptions.cs) — `Auth:Providers:Google:{ClientId,ClientSecret}` configuration schema.
