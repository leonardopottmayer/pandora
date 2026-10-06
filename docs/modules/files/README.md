# Files Module

> A catalog of everything on the user's disks, inside the Pandora modular monolith.
> **Language:** English is the primary documentation. 🇧🇷 [Versão em português](pt-BR/README.md).
>
> **Status: plan.** Nothing is built. Files comes after [Pandora Desktop](../../architecture/en/desktop-client.md)
> phases D1 (shell) and D2 (device credentials) — see [product-plan.md](en/product-plan.md).

The **Files** module indexes the folders the user chooses on their disks — movies, photos, college
material, books, manuals — so they can browse and search what they have from any device, without the
disk being reachable. A desktop agent on the PC with the disk walks the folders and reports what it
sees; the backend keeps the catalog and notices new, changed, missing and **moved** files. The disk
stays the source of truth: the agent never writes to it, and the backend never holds the bytes.

---

## How this documentation is organized

Like [Assistant](../assistant/README.md), Files has no implementation yet, so it does not have the
full `en/` + `pt-BR/` topic set. What exists today:

| Document | Language | What it covers |
|---|---|---|
| [Product Plan](en/product-plan.md) / [pt-BR](pt-BR/product-plan.md) | en + pt-BR | Scope, principles, scan protocol, data model draft, API, phases F1–F5 |
| [Pandora Desktop](../../architecture/en/desktop-client.md) / [pt-BR](../../architecture/pt-BR/desktop-client.md) | en + pt-BR | The desktop app the agent runs in: shell, bridge, desktop modules, device credentials (cross-cutting) |

Once F1 is built, the module moves to the usual per-topic structure (`overview.md`,
`architecture.md`, `data-model.md`, `scans.md`, `api-reference.md`, `implementation-status.md`), and
`product-plan.md` keeps only what remains.

---

## Quick facts

- **Backend:** not started. Target `Pottmayer.Pandora.Modules.Files.*`, schema `files`, tables
  `filXXX_`.
- **Agent:** `Pottmayer.Pandora.Desktop.Files`, a module of Pandora Desktop, authenticated with a
  device key scoped to `files.agent`.
- **Frontend:** `client-web/src/modules/files`; configuration is editable from anywhere, while the
  native folder picker and the live folder tree only show inside the desktop app on the device itself.
- **Customizable at every level:** any number of devices and roots, a folder selection tree per root,
  and filters (extension, glob, prefix, suffix, contains, regex) scoped to the user, a device, a root
  or a folder. Configuration lives on the server.
- **Off by default**, twice: account switch on the server, device switch on the PC.
- **Depends on:** [Pandora Desktop](../../architecture/en/desktop-client.md) (D1, D2),
  [Identity](../identity/README.md) (devices).
