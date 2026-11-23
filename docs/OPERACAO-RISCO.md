# Política de risco operacional do RAC

## Objetivo e escopo

Esta política define os limites da simulação local do RAC, os critérios de parada e as responsabilidades pela revisão de risco. Seu alcance é a negociação simulada à vista (Spot), com capital fictício, sem margem, alavancagem ou retiradas. Qualquer operação em produção exige autorização humana explícita e separada.

## Modos de operação

| Modo | Condição operacional |
| --- | --- |
| Simulação local (PAPER) | Único modo autorizado a simular operações. O RAC registra ordens e posições fictícias localmente, inclusive quando usa cotações públicas. |
| Binance Spot Testnet | Ambiente de homologação com ativos de teste. O envio de ordens permanece desativado (`TestnetOrdersEnabled = false`). Consultas públicas e testes isolados podem apoiar a homologação. |
| Produção | Ambiente da corretora que movimenta capital real. O envio de ordens reais permanece desativado (`RealOrdersEnabled = false`). A operação em produção segue sem autorização. |

## Limites da simulação

| Regra | Valor |
| --- | --- |
| Capital inicial simulado | R$ 100 |
| Aporte mensal simulado | R$ 200 a cada 30 dias simulados |
| Risco máximo planejado por operação | 0,5% do patrimônio simulado |
| Perda diária máxima | 1% |
| Drawdown acumulado máximo | 10% |
| Alocação máxima por posição | 20% do patrimônio simulado |
| Exposição total máxima | 30% do patrimônio simulado |
| Máximo de posições abertas simultaneamente | 1 |
| Reinvestimento dos ganhos | 100% |
| Retiradas | Proibidas |

A perda acumulada é medida em relação ao pico do patrimônio ajustado pelos aportes. Cada aporte simulado eleva a referência do pico no mesmo valor. Os aportes e o resultado das operações são acompanhados separadamente, de modo que um aporte não oculte perdas.

## Parada e revisão humana

1. Ao atingir a perda diária de 1% ou o drawdown acumulado de 10%, o RAC bloqueia novas entradas, pausa a simulação, encerra as posições sintéticas abertas e persiste a trava de risco no SQL Server.
2. A trava permanece ativa após aportes e reinícios da API, sejam manuais ou automáticos. Reiniciar a API não autoriza retomar a simulação nem liberar a trava. Uma falha de persistência interrompe os comandos da simulação.
3. Um `Administrator` ou `RiskReviewer` autenticado analisa o motivo da parada, os registros das operações e as perdas. Se aprovar a liberação, registra uma justificativa de 20 a 500 caracteres na revisão humana.
4. A aprovação é auditada no evento `RISK_REVIEW_APPROVED` e libera a trava para uma nova simulação pausada, com preservação do histórico. Um `Operator` pode comandar a simulação, inclusive iniciar o novo cenário após a liberação. A liberação é reservada aos perfis autorizados a revisar o risco.

## Checklist antes de considerar produção

- [ ] Definir capital real inicial, limites de risco e critérios mensuráveis de aprovação. Verificar se os mínimos da corretora permitem respeitar o orçamento de risco.
- [ ] Validar a estratégia com backtest fora da amostra e simulação em tempo real. Revisar resultado líquido após taxas, spread e slippage, drawdown e qualidade dos dados.
- [ ] Autorizar a etapa de Testnet separadamente e, quando autorizada, validar ordens Spot, filtros de símbolo, cancelamento e reconciliação com credenciais exclusivas do ambiente de teste.
- [ ] Comprovar o comportamento sob falhas de rede, retorno incerto e reinício. Validar reenvio idempotente, reconciliação de posições e saldos, parada de emergência, persistência da trava e trilha de auditoria.
- [ ] Concluir a revisão de segurança e definir quem opera, quem revisa o risco e quem autoriza a retomada. Prever credenciais segregadas e protegidas, permissões restritas a Spot, sem margem, alavancagem ou saques, e restrição de acesso por IP.
- [ ] Registrar a aprovação humana explícita da mudança de modo e os responsáveis pelo monitoramento e pela interrupção da operação. Até essa aprovação, a produção permanece desativada.

## Referências no repositório

- [Configuração versionada de risco e dos modos](../src/RAC.Api/appsettings.json)
- [Permissões e revisão de risco](../SEGURANCA.md)
- [Simulação e etapas de validação](../README.md)
