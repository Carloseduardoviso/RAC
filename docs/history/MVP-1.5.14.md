# RAC MVP 1.5.14 — Monitoramento de segurança e testes de recuperação

Data da implementação: 23/11/2026. Computador: `080001325ADPLAN`.

## Implementação

- `RacSafetyDiagnostics`: avaliador puro de segurança operacional, sem comandos de execução, rede, escrita ou autorização de ordens.
- `RacSafetyMonitoringService`: consulta SQL Server **somente leitura** para estado da conta PAPER, último ciclo e amostra do monitoramento, alertas persistidos, estado Testnet, quantidade de ordens Testnet históricas e propostas de risco pendentes.
- Rota `GET /api/risk/safety-monitoring` protegida por `RacRiskReviewer`, resposta `READ_ONLY_SAFETY_DIAGNOSTICS`, `realOrdersEnabled=false`. Em falha SQL identificada, HTTP 503 e sem habilitar operações.
- Proxy Web usa allowlist fechada e encaminha a nova rota.
- Nova página isolada `/risk-security.html` e script `/risk-security.js`, com indicadores e incidentes, uso de `textContent` para dados e sem controles de operação.
- O dashboard principal (`index.html`, `app.js`, `styles.css`) possui alterações locais de outro trabalho; **não foi modificado nem incluído neste commit**.
- Alertas são diagnósticos avaliados **ao consultar a rota**, não notificações proativas nem garantias de operação saudável.

## Testes executados

- Testes unitários de estado saudável, pausa voluntária do PAPER, dados antigos, falta de estado persistido, bloqueio de risco, pendências e ativação inesperada da Testnet.
- Teste real HTTP de autorização na instância de teste isolada: usuários anônimos e `Viewer` impedidos, `RiskReviewer` autenticado autorizado e resposta com `realOrdersEnabled=false`.
- Teste SQL de recuperação com banco LocalDB **descartável**: reabertura de conexão, duas consultas de monitoramento e verificação de que PAPER, posição, fill, amostra e ordens Testnet permaneceram intactos.
- Suíte Release: **258 testes aprovados, zero falhas**.
- Build Release da solução: **0 erros, 0 avisos**.
- JavaScript `node --check` do painel separado aprovado.

## Ocorrência operacional detectada e recuperação

Durante a verificação final, a API/Web operacionais não estavam respondendo e a conta PAPER apresentava `Running=0`. Os testes usam banco descartável e não executam ordens, mas **a causa da parada não foi confirmada**. Os dados existentes mostravam **16 fills e 0 posições abertas**; não houve exclusão ou ajuste desses registros.
Os processos operacionais existentes do MVP 1.5.12 foram iniciados novamente a partir de `%LOCALAPPDATA%\RAC\staging\mvp-1.5.12\api` e `...\web` e retornaram HTTP 200 nas portas 5080 e 5081.
Após confirmar `RiskLocked=0` e `Testnet.Enabled=0`, o PAPER foi retomado por atualização SQL condicional, resultando em `Running=1`, 16 fills, 0 posições e zero ordens Testnet na checagem.
A ocorrência impede afirmar que o PAPER permaneceu ininterrupto durante esta etapa, embora não tenha sido pausado intencionalmente pela implementação. **Recomenda-se investigar o encerramento inesperado e configurar supervisão do processo** antes da próxima implantação.

## Entrega e limites

- **Somente código e testes**: não foi aplicado deploy do MVP 1.5.14 na API/Web; a página nova passará a estar disponível depois de um deploy controlado.
- Não foi realizado teste visual automatizado de navegação na nova página; foram validados lógica, JS, API autenticada e acesso aos dados em ambiente isolado.
- Nenhuma ordem enviada à Binance, nenhuma credencial modificada, nenhuma negociação real ou Testnet habilitada.
- O esquema do banco operacional não foi alterado por esta implementação.
