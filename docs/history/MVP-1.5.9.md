# RAC MVP 1.5.9 — preparação da política de risco para produção

Data: 23/11/2026.

## Escopo implementado nesta etapa

- Criado `src/RAC.Application/ProductionRiskGate.cs` como componente **isolado** do PAPER e do executor Testnet.
- `ProductionRiskLimits.Unapproved` começa com capital zero, sem pares autorizados nem limites aprovados: falha de forma segura.
- Valida política com capital máximo, valor máximo por ordem, perda diária, drawdown, posições simultâneas, teto de ordens por hora e dia, lista permitida de símbolos Spot.
- `ProductionRiskSnapshot` requer conta reconciliada, rede saudável, atualização de dados de conta e mercado com até 30 segundos, aprovação humana e ausência de trava de perda/emergência.
- `EvaluateReadiness` é **apenas diagnóstico**. `Validate` tem bloqueio fixo no código (`RealTradingImplementedAndAuthorized => false`); nenhuma entrada, credencial ou alteração de configuração o habilita.
- As funções não recebem chaves, não acessam Binance e não enviam ordens; não modificam o SQL e não afetam PAPER.
- Testes em `ProductionRiskGateTests.cs`: bloqueio estrutural, política ausente, símbolos e lados, valor da ordem, rede, reconciliação, dados antigos, emergência, revisão humana, capital, exposição, posições, frequência, perda e drawdown.

## Limitações explícitas e próximo desenvolvimento

Esta etapa NÃO significa que as regras estão aplicadas à Binance real. Ainda não há executor real, estado de risco de produção persistido, migração SQL própria, trilha de auditoria para aprovações, protocolo de assinatura de aprovações, endpoints de revisão nem kill switch real. Uma aprovação futura deve persistir em SQL com auditoria e autorização por papel, além de exigir nova aprovação após qualquer perda, emergência ou reinício. Um motor de produção futuro terá de obter estado reconciliado, impor checagem atomicamente imediatamente antes de cada ordem e não ignorar recusas.

Não foi definido capital real sem decisão humana. A escolha de capital, pares e demais valores deve ser formalmente aprovada antes de qualquer implementação operacional.

Não alterar `SubmissionExplicitlyAllowed => false` da Testnet nem `RealOrdersEnabled: false`.

## Validação

Teste automatizado: `dotnet test tests/RAC.Tests/RAC.Tests.csproj -c Debug --no-restore --verbosity quiet`.
A versão em execução nas portas locais não é modificada automaticamente por esta alteração.
