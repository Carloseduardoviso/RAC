# RAC MVP 1.3.5 — Arquivo mensal público de pesquisa isolada

Data: 22/11/2026.

## Escopo entregue

- Novo projeto de linha de comando `src/RAC.HistoryResearch` incluído em `RAC.slnx`. Execução **manual**, por exemplo `dotnet run --project src/RAC.HistoryResearch -c Release -- 2026-10`.
- Requisita, via `GET` sem credenciais, arquivos mensais `data.binance.vision/data/spot/monthly/klines/{symbol}/1m/{symbol}-1m-YYYY-MM.zip` dos 12 pares suportados, com **até um download por par**, somente para meses UTC completamente encerrados (agosto/2017 em diante).
- Limites por arquivo: até 20 MiB compactados e 28 MiB de CSV expandido; arquivo ZIP deve ter entrada única com nome previsto, 12 colunas por linha, preços/volumes válidos, close timestamp exato, sequência completa de 1 minuto, nenhum candle faltando ou duplicado, e o número exato de dias do mês.
- Aceita timestamps públicos Binance em **microssegundos** usados desde 2025, com conversão explícita; histórico antigo em milissegundos.
- Apenas **após validação integral** de cada símbolo, grava arquivo `.zip` via `.partial` e move atômico. Reexecuções usam e revalidam arquivo existente em vez de repetir download, mas falham em caso de arquivo corrompido. Grava manifesto JSON com SHA-256, cobertura de cada símbolo e métricas descritivas diárias.
- Diretório isolado em `%LOCALAPPDATA%\RAC\research\monthly\YYYY-MM` — **não escreve** SQL, não acessa a carteira, ordens, chaves, configuração do executor ou diretórios de deploy.
- Resumo descritivo de dias UP (>+1% da abertura ao fechamento), DOWN (<-1%) e SIDEWAYS (demais); **não é classificação preditiva**, não demonstra trades lucrativos e não escolhe parâmetros de negociação.

## Pesquisa histórica

Esta entrega cria o acervo íntegro para validação de estratégias ao longo de diferentes dias, **mas não roda backtests de 30 a 90 dias nesta versão**. A fase seguinte deve desenvolver motor de avaliação sobre esses snapshots com testes cronológicos independentes, custos realistas e base comparativa de não operar. Não haverá promoção automática por critérios estatísticos.

## Verificação

- Testes em `MonthlyArchiveResearchTests`: valida mês completo, 1440 candles/dia, microtimestamp, rejeição de lacunas, arquivo de outro mês e símbolo não suportado.
- Build da solução sem avisos e suíte de testes `dotnet test RAC.slnx -c Release`.
- Coleta pública real e checksum dos 12 pares para outubro de 2026: `MVP-1.3.5-VERIFICATION.md`.
- PAPER e Testnet permanecem bloqueados; serviços API e Web MVP1.3.4 mantidos ativos sem reinício.
