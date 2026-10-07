# Pandora Desktop — Cliente Desktop

> **Status:** As fases D1 (o casco, [`client-desktop/`](../../../client-desktop/README.md)) e D2
> (credenciais de dispositivo, [Identity → Dispositivos](../../modules/identity/pt-BR/devices.md)) estão
> implementadas; o resto é plano. O módulo [Files](../../modules/files/pt-BR/README.md) é o próximo e o
> primeiro a precisar do desktop.
> 🇺🇸 [English version](../en/desktop-client.md)
>
> Documento transversal: o desktop é cliente de todos os módulos e não pertence a nenhum.
> Afetados: [Files](../../modules/files/pt-BR/product-plan.md) (primeiro módulo desktop) ·
> [Identity](../../modules/identity/pt-BR/README.md) (credenciais de dispositivo, fase D2).
> Ver também: [Mensageria](messaging.md) · [Deploy no homelab](../../deployment/homelab-deploy.md)

---

## 1. O que é

O **Pandora Desktop** é um app Windows que mostra o mesmo `client-web` que você usa no navegador,
dentro de uma janela nativa, e acrescenta o que um navegador não consegue: ler o disco local, ficar
na bandeja, iniciar com o Windows, continuar trabalhando com a janela fechada.

É um **casco**, não um segundo front-end. Toda tela — inclusive as que só fazem sentido no desktop —
fica no `client-web`. O desktop contribui *capacidades*, expostas a essas telas por uma ponte pequena.

```
┌──────────────────────── servidor (hoje o homelab) ──────────────────────┐
│ backend/      módulos: Identity, Finances, Notes, ..., Files            │
│ client-web/   todas as telas, inclusive as só-desktop                   │
└─────────────────────────────────────────────────────────────────────────┘
          ▲ HTTP: telas (carga remota)           ▲ HTTP: dados (/api)
          │                                      │
┌──────────────────────── Pandora Desktop (PC do usuário) ────────────────┐
│ Casco (host)         janela + WebView2, bandeja, iniciar com Windows,   │
│                      auto-update, a ponte, a lista de módulos           │
│ Módulos desktop      uma biblioteca por feature nativa, cada uma        │
│   Desktop.Files      desligada por padrão: o varredor de disco (1º)     │
│   Desktop.<próximo>  o mesmo contrato, quando aparecer um motivo        │
└─────────────────────────────────────────────────────────────────────────┘
```

### O que ele não é

- **Não é uma segunda UI.** Nenhuma tela é escrita para o desktop. Se uma feature precisa de uma
  página, a página vai no `client-web` e pergunta à ponte se a capacidade de que precisa está lá.
- **Não funciona offline.** Os dados do Pandora vivem no backend; sem ele não há o que mostrar. O
  desktop não faz cache de dados nem enfileira escritas.
- **Não é um worker do Pandora.** A [Mensageria](messaging.md) diz que não existem workers separados,
  e isso continua valendo: o desktop é um **cliente** que chama a API pública com credencial própria,
  exatamente como o navegador ou o Telegram. Nada no backend depende de ele estar no ar.
- **Não é uma plataforma de plugins.** Os módulos desktop são compilados juntos e vão no instalador;
  não há carregamento de código de terceiros em tempo de execução (ver D7).

---

## 2. Nomenclatura e coordenadas

