# C-Coins: account cosmetics and PayMongo connection

## Implementation status

The Unity client now contains an account wallet, a profile cosmetic shop, a pending reward queue, safe purchase retries and public build configuration. The approved website is https://crewon-set-web.vercel.app/, using the same PlayFab accounts. PayMongo is the selected payment provider.

**The live server integration is not deployed by these changes.** The website repository, its authenticated wallet API, authoritative completion verification and PayMongo checkout/webhook implementation have not been provided. `CrewCCoins` is the required PlayFab function name, not an assertion that this function already exists. Missing server support leaves rewards pending and purchases unavailable; it never invents spendable coins or a catalog. Website checkout is disabled in the supplied settings until the backend is ready.

## Currency and reward rules

- B-Coins remain the career/crew production budget. C-Coins (`CC`) belong to a PlayFab account and only buy cosmetic appearance.
- The first successful single-player completion of each contract in a career queues **5 C-Coins**. S/A/B/C pass; F does not. Reopening results or replaying the same contract does not create another reward.
- The stable ID is `contract:<careerId>:<contractLevel>`. Career identity is preserved in checkpoint copies. New careers still require the server's eligibility policy; a locally generated career ID is not proof of eligibility.
- Offline/guest completion is pending, not spendable. After the existing save system imports unclaimed guest careers on sign-in, pending claims link to that account. The server must validate ownership and completion before granting anything.
- Single-player career retries, B-Coin resets and load-game rollbacks must never roll back the account wallet. Room rewards are not implemented: the Photon host's grade/room properties alone are not trusted evidence for account currency.
- Profile-frame cosmetics are the currently supported item type. Catalog, prices, ownership and balance come from the server. Equipping an already owned frame changes appearance only. Account balance/ownership are synced; the selected frame is cached per account on this device.

## Client boundaries

`CCoinService` installs automatically and persists across scenes. `GameSaveManager.SetAccount`/logout bind or clear its account view. `GradeManager` queues rewards only within its successful-submission branch. The main-menu and in-game shared profile retain the balance widget and expose a Shop icon tab through `AlmanacProfileAccount`, retaining the profile's movement/cursor owner. Frames use the supported catalog; Hat/Shirt/Pants/Shoes show an unavailable explanation until supported clothing assets and server item types exist. The older public shop-modal entry point remains compatible with existing callers.

The client uses the existing PlayFab SDK/title (`D4EA4`) to call `ExecuteCloudScript<CCoinOperationResult>`. It does not send session tickets to a custom HTTP endpoint, invoke client currency mint/debit APIs or ship payment/server keys.

Global PlayerPrefs keys `SaveSystem.CCoins.v1.<accountId>` contain a display cache, pending reward IDs, one durable purchase intent and selected cosmetic. This cache is **not authoritative**. Spending needs an authenticated, freshly reconciled wallet. An ambiguous purchase is retried with the same operation ID before further purchases are allowed. Late callbacks after logout/account switching/timeouts are discarded; older wallet revisions cannot replace a newer snapshot. Up to 20 reward claims run per sync, rotating unverified claims so later eligible careers are not starved. Automatic sync runs periodically and when returning from the website; SYNC is also available in the shop.

## Required server function contract

Function name: `CrewCCoins` (configurable public name).

Request: `{ "action": "wallet|claimContract|purchase", "data": null|object }`.

Result:

```json
{
  "operationId": "buy:example-stable-operation-id",
  "status": "applied",
  "wallet": {
    "accountId": "authenticated-playfab-id",
    "currency": "CC",
    "balance": 5,
    "revision": 1,
    "owned": [],
    "cosmetics": [
      {
        "id": "frame_ocean",
        "kind": "profile_frame",
        "name": "Ocean frame",
        "description": "A blue profile border. Appearance only.",
        "color": "#2868AA",
        "price": 5
      }
    ]
  }
}
```

This is a wire-format example, **not a deployed catalog or a promised coin-pack price**.

- `wallet`: return the authenticated account's authoritative snapshot/catalog. Clients do not choose the wallet owner.
- `claimContract`: data contains `careerId`, `contractLevel`, `completionId`, `operationId`. Recompute the ID and reward amount server-side. Verify trusted account/career ownership and completion, then settle once. Return `applied` or `already_applied` with the matching operation ID; otherwise use `awaiting_verification`. Do not trust the client rank, PlayerPrefs, client-writable PlayFab save data or submitted IDs as proof. No verified completion pipeline is added by the Unity patch.
- `purchase`: data contains only `itemId`, `operationId`. Resolve current price/catalog server-side, verify funds/ownership and commit wallet debit + entitlement + permanent idempotency record in **one atomic transaction**. Duplicate requests return the previous outcome; terminal rejections return `rejected` with the matching ID and current wallet.
- Return a nonnegative monotonic revision and the authenticated account ID/currency on every settled mutation. Do not return another account's data. Never return secrets or receipts in error messages.

The website and this PlayFab function must use **the same authoritative wallet/transaction store**, not two balances that periodically overwrite one another. A legacy CloudScript read/update of user data plus a separate virtual-currency call is not atomic and is not a safe implementation of paid-currency purchase/grant processing. PlayFab Economy V2 or a transactional backend can be used, with a permanent duplicate-operation ledger beyond short-lived API idempotency windows. Do not enable direct client currency changes for `CC`.

## PayMongo server implementation still required

1. Authenticate the website user to the same PlayFab title/account. No session token is appended to the game's website URL.
2. Create a server-side order with the authenticated account ID and an approved server-side coin-pack/price definition. The client must not supply a trusted coin amount, account or payment status. No pack quantities/prices were specified by the user yet.
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

Compile against the actual Unity Editor, standalone and Editor-tool reference sets. Source-backed tests exercise account isolation, stale callbacks/revisions, offline reward queues, duplicate results, pending purchase retries and rejected client PayMongo proof. These checks do not validate a deployed wallet, live payment or a complete standalone build.

Before enabling production purchases, test actual login/logout, two devices plus the website, server-confirmed 5-coin grants, repeat results, loading old saves, declined payments, forged/wrong-account claims, concurrent purchases, crashes between request/response, duplicate webhooks, webhook-signature failures and test/live separation. Also visually test the profile shop and owned-frame appearance in Unity and a fresh standalone build at different resolutions. Sandbox/test payments must not credit the live wallet.
