# Dispositivos

[← Voltar ao índice](README.md) · Relacionados: [Autenticação](authentication.md), [Modelo de Dados](data-model.md#idt009_device), [Pandora Desktop](../../../architecture/pt-BR/desktop-client.md)

---

Um **dispositivo** é um cliente pareado — hoje o Pandora Desktop; depois um agente headless ou um
celular — que chama a API com **chave própria** em vez da sessão do usuário. Trabalho em segundo plano
(o agente do Files escaneando um disco com a janela fechada) não pode depender de uma sessão: sessões
expiram e carregam todo o alcance do usuário. Uma chave de dispositivo é de longa duração, **limitada
aos seus escopos** e **revogável** a qualquer momento.

## 1. Pareamento

O usuário está logado; o dispositivo é pareado pelo web, com a sessão:

```
POST /identity/devices  { name, platform, form, scopes[] }
→ { device, key }        ← a chave aparece aqui uma vez, nunca mais
```

- `platform`: `windows` | `linux` | `macos` | `android` | `ios`. `form`: `desktop` | `headless` | `mobile`.
- `scopes`: nomes em minúsculas separados por ponto (`files.agent`). **Não** são conferidos contra um
  catálogo — o usuário concede ao próprio dispositivo acesso aos próprios dados, e um escopo só
  significa algo onde um endpoint o exige.
- A chave é `pdk_` + 32 bytes aleatórios (base64url). Só o **SHA-256** dela é guardado
  (`idt009.key_hash`), como todo token deste módulo.

Dentro do Pandora Desktop, **Conta → Dispositivos** mostra "Conectar este computador": a página
registra o dispositivo com o nome do computador e entrega a chave ao app pela ponte
(`desktop.storeCredential`), que a criptografa com DPAPI. Ver
[desktop-client §4.5](../../../architecture/pt-BR/desktop-client.md#45-credencial-de-dispositivo-fase-d2-necessária-para-o-files).

## 2. Autenticando com uma chave

O dispositivo envia `X-Api-Key: pdk_…`. O esquema é o do Tars (`AddTarsIdentityApiKey`), registrado
com o nome `ApiKey` ao lado do JWT. O `DeviceApiKeyValidator` do Pandora calcula o hash da chave,
encontra o dispositivo não revogado e devolve um principal com:

| Claim | Valor |
|---|---|
| `Id` (e o name identifier) | o id do **usuário** — então o contexto de usuário e toda consulta por usuário funcionam exatamente como com uma sessão |
| `device_id` | o id do dispositivo |
| `scope` | uma claim por escopo concedido |

Cada requisição bem-sucedida registra `last_seen_at`, no máximo uma vez a cada 5 minutos.

## 3. Quais endpoints aceitam uma chave

A **política padrão continua só-JWT**: `[Authorize]` nunca aceita chave de dispositivo. Um endpoint
opta nomeando o esquema de dispositivo e uma política de dispositivo — os nomes ficam no
`Identity.Abstractions` (`DeviceAuthorization`) para qualquer módulo usar:

```csharp
[Authorize(AuthenticationSchemes = DeviceAuthorization.Scheme, Policy = DeviceAuthorization.Policy)]                           // qualquer dispositivo
[Authorize(AuthenticationSchemes = DeviceAuthorization.Scheme, Policy = DeviceAuthorization.ScopePolicyPrefix + "files.agent")] // esse escopo
```

As políticas são montadas sob demanda pelo `DevicePolicyProvider` (nada a registrar por escopo); as
duas também exigem a claim `device_id`, então um token de sessão nunca as satisfaz. A consequência,
fixada pelos testes de integração: uma chave recebe **401 em todo endpoint de sessão**, e uma sessão
recebe **401 em todo endpoint de dispositivo**.

## 4. Listando e revogando

`GET /identity/devices` lista os dispositivos **ativos** do usuário (mais novos primeiro).
`DELETE /identity/devices/{id}` revoga: `revoked_at` é preenchido e a chave falha na próxima
requisição. Dispositivos revogados saem da lista; as linhas ficam. O dispositivo de outro usuário
responde 404.

O Pandora Desktop confere a chave ao iniciar (`GET /identity/devices/me`) e a esquece num 401, então
um computador revogado de outro lugar aparece como "não conectado" na próxima vez que abrir.
