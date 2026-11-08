# Status de implementação

[← Voltar ao índice](README.md)

Um retrato do que está construído no código versus o que ainda é plano. Use para separar "documentado
porque existe" de "documentado como plano" ([Product plan](product-plan.md)).

---

## Implementado

| Área | Notas |
|---|---|
| **Revisão do frontend** | Configurações e chat do assistente exibem falhas de consulta; leituras com erro não liberam valores padrão editáveis. Alertas usam a API atual do Ant Design. |
| **Estrutura do módulo** | Sete projetos em camadas, schema `assistant`, wiring no Host, opções `Pandora:Assistant` (`HistoryMessageLimit`). O `Contracts` está vazio. |
| **Provedor** | Gemini via `Tars.Ai.Chat.Gemini`, factory com keyed DI, modelo e chave por chamada, nenhuma chave na configuração. |
| **Perfil** | `ast001`; leitura (com padrões) / save; conta de chat validada contra o Integrations; "usado por" informado ao Integrations. |
| **Teste de alcance** | `POST /providers/{provider}/test` com `no_key` / `rejected` / `unreachable`. |
| **Pipeline de interpretação** | System prompt v6 (hora, fuso, início da semana, idioma, exemplos), histórico multi-turno (limitado no tempo pela conversa de 30 min e a N mensagens), várias tool calls por frase, nova tentativa quando o modelo imita um recap, todo resultado registrado em `ast004`. |
| **Voz** | Transcrição pelo mesmo modelo (áudio como `ChatAttachment`), ≤ 5 MB, áudio descartado; notas de voz do Telegram e o gravador web (WAV 16 kHz, ≤ 150 s). |
| **Confirmação** | Políticas `Never`/`WhenAmbiguous`/`Always`/`Required`, níveis `strict`/`balanced`/`trusting`, janela de 10 min, endpoints confirmar/cancelar, botões no Telegram, sim/não digitado. |
| **Apontar por número** | `ast002.last_listing`, `ListedRefs.Pin`/`PinTarget`, tools targeted acham o item antes da confirmação. |
| **Privacidade** | Respostas de leitura vão só ao usuário; o histórico guarda recaps sem conteúdo; nomes casados localmente. |
| **Catálogo de tools** | 26 tools: Agenda 15 (criar/alterar/concluir/reabrir/excluir tarefas, eventos, lembretes; três listas), Notes 5 (`create_note`, `search_notes`, `ask_notes`, `read_note`, `append_to_note`), Finances 6 (`record_expense` no inbox + 5 só leitura). |
| **Telegram** | Subscriber no bot `assistant` (texto, voz, arquivos), subscriber de toque em botão, respostas via `SendAssistantReply` pelo outbox do Assistant + relay. |
| **Arquivos compartilhados** | `IAssistantFileQueue`; fila do Finances por palavras da legenda em `fin017` (imagens/PDFs, ≤ 20 MB). |
| **Frontend** | `/assistant` (abas chat + histórico) e `/settings/assistant`. |
| **Testes** | Testes unitários do módulo, testes das tools em Agenda/Notes, o teste live opt-in contra o Gemini real. |

### Mudanças em relação ao plano original

- `ast001.credential_ref` virou **`chat_account_id`** (um usuário pode ter várias chaves Gemini e escolhe
  uma); `transcription_provider/model` e `endpoint` nunca foram criados — a transcrição usa o modelo de chat.
- `ast002` não tem `source` / `is_active`: uma conversa serve web e Telegram, a expiração é calculada a
  partir de `last_activity_at`.
- `ast003` não tem `role = tool`, `audio_ref` nem `token_count`; os tokens ficam em `ast004`.
- `ast004` não tem `message_id`; os status ganharam `clarification`, `provider-error` e `cancelled`, e a
  tabela guarda `utterance` e `expires_at`.
- O caminho do Telegram não usa um job do módulo drenando linhas: o subscriber de integration event roda o
  pipeline, e a resposta fica durável pelo outbox.
- Sem `ITranscriptionClient` separado; sem endpoints de conversas/mensagens nem de comandos
  (`GET /conversations`, `GET /commands`) — a aba Histórico lê `/invocations`.

## Ainda não implementado

| Área | Status |
|---|---|
| **Provedor OpenAI** | Sem `Ai.Chat.OpenAi`; o `GetProviders` lista só `gemini`. |
| **Eval set** | Nenhum conjunto medido de frases para escolher modelo; o teste live não verifica as escolhas do modelo. |
| **Opt-in de retenção de áudio** | Não construído; o áudio é sempre descartado. |
| **Detecção de ambiguidade para `WhenAmbiguous`** | Não construída — ver Pontos em aberto. |
| **Resumos proativos** | Não planejados por enquanto. |

## Pontos em aberto conhecidos

1. **`WhenAmbiguous` se comporta como `Never` no `balanced`.** Nenhum sinal de confiança é lido; só o
   deslocamento do nível (`strict`) faz ela confirmar. Nenhuma tool declara `Always`, então o `balanced`
   só retém exclusões.
2. **Sem varredura de expiração.** Uma chamada retida sem resposta fica `pending-confirmation` depois de
   `expires_at` até um confirmar transformá-la em `expired`; a aba Histórico mostra como pendente.
3. **Duas listas num turno.** O `last_listing` guarda só a última (marcado com `ponytail:` no
   `InterpretCommandHandler`).
4. **Retenção de conversas e mensagens.** `ast002`–`ast004` nunca são expurgadas.
5. O comentário da migration do `ast004` ainda diz `'pending_confirmation'`; o CHECK e o código usam
   `pending-confirmation`.
