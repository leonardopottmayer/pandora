# Communications Module — Product Plan (what is left)

> **Status:** M1 (connect and read), M2 (organize), M3 (send) and M4 (rules) are built. What exists is in
> [Implementation Status](implementation-status.md) and the topic documents; this file keeps only what is
> not built yet.
> 🇧🇷 [Versão em português](../pt-BR/product-plan.md)
>
> Related: [Integrations](../../integrations/README.md) · [Channels](../../channels/README.md) ·
> [Radar](../../radar/README.md)

---

## Later (not scheduled)

| Item | Why it waits |
|---|---|
| **Gmail push (Pub/Sub) instead of polling** | The 2-minute cycle is enough for one person; push needs a public endpoint and a Pub/Sub topic. |
| **Radar consuming `MailMessageReceived`** | Waits for the [Radar](../../radar/README.md) module. |
| **More providers** (Outlook through Microsoft Graph) | Only when there is a mailbox to connect. |
| **ManageSieve for Titan** | Titan does not offer it today, so its rules run in Pandora's engine. |
| **AI conditions in rules** | Needs a decision on what mail content may go to the model. |
| **"Apply to existing messages"** for a rule | Rules act on arrivals only. |
| **Per-mailbox signature, server-side drafts** | Compose keeps its draft in the browser. |
| **Provider-side body search** | Search reads the mirrored headers only (see open point 4 in [Implementation Status](implementation-status.md#known-open-points)). |
| **Attachment cache** | Attachments are fetched from the provider on every download. |
