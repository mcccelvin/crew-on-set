# C-Coins: account cosmetics and PayMongo connection

## Implementation status

The Unity client now contains an account wallet, a profile cosmetic shop, a pending reward queue, safe purchase retries and public build configuration. The approved website is https://crewon-set-web.vercel.app/, using the same PlayFab accounts. PayMongo is the selected payment provider.

The website and game use the same PlayFab account and `CC` virtual currency. The game reads balance and cosmetic ownership from PlayFab inventory, and cosmetic purchases use the website's PlayFab catalog item IDs, so website top-ups and purchases appear after the next game sync. A website running in mock/demo mode is separate sample data and cannot sync to a real game wallet. `Tools/CloudScript/CrewCCoins.js` is prepared for the Development title but must still be uploaded and deployed in Game Manager; until then, level rewards stay pending. The supplied PayMongo coin-pack UI remains disabled in the game.

## C-Coin pack storefront

The profile's **BUY C-COINS** button now opens `CCoinPackShopUI`, a four-card storefront inside the existing profile modal. It reuses the original stage artwork and blue/red buttons, with cream paper, dark ink outlines and gold C-stamped coin/case illustrations (`CCoinPackArt`). Mesh illustrations remain sharp at different resolutions. Closing it returns to the underlying customization page without changing movement/cursor ownership. Closing the whole profile also dismisses this storefront.

`Assets/Resources/CCoinSettings.asset` contains four display offers. The confirmed Starter Pack is **50 C-Coins / PHP 49.00** (`starter_50`, `priceCentavos: 4900`). Studio, Director and Premiere are **Coming soon**: their amounts and prices are unconfigured, not invented. Edit `coinPacks` after confirming further offers. These local values are display configuration, never trusted payment prices or grants.

**Payment is still disabled.** The user will supply backend details later. Configured cards retain their displayed price but cannot start checkout while `websitePurchasesEnabled` is false, the URL is not approved, the player is signed out or the wallet is busy. Click handlers recheck those conditions. No payment API, checkout route or secret has been guessed. The existing website handoff remains available for future activation: it opens the approved website, where the user must select/confirm the pack again. It does not automatically send/select the pack or create a PayMongo session. Match the website's authoritative catalog, authenticated checkout and verified wallet/webhook first, then implement that pack-specific handoff and test it before enabling purchases.

The shop identifies cached balances, offers **SYNC**, and explicitly states when it is only a preview. It does not grant coins on opening, clicking, browser return or local offer changes. Existing server-confirmed wallet refresh remains the only source of spendable coins.

## Currency and reward rules

- B-Coins remain the career/crew production budget. C-Coins (`CC`) belong to a PlayFab account and only buy cosmetic appearance.
- The first successful single-player completion of each contract in a career queues **100 C-Coins**. S/A/B/C pass; F does not. Reopening results or replaying the same contract does not create another reward. The game shows the pending reward on Client Feedback. The interim server handler allows one claim per level per PlayFab account (up to 500 CC), but cannot prove the local client actually passed.
- The stable ID is `contract:<careerId>:<contractLevel>`. Career identity is preserved in checkpoint copies. The interim handler caps by PlayFab account and level rather than trusting client-generated career IDs.
- Offline/guest completion stays pending locally until linked to an account. The interim handler derives the account from the authenticated PlayFab call, but does not prove ownership of a career or a real pass.
- Single-player career retries, B-Coin resets and load-game rollbacks must never roll back the account wallet. Room rewards are not implemented: the Photon host's grade/room properties alone are not trusted evidence for account currency.
- Supported cosmetics include profile frames plus 36 bundled character parts: 6 accessories, 6 hairstyles, 5 faces, 2 bodies, 5 shirts, 6 pants and 6 shoes. Matching website cosmetics use the website catalog's names, descriptions and prices; the five face expressions are free and auto-owned in the game. Bodies have no website counterpart and are not purchasable through the connected store. Frames keep their server catalog prices. Account balance and owned website cosmetics sync through PlayFab; equipped selections remain local to this device.

## Client boundaries

`CCoinService` installs automatically and persists across scenes. `GameSaveManager.SetAccount`/logout bind or clear its account view. Wallet refresh reads the account-wide PlayFab `CC` balance and owned cosmetic inventory; game purchases use the website's PlayFab catalog IDs and `PurchaseItem` API. `GradeManager` queues rewards only for a first passing submission in a contract. The shared profile Shop has Accessories, Hair, Face, Body, Tops, Bottoms, Shoe Wear and Frames tabs. TRY ON renders an unsaved preview without charging or granting ownership. EQUIP selects an owned part for its category; DEFAULT LOOK clears selections, not entitlements. The older public shop-modal entry point remains frame-only.

