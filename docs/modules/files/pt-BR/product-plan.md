# Módulo Files — Plano de Produto

> **Status:** F1a (backend) e F1b (o agente desktop) implementadas — catálogo, protocolo de scan,
> seleção e filtros, caixa de revisão, e o agente Windows que escaneia. A F1c (a web) vem a seguir. Construído sobre as fases D1 (casco) e
> D2 (credenciais de dispositivo) do [Pandora Desktop](../../../architecture/pt-BR/desktop-client.md).
> 🇺🇸 [English version](../en/product-plan.md)
>
> Relacionados: [Pandora Desktop](../../../architecture/pt-BR/desktop-client.md) ·
> [Identity](../../identity/pt-BR/README.md) · [Assistant](../../assistant/pt-BR/product-plan.md) ·
> [Notes](../../notes/pt-BR/README.md)

---

## 1. O que o módulo faz

O **Files** é um catálogo de tudo o que está nos discos do usuário: filmes e séries, fotos, material
da faculdade, livros, manuais, PDFs. Ele responde "o que eu tenho, e onde está?" sem o usuário
precisar navegar por 20 TB de pastas.

- **Quantos dispositivos o usuário quiser** alimentam um único catálogo — hoje um PC Windows; depois
  Linux, macOS, um servidor sem tela e celulares. Cada um roda um **agente** (no desktop, o módulo
  `Desktop.Files` do [Pandora Desktop](../../../architecture/pt-BR/desktop-client.md)).
- O usuário decide **exatamente o que é catalogado**, por dispositivo, por disco, por pasta: marca
  pastas numa árvore, exclui uma subpasta, inclui de novo uma mais funda, e aplica **filtros** por
  extensão, glob, prefixo, sufixo, "contém" ou regex.
- O backend mantém o **catálogo**: cada arquivo e pasta com caminho, tamanho, datas, tipo e se ainda
  está lá. Ele percebe arquivos novos, alterados, que sumiram e que foram **movidos ou renomeados**
  (mantendo o que o usuário associou a eles).
- O web permite **navegar** e **buscar** de qualquer dispositivo, sem o disco precisar estar
  acessível.
- Fases posteriores adicionam **metadados** (EXIF, título de PDF, duração de vídeo), **tags do
  usuário** e **classificação sugerida por IA**.

### O que ele não é

- **Não é ferramenta de sync ou backup.** A cópia na nuvem (hoje o iDrive) fica fora do Pandora; o
  módulo não sabe que ela existe. Trocar de provedor de backup não muda nada aqui.
- **Não é servidor de arquivos.** O backend nunca guarda os bytes dos arquivos. Abrir um arquivo só
  funciona no dispositivo que o tem (na F1, mostrando-o no explorador de arquivos); servir ou fazer
  streaming para outros dispositivos é uma decisão separada, para depois.
- **Não é gerenciador de arquivos.** O agente **nunca escreve no disco** — não apaga, não move, não
  renomeia. O catálogo segue o disco; nunca o comanda.

---

## 2. Nomenclatura e coordenadas

| Coisa | Valor |
|---|---|
| Projetos do backend | `Pottmayer.Pandora.Modules.Files.{Abstractions,Application,Domain,Infrastructure,Persistence,Presentation}` (um projeto `Contracts` vem com o primeiro evento de integração) |
| Compartilhado com os agentes | `Pottmayer.Pandora.Modules.Files.Agent` — DTOs do protocolo + o motor de seleção/filtros (4.3), `net10.0`, sem dependências do servidor |
| Schema PostgreSQL | `files` |
| Prefixo de tabela | `filXXX_`, PK `uuid_generate_v7()` |
| Base da API (usuário) | `/api/v{version}/files` — JWT, o escopo de usuário de sempre |
| Base da API (agente) | `/api/v{version}/files/agent` — só chave de dispositivo (qualquer dispositivo pareado; age sobre as próprias raízes) |
| Frontend | `client-web/src/modules/files` |
| Módulo desktop | `client-desktop/Pottmayer.Pandora.Desktop.Files` |
| Migrations | `migrations/migrations/files/` |

