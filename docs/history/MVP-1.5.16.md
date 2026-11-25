# RAC MVP 1.5.16 — Recuperação segura da API/Web e diagnóstico PAPER

Data: 23/11/2026. Computador: `080001325ADPLAN`. Branch: `main`.

## Diagnóstico e limites das evidências

- Evento operacional `PAPER_RISK_LOCK` registrado em **2026-11-23 16:14:13 UTC**. PAPER permaneceu `Running=false` e `RiskLocked=true`.
- Motivo anterior persistido: `Carlos: erro de rede, dados ou persistência. Revisão obrigatória.`.
- Código anterior capturava qualquer exceção do ciclo e persistia apenas a mensagem genérica. Os logs disponíveis não permitem reconstruir a exceção exata que iniciou a parada. Não se afirma que a causa raiz da falha de 16:14 tenha sido identificada.
- `RacPaperFailureClassifier` agora categoriza futuras falhas (SQL_PERSISTENCE, PUBLIC_NETWORK, TIMEOUT, MARKET_DATA_VALIDATION, PAPER_STATE_VALIDATION ou UNEXPECTED) sem persistir mensagens potencialmente sigilosas.
- O worker aplica a mesma política de falha segura tanto na inicialização quanto nos ciclos periódicos: registra diagnóstico, tenta persistir trava e **encerra**. Não existe retry nem retomada automática do PAPER.
- A aprovação humana para liberar trava permanece obrigatória.

## Recuperação de processos — somente serviços locais

- Script `tools/Start-RacPaperLocal.ps1` mantém bind `127.0.0.1`, conexão SQL LocalDB, valida explicitamente `RealOrdersEnabled=false` e `TestnetOrdersEnabled=false` e consulta estado PAPER sem alterar o bloqueio. Versão padrão: 1.5.16.
- Novo `tools/Watch-RacServices.ps1`: trava por mutex para evitar sobreposição, verifica API `/api/health` e Web HTML estaticamente (independente da API). Reinicia apenas serviço **sem listener**; não finaliza processos desconhecidos nem reinicia porta ocupada porém não saudável.
- Se há listener não saudável, falha fechado e registra ocorrência para revisão manual. Incidentes/reinícios são registrados em `%LOCALAPPDATA%\RAC\logs\mvp1516-watchdog.log`, sem armazenar mensagens de exceções.
- `tools/Install-RacServiceWatchdog.ps1` registrou tarefa `RAC-MVP1516-Watchdog` com privilégios limitados do usuário conectado, verificando a cada 2 minutos. Usa logon interativo e duração de repetição de 365 dias; **não** substitui um serviço Windows independente de login ou alta disponibilidade.
- Para inspecionar: `Get-ScheduledTask -TaskName RAC-MVP1516-Watchdog`. Para remover: `powershell -File tools\Install-RacServiceWatchdog.ps1 -Remove`.
- Teste sem tocar nos serviços: `powershell -File tools\Watch-RacServices.ps1 -ApiPort 55080 -WebPort 55081 -DryRun`.

## Backup, implantação e reconciliação

- Backup operacional com CHECKSUM e `RESTORE VERIFYONLY WITH CHECKSUM` aprovados: `%LOCALAPPDATA%\RAC\backups\RAC-pre-MVP1516-20261123-133342.bak`.
- API/Web compiladas em Release e publicadas em `%LOCALAPPDATA%\RAC\staging\mvp-1.5.16\api` e `...\web`. Variáveis de habilitação de ordens permaneceram falsas.
- Processos anteriores, identificados por portas e DLLs, substituídos de forma controlada em 127.0.0.1:5080/5081. API/Web/proxy e página de segurança responderam HTTP 200.
- Testes de falha **reais, locais e controlados**: parar apenas Web PID 14092 e executar watchdog => Web PID 6428 recuperado; parar API PID 4108 e executar watchdog => API PID 38876 recuperada, sem criar instâncias duplicadas.
- Reconciliação antes e depois: `Running=false`, `RiskLocked=true`, **17 fills**, **1 posição DOTUSDT**, **0 ordens Testnet**, `Testnet.Enabled=false`. Nenhum histórico ou posição removido.
- Testes .NET Release: **269 aprovados**, 0 falhas. Incluem testes de classificação sem vazamento de dados e de reinício com trava persistente, posições, fills e auditoria em SQL descartável.
- Probes PowerShell: arquivos sem erros de parsing, ambiente saudável, simulação dry-run de portas indisponíveis. A tarefa agendada foi executada manualmente e retornou `LastTaskResult=0`, com próxima execução programada.
- Não foram enviados comandos de ordem à Binance, não foram alteradas credenciais nem autorizadas operações reais/Testnet.

## Pendências explícitas

1. Identificar a origem da falha de 16:14 exigirá registros de exceção contemporâneos ou nova ocorrência; a mensagem histórica genérica não permite diagnóstico retroativo conclusivo.
2. Revisar manualmente o incidente e a posição DOTUSDT antes de qualquer desbloqueio; nenhuma liberação foi realizada.
3. Validar execução da tarefa no Agendador e renovar a configuração após 365 dias. Para operação sem usuário conectado, planejar serviço dedicado com identidade e acesso ao SQL apropriados, após homologação.
4. Homologar login, navegação visual do mapa e acesso autenticado ao painel de segurança no navegador real.
