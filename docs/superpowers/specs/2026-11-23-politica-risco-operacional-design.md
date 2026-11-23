# Desenho: política de risco operacional do RAC

## Intenção

Formalizar em documentação versionada as regras financeiras que governam o RAC antes de qualquer futura operação com capital real.

## Escopo

- Registrar os limites atualmente configurados no simulador.
- Deixar explícito que os valores são simulados e não autorizam ordens reais.
- Definir travas, critérios de parada e revisão humana.
- Servir como referência para uma futura implementação de execução real.

## Fora do escopo

- Habilitar ordens reais ou Testnet.
- Alterar a estratégia, o motor de simulação ou os valores de `appsettings.json`.
- Criar chaves da Binance, contas ou infraestrutura de produção.

## Regras-base propostas

- Capital inicial simulado: R$ 100.
- Aporte mensal simulado: R$ 200 a cada 30 dias simulados.
- Risco máximo planejado por operação: 0,5%.
- Perda diária máxima: 1%.
- Drawdown acumulado máximo: 10%.
- Alocação máxima por posição: 20% do patrimônio.
- Exposição total máxima: 30% do patrimônio.
- Máximo de posições abertas: 1.
- Retiradas: não permitidas no simulador; reinvestimento: 100%.

## Comportamento de segurança

Ao atingir perda diária ou drawdown acumulado, o RAC deve pausar, encerrar posições sintéticas, persistir o bloqueio e exigir revisão humana autorizada. Nenhum aporte ou reinício automático deve liberar o bloqueio.

## Critério de aprovação

O documento operacional definitivo será criado somente após aprovação deste desenho. Ele deverá manter a distinção entre simulação, Testnet e produção e incluir uma lista de decisões ainda necessárias antes de qualquer uso de dinheiro real.