---

## 3. Princípios

1. **O disco é a fonte de verdade.** O catálogo é um espelho que pode ser reconstruído escaneando de
   novo. O que é realmente do Pandora é só o que o usuário adiciona por cima — tags, notas,
   classificações. *(F1)*
2. **O agente é somente leitura.** Ele lista, lê atributos e lê os primeiros e últimos bytes dos
   arquivos. Nunca modifica nada no disco. *(F2)*
3. **Nada sai do catálogo sozinho.** Um arquivo só é marcado como *sumido* por um scan **concluído**
   de uma raiz **acessível**; um que uma mudança de configuração deixa de fora é marcado como
   *excluído*. Nos dois casos ele mantém a linha e os metadados, e só sai do catálogo **quando o
   usuário aprova** na caixa de revisão (4.7). *(F3)*
4. **A identidade sobrevive a uma movimentação.** Uma **impressão digital** (tamanho + hash dos
   primeiros e últimos 64 KiB) permite ao catálogo reconhecer um arquivo renomeado ou movido e manter
   seu id, tags e histórico. *(F4)*
5. **O backend decide, o agente relata.** O catálogo e a configuração vivem no backend. O agente não
   mantém banco local: busca a sua configuração, envia o que vê e responde às perguntas do backend.
   *(F5)*
6. **O usuário decide o que é catalogado.** Dispositivos, raízes, seleção de pastas e filtros são do
   usuário, em todos os níveis, e nada é catalogado sem ter sido selecionado. Os padrões são
   configurações comuns, editáveis. *(F6)*
7. **Protocolo neutro de plataforma.** O backend nunca assume Windows. Um agente é qualquer coisa que
   fale o protocolo de agente; os caminhos são normalizados (4.8) para que um agente Linux e um
   Windows produzam catálogos comparáveis. *(F7)*
