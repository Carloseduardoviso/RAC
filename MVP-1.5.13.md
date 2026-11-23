# RAC MVP 1.5.13 — Homologação HTTP autenticada do fluxo de políticas

Data: 23/11/2026. Host Windows: `080001325ADPLAN`.

## Implementado e executado

- Criado teste automatizado `ProductionRiskPolicyHttpHomologationTests` para exercitar uma instância **isolada** da API RAC em porta IPv4 aleatória, com banco SQL Server LocalDB descartável `RAC_MVP1513_HTTP_<guid>`.
- Banco isolado criado com migrações EF Core; usuários temporários `Administrator`, `RiskReviewer` e `Viewer` com senhas aleatórias em memória, sem utilizar credenciais reais do usuário.
- API subprocessada aponta explicitamente para o banco descartável com `RAC_SQLSERVER_CONNECTION`, sem substituir o serviço real nas portas 5080/5081. O subprocesso é encerrado e o banco de testes excluído no bloco `finally`.

## Cenários confirmados por requisições HTTP reais

1. Acesso anônimo a propostas recebe HTTP 401.
2. Requisição POST autenticada sem cabeçalho CSRF recebe HTTP 403.
3. Login por cookie funciona para três perfis: `Administrator`, `RiskReviewer`, `Viewer`.
4. `Viewer` não consulta nem propõe políticas (HTTP 403).
5. `RiskReviewer` consulta e propõe políticas (HTTP 200), mas não revisa (HTTP 403).
6. `Administrator` pode revisar proposta de outro usuário e retorna `REVIEWED_NO_TRADING_PERMISSION` e `orderPlacementEnabled=false`.
7. Nova tentativa de revisar a mesma proposta retorna conflito HTTP 409.
8. Administrador não pode revisar a própria proposta (HTTP 409).
9. Histórico autenticado retorna indicadores de uma proposta pendente, uma revisada e `tradingEnabled=false`.
10. Usuário desabilitado no SQL perde autorização de sessão na requisição seguinte (HTTP 401).
11. Banco de teste contém exatamente 2 propostas e 3 auditorias; zero ordens Testnet.

## Evidência de execução

- Teste HTTP isolado: aprovado.
- Suíte completa .NET Release: **251 testes aprovados**, 0 falhas.
- Build Release da solução: zero erros, zero avisos.
- Instância operacional: PAPER `Running=1`, `RiskLocked=0`; Testnet `Enabled=0`; zero ordens Testnet; zero propostas e auditorias no banco operacional. `/api/health` responde `status=ok`, `realOrdersEnabled=false`, `testnetOrderEnabled=false`.

## Limites

- A homologação abrangeu os endpoints HTTP e o contrato de autenticação/CSRF, não incluiu inspeção visual com navegador ou teste automatizado de cliques nos formulários.
- A sessão foi invalidada por desabilitação do usuário; não foi aguardada a expiração temporal de oito horas.
- Nenhum deploy operacional, reinício, alteração SQL operacional ou envio de ordens à Binance foi realizado.
- Os arquivos modificados `src/RAC.Web/wwwroot/app.js`, `index.html`, `styles.css` e os arquivos `docs/superpowers/` foram preservados como alterações locais de trabalho distinto, sem commit nesta etapa.
