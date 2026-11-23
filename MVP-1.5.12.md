# RAC MVP 1.5.12 — Homologação e reforço da auditoria documental

Data: 23/11/2026. Host: `080001325ADPLAN`.

## Implementação

- `ProductionRiskPolicyStore.HistoryAsync` valida hashes SHA-256 dos documentos de política e consistência entre registros de propostas e revisões. Detecta auditoria ausente, incompatível ou duplicada, e bloqueia a apresentação como válida.
- Os indicadores de observabilidade documental incluem propostas pendentes, revisões favoráveis (sem permissão de negociação), rejeitadas e contagem de alertas de integridade. A checagem explícita de integridade cobre as **40 políticas mais recentes** devolvidas pelo histórico; totais de status vêm do SQL. Não são métricas de segurança em tempo real do motor de trading.
- `ProductionRiskPolicyStore.ReviewAsync` usa `UPDLOCK, HOLDLOCK` dentro da transação para serializar revisões concorrentes da mesma proposta e evitar deadlock por conversão de lock; a auditoria e revisão permanecem transacionais.
- A API trata erros detectados de integridade com conflito HTTP 409, e indisponibilidade SQL reconhecida com HTTP 503; não divulga detalhes internos.
- O dashboard `Políticas de risco` apresenta cartões de status, e recusa dados incompatíveis com o modo de pesquisa e auditoria.
- A autenticação vigente mantém os papéis `RacRiskReviewer` (Administrator/RiskReviewer) para consulta/proposta e `RacWalletOwner` (somente Administrator) para revisão. O mecanismo HTTP existente exige cabeçalho `X-RAC-CSRF: 1` nos POSTs.

## Verificação

- Suíte Release do projeto: **250/250 testes aprovados**, zero falhas.
- Teste novo com LocalDB descartável comprova indicador pendente/rejeitado, detecção de alteração do JSON e não liberação de operações.
- Teste concorrente reproduziu deadlock com o mecanismo anterior e passou após bloqueio de atualização: duas tentativas de revisão simultâneas geram exatamente **uma** revisão persistida e dois eventos de auditoria (proposta + revisão).
- Teste de configuração das políticas de autorização confirma papéis exigidos e recusa de usuários anônimos no contrato de autorização.
- Build da solução Release: zero avisos/erros. `node --check` no JavaScript: aprovado.
- Verificação operacional: PAPER `Running=1`, `RiskLocked=0`; Testnet `Enabled=0`, nenhuma ordem Testnet; zero propostas e zero auditorias no banco operacional; API existente saudável e com `realOrdersEnabled=false`.

## Limites e próximos controles

- **Não houve deploy nem reinício da API/Web.** O dashboard em execução continua usando o código anterior; a alteração exige implantação controlada futura.
- Não foi realizada navegação autenticada ponta a ponta nem teste de sessão expirada na API em execução. A checagem de políticas é de configuração, não substitui testes HTTP autenticados completos.
- A integridade por SHA-256 não é assinatura digital contra administradores do banco com permissão de edição: exige trilha de auditoria externa/append-only para garantia independente.
- Indisponibilidade transitória específica de banco e limites de concorrência mais amplos exigem testes de carga adicionais; não configurar retry cego de transações que possam duplicar revisão.
- O mecanismo continua **documental**, nenhuma estratégia foi aprovada para execução e `ProductionRiskGate.RealTradingImplementedAndAuthorized` permanece falso.
- Não foram enviadas ordens à Binance, não houve alteração de PAPER, Testnet, credenciais ou SQL operacional.
- Arquivos locais de outra tarefa (`docs/` e alterações em `styles.css`) foram preservados e **não** incluídos neste MVP.