8. **Opt-in duas vezes.** O interruptor da conta (`fil004_preferences.is_enabled`) e o do dispositivo
   começam desligados — ver [os dois interruptores](../../../architecture/pt-BR/desktop-client.md#44-os-dois-interruptores). *(F8)*
9. **Feito para milhões de arquivos.** O tamanho do acervo é desconhecido, mas grande. Toda lista é
   paginada ou limitada a uma pasta, nada carrega uma árvore inteira, e o primeiro scan informa os
   números reais. *(F9)*

---

## 4. Arquitetura

```
 dispositivos (quantos forem, qualquer plataforma)  servidor
┌──────────────────────────────┐            ┌──────────────────────────────────┐
│ Pandora Desktop (ou headless)│            │ Módulo Files                     │
│  Desktop.Files               │  X-Api-Key │  endpoints de agente /files/agent│
│   busca a configuração       │ ─────────► │   config, scans, lotes           │
│   varredor + seleção/filtros │            │   diferença contra o catálogo    │
│   fingerprinter (sob pedido) │ ◄───────── │   "calcule a impressão destes"   │
│   ponte: files.*             │            │                                  │
└──────────────┬───────────────┘            │  endpoints de usuário /files/*   │
               │ ponte                      │   raízes, seleção, filtros,      │
┌──────────────▼───────────────┐   JWT      │   navegar, buscar, revisão, prefs│
│ client-web (dentro do app    │ ─────────► │                                  │
│ ou em qualquer navegador)    │            │  fil001–fil006                   │
└──────────────────────────────┘            └──────────────────────────────────┘
```

### 4.1 Vocabulário

| Termo | Significado |
|---|---|
| **Dispositivo** | Uma instalação pareada de agente (Identity, fase D2), com sua plataforma. Um usuário, vários dispositivos. |
| **Raiz** | Um local de topo num dispositivo — um disco inteiro (`E:\`, `/mnt/hd`) ou uma pasta. Identificada por `(dispositivo, caminho local)`. |
| **Seleção** | Por raiz, quais pastas entram: um conjunto de marcas de incluir/excluir em caminhos, a marca mais funda vence (4.3). |
| **Filtro** | Uma regra de nome aplicada sobre a seleção: incluir-somente ou excluir, por extensão, glob, prefixo, sufixo, contém ou regex, com escopo no usuário, num dispositivo, numa raiz ou numa pasta (4.3). |
| **Entrada** | Um arquivo ou pasta sob uma raiz, pelo caminho relativo à raiz. |
| **Scan** | Uma varredura completa de uma raiz, do início até *concluído* (aplicado), *abortado* (descartado) ou *retido* (aguardando o usuário, ver 4.4). |
| **Impressão digital** | `tamanho + SHA-256(primeiros 64 KiB ‖ últimos 64 KiB)`. Barata o bastante para 20 TB, forte o bastante para reconhecer o mesmo arquivo em outro caminho. Não é hash do conteúdo inteiro. |
| **Sumido** | Uma entrada que um scan concluído não viu. Mantida até o usuário revisá-la. |
| **Excluído** | Uma entrada que a seleção ou os filtros atuais deixam de fora, depois de uma mudança de configuração. Mantida até o usuário revisá-la. |
| **Caixa de revisão** | Onde entradas sumidas e excluídas esperam a decisão do usuário, exibidas como árvore de pastas (4.7). |

### 4.2 Dispositivos e raízes

Um usuário tem quantos dispositivos quiser, cada um com quantas raízes quiser. Tudo isso é
**configuração guardada no servidor**, editada no `client-web` e buscada pelo agente
(`GET /files/agent/config`) no início de cada scan. Guardar no servidor é o que permite configurar um
agente sem tela e um celular do mesmo jeito que o desktop, e ver e editar a configuração de todos os
dispositivos de qualquer lugar.

- **Adicionando uma raiz.** No próprio dispositivo, a página oferece o seletor de pasta nativo
  (`files.pickFolder`). Em qualquer outro lugar, o caminho é digitado; o agente o valida no próximo
  scan e aborta com `root-unavailable` se ele não existir, o que a página mostra.
- **Configurações por raiz:** nome, agenda de scan (diária num horário, ou só manual), incluir
  arquivos ocultos/de sistema (desligado por padrão), sensibilidade a maiúsculas (padrão pela
  plataforma, ver 4.8).
- **Visão de dispositivos:** cada dispositivo com sua plataforma, última vez em que foi visto, suas
  raízes, o último scan concluído e as contagens de entradas.
- **Remover uma raiz** para de escaneá-la; as entradas viram *excluídas* e vão para a caixa de
  revisão.

### 4.3 Seleção e filtros

A customização tem duas camadas. A **seleção** responde "quais pastas?"; os **filtros** respondem
"quais nomes, dentro delas?".

**Seleção — uma árvore de checkboxes.** Por raiz, um conjunto de marcas `(caminho, incluir |
excluir)`, onde `/` é a própria raiz. Uma pasta assume a marca do seu **ancestral marcado mais
fundo** (ou a própria marca); sem marca nenhuma, ela entra. É o mesmo modelo da árvore de pastas de
um programa de backup, e cobre todos os formatos sem regra de ordem:

| Quero | Raiz | Marcas |
|---|---|---|
| Disco 1: pastas A e B, não a C | `D:\` | `/` excluir · `/A` incluir · `/B` incluir |
| Disco 2: A, B, e só `Sub` dentro de C | `E:\` | `/C` excluir · `/C/Sub` incluir |
| Um disco inteiro menos as pastas de sistema | `C:\` | `/Windows` excluir · `/Program Files` excluir |

O varredor **poda**: ele não desce numa pasta excluída, a menos que alguma marca abaixo dela inclua
algo, então um galho excluído não custa nada para escanear. No dispositivo, a árvore é desenhada a
partir do disco ao vivo pela ponte (`files.listFolders`); em outros lugares, a partir do que o
catálogo já conhece mais caminhos digitados.

**Filtros — regras de nome sobre a seleção.** Cada filtro tem:

| Campo | Valores |
|---|---|
| ação | `include` (só os arquivos que casam ficam) · `exclude` (os itens que casam saem) |
| aplica-se a | `file` · `folder` (filtros de pasta só excluem, e podam: `node_modules`, `.git`) |
| tipo de comparação | `extension` (`mkv, mp4, avi`) · `glob` (`*.part`, `**/backup/**`) · `starts-with` · `ends-with` · `contains` · `regex` |
| escopo | o usuário inteiro · um dispositivo · uma raiz · uma pasta de uma raiz (herdado por tudo abaixo) |
| diferencia maiúsculas | sim / não / o padrão da raiz |
| ativo | ligado / desligado, sem apagá-lo |

Um arquivo é catalogado quando (1) sua pasta está selecionada, (2) se houver algum filtro `include`
de arquivo no escopo, ele casa com pelo menos um, e (3) ele **não** casa com nenhum filtro `exclude`
no escopo. **Excluir sempre vence.** Não existe ordem de regras para raciocinar.

**Padrões são filtros.** Um usuário novo recebe um conjunto semeado de filtros de exclusão no escopo
do usuário — `Thumbs.db`, `desktop.ini`, `.DS_Store`, `$RECYCLE.BIN`, `System Volume Information`,
`*.tmp`, `*.part` — marcados como embutidos, mas fora isso comuns: editáveis, desligáveis,
apagáveis.

**Pré-visualizar antes de salvar.** Editar um filtro mostra o que ele casaria **agora**, avaliado
pelo backend contra o catálogo — uma contagem e uma amostra —, então dá para testar uma regex sem
esperar um scan. Regexes são validadas ao salvar e rodam com timeout (um padrão patológico não trava
um scan).

**Mudanças valem no próximo scan.** O agente busca a configuração no início de cada scan; "Escanear
agora" logo depois de uma edição a aplica na hora. Entradas que a nova configuração deixa de fora
viram *excluídas* (não apagadas — F3); as que ela traz para dentro aparecem como novas.

**Um motor só.** A avaliação de seleção e filtros fica no `Files.Agent`, usado tanto pelo backend
(para a pré-visualização) quanto pelos agentes .NET, então a pré-visualização e o scan nunca
discordam. Agentes em outras linguagens (um celular) implementam as mesmas regras contra um conjunto
de vetores de teste mantido junto com os docs.

### 4.4 Protocolo de scan

```
0. GET  /files/agent/config                → raízes, seleção e filtros deste dispositivo

1. POST /files/agent/scans                {rootId}                       → {scanId}
      Um scan em andamento ou retido daquela raiz é abortado como "superseded": só este
      dispositivo escaneia a raiz, então uma execução anterior está morta (agente que caiu) ou
      velha (scan retido).

2. POST /files/agent/scans/{id}/batches   {entries: [{path, kind, size, modifiedAt}]}   (≤ 1000)
      → {needsFingerprint: [path, ...]}
      O backend compara cada caminho com o catálogo daquela raiz:
        mesmo size + modifiedAt  → visto, nada a fazer
        novo ou alterado         → pede a impressão digital

3. POST /files/agent/scans/{id}/batches   {entries: [{path, ..., fingerprint}]}
      As respostas do passo 2, como mais lotes.

4. POST /files/agent/scans/{id}/complete  {entriesSeen}
   ou  POST /files/agent/scans/{id}/abort {reason}      (ex. root-unavailable)
```

O agente só envia o que a seleção e os filtros deixam passar. No **complete**, numa única transação:

1. **Movimentações.** As entradas da raiz não vistas neste scan são candidatas a *sumido*. Uma
   candidata cuja impressão digital bate com **exatamente uma** entrada criada desde o scan concluído
   anterior desta raiz (mesmo usuário, qualquer raiz, qualquer dispositivo — então mover entre duas
   raízes também conta) foi movida: a linha antiga assume o novo caminho e a nova raiz, mantendo id e
   metadados, e a linha mais nova é descartada. Correspondências ambíguas (duplicatas) não são
   mescladas — ficam *nova + sumida*, o que é seguro.
2. **Sumido ou excluído.** Cada candidata restante é conferida contra a configuração com que o scan
   rodou: se a configuração a deixa de fora, vira `excluded`; senão, `missing`. As duas recebem
   `missing_since = now` e vão para a caixa de revisão. Uma entrada vista de novo volta para
   `present`.
3. **Freio de segurança.** Se o scan fosse marcar mais de **20 %** da raiz como sumida (um disco que
   subiu vazio, uma letra de unidade errada), o scan vai para `held` em vez de ser aplicado. O web
   mostra; o usuário **confirma** (aplica) ou **descarta**. Exclusões causadas por mudança de
   configuração não contam para o freio — foi o usuário que pediu.

Antes de tudo isso, `entriesSeen` precisa bater com as entradas distintas que o backend recebeu;
senão um lote se perdeu e o scan é abortado (`entries-seen-mismatch`) em vez de marcar essas entradas
como sumidas.

No **abort**, ou se o agente parar de enviar lotes por 30 minutos, o scan é descartado: nada
é marcado como sumido. As entradas criadas pelo scan ficam (são arquivos reais que foram vistos).

Pastas são entradas com `kind = directory`. Não têm impressão digital; uma pasta movida aparece como
pastas novas mais arquivos movidos, o que basta porque pastas não carregam metadados do usuário na F1.

**Custo do primeiro scan.** Na primeira vez, todo arquivo precisa de impressão digital — cerca de
128 KiB lidos por arquivo. Com milhões de arquivos em discos mecânicos, são muitas horas, uma vez. Os
scans seguintes só calculam a impressão do que é novo ou mudou.

### 4.5 Agendamento

Cada raiz tem sua própria agenda — diária num horário escolhido (o padrão), ou só manual — mais o
**"Escanear agora"** da página (`files.scanNow`), com o progresso enviado à página
(`files.scanProgress`). Monitoramento em tempo real fica fora da F1: um scan diário combina com o
ritmo de mudança desse acervo.

Como o agente aplica isso (F1b):

- O **primeiro scan de uma raiz é sempre o "Escanear agora"**: a agenda nunca inicia uma raiz que ainda
  não concluiu um scan, então um disco inteiro não é catalogado antes de o usuário terminar a seleção.
- Depois disso, um scan está pendente quando o último horário agendado passou sem scan concluído desde
  então — um PC que estava desligado às 03:00 escaneia quando volta (a config traz
  `lastCompletedScanAt`).
- Uma tentativa por horário agendado: um scan que o backend reteve ou abortou espera o dia seguinte (ou
  o "Escanear agora"); só uma tentativa que falhou (servidor fora do ar) tenta de novo, depois de uma
  hora.
- Um scan por vez no dispositivo; pedidos de "Escanear agora" entram na fila atrás do atual.
- O agente atualiza a configuração a cada 10 minutos, antes de todo "Escanear agora", e quando a página
  pede (`files.status`).

A ponte do agente: `files.status` (pareado, interruptor da conta, raízes deste dispositivo, o scan em
andamento), `files.scanNow {rootId}`, `files.pickFolder`, `files.listFolders {path}` (discos quando
vazio) ou `{rootId, path}` (dentro de uma raiz, com os caminhos do catálogo para as marcas de seleção),
`files.reveal {rootId, path}`.

### 4.6 Navegação e busca

- **Navegar:** `GET /files/roots/{id}/entries?parentPath=` — os filhos de uma pasta, paginados,
  pastas primeiro.
- **Buscar:** `GET /files/search?q=&deviceId=&rootId=&category=&minSize=&maxSize=&modifiedFrom=&modifiedTo=&status=`.
  A busca por nome usa um **índice de trigramas** (`pg_trgm`, novo no Pandora) em `name`, porque nomes
  de arquivo são buscados por fragmentos (`breaking bad s02`, `calculo_2`), não por palavras.
- **Categoria** é derivada da extensão pelo domínio (`FileCategory.FromExtension`): `video`, `audio`,
  `image`, `document`, `ebook`, `archive`, `code`, `other`. Armazenada para filtrar.
- **Mostrar no explorador:** no dispositivo dono da raiz, um resultado oferece *Mostrar no
  Explorer/Finder* (`files.reveal`). Em outros lugares, o caminho é exibido e pode ser copiado.

### 4.7 Caixa de revisão

Entradas sumidas e excluídas esperam numa **caixa de revisão**, a mesma ideia da
[inbox do Finances](../../finances/pt-BR/recurrences-and-inbox.md): o sistema propõe, o usuário
aprova.

- **Exibida como árvore.** A caixa é a árvore de pastas de cada raiz, podada para os galhos que têm
  entradas a revisar, cada pasta com a contagem abaixo dela e o motivo (*sumido* / *excluído*). As
  pastas carregam sob demanda ao expandir, então uma raiz com milhões de entradas nunca carrega de uma
  vez. Uma subárvore inteira que sumiu aparece como um galho, não como milhares de linhas.
- **Duas decisões, numa entrada ou numa pasta inteira** (que vale para tudo o que está em revisão
  abaixo dela):
  - **Esquecer** — a entrada sai do catálogo, junto com o que o usuário associou a ela. É o único
    caminho pelo qual uma entrada é removida.
  - **Manter** — ela fica no catálogo como sumida/excluída e sai da caixa (ex. um arquivo num disco
    externo desconectado de propósito). Continua encontrável na busca com o filtro de status.
- Uma entrada que reaparece, é reconhecida como movida, ou volta por uma mudança de configuração sai
  da caixa sozinha.

### 4.8 Caminhos entre plataformas

- `relative_path` é guardado com separador `/` e em Unicode **NFC** (os sistemas de arquivos do macOS
  entregam NFD; sem normalizar, `Ação` vindo de um Mac e do Windows seriam dois nomes diferentes).
- O `local_path` de uma raiz é guardado como o dispositivo o informa (`E:\`, `/mnt/hd`,
  `/Volumes/Fotos`).
- **Sensibilidade a maiúsculas** é uma configuração por raiz, com padrão pela plataforma: insensível
  no Windows e no macOS, sensível no Linux. Ela rege a comparação de caminhos, as marcas de seleção e
  o padrão dos filtros.

---

## 5. Modelo de dados (rascunho)

Valores de enum usam hífen; colunas em snake_case. Nenhuma FK sai do schema `files` — `user_id` e
`device_id` referenciam o Identity só logicamente.

**`fil001_root`** — `id`, `user_id`, `device_id`, `name`, `local_path`, `kind` (`folder`; depois
`media-library` para celulares), `case_sensitive`, `include_hidden`, `scan_time` (nulo = só manual),
`status` (`active` | `removed`), `last_completed_scan_at`, `entry_count`, `created_at/by`,
`updated_at/by`. Único `(device_id, local_path)`.

**`fil002_entry`** — `id`, `user_id`, `root_id` → fil001, `kind` (`file` | `directory`),
`relative_path`, `parent_path`, `name`, `extension`, `category`, `size_bytes`, `modified_at`,
`fingerprint` (nulo para pastas e até ser calculado), `status` (`present` | `missing` | `excluded`),
`missing_since`, `kept_at` (a decisão *Manter* — fora da caixa), `first_seen_at`,
`last_seen_scan_id`.
Único `(root_id, relative_path)`; índice `(root_id, parent_path)` para navegar; índice
`(user_id, fingerprint)` para detectar movimentação; GIN de trigramas em `name`.

**`fil003_scan`** — `id`, `root_id` → fil001, `status` (`running` | `completed` | `aborted` |
`held`), `started_at`, `last_batch_at`, `finished_at`, contadores (`seen`, `created`, `changed`,
`moved`, `missing`, `excluded`), `error`.

**`fil004_preferences`** — `user_id` (PK), `is_enabled` (padrão `false`), `created_at`,
`updated_at`. O interruptor da conta.

**`fil005_selection_mark`** — `id`, `root_id` → fil001 (cascade), `path` (`/` = a raiz), `mode`
(`include` | `exclude`). Único `(root_id, path)`.

**`fil006_filter`** — `id`, `user_id`, `device_id` (nulo), `root_id` → fil001 (nulo), `scope_path`
(nulo), `name`, `action` (`include` | `exclude`), `applies_to` (`file` | `folder`), `matcher`
(`extension` | `glob` | `starts-with` | `ends-with` | `contains` | `regex`), `pattern`,
`case_sensitive` (nulo = o da raiz), `is_enabled`, `is_builtin`, `created_at/by`, `updated_at/by`.
O escopo é o mais específico não nulo entre `scope_path` (exige `root_id`) → `root_id` → `device_id`
→ usuário.

Fases posteriores adicionam metadados (`fil002.metadata jsonb`), tags e classificações (`fil007`+).

---

## 6. Superfície da API (F1)

| Método | Caminho | Quem | Para quê |
|---|---|---|---|
| GET / PUT | `/files/preferences` | usuário | o interruptor da conta |
| GET · POST | `/files/roots` | usuário | listar / adicionar uma raiz `{deviceId, name, localPath, ...}` |
| PATCH · DELETE | `/files/roots/{id}` | usuário | configurações da raiz / remover (entradas → caixa) |
| PUT | `/files/roots/{id}/selection` | usuário | as marcas de seleção, substituídas como conjunto (vêm com cada raiz em `GET /files/roots`) |
| GET · POST · PATCH · DELETE | `/files/filters` | usuário | filtros em qualquer escopo |
| POST | `/files/filters/preview` | usuário | o que um filtro em rascunho casaria no catálogo |
| GET | `/files/roots/{id}/entries?parentPath=` | usuário | navegar numa pasta |
| GET | `/files/search` | usuário | busca (4.6); `status` é `present` (padrão), `missing`, `excluded` ou `all` |
| GET | `/files/entries/{id}` | usuário | uma entrada |
| GET | `/files/scans?rootId=` | usuário | histórico de scans |
| POST | `/files/scans/{id}/confirm` · `/discard` | usuário | resolver um scan retido |
| GET | `/files/review/tree?rootId=&parentPath=` | usuário | caixa: a árvore podada, um nível por vez, com contagens |
| POST | `/files/review/forget` · `/files/review/keep` | usuário | decidir sobre entradas e/ou pastas `{entryIds, folders: [{rootId, path}]}` |
| GET | `/files/agent/config` | dispositivo | raízes, seleção e filtros deste dispositivo |
| POST | `/files/agent/scans` · `/{id}/batches` · `/{id}/complete` · `/{id}/abort` | dispositivo | protocolo de scan (4.4) |

Os endpoints de agente só aceitam chave de dispositivo (a policy `device` dos
[dispositivos do Identity](../../identity/pt-BR/devices.md)), nunca uma sessão, e só agem sobre raízes
do próprio dispositivo da chave. Nenhum escopo é necessário: um dispositivo não faz nada no Files até o
usuário dar raízes a ele. Os dispositivos em si (plataforma, última vez visto) vêm do
`GET /identity/devices` do Identity; a web junta com as raízes.

Caminhos na API e no catálogo são relativos à raiz, na forma `/Filmes/a.mkv` (`/` é a própria raiz).

---

## 7. Roadmap

Pré-requisitos: [Desktop D1 e D2](../../../architecture/pt-BR/desktop-client.md#6-roadmap).

### Fase F1 — Catálogo *(o MVP)*

- *(Feito — F1a.)* Backend: os projetos do módulo mais o `Files.Agent`; `fil001`–`fil006`; `pg_trgm`; endpoints de
  agente e de usuário; o protocolo de scan com detecção de movimentação, entradas excluídas e o freio
  de segurança; filtros padrão semeados; pré-visualização de filtros; um job que expira scans sem
  lotes.
- *(Feito — F1b.)* Desktop (Windows): `Desktop.Files` — interruptor do dispositivo, pareamento (D2), busca da
  configuração, varredor com poda pela seleção e filtros, fingerprinter, agenda por raiz + escanear
  agora com progresso, `files.pickFolder` / `files.listFolders` / `files.reveal`.
- Web: configurações do Files (interruptor da conta), dispositivos e raízes, a árvore de seleção,
  filtros com pré-visualização, navegador de pastas, busca, confirmação de scan retido, caixa de
  revisão em árvore.
- **Pronto quando:** no homelab você seleciona o disco 2 com só `C/Sub` dentro de `C`, adiciona um
  filtro `extension` de inclusão para vídeos, e o scan cataloga exatamente isso; do navegador do
  notebook você encontra um arquivo por um pedaço do nome; você o renomeia no disco, escaneia de novo,
  e ele é a mesma entrada (mesmo id).

### Agentes em outras plataformas

Segue o [roadmap do desktop](../../../architecture/pt-BR/desktop-client.md#6-roadmap): um agente
headless (servidor/NAS Linux, serviço do Windows), cascos desktop para Linux/macOS, e celulares. O
módulo não muda para uma nova plataforma desktop — é isso que o F7 garante. Celulares acrescentam um
`kind` de raiz (`media-library`) e um agente próprio; ver o doc do desktop para o porquê de virem por
último.

### Fase F2 — Metadados

O agente extrai o que os bytes dizem — EXIF (data, câmera, local), título e número de páginas do PDF,
duração e resolução de vídeo/áudio — e envia junto com o lote. Guardado como `metadata jsonb`; os
filtros de busca crescem a partir disso.

### Fase F3 — Tags

Tags do usuário em entradas e pastas (uma tag numa pasta vale para o que está abaixo dela ao
filtrar). Sobrevivem a movimentações pelo F4.

### Fase F4 — Classificação por IA

Categoria/tags sugeridas a partir de nome, caminho e metadados, pelo `Tars.Ai` existente (Gemini,
chave no Integrations). Sugestões são aceitas pelo usuário, nunca aplicadas em silêncio. Ler o
conteúdo dos arquivos (ex. o texto de um PDF) é um opt-in separado.

### Fase F5 — Entre módulos

- Ferramenta `search_files` no Assistant ("acha o manual da geladeira").
- Notes: link para uma entrada a partir de uma página.

### Ideias — sem fase definida

- **Tela de duplicatas.** O índice `(user_id, fingerprint)` já encontra arquivos idênticos entre
  raízes e dispositivos; uma página listando-os (com tamanhos, para ver quanto espaço liberaria) é
  barata de adicionar. Não é necessária na F1.
- **Mais tipos de filtro:** faixa de tamanho, faixa de data de modificação.
- **Monitoramento em tempo real** de uma raiz, para acervos que mudam a cada minuto.

### Não planejado

Servir ou fazer streaming de arquivos para outros dispositivos; conhecer o backup em nuvem (iDrive ou
qualquer outro provedor); escrever no disco. Cada um seria uma decisão própria, revista quando fizer
por merecer.

---

## 8. Questões em aberto

1. **Tamanho do acervo.** Desconhecido, mas muito grande ("muitos" arquivos em 20 TB). O desenho
   assume milhões; o tamanho do lote e o índice de trigramas são ajustados quando o primeiro scan
   informar os números reais.
2. **Pastas de rede como raiz.** A F1 assume discos ligados ao dispositivo. Uma pasta que vive em
   outra máquina mas está montada no dispositivo (`Z:\` mapeado para um NAS) funcionaria com o mesmo
   agente, mas cai com muito mais frequência que um disco local — o freio de segurança dispararia
   mais. A resposta mais limpa costuma ser um agente headless na máquina que tem o disco.
