# Deploy do Pandora no homelab

Plano para rodar Pandora (front + back + Postgres) na máquina homelab (Windows, antigo PC gamer), sem custo de hospedagem externa.

## Visão geral

| Camada | Onde roda | Como |
|---|---|---|
| Postgres | homelab, container Docker, named volume | 1 instância, 1 banco por app (`pottmayer_pandora_*`, futuros apps depois) |
| Backend (.NET) | homelab, container Docker | imagem buildada via CI (GitHub Actions), publicada no GHCR |
| Frontend (client-web) | homelab, container Docker (nginx) | serve estático + proxy `/api` -> backend (mesma origem, sem CORS) |
| Acesso público (fase 2) | Cloudflare Tunnel | `cloudflared` na máquina, sem porta aberta no roteador, com Cloudflare Access na frente |
| Backup do banco | `pg_dump` semanal -> subpasta dedicada na área já monitorada pelo iDrive | 3-2-1 já coberto pelo iDrive existente |
| Secrets/config | arquivo `.env` ao lado do `docker-compose.yml`, cópia guardada no Bitwarden | `docker compose up -d` injeta tudo automaticamente |

Princípio de isolamento: nada do Docker/Postgres usa volume dentro das pastas de arquivos pessoais do HD de 20TB (exceto a subpasta de backup). Tailscale fora do escopo por enquanto.

---

## Fase 1 — Rodando na rede local

**Meta:** Pandora acessível de qualquer máquina na rede de casa via IP local do homelab (ex. `http://192.168.1.X`).

### 1.1 Containerização

Estado atual do monorepo: não há `Dockerfile`, `docker-compose.yml` nem `VERSION` no `main`. Construir do zero, simples e documentado (o stash antigo `gambi-docker` foi descartado).

Itens a criar:

1. `VERSION` na raiz (fonte única de versão) + `Directory.Build.props` lendo ele.
2. `Dockerfile` do backend (multi-stage build .NET SDK -> runtime).
3. `Dockerfile` do client-web (build Vite -> nginx servindo estático + proxy `/api`).
4. `docker-compose.yml`: postgres + backend + frontend, rede interna, volumes nomeados, variáveis lidas de um `.env`.
5. `ForwardedHeaders` + `HttpsRedirection` condicional no `Program.cs` (necessário para rodar atrás de proxy — já prepara para a fase 2).
6. `.env` (não commitado) com os secrets: senha do Postgres, `Jwt:SigningKey`, `Mfa:EncryptionKey`. Cópia guardada no Bitwarden.

Nesta fase, `Cors:AllowedOrigins`, `ActivationUrlTemplate` e `PasswordResetUrlTemplate` podem usar o IP local ou ficar sem configuração de URL pública ainda. E-mail pode continuar apontando para Mailhog (captura local) enquanto não há URL definitiva.

### 1.2 Banco de dados

- Container Postgres único, named volume Docker (não bind mount na área de arquivos pessoais).
- Volume sobrevive a `docker rm`/`docker compose down` — só apagado com comando explícito.

### 1.3 Backup

- Script `pg_dump -Fc` por banco, semanal, via Task Scheduler do Windows.
- Grava numa subpasta nova dentro da área já monitorada pelo iDrive.
- Script de retenção apaga dumps além do limite acordado.
- Confirmar que o iDrive está monitorando essa subpasta específica.

### 1.4 CI / deploy

- GitHub Actions (runner padrão) builda as imagens e publica no GHCR a cada push na `main`.
- No homelab: `docker compose pull && docker compose up -d` manual, seguido de `migris apply <env>` quando houver migração nova.

### 1.5 Pendências da fase 1

- [ ] Gerar e guardar no Bitwarden os secrets de produção (`Jwt:SigningKey`, `Mfa:EncryptionKey`, senha do Postgres).
- [ ] Criar subpasta de backup e confirmar que o iDrive a monitora.
- [ ] Definir retenção dos dumps semanais (quantas semanas manter).

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
