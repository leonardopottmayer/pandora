# Deploy do Pandora no homelab

Plano para rodar Pandora (front + back + Postgres) na máquina homelab (Windows, antigo PC gamer), sem custo de hospedagem externa.

## Visão geral

| Camada | Onde roda | Como |
|---|---|---|
| Ambientes | homelab, 1 stack Docker por ambiente | `prod` e `staging` isolados via project name do Compose; mesma imagem, `.env` por ambiente. Ver [deployment.md](deployment.md) |
| Postgres | homelab, container Docker, named volume | 1 instância **por ambiente**; dentro do ambiente, 1 banco por app (`pottmayer_pandora`, futuros apps depois) |
| Backend (.NET) | homelab, container Docker | imagem buildada via CI (GitHub Actions), publicada no GHCR |
| Frontend (client-web) | homelab, container Docker (nginx) | serve estático + proxy `/api` -> backend (mesma origem, sem CORS) |
| Acesso público (fase 2) | Cloudflare Tunnel | `cloudflared` na máquina, sem porta aberta no roteador, com Cloudflare Access na frente |
| Backup do banco | `pg_dump` semanal -> subpasta dedicada na área já monitorada pelo iDrive | 3-2-1 já coberto pelo iDrive existente |
| Secrets/config | arquivo `.env.<ambiente>` ao lado do `docker-compose.yml`, cópia guardada no Bitwarden | `docker compose --env-file .env.<ambiente> up -d` injeta tudo |

Princípio de isolamento: nada do Docker/Postgres usa volume dentro das pastas de arquivos pessoais do HD de 20TB (exceto a subpasta de backup). Tailscale fora do escopo por enquanto.

---

## Fase 1 — Rodando na rede local

**Meta:** Pandora acessível de qualquer máquina na rede de casa via IP local do homelab (ex. `http://192.168.1.X`).

### 1.1 Containerização — ✅ implementado

Guia operacional (build/run/ambientes): [deployment.md](deployment.md).

Decisões tomadas na implementação:
- **Ambientes:** `prod` (porta 8730, tag fixa) e `staging` (porta 8731, `:latest`), cada um um stack completo isolado por `COMPOSE_PROJECT_NAME`. Só o nginx publica porta no host; Postgres/backend ficam internos. Mesma imagem serve os dois; só o `.env.<ambiente>` muda.
- **Banco:** um `pottmayer_pandora` por ambiente (as 3 connection strings do app apontam pro mesmo DB com schemas separados).
- **Feed NuGet privado:** os pacotes `Pottmayer.Tars.*` vêm do GitHub Packages; o build do backend autentica via build secret (`github_token`, PAT com `read:packages`).
- **E-mail:** `mailhog` como serviço sob profile do Compose (`COMPOSE_PROFILES=mailhog`) — sobe no staging (captura, UI na LAN em 8732), fica de fora do prod. SMTP host/porta do backend vêm do `.env`; prod aponta pra provedor real (fase 2).

Itens criados:

1. `VERSION` na raiz (fonte única de versão) + `Directory.Build.props` lendo ele.
2. `Dockerfile` do backend (multi-stage build .NET SDK -> runtime).
3. `Dockerfile` do client-web (build Vite -> nginx servindo estático + proxy `/api`).
4. `docker-compose.yml`: postgres + backend + frontend, rede interna, volumes nomeados, variáveis lidas de um `.env`.
5. `ForwardedHeaders` + `HttpsRedirection` condicional no `Program.cs` (necessário para rodar atrás de proxy — já prepara para a fase 2).
6. `.env.<ambiente>` (não commitado) com os secrets: senha do Postgres, `Jwt:SigningKey`, `Mfa:EncryptionKey`. Cópia guardada no Bitwarden.

Nesta fase, `Cors:AllowedOrigins`, `ActivationUrlTemplate` e `PasswordResetUrlTemplate` podem usar o IP local ou ficar sem configuração de URL pública ainda. E-mail pode continuar apontando para Mailhog (captura local) enquanto não há URL definitiva.

