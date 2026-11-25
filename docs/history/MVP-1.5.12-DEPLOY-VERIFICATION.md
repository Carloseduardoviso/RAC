# RAC MVP 1.5.12 — verificação de implantação controlada

Data: 23/11/2026. Computador: `080001325ADPLAN`.

## Backup e proteção de dados

- Banco LocalDB RAC: backup com CHECKSUM criado em `%LOCALAPPDATA%\RAC\backups\RAC-pre-MVP1512-20261123.bak` (10.145.792 bytes).
- `RESTORE VERIFYONLY WITH CHECKSUM`: backup íntegro.
- Antes: PAPER `Running=1`, `RiskLocked=0`, 1 posição, 13 fills; Testnet desabilitada, 0 ordens, 0 políticas.
- PAPER pausado temporariamente com alteração SQL condicional após backup; 1 posição e 13 fills preservados.
- Arquivos Release publicados em `%LOCALAPPDATA%\RAC\staging\mvp-1.5.12\api` e `...\web`.
- CSS do artefato Web foi mantido exatamente igual ao do pacote 1.5.11, por cópia após publish e verificação de hash, evitando publicar alteração de CSS de outro trabalho local.

## Ativação

- Processos anteriores 24580 (API) e 24312 (Web) parados.
- A primeira tentativa dos novos serviços usando `http://localhost:5080` / `5081` falhou com `AddressInUseException` apesar de ausência posterior de sockets ouvindo nessas portas. Nenhuma ordem enviada, PAPER continuou pausado durante o diagnóstico.
- A segunda tentativa, com bind IPv4 explícito `http://127.0.0.1:5080` e `http://127.0.0.1:5081`, foi bem-sucedida: API PID 25168, Web PID 15640 durante os testes.
- `/api/health`: HTTP 200 e `realOrdersEnabled=false`, `testnetOrderEnabled=false`.
- Web `/` e JavaScript: HTTP 200. Tanto `http://localhost:5081/` quanto `http://localhost:5080/api/health` funcionaram após bind IPv4.
- `GET /api/risk/policies`, `POST /api/risk/policies` e `POST /api/risk/policies/1/review`: HTTP 401 sem autenticação, através do proxy; nenhuma proposta inserida.
- Nova seção e indicador `policyIntegrity` confirmados no HTML estático.
- Logs da segunda tentativa `mvp1512-api-retry.err.log` e `mvp1512-web-retry.err.log`: 0 bytes na validação.
- Navegação autenticada ponta a ponta não foi executada por ausência de sessão de usuário.

## Resultado final

- PAPER restaurado ao estado original `Running=1`, `RiskLocked=0`; 1 posição e 13 fills no banco após retomada.
- Testnet permanece `Enabled=0`, 0 ordens registradas; 0 propostas e 0 eventos de revisão no banco operacional.
- Nenhum executor de ordens reais foi habilitado, nenhuma ordem à Binance enviada, nenhuma credencial alterada.
- Mudança local em `src/RAC.Web/wwwroot/styles.css` e arquivos `docs/superpowers/` de outras tarefas foram preservados sem commit.
