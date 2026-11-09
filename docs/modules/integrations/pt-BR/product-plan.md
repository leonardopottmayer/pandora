# Módulo Integrations — Roadmap (o que falta)

> **Status:** as fases **I1 (núcleo)**, **I2 (resiliência)**, **I3 (credenciais digitadas)** e **I5
> (várias contas por provedor)** estão implementadas. Este arquivo acompanha só o que **ainda não
> existe**. Para o que existe, veja os docs do módulo: [README](README.md) ·
> [Visão geral](overview.md) · [Arquitetura](architecture.md) · [Modelo de dados](data-model.md) ·
> [OAuth e credenciais](oauth-and-credentials.md) · [Status de implementação](implementation-status.md).
> 🇺🇸 [English version](../en/product-plan.md)
>
> Planos relacionados: [Agenda](../../agenda/pt-BR/product-plan.md) ·
> [Channels](../../channels/pt-BR/product-plan.md) · [Assistant](../../assistant/pt-BR/product-plan.md)

---

## Verificar a I5 com o Google real

A I5 está construída e coberta por testes, mas não foi verificada à mão com o projeto real do Google
Cloud:

- três contas Google e duas chaves Gemini conectadas e nomeadas;
- o Assistant roda na chave escolhida nas configurações dele;
- desconectar uma chave em uso mostra "Usada por: Assistant · chat" e desconecta mesmo assim;
- o Assistant então pede uma conta.

## Provedor `openai`

Um `CredentialProviderDescriptor(..., AuthKind.ApiKey)` no catálogo. Entra junto com o
[segundo provedor](../../assistant/pt-BR/product-plan.md#1-segundo-provedor-openai) do Assistant.

## Fase I4 — Mais provedores *(conforme a demanda, sem data)*

- Microsoft (Outlook Calendar / To Do), CalDAV (genérico; cobre Apple/Fastmail/Nextcloud).

---

## Questões em aberto

1. **Trava de refresh no processo vs. advisory lock.** A implementação serializa o refresh com uma trava
   no processo (monolito de processo único). Rever só se o host for escalado horizontalmente.
