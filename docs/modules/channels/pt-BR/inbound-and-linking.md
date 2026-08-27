# Entrada e Vínculo

[← Voltar ao índice](README.md) · Relacionados: [Arquitetura](architecture.md), [Modelo de dados](data-model.md)

---

## 1. Vincular um chat do Telegram

Um chat id **nunca é aceito do cliente**. A única coisa que autoriza um é um handshake de uso único:

```
1. SPA    → POST /channels/telegram/link          ← { deepLink, token }   (chn002 emitido)
2. Usuário toca o deep link → o Telegram abre o bot com /start <token>
3. O bot recebe o update; a triagem o classifica como Command
4. ConsumeTelegramLink: valida e consome o token, faz upsert em chn001 (verificado),
   guarda o chat id + metadados (username/primeiro nome)
5. SPA    → GET /channels                          ← Telegram agora aparece como vinculado
```

`ChannelLinkTokens` emite o token; `ConsumeTelegramLinkCommand` o consome. Tokens são de uso único com
TTL ~15 minutos. `UnlinkChannel` esquece o endereço.

## 2. Dois drivers, um handler

O ingress roda sobre **long polling** (`TelegramLongPollingService`): `getUpdates(timeout=30)` mantém a
conexão aberta até um update chegar ou o timeout expirar — uma conexão aberta que retorna
imediatamente, não short polling num loop. Long polling funciona atrás de NAT sem túnel, que é o
cenário de desenvolvimento e do homelab.

Um **driver de webhook** está adiado (ver [product-plan.md](product-plan.md)); quando chegar, entrega os
updates à *mesma* triagem que o driver de long-polling usa.

## 3. Ingress idempotente

Todo update é escrito em `chn004_inbound_update` **antes** de ser processado. Como
`(provider, provider_update_id)` é único, reprocessar é inofensivo: uma queda entre escrita e
processamento reproduz o update em vez de perdê-lo, e o maior `provider_update_id` visto é o offset do
long-polling no startup.

## 4. Triagem — estrutural, nunca semântica

`TelegramInboundTriage` classifica cada update em um de quatro valores de `classification`, puramente
por estrutura:

| Classificação | O que é | O que acontece |
|---|---|---|
| **Interaction** | Um callback de botão inline (`callback_data` = um id de `chn003`) | Resolve a interação, checa que o remetente é o dono, consome (uso único — um segundo toque é "expirado") e publica `InboundInteractionReceived` ao módulo dono. |
| **Command** | Um `/comando` — notavelmente `/start <token>` | Tratado aqui (vínculo); outros comandos são roteados como próprios. |
| **Message** | Texto livre ou uma nota de voz | Publica `InboundMessageReceived(userId, channel, text?, mediaRef?, mediaMimeType?)`. |
| **Discarded** | Chat desconhecido, update inutilizável | Registrado e descartado. |

O módulo resolve um id numa tabela e lê a coluna que o módulo dono escreveu; **nunca interpreta o que
uma ação significa** (princípio C5).

## 5. Roteamento de volta aos donos

- **Interações** carregam `owner_module` + `action` + `payload` opaco. Channels monta a chave de rota a
  partir de `owner_module` e publica `InboundInteractionReceived`; o dono (ex. Agenda: `task_done`,
  `snooze_1h`) age e o botão já está consumido. É o mecanismo por trás de *"apertar Concluído fecha a
  tarefa, e o segundo clique diz que expirou."*
- **Mensagens** são publicadas como `InboundMessageReceived`. O Assistant é o consumidor pretendido —
  ele transcreve/interpreta/executa. Channels não processa o conteúdo.

## 6. Mídia (notas de voz)

O Assistant precisa dos bytes do áudio mas não pode saber que o Telegram existe. Channels expõe uma
porta de `Abstractions`:

```csharp
public interface IInboundMediaReader
{
    Task<Stream> OpenAsync(string channel, string mediaRef, CancellationToken ct);
}
```

`TelegramInboundMediaReader` a implementa sobre a Bot API (`getFile` + download). O evento
`InboundMessageReceived` carrega `mediaRef` + `mediaMimeType`; o consumidor abre o stream pela porta.
Trocar para WhatsApp, ou entrar pela web, não toca em nada no consumidor.

## 7. Retenção do bruto

`chn004.raw` guarda dados pessoais mantidos só para debug. `InboundUpdateRetentionBackgroundService`
roda diariamente e anula o payload `raw` de linhas mais velhas que `Channels:RawRetention:RetentionDays`
(padrão 7; alternado por `Channels:RawRetention:Enabled`), **mantendo a linha** — ela ainda é a guarda
de idempotência e o offset do long-polling.
