# RAC MVP 1.3.9 — Verificação da consolidação SHADOW

Data: 22/11/2026. Executado na máquina local com o comando `--entry-gates-summary 2026-08 2026-09 2026-10`, usando **apenas três relatórios de pesquisa** isolados, um por mês. Os três SHA-256 de entrada são incorporados ao manifesto JSON produzido.

## Resultado aferido

```text
CROSS_MONTH_GATE days=1104 noTrade=1072 active=32 trades=364 positive=2 rejected=12 review=0 orders=False
```

- **12 agentes, três meses completos, 92 dias por agente**, perfazendo **1.104 janelas de teste**.
- **1.072** janelas em que o treino escolheu **não operar** (97,10%); 32 janelas com operações simuladas.
- **364 operações simuladas encerradas** nos testes escolhidos; **2 janelas positivas** e **12 agentes rejeitados** para revisão de pesquisa.
- Zero ordens e zero promoção: `Mode=THREE_MONTH_ENTRY_GATES_SHADOW_ONLY`, `PaperModified=false`, `RiskLimitsModified=false`, `OrderPlacementEnabled=false`, `PromotionAllowed=false`.
- Quando não há operação, a janela tem retorno e drawdown 0%; esse resultado **não conta como vantagem lucrativa**, apenas como ausência de exposição hipotética.

## Evidência e limites

- Relatório detalhado em `%LOCALAPPDATA%\RAC\research\comparisons\entry-quality-three-month-2026-08-to-2026-10.json`.
- Originais auditáveis em `%LOCALAPPDATA%\RAC\research\monthly\YYYY-MM\entry-quality-no-trade.json`.
- Novos testes verificam rejeição de mês ausente, desordem cronológica, flags inseguras e impossibilidade de ativar ordens mesmo com dados sintéticos muito favoráveis.
- **157/157 testes automatizados aprovados**. Estudo offline; nenhuma execução de PAPER/SQL ou envio de ordens Binance.
- Esta análise depende de custos estimados, candidatos de entrada predefinidos e capital fictício reiniciado diariamente; não é garantia de lucro nem substituto de homologação e gestão de risco.
