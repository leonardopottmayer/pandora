# Módulo Communications — Plano de Produto (o que falta)

> **Status:** M1 (conectar e ler), M2 (organizar), M3 (enviar) e M4 (regras) estão construídas. O que
> existe está no [Status de implementação](implementation-status.md) e nos documentos de tópico; este
> arquivo guarda só o que ainda não existe.
> 🇺🇸 [English version](../en/product-plan.md)
>
> Relacionados: [Integrations](../../integrations/pt-BR/README.md) · [Channels](../../channels/pt-BR/README.md) ·
> [Radar](../../radar/pt-BR/README.md)

---

## Depois (sem data)

| Item | Por que espera |
|---|---|
| **Push do Gmail (Pub/Sub) no lugar do polling** | O ciclo de 2 minutos basta para uma pessoa; push precisa de um endpoint público e de um tópico no Pub/Sub. |
| **Radar consumindo o `MailMessageReceived`** | Espera o módulo [Radar](../../radar/pt-BR/README.md). |
| **Mais provedores** (Outlook pelo Microsoft Graph) | Só quando houver uma caixa para conectar. |
| **ManageSieve no Titan** | O Titan não oferece hoje, então as regras dele rodam no motor do Pandora. |
| **Condições por IA nas regras** | Precisa de uma decisão sobre que conteúdo de email pode ir ao modelo. |
| **"Aplicar às mensagens existentes"** numa regra | As regras agem só sobre chegadas. |
| **Assinatura por caixa, rascunhos no servidor** | A composição guarda o rascunho no navegador. |
| **Busca no corpo pelo provedor** | A busca lê só os cabeçalhos espelhados (ver ponto 4 em [Status de implementação](implementation-status.md#pontos-em-aberto-conhecidos)). |
| **Cache de anexos** | Os anexos vêm do provedor a cada download. |
