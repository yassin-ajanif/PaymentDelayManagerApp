# AlterWatcherService + PaymentDelayApp — integration plan

## Shared configuration (JSON)

- **Path:** Same directory as the SQLite database: `%LocalAppData%\PaymentDelayApp\watcher-settings.json` (see `PaymentDelayDbPaths` in `PaymentDelayApp.DataAccessLayer`).
- **Fields:** `schemaVersion` (e.g. `1`), `scanIntervalMinutes` (1–1440), optional **`paymentDelayAppExePath`** (absolute path to `PaymentDelayApp.exe`).
- **Writer:** PaymentDelayApp **Paramètres** — load on open; **Save** writes JSON using an **atomic replace** (temp file in same folder, then move/replace).
- **Reader:** AlterWatcherService — read interval **at the start of each wait** (or when file last-write time changes).

## Phase A — Trigger definition

- Align detection with dashboard “factures en alerte” (`PaymentAlertEvaluator`: `IsPaymentAlert` + not settled).
- Watcher uses the same DB path as the app (`PaymentDelayDbPaths`).

## Phase B — Watcher host + poll loop

- Hosted worker: **scan → act →** `Task.Delay(TimeSpan.FromMinutes(interval))` from JSON.
- If file missing or invalid: **safe default** (5 minutes) + log.

## Phase C — Launch / single instance

- After each interval: refresh alert flags, **count** unsettled rows with `IsPaymentAlert`; if that count is **greater than zero**, start `PaymentDelayApp.exe` with **`--show-alerts`**. Optional JSON **`paymentDelayAppExePath`** if auto-resolve fails.
- **Single instance:** mutex only in **PaymentDelayApp** (`PaymentDelayAppSingleInstance` + `Program.Main`). The watcher may call `Process.Start` each interval; a second process exits at startup if the mutex already exists.

## Phase D — IPC / CLI

- **`PaymentDelayApp --show-alerts`:** used when the watcher starts the GUI.

## Phase E — Cooldown / UX

- Optional later: debounce / single instance / `lastNotifiedUtc` in JSON to avoid repeated windows.

## Phase F — PaymentDelayApp: Paramètres

- **Watcher / surveillance:** `scanIntervalMinutes`, Save; note that the interval applies from the **next** wait cycle.

## Phase G — Deploy, logging, verification

- Log resolved paths to `watcher-settings.json` and `app.db` on watcher startup.
- **Service account:** If the service runs as **LocalSystem**, `%LocalAppData%` is not the interactive user’s profile. Either run the service as the **same user** as the desktop app, or move user data to **`CommonApplicationData\PaymentDelayApp\`** with ACLs (product-wide path change).

## Phase H — Resilience

- `schemaVersion` in JSON for future evolution.
- Watcher tolerates corrupt/partial JSON (fallback + log).

## Implementation order

1. Path helper + `WatcherSettingsFile` in DataAccessLayer.
2. Paramètres UI in the app (atomic save).
3. Watcher worker loop + JSON + logging.
4. Optional: single-instance + wake existing window; JSON `lastNotifiedUtc` cooldown.