| Coisa | Valor |
|---|---|
| Pasta no monorepo | `client-desktop/` (irmã de `client-web/`) |
| Solution | `client-desktop/Pottmayer.Pandora.Desktop.slnx` |
| Casco (o `.exe`) | `Pottmayer.Pandora.Desktop.Host` — `net10.0-windows`, WinForms como contêiner do controle WebView2 e do ícone da bandeja (sem UI própria além disso) |
| Contrato de módulo | `Pottmayer.Pandora.Desktop.Abstractions` — `IDesktopModule`, `IBridgeHandler` — `net10.0` puro |
| Primeiro módulo | `Pottmayer.Pandora.Desktop.Files` (fase F1 do [Files](../../modules/files/pt-BR/product-plan.md)) — `net10.0` puro |
| Regra de plataforma | **só o casco referencia o Windows**; contrato e módulos continuam multiplataforma (ver 4.8) |
| Versão | o `/VERSION` do repo, o mesmo do backend e do web (lockstep) |
| Instalador / atualizações | [Velopack](https://velopack.io) — `Setup.exe`, instalação por usuário, atualizações via GitHub Releases |
| Dados locais | `%LOCALAPPDATA%\Pandora\` — `settings.json`, `credentials.bin` (DPAPI), `WebView2\` (perfil do navegador) |
| Ponte no JS | `window.pandoraDesktop` — ausente num navegador comum |

---

## 3. Princípios

1. **O casco não conhece nenhuma feature.** Ele hospeda módulos do mesmo jeito que o `Host` do
   backend hospeda Finances e Notes sem conhecer suas regras. Adicionar uma feature desktop nunca
   edita o casco. *(D1)*
2. **Um só front-end.** Todas as telas vivem no `client-web`. Uma página pergunta à ponte por uma
   *capacidade* ("o `files` está ativo aqui?"), nunca "estou no desktop?". *(D2)*
3. **As telas são carregadas remotamente.** O casco abre a URL do Pandora do usuário; uma mudança de
   front-end sai com o deploy normal do web, nunca como release do desktop. *(D3)*
4. **Tudo é opt-in, em dois níveis.** O interruptor da conta fica nas configurações do módulo no
   servidor; o do dispositivo fica no PC. Os dois começam desligados. Um módulo inativo nem inicia.
   *(D4)*
5. **A ponte só responde ao Pandora.** Chamadas nativas só são aceitas a partir da origem do servidor
   configurado. Qualquer outra página no WebView fica sem ponte. *(D5)*
6. **Trabalho em segundo plano tem credencial própria.** Um módulo que roda com a janela fechada usa
   uma **credencial de dispositivo** — com escopo, revogável, nunca a sessão ou a senha do usuário.
   *(D6)*
7. **Vai tudo junto, desligado por padrão.** Todo módulo desktop está no instalador; ligar um é uma
   configuração, não um download. Add-ons de verdade ficam para quando um módulo for grande demais
   para ir para todo mundo. *(D7)*

---

## 4. Arquitetura

### 4.1 Carregando o front-end

- **A primeira execução** pede a URL do servidor (ex. `http://192.168.1.10:8730` na rede local) e a
  guarda no `settings.json`. O WebView navega até lá; o usuário entra pela tela de login normal, MFA
  incluído.
- O `client-web` mantém os tokens onde já mantém (o armazenamento do perfil do WebView2), então a
  sessão sobrevive a reinícios exatamente como num navegador. **Nada muda no fluxo de autenticação
  na D1.**
- **Navegação para fora da origem do servidor** (um link externo) abre no navegador padrão, não na
  janela do app.
- **Servidor inacessível:** o casco mostra uma pequena página local — "não consegui falar com o
  Pandora em …, tentar de novo / trocar servidor" — em vez do erro cru do WebView. Essa página é o
  único HTML que vai junto com o app.

Por que remoto e não embutido (o `dist/` dentro do instalador): a única coisa que embutir traria é
abrir as telas sem o servidor, e as telas do Pandora ficam vazias sem o servidor. O que custaria é
um release do desktop para cada mudança de UI e o risco de um front-end antigo falar com uma API mais
nova.

### 4.2 A ponte

O casco injeta um script em todo documento criado a partir da origem permitida, que define:

```ts
interface PandoraDesktop {
  version: string                                     // o /VERSION do app
  capabilities(): Promise<string[]>                   // módulos ativos neste PC, ex. ["files"]
  invoke<T>(method: string, args?: unknown): Promise<T>  // "files.pickFolder", "desktop.setAutostart"
  on(event: string, handler: (payload: unknown) => void): () => void  // "files.scanProgress"
}
```

O transporte é o canal de mensagens do próprio WebView2 (`chrome.webview.postMessage` ↔
`WebMessageReceived`), com um envelope JSON `{ id, method, args }` / `{ id, result | error }`. A cada
mensagem o casco confere a origem do remetente contra a URL do servidor configurado e descarta o resto
(D5).

Os nomes de método têm **namespace por módulo** (`files.*`). O casco é dono do namespace `desktop.*`
para as próprias configurações (iniciar com o Windows, URL do servidor, interruptores dos módulos,
versão do app).

No `client-web`, um único hook a envolve — algo como `useDesktop()`, que retorna `null` no navegador —
para que as páginas nunca toquem `window.pandoraDesktop` diretamente e toda UI só-desktop vire
"não exibida" no navegador.

### 4.3 Módulos desktop

```csharp
// Pottmayer.Pandora.Desktop.Abstractions
public interface IDesktopModule
{
    string Name { get; }                          // "files" — namespace da ponte e chave das configurações
    void Register(IServiceCollection services);   // IHostedService, IBridgeHandler, options...
}

public interface IBridgeHandler
{
    string Method { get; }                        // "files.pickFolder"
    Task<object?> HandleAsync(JsonElement? args, CancellationToken ct);
}
```

- O casco roda um **generic host** (`Microsoft.Extensions.Hosting`). Na inicialização ele percorre a
  lista de módulos — uma lista simples na composition root, como o `Program.cs` do backend — e chama
  `Register` **só para os módulos ligados neste dispositivo**. Um módulo desligado não tem serviços,
  handlers nem trabalho em segundo plano. Ligar ou desligar um módulo reinicia o host do app.
- Trabalho em segundo plano é um `IHostedService` comum. Continua rodando com a janela escondida.
- `capabilities()` retorna os nomes dos módulos registrados.

### 4.4 Os dois interruptores

| Interruptor | Onde | Significado | Padrão |
|---|---|---|---|
| **Conta** | configurações do módulo no servidor (mesmo formato do `is_enabled` do Assistant) | "eu uso esta feature" — desligado esconde o módulo em todo lugar, web e desktop | desligado |
| **Dispositivo** | `settings.json`, sob o nome do módulo, editado numa tela de configurações do desktop | "este PC faz o trabalho nativo desta feature" — ex. o PC com o disco escaneia, o notebook não | desligado |

Uma página mostra suas partes só-desktop quando o interruptor da conta está ligado **e** a capacidade
está em `capabilities()`. Exemplo: a tela "Pastas monitoradas" do Files aparece no app desktop do
homelab, não no do notebook, e não no navegador.

### 4.5 Credencial de dispositivo *(fase D2, necessária para o Files)*

Trabalho em segundo plano não pode depender da sessão do usuário: ela expira, e tem todo o alcance do
usuário. Um módulo que precisa chamar a API em segundo plano usa uma credencial de dispositivo.

- **Pertence ao Identity**, porque é autenticação e é compartilhada por todo futuro módulo desktop:
  uma tabela nova (`idt0XX_device`) com `user_id`, nome do dispositivo, `platform` (`windows` |
  `linux` | `macos` | `android` | `ios`), `form` (`desktop` | `headless` | `mobile`), o **hash** da
  chave, os **escopos** concedidos (ex. `files.agent`), `last_seen_at`, `revoked_at`.
- **Pareamento, de dentro do app, já logado:** **Conta → Dispositivos** oferece "Conectar este
  computador" (genérico, não ligado a um módulo). O web chama `POST /identity/devices` com a sessão
  normal do usuário; o backend devolve a chave **uma única vez**; a página a entrega à ponte
  (`desktop.storeCredential`), que a criptografa com **DPAPI** (usuário atual do Windows) em
  `credentials.bin`. A chave só existe em texto puro nessa única resposta. A D2 pareia sem escopos;
  o primeiro módulo que precisar de um (Files, `files.agent`) define como o escopo é concedido.
- **Uso:** o módulo a envia como `X-Api-Key`. O Tars já tem o esquema — `AddTarsIdentityApiKey` +
  `ApiKeyAuthenticationHandler`, que chama um `IApiKeyValidator` implementado pelo Pandora (busca pelo
  hash → principal com o id do usuário e as claims de escopo). O Pandora não usa esse esquema hoje.
- **Alcance:** a política de autorização padrão continua só-JWT. Uma chave de dispositivo é aceita
  **apenas** nos endpoints que optam pelo esquema de dispositivo mais uma política de escopo (o
  `/files/agent/*` do Files exige `files.agent`). Uma chave roubada não lê o Finances.
- **Revogação:** uma lista "Dispositivos conectados" nas configurações do web (Identity). Revogar faz
  a próxima chamada dar 401, e o módulo se mostra desconectado até ser pareado de novo.
- **Pareamento sem tela** (host headless, 4.8): o host pede ao backend um código curto, o imprime com
  a URL e fica consultando; o usuário confirma o código no web, já logado; o host então recebe a sua
  chave. É o device authorization flow (o padrão do "login na TV"). O desktop não precisa disso — ele
  já está logado.

### 4.6 Ciclo de vida

- **Bandeja:** fechar a janela a esconde; o menu da bandeja tem *Abrir*, *Configurações* e *Sair*.
  *Sair* para o host, então os módulos em segundo plano param também.
- **Instância única:** abrir de novo foca a janela existente.
- **Iniciar com o Windows:** um interruptor (`desktop.setAutostart`) que grava a chave
  `HKCU\…\Run` — sem admin. Inicia escondido na bandeja.
- **Atualizações:** o Velopack verifica o GitHub Releases ao iniciar e periodicamente, baixa em
  segundo plano e aplica no próximo reinício. Uma atualização que falha deixa a versão atual rodando.

### 4.7 Contra qual servidor ele roda

Hoje o servidor é o homelab (Windows, Docker), acessível na rede local — ver
[deploy no homelab](../../deployment/homelab-deploy.md). Com a fase pública (Cloudflare Tunnel +
Cloudflare Access), o WebView passa pelo Access como qualquer navegador, mas as **chamadas em segundo
plano** de um módulo não têm sessão de navegador para o Access; vão precisar de um service token do
Access ou de um bypass do Access nas rotas de chave de dispositivo (questão em aberto 2).

---

### 4.8 Outras plataformas

A separação que mantém isso barato já está no 4.3: os módulos só conhecem `IServiceCollection`,
`IHostedService` e `IBridgeHandler`, e têm como alvo `net10.0` puro, que roda em Windows, Linux e
macOS. Só o casco é específico do Windows. Cada plataforma nova é, então, um novo **host**, não uma
reescrita dos módulos.

| Plataforma | Host | O que exige | Esforço |
|---|---|---|---|
| Windows (D1) | WinForms + WebView2 | — | a base |
| **Headless** — servidor/NAS Linux, Docker, serviço do Windows | um host de console (generic host + integração systemd / Windows Service): sem janela, sem ponte | pareamento por código (4.5); a configuração já vive no servidor (ex. raízes e filtros do Files), então nada é configurado localmente; chave guardada num arquivo legível só pelo usuário do serviço | pequeno — o próximo passo mais útil |
| Desktop Linux / macOS | outro casco em volta do webview do sistema (ex. Photino.NET sobre WebKitGTK / WKWebView) ou Avalonia | bandeja, iniciar com o sistema (arquivo `.desktop` / LaunchAgent), empacotamento (AppImage / `.dmg`), DPAPI → libsecret / Keychain | médio, por SO |
| Android / iOS | um casco mobile envolvendo o mesmo `client-web` (ex. Capacitor) mais plugins nativos | o SO restringe os dois lados: nada de varrer o sistema de arquivos livremente (só a galeria e pastas concedidas pelo usuário), e trabalho em segundo plano é racionado — o iOS sobretudo roda quando o sistema decide. Código nativo em outra linguagem, então os módulos são reimplementados, não reaproveitados | grande |

**Recomendação.** A D1 continua só Windows — o homelab é Windows —, sob a regra de que nada além do
casco referencia o Windows. A primeira outra plataforma que vale construir é o **host headless**: ele
cobre servidores e NAS Linux, e também o caso "ninguém logado" no Windows (questão em aberto 1).
Cascos desktop para Linux/macOS vêm quando houver uma máquina dessas para rodá-los. Celulares vêm por
último, e para o Files o escopo útil deles é "as fotos e vídeos do celular", não pastas quaisquer.

---

## 5. Comportamento em falhas

| Situação | Comportamento |
|---|---|
| Servidor inacessível | página local "não consegui falar com o Pandora" com *Tentar de novo* / *Trocar servidor*; módulos em segundo plano esperam e tentam de novo |
| Sessão expirada | tratada pelo `client-web` como hoje (refresh, senão a tela de login) |
| Chave de dispositivo revogada ou inválida (401) | o módulo para o trabalho, se marca desconectado, a página oferece parear de novo |
| Chamada da ponte de origem estranha | descartada e registrada em log; a página nunca recebe resposta |
| Atualização falha | a versão atual continua rodando; nova tentativa na próxima verificação |

---

## 6. Roadmap

### Fase D1 — O casco *(implementada)*

- `client-desktop/` com `Desktop.Host` e `Desktop.Abstractions`; lista de módulos vazia.
- URL do servidor na primeira execução, carga remota, links externos no navegador padrão, página
  offline.
- Bandeja, instância única, iniciar com o Windows (desligado por padrão).
- Ponte com `version`, `capabilities()` (vazia) e os métodos `desktop.*`; checagem de origem; hook
  `useDesktop()` no `client-web` mais uma pequena seção "Desktop" nas configurações do web
  (iniciar com o Windows, servidor, versão), exibida só dentro do app.
- Empacotamento com Velopack e um job de CI publicando `Setup.exe` + atualizações numa tag de release.
- **Pronto quando:** você instala, entra, usa o Pandora exatamente como no navegador; fechar a janela
  mantém o app na bandeja; reiniciar o Windows o traz de volta; um release novo o atualiza sozinho.

### Fase D2 — Credenciais de dispositivo *(implementada)*

- Identity: `idt0XX_device`, `POST/GET/DELETE /identity/devices`, `IApiKeyValidator`, políticas de
  escopo; `AddTarsIdentityApiKey` registrado junto com o JWT sem mudar a política padrão.
- Casco: `desktop.storeCredential` + armazenamento com DPAPI; um `HttpClient` autenticado para os
  módulos.
- Web: "Dispositivos conectados" nas configurações.
- **Pronto quando:** uma chave de dispositivo alcança um endpoint que opta pelo seu escopo, recebe 401
  em todos os outros, e para de funcionar no momento em que é revogada.

### Depois — quando aparecer um motivo

- `Desktop.Assistant`: um atalho global abrindo a barra de comando do Assistant em qualquer lugar do
  Windows.
- `Desktop.Agenda`: notificações nativas do Windows para lembretes, como canal alternativo ao
  Telegram.
- Soltar um arquivo no ícone da bandeja para mandá-lo à fila de entrada do Finances.
- **D3 — Host headless** (4.8): os mesmos módulos num host de console/daemon no Linux, no Docker ou
  como serviço do Windows, com pareamento por código. É também a resposta à questão em aberto 1.
- Cascos desktop para Linux / macOS; cascos mobile (4.8).

---

## 7. Questões em aberto

1. **Sessão logada no homelab.** Um app de bandeja só roda depois de um login no Windows. O homelab
   roda o Docker Desktop, que já exige um, então se assume que a bandeja basta. Se isso mudar, o host
   headless (D3) roda os mesmos módulos como serviço do Windows e a janela vira só UI — o contrato de
   módulo não muda, só qual processo o hospeda.
2. **Cloudflare Access e chamadas em segundo plano** (ver 4.7): service token vs. bypass nas rotas de
   chave de dispositivo. Só importa quando a fase pública estiver no ar; na rede local não se aplica.
3. **Assinatura de código.** Um `Setup.exe` não assinado dispara o SmartScreen. Aceitável para uso
   pessoal; rever se o app for compartilhado com outras pessoas.
4. **Tecnologia do casco para Linux/macOS.** Photino.NET, Avalonia, Tauri ou Electron — decidido
   quando essa fase começar, contra o que estiver maduro na época. O contrato de módulo e a ponte
   continuam de qualquer jeito.
