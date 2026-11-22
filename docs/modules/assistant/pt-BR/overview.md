# Visão geral — Negócio e princípios

[← Voltar ao índice](README.md) · Relacionados: [Arquitetura](architecture.md), [Modelo de dados](data-model.md)

---

## 1. O que o módulo faz

O módulo **Assistant** transforma linguagem natural — digitada ou falada — em comandos executados
contra os próprios módulos do Pandora:

> "lembra de ligar pro dentista amanhã às 9"
> → `create_reminder({ "title": "Ligar pro dentista", "remindAt": "2026-08-16T09:00:00-03:00" })`
> → um lembrete de verdade na Agenda, e uma confirmação no mesmo chat.

Ele permite a um único usuário:

- Falar com o Pandora pelo **console web** (`/assistant`, texto ou nota de voz gravada) ou pelo **bot
  de Telegram do assistant** (texto ou notas de voz).
- **Criar e gerenciar** itens da Agenda (lembretes, tarefas, eventos), **escrever e ler** Notes,
  **registrar um gasto** no inbox do Finances e **perguntar sobre** o dinheiro (lançamentos, totais,
  saldos, cartões, inbox) — 25 tools hoje, ver [Arquitetura §3](architecture.md#3-o-catálogo-de-tools).
- Apontar para uma linha da última lista pelo número ("cancela o 2").
- Confirmar ou cancelar uma chamada retida com botões (web, Telegram) ou com um "sim" / "não" digitado.
- **Compartilhar um arquivo** (comprovante, boleto) com o bot do Telegram; a legenda manda ele para a
  fila de um módulo (hoje o Finances) sem interpretá-lo.
- Inspecionar depois cada interpretação na aba **Histórico**: a tool call exata, o resultado, modelo,
  latência e tokens.

Não é um chatbot com opinião nem guarda regra de negócio: toda ação é um caso de uso que o módulo dono
já expõe. Se o assistant consegue fazer algo que a API não consegue, isso é bug.

## 2. Princípios

1. **O LLM escolhe; o Pandora decide.** A única saída útil do modelo é uma tool call (nome + argumentos
   JSON). Ler os argumentos, achar o item e rodar o caso de uso é código comum; uma chamada alucinada
   falha como qualquer request inválido.
2. **Os módulos são donos das suas tools.** Cada módulo registra implementações de `IAssistantTool` na
   própria pasta `Application/Assistant`. O Assistant as descobre por DI e nunca sabe o que é um lembrete.
3. **Escritas são confirmadas por política.** Cada tool declara uma `ConfirmationPolicy`; o
   `confirmation_level` do usuário desloca ela um degrau, exceto `Required` (exclusões), que sempre confirma.
4. **O tempo é dado, nunca adivinhado.** O system prompt leva a hora local, o fuso IANA e o início da
   semana do usuário; o modelo devolve timestamps ISO-8601 absolutos.
5. **Os dados pessoais ficam em casa.** A frase vai para o modelo hospedado; os dados do usuário não.
   Tools de leitura formatam a resposta aqui e mandam direto ao usuário; o histórico reenviado ao modelo
   guarda só um resumo sem conteúdo. Itens nomeados por palavras são casados localmente.
6. **Nunca dizer que deu certo quando não deu.** O status da invocação vem do resultado real do caso de
   uso, não da prosa do modelo.
7. **Tudo é registrado.** Toda frase deixa pelo menos uma linha em `ast004` — inclusive as que não
   fizeram nada.

## 3. Linguagem ubíqua (glossário)

| Termo | Significado |
|---|---|
| **Perfil** (Profile) | A configuração do assistant do usuário: provedor, a conta de API key do Integrations que ele gasta, modelo, ligado/desligado, idioma, nível de confirmação (`ast001`). |
| **Frase** (Utterance) | O que o usuário disse: o texto digitado, ou a transcrição de uma nota de voz. |
| **Tool** | Um comando que um módulo contribui (`IAssistantTool`): um descriptor mostrado ao modelo mais `ExecuteAsync` e `Describe`. |
| **Descriptor** | `AssistantCommandDescriptor`: nome, descrição, JSON schema dos argumentos, política de confirmação, exemplos few-shot. |
| **Tool call** | A escolha do modelo: um nome de tool e seus argumentos. Uma frase pode produzir várias. |
| **Invocação** | Uma tool call registrada (ou a falta dela) e como terminou (`ast004`). A trilha de auditoria. |
| **Chamada retida** | Uma invocação em `pending-confirmation`: válida, esperando Confirmar/Cancelar por até 10 minutos. |
| **Conversa** | Uma sequência curta de turnos (`ast002`), que expira após 30 minutos de silêncio; compartilhada entre web e Telegram. |
| **Recap** | O que o histórico guarda de uma resposta, para o próximo turno ter contexto sem os dados do usuário. |
| **Listagem** | As linhas numeradas da última lista mostrada (`ast002.last_listing`), para resolver "o 2". |
| **Fixar** (pinning) | Gravar nos argumentos o item que um número (ou um nome) representa antes de a chamada rodar ou ser retida (`ListedRefs`). |
| **Fila de arquivos** | A caixa de entrada de um módulo para arquivos compartilhados com o bot (`IAssistantFileQueue`), escolhida por palavras da legenda. |

## 4. Escopo

### Dentro do escopo (implementado — ver [Status de implementação](implementation-status.md))

Perfil + teste do provedor, o pipeline de interpretação (texto e voz, multi-turno, várias chamadas por
frase), políticas e níveis de confirmação, apontar por número, o catálogo de 25 tools entre Agenda,
Notes e Finances, o caminho do Telegram (mensagens, voz, botões de confirmação, arquivos compartilhados)
com respostas pelo outbox, o console web e a página de configurações.

### Fora do escopo / futuro

| Funcionalidade | Status |
|---|---|
| **OpenAI (segundo provedor)** | Não construído — um cliente `Ai.Chat.OpenAi` + uma entrada no catálogo. |
| **Eval set de frases reais** | Não construído; o teste live opt-in é o mais próximo. |
| **Opt-in de retenção de áudio** | Não construído — o áudio é sempre descartado após a transcrição. |
| **Resumos proativos** | Não planejado por enquanto; `list_agenda` sob pedido cobre. |
| **Alterar o Finances pelo chat** | Fora de propósito: só `record_expense` (no inbox) escreve. |

Ver o [Product plan](product-plan.md) para o que falta.
