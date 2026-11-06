# Módulo Admin

> Feature flags globais, permissões por ação e o painel do administrador master.
> **Idioma:** o inglês é a documentação primária. 🇺🇸 [English version](../README.md).

O módulo **Admin** decide o que está ligado no sistema (feature flags) e o que cada usuário pode fazer
(permissões por ação, concedidas por papéis e por exceções individuais de permitir/negar). O motor
vive no Tars (`Pottmayer.Tars.FeatureFlags`, `Pottmayer.Tars.Security.Authorization`); o Admin é dono
do armazenamento, do bootstrap do admin master, do log de auditoria e do painel.

**Status:** planejado. Nada construído ainda.

| # | Documento | O que cobre |
|---|---|---|
| 1 | [Plano de Produto](product-plan.md) | Conceitos, pacotes do Tars, modelo de dados, API, telas, etapas de entrega e decisões |
