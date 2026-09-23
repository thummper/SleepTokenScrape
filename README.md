# Sleep Token Store Watcher

Polls the [Sleep Token UK store](https://storeuk.sleep-token.com/collections/all-products) and emails you when
something changes:

- **New products** appear in the collection
- **Back in stock** — a sold-out product or size becomes available again
- **Price changes** — up or down, per variant

Runs as a .NET 10 worker service in Docker, designed to sit on a server indefinitely.

## How it gets the data

The store runs on Shopify, which publishes the collection as JSON:

```
https://storeuk.sleep-token.com/collections/all-products/products.json?limit=250
```

That is Shopify's own structured feed — product ids, variants, prices and stock flags — so there is no HTML
parsing and nothing breaks when the theme changes. Pagination is followed automatically.

## Which stores it watches

The stores are listed under `Watcher:Stores` in
[`src/SleepTokenWatcher/appsettings.json`](src/SleepTokenWatcher/appsettings.json). One container checks
all of them, one after another, every poll interval. Each store has its own state file in `/data`, and each
sends its own emails.

| Key | Platform | Feed |
|---|---|---|
| `sleep-token` | Shopify | `https://storeuk.sleep-token.com/collections/all-products/products.json` |
| `york-ghost-merchants` | Squarespace | `https://www.yorkghostmerchants.com/shop?format=json` |

To add a store, add an entry to the list, push, and redeploy. Shop details are not secrets, so they live in
the repository; only the email credentials come from the environment.

| Field | Meaning |
|---|---|
| `Key` | Unique id with lowercase letters, digits and dashes. Names the state file (`{Key}.json`). |
| `Name` | Shown in email subjects and headings. |
| `Platform` | `Shopify` or `Squarespace`. |
| `BaseUrl` | The shop's root URL. |
| `CollectionHandle` | Shopify: the collection handle. Squarespace: the shop page's URL slug. |
| `AllowEmptyCatalog` | `true` for shops that hide every product between drops. |
| `StateFileName` | Optional override of `{Key}.json`. |

York Ghost Merchants hides every product between drops, so its feed is usually empty.
`AllowEmptyCatalog: true` makes the watcher record that empty shop as the baseline. When a drop goes live,
every product in it is reported as new. Without the flag, the watcher skips empty checks and the first check
of a drop becomes the baseline, so you would get no email for it.

A new store establishes its own baseline on its first check without affecting the others.

## Setup

### 1. Create a Gmail App Password

The watcher signs in to Gmail's SMTP server, which will not accept your normal password.

1. Enable 2-Step Verification on the Google account: <https://myaccount.google.com/security>
2. Create an app password: <https://myaccount.google.com/apppasswords>
3. Copy the 16 characters Google shows you.

### 2. Configure

```bash
cp .env.example .env
```

Edit `.env` and set `GMAIL_ADDRESS`, `GMAIL_APP_PASSWORD` and `NOTIFY_ADDRESS`. Leave
`SEND_STARTUP_TEST_EMAIL=true` for the first run so you get confirmation that SMTP works.

### 3. Run

```bash
docker compose up -d --build
docker compose logs -f
```

The first cycle records a baseline and does **not** email you about the products already in each store — only
about changes after that point. You should see:

```
[sleep-token] Baseline established with 36 products.
[york-ghost-merchants] Baseline established with 0 products.
Sent "Sleep Token UK Store: watcher started" to you@gmail.com.
Sent "York Ghost Merchants: watcher started" to you@gmail.com.
```

Set `SEND_STARTUP_TEST_EMAIL=false` afterwards and `docker compose up -d` to apply.

## Deploying to Portainer

Portainer builds the image on the server straight from this repository — no registry needed.

1. **Stacks → Add stack**, name it `sleep-token-watcher`.
2. Build method: **Repository**.
3. Repository URL: this repo's URL. Reference `refs/heads/main`, compose path `docker-compose.yml`.
   Leave authentication off for a public repo.
4. Under **Environment variables**, add:

   | Name | Value |
   | --- | --- |
   | `GMAIL_ADDRESS` | your Gmail address |
   | `GMAIL_APP_PASSWORD` | the 16-character app password |
   | `NOTIFY_ADDRESS` | where alerts go |
   | `SEND_STARTUP_TEST_EMAIL` | `true` for the first deploy |

5. **Deploy the stack.** The first build pulls the .NET SDK image, so it takes a few minutes;
   later rebuilds are cached and quick.

Check the container logs for `Baseline established` once per store and confirm the test email arrives.
Then set `SEND_STARTUP_TEST_EMAIL` to `false` and hit **Update the stack**.

The compose file deliberately has no `env_file:` entry — Portainer supplies these variables itself, and
locally Compose picks the same names up from `.env`. Any missing required variable fails the deploy with a
message naming it, rather than starting a container that cannot send mail.

**Never put the app password in the repository.** It belongs in Portainer's environment variables (or your
local `.env`, which is gitignored).

