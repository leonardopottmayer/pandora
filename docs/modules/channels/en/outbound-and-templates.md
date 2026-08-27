# Outbound & Templates

[← Back to index](../README.md) · Related: [Architecture](architecture.md), [Data Model](data-model.md)

---

## 1. Two ways in

The rule for which entry point a producer uses is simple: **whoever owns the buttons owns the
`NotifyUserRequested`.**

- **No buttons (`identity.*`).** The producer publishes a *fact* (`PasswordResetRequested`,
  `AccountActivationRequested`, `MfaEnabled`, …) and a **subscriber in this module** maps it to a
  `TemplateKey` + payload and category. This is how Identity works; it stays that way. Subscribers:
  `AccountActivationRequestedHandler`, `AccountActivatedHandler`, `PasswordResetRequestedHandler`,
  `PasswordChangedHandler`, `MfaEnabledHandler`, `MfaDisabledHandler`.
- **With buttons (`agenda.*`, `assistant.*`).** The caller publishes **`NotifyUserRequested`** directly,
  because each button's action and payload are its domain. Handled by `NotifyUserRequestedHandler`.
- **Ad-hoc.** `SendNotificationRequested` is the escape hatch — a plain addressed send, not attributed
  to a user. Handled by `SendNotificationRequestedHandler`.

## 2. The rendering chain

Rendering happens **at enqueue, not at send** (principle C6), so what went out is stored and retry
resends identical bytes.

| Participant | Knows | Does not know |
|---|---|---|
| **Producer** (Identity, Agenda, …) | The fact + (for buttons) the actions and payloads. | That email, templates or channels exist. |
| **Subscriber** (here) | fact → `TemplateKey` + flat payload + category; builds derived values (e.g. the reset URL). | How the text is written. |
| **Renderer** (here) | The file, by `(key, channel, locale)`. Substitutes placeholders, nothing else. | Where the payload came from. |
| **Queue** (here) | The already-rendered content. | Everything else. |

`NotificationEnqueuer` renders per resolved channel, then persists one `Notification` per channel.

## 3. Fan-out

`NotifyUserRequested` names a user and a category. The enqueuer:

1. Reads the user's **preference** for that category (`chn005`) — the ordered channel list; empty ⇒
   muted (except `identity.*`, which is mandatory and skips this).
2. Intersects it with the user's **usable** channels (`chn001`, verified **and** enabled).
3. Renders and enqueues **one row per surviving channel**, all sharing a `group_id`.
4. Dedup is per channel via `uq_chn006_correlation_channel (correlation_id, channel)` — the same
   request reaching email and Telegram is two rows, not one swallowed as a duplicate.

Each row retries and fails independently, so the group's status is honest per channel.

## 4. Templates

Templates are **files in the repository** (embedded resources), not database rows — content that goes
through code review and tracks the code version that fills it. Layout is a tree keyed by
`key / channel.locale`:

```
Templates/
├── password-reset/
│   ├── email.pt-BR.txt          (line 1 = subject, rest = body)
│   └── email.en.txt
└── agenda.reminder.due/
    ├── email.pt-BR.txt
    ├── telegram.pt-BR.txt
    └── telegram.en.txt
```

`FileNotificationTemplateRenderer` selects the file and substitutes `{{placeholder}}` values — no
logic. `TemplateCatalog` + `TemplateCatalogValidator` enumerate known keys × allowed channels ×
locales and **fail startup** if a variant is missing, so a missing Telegram template is a boot error,
not a runtime surprise.

## 5. Buttons

Label is content; action is domain.

- The **caller** declares the actions and each one's payload.
- The **template** for the channel carries the labels per action (labels have a locale):
  `buttons.task_done = ✓ Done`.
- The **merge** happens at render time. At enqueue, each action becomes an `Interaction` row (`chn003`)
  and the rendered Telegram button carries the **interaction id** (not the action) as `callback_data`.
  Channels that don't support buttons drop the list without error.

## 6. The dispatcher & retry

`NotificationDispatcherBackgroundService` scans due rows (`status = Pending`, `next_attempt_at ≤ now`
via `ix_chn006_status_next_attempt_at`), sends through the channel's `IChannelTransport`
(`EmailChannelTransport` / `TelegramChannelTransport`), and advances the aggregate:

- **Success** → `Sent`, records `provider_message_id` (enables threaded replies).
- **Transient failure** → back to `Pending` with exponential backoff (`attempt_count`,
  `next_attempt_at`), until `max_attempts` → `Dead`.
- **Permanent transport failure on a channel** (e.g. the user blocked the bot) → disables the
  `UserChannel` (`disabled_reason`) and publishes `UserChannelDisabled`.