`CharacterCosmeticCatalog` references the original FBX assets under `Assets/Player/CUSTOMIZATION/fbx` so standalone builds include them. It preserves their common origin and imported scales for item thumbnails. The original source parts are static meshes; `CharacterCosmeticRigBaker` creates a separate `CharacterCosmeticRig` resource with transferred weights and original bind matrices for the existing profile skeleton. TRY ON and confirmed equipped parts now replace only their matching slots on the original character before its idle pose is sampled. Other default/equipped parts remain visible, and multiple trial slots can be combined without granting ownership. No animated gameplay/multiplayer rig is replaced. Thumbnails are cached at 256x256 and released when the catalog unloads; the full dressed portrait uses the existing preview lifecycle. See `Docs/PlayerProfile.md` for the regeneration menu and fitting limitations.

The client uses the existing PlayFab SDK/title (`D4EA4`) for authenticated wallet reads and catalog purchases. It does not send session tickets to a custom HTTP endpoint, invoke client currency mint/debit APIs or ship payment/server keys.

The shared profile's **SYNC ACCOUNT** button requests this wallet refresh separately from checkpoint stats/B-Coin sync. A successful stats checkpoint upload never marks the C-Coin cache as verified. Balance and website cosmetic inventory sync directly from PlayFab. The included Legacy CloudScript handler grants one 100-CC reward per level per account and caps the account at five rewards (500 CC).

Global PlayerPrefs keys `SaveSystem.CCoins.v1.<accountId>` contain a display cache, pending reward IDs, one durable purchase intent and selected cosmetic. This cache is **not authoritative**. Spending needs an authenticated, freshly reconciled wallet. An ambiguous purchase is retried with the same operation ID before further purchases are allowed. Late callbacks after logout/account switching/timeouts are discarded; older wallet revisions cannot replace a newer snapshot. Up to 20 reward claims run per sync, rotating unverified claims so later eligible careers are not starved. Automatic sync runs periodically and when returning from the website; SYNC is also available in the shop.

## Required server function contract

Function name: `CrewCCoins` (configurable public name). It is required for gameplay reward claims, not ordinary wallet reads or website cosmetic purchases.

Request: `{ "action": "claimContract", "data": { "careerId", "contractLevel", "completionId", "operationId" } }`.

Reward claim result:

```json
{
  "operationId": "contract:career-id:1",
  "status": "applied"
}
```

The client refreshes PlayFab after an `applied` or `already_applied` result to read the new `CC` balance. The server ignores any player ID or amount in the request and uses the authenticated `currentPlayerId` plus the fixed server amount.

`Tools/CCoins.character-catalog.json` documents the game model IDs, website PlayFab item IDs and matching display metadata. The website catalog must include the corresponding product IDs and prices. Body models have no website counterpart. The game verifies the current PlayFab catalog price before purchase; PlayFab settles the debit and entitlement. Never trust a client price or ownership claim. This file is a mapping reference, not a deployed reward backend.

- `claimContract`: `Tools/CloudScript/CrewCCoins.js` validates the level and deterministic claim envelope, derives the player from PlayFab, and stores a server-only per-account/per-level claim marker before granting 100 currency `CC`. A repeated claim returns `already_applied`; only five level claims can be credited per PlayFab account. A marker left at `claiming` needs admin review before it is cleared or changed.
- This handler does not implement `wallet` or cosmetic `purchase`; the game reads the PlayFab balance/inventory directly and buys website cosmetics through the PlayFab catalog API. Legacy profile-frame purchases still need a separate implementation if used.

**Reward verification limit:** this is a bounded interim system, not proof that a level was genuinely passed. The grade and result are produced by the Unity client, and the client can write the uploaded production log. The handler caps ordinary/replayed claims, but a modified client can still request an unearned first claim for a level. Legacy currency and Internal Data writes are separate operations; rare interrupted claims can remain in `claiming` for admin review, and concurrent first claims are not protected by a transactional compare-and-swap. Full completion verification requires a server-authoritative game result or another trusted validation source. Do not describe this interim path as cheat-proof or financially abuse-proof.

### Deploy the Development handler

1. In PlayFab Game Manager, select the **Development** title and open **Live Ops → Cloud Script → Revisions (Legacy)**.
2. Confirm **Currency (Legacy)** contains currency code `CC` in this Development title. Upload `Tools/CloudScript/CrewCCoins.js` as a new legacy revision. This is a standalone handler file; if the revision editor requires a full script, add this handler to the existing sample instead of replacing other project handlers.
3. Review the submitted revision, then explicitly deploy that revision to **Live** for the Development title. Uploading alone does not activate it.
4. Test with a development account by passing a level and checking the PlayFab `CC` balance and server-only `CCoinReward.Level1` marker. A sequential second claim for that same level/account should not add currency again.
5. Do not deploy to the production title until the Development result and account-wide five-reward cap are confirmed. Keep the unearned-claim limitation above in mind.
- `purchase`: data contains only `itemId`, `operationId`. Resolve current price/catalog server-side, verify funds/ownership and commit wallet debit + entitlement + permanent idempotency record in **one atomic transaction**. Duplicate requests return the previous outcome; terminal rejections return `rejected` with the matching ID and current wallet.
- Any future wallet/purchase handler must return a nonnegative monotonic revision and authenticated account/currency, and must not return another account's data or secrets. Those actions are not implemented in the current reward-only script.

