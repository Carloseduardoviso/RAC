# Etapa 07 - homologacao de pesquisa dinamica

Baseline confirmado: 9dccad9; 584 testes aprovados, zero falhas e zero ignorados. A revisao adicional do Schema V2 foi aprovada. Encerramento tecnico da pesquisa simulada; homologacao operacional permanece pendente.

## Evidencias
- Daniel: descoberta Spot USDT, cobertura, ranking de ate 20 candidatos e snapshots auditaveis.
- Gabriel: matriz dinamica de seis meses (maio a outubro de 2026), 24 configuracoes por ativo, 12 de estresse; arquivos publicos com SHA256 e validacao de cobertura.
- Rebeca: vinculo Daniel/Gabriel, auditoria individual e parecer sem autorizacao de negociacao.
- Ana: auditoria SQL e consolidacao com Carlos e Lucas em banco isolado.
- Renovacao de lease, cancelamento cooperativo, timeout global de 30 minutos e recuperacao limitada de HTTP.

## Desempenho sintetico
| Ativos | Candles | Tempo (s) |
|---|---:|---:|
| 1 | 264960 | 1,65 |
| 2 | 529920 | 3,38 |
| 5 | 1324800 | 8,78 |
| 10 | 2649600 | 16,81 |
| 20 | 5299200 | 32,90 |

Teste isolado de 20 ativos: 480 configuracoes, 2880 resultados mensais, CPU 32,64 s, pico do processo 108601344 bytes, alocacoes acumuladas 8855813816 bytes. Alocacao acumulada nao equivale a RAM simultanea. Dados sinteticos nao demonstram rentabilidade.

## Disponibilidade e limites
- API /api/health HTTP 200 em 2158 ms; Web / HTTP 200 em 2081 ms, verificacoes pontuais.
- Probe publico BTCUSDT aprovado; downloads reais de 20 ativos e carga sustentada concorrente nao homologados.
- Fonte dinamica opt-in desabilitada por padrao. Nao altera universo negociado, nao envia ordens e nao desbloqueia risco.
- Validacao independente, homologacao PAPER e Testnet permanecem separadas.
- Aviso xUnit2031 preexistente nao bloqueante.

## Etapa 08 - aprendizado continuo
Proximos incrementos: catalogo de experimentos e versoes, rastreabilidade de dados, separacao temporal de treino e validacao independente, criterios de promocao e rejeicao, registro de decisoes humanas e monitoramento de degradacao. Sem promocao automatica para negociacao, sem alteracao de capital ou limites de risco.
