# Admin Module

> Global feature flags, per-action permissions and the master administrator's panel.
> **Language:** English is the primary documentation. 🇧🇷 [Versão em português](pt-BR/README.md).

The **Admin** module decides what is switched on in the system (feature flags) and what each user may
do (permissions per action, granted through roles and per-user allow/deny exceptions). The engine
lives in Tars (`Pottmayer.Tars.FeatureFlags`, `Pottmayer.Tars.Security.Authorization`); Admin owns
the catalogs' storage, the master admin bootstrap, the audit log and the panel.

**Status:** in progress. The Tars packages, the module and the enforcement in every module are built
(A1–A3); the panel (A4) is next.

| # | Document | What it covers |
|---|---|---|
| 1 | [Product Plan](en/product-plan.md) | Concepts, Tars packages, data model, API, screens, delivery steps and decisions |