### 1.2 Banco de dados

- Container Postgres único, named volume Docker (não bind mount na área de arquivos pessoais).
- Volume sobrevive a `docker rm`/`docker compose down` — só apagado com comando explícito.
- Porta publicada só em `127.0.0.1` (não na LAN), pra o `migris` do host conectar. Uma porta por ambiente (`POSTGRES_PORT`: prod 5432, staging 5433).

### 1.3 Backup

- Script `pg_dump -Fc` por banco, semanal, via Task Scheduler do Windows.
- Grava numa subpasta nova dentro da área já monitorada pelo iDrive.
- Script de retenção apaga dumps além do limite acordado.
- Confirmar que o iDrive está monitorando essa subpasta específica.

### 1.4 CI / deploy — ✅ implementado (workflow)

- Workflow `.github/workflows/build-images.yml` builda backend + frontend e publica no GHCR a cada push na `main`. Tags: `latest`, versão do `VERSION` e `sha-<commit>`.
- Push no GHCR usa o `GITHUB_TOKEN` automático; o restore dos `Pottmayer.Tars.*` (repo separado) usa o secret **`NUGET_GITHUB_TOKEN`** (PAT com `read:packages`).
- No homelab: `docker compose --env-file .env.<ambiente> pull && ... up -d` manual, seguido de `migris apply <ambiente>` quando houver migração nova.

### 1.5 Pendências da fase 1

- [x] Criar o PAT `read:packages` e adicioná-lo como secret `NUGET_GITHUB_TOKEN` do repositório. CI verde, imagens publicadas no GHCR.
- [x] Secrets de **staging** gerados (`.env.staging`) e guardados no Bitwarden.
- [ ] Subir o staging no homelab e testar; depois replicar pra prod.
- [ ] Gerar e guardar no Bitwarden os secrets de **produção** (`.env.prod`).
- [ ] `docker login ghcr.io` no homelab com o PAT `read:packages` (imagens são privadas).
- [ ] Config local (não versionada) do `migris` no homelab com as conexões prod/staging apontando pra `127.0.0.1:POSTGRES_PORT` — não commitar senha de prod no `migrations/config.json` (repo público).
- [ ] Backup: adiado (pulado por ora).

---

## Fase 2 — Exposição à internet

**Meta:** Pandora acessível de fora de casa via `pandora.pottmayer.dev`, com Cloudflare Access bloqueando qualquer acesso que não seja o do Leonardo.

### 2.1 DNS

- Migrar a zona `pottmayer.dev` para a Cloudflare (mudar nameservers no GoDaddy).
- Antes de trocar os nameservers: recriar no painel da Cloudflare o registro existente que aponta para a Digital Ocean (portfólio), para não haver downtime.
- Criar subdomínio `pandora.pottmayer.dev` (ou outro) apontando para o túnel.

### 2.2 Cloudflare Tunnel + Access

- Instalar e configurar `cloudflared` na máquina homelab — só abre conexão de saída, nada escuta entrada vinda da internet.
- Ligar **Cloudflare Access** na frente do `pandora.pottmayer.dev`, restrito ao e-mail do Leonardo.
- O que o túnel não cobre: bugs de segurança dentro do próprio app e segurança da conta Cloudflare (senha forte + 2FA nela).

### 2.3 Ajustes no app

- `Cors:AllowedOrigins` -> `https://pandora.pottmayer.dev`.
- `ActivationUrlTemplate` / `PasswordResetUrlTemplate` -> URL de produção.
- Substituir Mailhog por provedor de SMTP real.

### 2.4 Pendências da fase 2

- [ ] Confirmar nome do subdomínio (`pandora.pottmayer.dev` ou outro).
- [ ] Escolher provedor de SMTP gratuito: Brevo (300 e-mails/dia) ou Resend (3.000/mês) — recomendação: Brevo.
- [ ] Migrar zona DNS para a Cloudflare e recriar registro da Digital Ocean antes de trocar os nameservers.
