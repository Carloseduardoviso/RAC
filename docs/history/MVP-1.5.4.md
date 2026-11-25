# RAC MVP 1.5.4 — Replay de decisões congeladas

Data: 23/11/2026.

## Implementação

- A escolha por voto é congelada exclusivamente a partir dos relatórios de treinamento maio–julho de 2026, antes de ler as janelas posteriores.
- Comando manual `dotnet run --project src/RAC.HistoryResearch -c Release -- --fixed-holdout-replay` valida os manifestos e SHA-256 dos ZIPs de agosto–outubro e executa `RacFixedHoldoutReplay` por símbolo/mês.
- Para decisão ativa fixa, o motor valida candles mensais, avalia apenas as 720 últimas velas de cada dia UTC com gate/horizonte imutáveis, custos ilustrativos 1x, stress de liquidez e auditoria de capacidade de saída. A ausência de evidências de saída retém o retorno (null) e bloqueia aprovação.
- Para NO_TRADE, a validação do arquivo histórico continua obrigatória, mas o resultado é uma linha de base de abstenção, sem execução de estratégia ativa, zero operações e zero retorno fictício.
- Relatório segregado `%LOCALAPPDATA%\RAC\research\monthly\fixed-replay-2026-08-to-2026-10.json` (não pertence ao banco operacional).
- Nenhuma operação de corretora, chamada autenticada à Binance, alteração de limites de risco ou modificação do PAPER.

## Validação neste computador

- Compilação Release do motor de pesquisa: zero erros e avisos.
- Testes .NET Release: 211/211 aprovados, zero falhas.
- Execução dos 36 casos (12 agentes x 3 meses): 36 NO_TRADE, 0 ordens hipotéticas, 0 habilitados para revisão; processo terminou com exit code 0.

## Limitações

- Nenhum gate ativo venceu o congelamento pré-agosto no conjunto atual; por isso o replay ativo não possui desempenho empiricamente validado nesta execução.
- Mesmo quando houver gate ativo, os fills, taxas e capacidade de saída do motor são aproximações SHADOW, não comprovam resultado futuro ou autorização de negociação.
- O SHA-256 contra os manifestos locais não prova assinatura criptográfica independente da Binance.
- Não foram reiniciados API/Web nem feitos deploys operacionais.
