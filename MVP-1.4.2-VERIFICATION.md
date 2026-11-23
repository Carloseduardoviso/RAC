# RAC MVP 1.4.2 — Verificação da auditoria de liquidez e custo

Data: 23/11/2026. Fonte: ZIPs públicos Binance Spot mensais de outubro de 2026, revalidados por manifesto SHA-256. Comando manual de pesquisa `--liquidity-audit 2026-10`.

## Resultado observado

- **12 símbolos × 744 amostras horárias = 8.928 observações**.
- **4 amostras sem qualquer volume preenchível** no cenário de participação e **6.551 amostras com preenchimento hipotético parcial**. O tamanho solicitado é de 100 USDT por observação, com limite conservador de 0,1% do volume do candle (modelo, não profundidade observada do book).
- **Custo médio hipotético round-trip de 0,3216%** no cenário base e **0,6403%** no cenário de custos duplicados; médias não ponderadas dos 12 resultados por símbolo, observações com zero fill computadas como custo 0.
- Modo `HOURLY_KLINE_CAPACITY_STRESS_SHADOW_ONLY`, `count=12`, `historicalOrderBookAvailable=false`, `actualFillProbabilityKnown=false`, `modelOnly=true`, `paperModified=false`, `orderPlacementEnabled=false`, `promotionAllowed=false`.
- Dados completos em `%LOCALAPPDATA%\RAC\research\monthly\2026-10\liquidity-capacity-audit.json`.

## Testes

- **166/166 testes .NET em Release aprovados**, incluindo cenários de zero volume, preenchimento parcial, conservação de valores solicitado/preenchido/não preenchido, maiores custos sob estresse, maior penalidade em candle volátil, parâmetros inválidos e flags de segurança.
- Script JavaScript do painel verificado sem alterações de comportamento; publicar apenas a ferramenta separada de pesquisa.

## Interpretação

O teste não mede rentabilidade de trades reais nem comprova oportunidades. Percentuais estimados são penalidades do **modelo de simulação**. Candles OHLCV não permitem inferir profundidade instantânea, execução parcial real ou taxas personalizadas. **Nenhuma estratégia foi aprovada e nenhuma ordem é autorizada.**
