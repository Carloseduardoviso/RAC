# RAC MVP 1.3.2 — Filtros de entrada, pesquisa SHADOW walk-forward

Data: 22/11/2026.

## Escopo

- `PaperEntryFilterResearch` é um módulo exclusivamente analítico: compara a regra observacional atual com três filtros fixos de entrada **sem alterar a operação**.
- As quatro alternativas são: `BASELINE` (sem filtro adicional), `TREND_STRENGTH` (SMA9 - SMA21 >= 0,05% do preço), `ATR_COST` (ATR14/preço >= metade do custo fictício de ida e volta), `TREND_AND_ATR` (ambos).
- Reutiliza o snapshot público de no máximo 1.000 candles 1m **já coletado pelo botão walk-forward**, sem aumentar consultas Binance e sem gravar candles no SQL.
- Divisão de tempo por agente: 180 candles de treino para seleção de filtro, seguidos de 120 candles posteriores de teste; os testes não se sobrepõem. Janelas de treino consecutivas podem se sobrepor. A seleção usa **apenas o treino anterior**.
- Não se escolhe filtro com menos de oito observações de compra no treino. Empates mantêm a alternativa anterior, favorecendo a BASELINE. Nenhum filtro candidato é promovido automaticamente.
- O resultado por janela inclui sinais de compra de referência/selecionados, taxa líquida estimada por **candle imediatamente seguinte** e contagem de movimentos líquidos positivos. A análise não soma retornos por candle para fabricar uma carteira nem representa número de trades realmente executados.
- Relatório `entryFilterResearch` é acrescentado ao objeto JSON já autenticado `GET /api/strategy/walk-forward`, sem novo endpoint de ordens e sem alteração da allowlist de escrita. O painel mostra resumo para os 12 agentes.

## Limitações

- A resposta medida é a variação da abertura ao fechamento do candle **posterior a cada sinal de compra**, com taxa hipotética por lado de 0,1%, slippage por lado de 0,05% e meio spread de 0,01%. Sinais consecutivos podem se sobrepor, não são preenchimentos reais, e o resultado não é PnL da carteira PAPER.
- O próprio sinal base já impõe SMA9 > SMA21 e RSI; o filtro de força de tendência é **adicional** e não substitui o avaliador de estratégias.
- O critério de escolha é média líquida por observação na janela de treino. Pode escolher um filtro que realize poucas observações; o número real de operações, capital ocioso, risco e retorno total da carteira **não** são estimados.
- Folds sem sinais de compra no teste retornam média `null`, e não lucro zero. Uma amostra com no máximo ~17 horas de 1m não comprova resultado estatisticamente robusto.
- Custo assumido não é cotação de spread efetivo nem inclui profundidade, lote mínimo, latência e variações reais da Binance.

## Segurança e release

- A API retorna `mode=ENTRY_FILTER_WALK_FORWARD_SHADOW_ONLY`, `paperModified=false`, `historyPersisted=false`, `orderPlacementEnabled=false`, `promotionAllowed=false`, `riskLimitsModified=false`.
- Não há chamadas autenticadas privadas à Binance, configurações de executor, ordens nem modificações da conta PAPER. As travas financeiras e Testnet mantêm-se inalteradas.
- Testes automatizados verificam determinismo, ausência de trades fictícios em janelas vazias, mínimos de amostra, dados antigos e falha conservadora. Código JavaScript é validado com `node --check`.
- Preparar release em `%LOCALAPPDATA%\RAC\staging\mvp-1.3.2`, sem substituir os serviços; ativação da nova API e Web fica para procedimento separado com backup SQL e rollback.