### Updating

Push to `main`, then **Pull and redeploy** on the stack. To automate it, turn on **GitOps updates** in the
stack settings — either polling, or a webhook you call from a GitHub Action.

State lives in the stack's `watcher-state` volume and survives redeploys, so updating will not re-baseline or
re-alert you about products you have already seen.

## Operating it

```bash
docker compose logs -f              # follow activity
docker compose ps                   # includes health status
docker compose restart              # apply .env changes
docker compose down                 # stop (state is kept in the volume)
docker compose down -v              # stop and wipe state; next start re-baselines
```

The container reports **healthy** while it completes cycles, and **unhealthy** if it has not finished one in an
hour. State lives in the `watcher-state` Docker volume as `state.json`; deleting it forces a new baseline.

## Configuration

Everything in `appsettings.json` can be overridden by an environment variable, using `__` between nested keys.
`docker-compose.yml` already maps the common ones to friendlier `.env` names.

| Setting | Env var | Default | Notes |
| --- | --- | --- | --- |
| Poll interval | `POLL_INTERVAL` | `00:15:00` | `HH:MM:SS`. Every 15 minutes is plenty and stays polite. |
| Startup test email | `SEND_STARTUP_TEST_EMAIL` | `false` | Only fires when a baseline is being established. |
| Gmail account | `GMAIL_ADDRESS` | — | Used for both SMTP auth and the From address. |
| App password | `GMAIL_APP_PASSWORD` | — | Not your normal password. |
| Recipient | `NOTIFY_ADDRESS` | — | Add more via `Email__ToAddresses__1`, `__2`, … |
| SMTP host / port | `SMTP_HOST` / `SMTP_PORT` | `smtp.gmail.com` / `587` | |
| Collection | `Watcher__CollectionHandle` | `all-products` | Any collection handle on the store. |
| Store | `Watcher__StoreBaseUrl` | `https://storeuk.sleep-token.com` | Works against any Shopify storefront. |
| Time zone | `Watcher__DisplayTimeZone` | `Europe/London` | IANA id, used for email timestamps. |
| SMTP security | `Email__Security` | `StartTls` | `StartTls`, `SslOnConnect`, `Auto`, `None`. |

## Design notes

**Alerts are not lost if email fails.** State is only advanced *after* a successful send, so a broken SMTP
connection means the same changes are re-detected and re-sent on the next cycle rather than silently dropped.
This is at-least-once delivery: a crash between sending and saving can re-send an alert, which is the safer
failure to have.

**An empty response is not treated as "everything vanished".** If the store returns no products — maintenance,
rate limiting, a bad deploy — the cycle is skipped and the previous state is left intact.

**A bad cycle never kills the container.** Exceptions are logged and the loop continues at the next interval.
Transient HTTP failures are retried three times with exponential backoff before the cycle gives up.

**Corrupt state self-heals.** An unreadable `state.json` is logged and treated as "no baseline" rather than
crash-looping.

**Products removed from the collection are ignored** by design — you asked for additions, restocks and price
changes. To alert on removals too, add the case to `ChangeDetector.Compare`.

## Project layout

```
src/SleepTokenWatcher/
  Program.cs                      host, DI and options wiring
  Worker.cs                       the poll loop
  Configuration/                  strongly-typed, validated settings
  Shopify/                        products.json client and DTOs
  State/                          snapshot model and atomic JSON persistence
  Detection/ChangeDetector.cs     pure snapshot diff — the alerting rules
  Notifications/                  HTML/text email building and SMTP delivery
tests/SleepTokenWatcher.Tests/    unit tests for the diff rules
```

## Tests

`ChangeDetector` is pure — no I/O — so the rules that decide whether you get spammed or miss a drop are directly
testable:

```bash
dotnet test
```

Or, without a local .NET 10 SDK:

```bash
docker run --rm -v "${PWD}:/work" -w /work mcr.microsoft.com/dotnet/sdk:10.0 \
  dotnet test tests/SleepTokenWatcher.Tests/SleepTokenWatcher.Tests.csproj
```

## Etiquette

At the default 15-minute interval this is 96 requests a day against a public JSON endpoint the storefront serves
to every browser anyway. Do not drop the interval to seconds — it adds nothing for a merch drop and starts to
look like abuse.