The website and this PlayFab function must use **the same authoritative wallet/transaction store**, not two balances that periodically overwrite one another. A legacy CloudScript read/update of user data plus a separate virtual-currency call is not atomic and is not a safe implementation of paid-currency purchase/grant processing. PlayFab Economy V2 or a transactional backend can be used, with a permanent duplicate-operation ledger beyond short-lived API idempotency windows. Do not enable direct client currency changes for `CC`.

## PayMongo server implementation still required

1. Authenticate the website user to the same PlayFab title/account. No session token is appended to the game's website URL.
2. Create a server-side order with the authenticated account ID and an approved server-side coin-pack/price definition. The client must not supply a trusted coin amount, account or payment status. The game displays the confirmed Starter Pack (50 C-Coins / PHP 49.00); the other three packs remain unconfigured. Mirror approved offers in the authoritative backend and validate prices there, not from client data.
3. Create PayMongo hosted checkout on the server with its secret key. The game opens the approved website; no checkout route is invented before the website source is available.
4. Verify the PayMongo webhook against its raw request body and webhook signing secret, using the appropriate test/live signature and a timestamp replay check. Confirm the checkout/order, paid event, expected amount/currency and live/test mode against the server order.
5. Atomically credit the account wallet and store a permanent unique provider transaction/order record. Duplicate webhook delivery must not credit twice. A successful browser redirect, screenshot or client-supplied receipt is never sufficient proof.
6. Refresh the website and game from that wallet. Returning focus to the game requests a sync; it never directly grants coins.

Official references: [PayMongo hosted checkout quick start](https://docs.paymongo.com/docs/payment-channels-hosted-checkout-quick-start), [PayMongo webhook verification](https://docs.paymongo.com/docs/developer-tools-webhook-setup-management), [PlayFab idempotency](https://learn.microsoft.com/en-us/gaming/playfab/economy-monetization/economy-v2/tutorials/idempotent-transactions-and-retries).

`ValidateStoreReceipt` is a future, unconnected store-adapter entry point; its generic `validateReceipt` action requires a separate real server/store integration. PayMongo is explicitly rejected there because its verified website webhook owns payment credits. This is not a Unity IAP adapter or an enabled payment implementation.

## Environment configuration and standalone builds

`Tools/CCoins.client.env.example` lists **public configuration only**:

```dotenv
PLAYFAB_TITLE_ID=D4EA4
CREW_CCOINS_FUNCTION=CrewCCoins
CREW_CCOINS_WEBSITE_URL=https://crewon-set-web.vercel.app/
```

The existing PlayFab title remains configured by the project's existing PlayFab settings; this change does not overwrite it. These are environment-variable names, not an automatically loaded `.env` file. The wallet balance itself is dynamic account data, **never an environment variable**.

`CCoinSettings` reads public function/website overrides from the process environment. `Assets/Resources/CCoinSettings.asset` supplies standalone defaults. Only the user-approved HTTPS host `crewon-set-web.vercel.app` is accepted, without credentials/query/fragment.

After setting variables in the Unity process, use **Crew-On-Set > C-Coins > Copy Public Environment Configuration into Build**, then rebuild. The build guard prevents a mismatched environment override from silently shipping different defaults. Machine variables on the developer PC are not copied into someone else's build automatically. Set `websitePurchasesEnabled` only after verified website checkout, wallet and webhook deployment/testing.

Server-only environment variables belong in Vercel/server configuration: `PAYMONGO_SECRET_KEY`, `PAYMONGO_WEBHOOK_SECRET`, `PLAYFAB_SECRET_KEY` and any transactional-database credentials. Never put their values in Unity assets, client `.env` files, `NEXT_PUBLIC_*` variables, Git or browser code. Do not send secret values through chat.

## Verification and remaining acceptance tests

Editor/Development builds have an F12 **+100 TEST C-COINS** button for offline shop testing. It activates an isolated in-memory wallet/catalog; test BUY/EQUIP never save to the real account cache or call the backend. **RESTORE REAL C-WALLET**, changing accounts or restarting discards it. Normal release builds cannot activate it. Committing the CloudScript file does not upload or deploy it to PlayFab. See `Docs/DeveloperCommands.md`.

Compile against the actual Unity Editor, standalone and Editor-tool reference sets. Source-backed tests exercise account isolation, stale callbacks/revisions, offline reward queues, duplicate results, pending purchase retries and rejected client PayMongo proof. These checks do not validate a deployed wallet, live payment or a complete standalone build.

Before enabling production purchases, test actual login/logout, two devices plus the website, server-confirmed 5-coin grants, repeat results, loading old saves, declined payments, forged/wrong-account claims, concurrent purchases, crashes between request/response, duplicate webhooks, webhook-signature failures and test/live separation. Also visually test the profile shop and owned-frame appearance in Unity and a fresh standalone build at different resolutions. Sandbox/test payments must not credit the live wallet.
