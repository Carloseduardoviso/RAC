# RAC — Persistência e segurança no SQL Server (MVP 0.6)

## Ambiente local

- Instância: `(localdb)\MSSQLLocalDB`
- Banco: `RAC`
- Schema: `rac`
- Autenticação integrada do Windows; **sem usuário sa ou senha no projeto**.
- Configuração da API: `src/RAC.Api/appsettings.Development.json`.
- Configuração das ferramentas EF Core: `src/RAC.Infrastructure/RacDbContextFactory.cs`.
- Variável opcional de substituição: `RAC_SQLSERVER_CONNECTION` (tem precedência sobre appsettings).

`TrustServerCertificate=True` é usado apenas no LocalDB de desenvolvimento. Em um servidor real, instale e valide o certificado TLS, use uma identidade restrita e mantenha segredos fora do Git.

## Migrations aplicadas ao banco RAC

- `InitialCreate`: tabelas de agentes, estratégias, ordens, execuções, posições, aportes, patrimônio, bloqueios e auditoria.
- `DurableSimulatorState`: tabela `rac.SimulationCheckpoints` com versão de linha (rowversion) e identificador de posição simulada único.
- `RacUserSecurity`: tabela `rac.Users` com usuário, hash de senha, função, habilitação e bloqueio temporário.
- `BinancePublicMarketData`: tabelas `rac.MarketQuotes` (última cotação por par) e `rac.MarketCandles` (candles públicos de 1 minuto, somente fechados, índice único por par/intervalo/abertura).

A API **não aplica migrations automaticamente**. Atualize explicitamente em ambiente controlado:

```powershell
dotnet ef database update --project src/RAC.Infrastructure --startup-project src/RAC.Infrastructure --context RacDbContext
```

## Estado e histórico

O serviço `RacPersistentEngine` grava em uma única transação, a cada alteração:
- Checkpoint integral de mercados sintéticos, sinais, capital, aportes, posições, histórico de trades, limites e bloqueios.
- Série temporal em `rac.EquitySnapshots`.
- Entradas e saídas simuladas em `rac.Orders` e `rac.Executions`.
- Posições em `rac.Positions`, incluindo identificação da sessão sintética.
- Aportes virtuais em `rac.Contributions`, bloqueios em `rac.RiskLocks` e trilha em `rac.AuditEvents`.

Na reinicialização, a API recarrega o último checkpoint do banco e **sempre inicia pausada**. Se houver bloqueio de risco ativo, o painel recusa Iniciar e Reiniciar (HTTP 409). A revisão autorizada já possui fluxo autenticado: o servidor valida papel e justificativa e registra a liberação do bloqueio e o novo checkpoint na mesma transação.

Falha de persistência interrompe a simulação; não há execução real de ordens. O sistema valida migrations pendentes antes de iniciar.

## API e painel

- `GET /api/state`: estado simulado atual restaurado do checkpoint.
- `GET /api/history`: patrimônio, ordens, aportes e bloqueios consultados do SQL Server.
- `POST /api/control/{start|pause|step|reset}`: salva checkpoint e eventos transacionalmente.

A página RAC.Web mostra uma seção de histórico armazenado. Seu navegador consulta a API via proxy local restrito.

## Limitações e segurança

O motor continua sintético e **não é um algoritmo de trading pronto para dinheiro real**. A autenticação local e o fluxo de revisão de risco foram adicionados no MVP 0.6 (consulte `SEGURANCA.md`), mas **a API e o Web não devem ser publicados na Internet** sem HTTPS completo, MFA, recuperação de contas e avaliação de segurança. A API não oferece rotas de envio de ordens reais ou autenticadas na Testnet. Execute apenas uma instância ativa da API por banco de simulação.

## Mercado público Binance Spot (MVP 0.7)

- Fonte **somente leitura**: `https://data-api.binance.vision/`, endpoints `GET /api/v3/ticker/price` e `GET /api/v3/klines`. **Não exige API Key.**
- O `RacMarketDataWorker` atualiza as 12 cotações e obtém os candles fechados de 1 minuto com intervalo mínimo de **60 segundos**. Faz a carga inicial dos últimos ~90 candles por ativo, sem salvar o candle em formação.
- As cotações são mantidas em `rac.MarketQuotes`, uma linha por par. O histórico OHLCV é gravado em `rac.MarketCandles` e dados com mais de **14 dias** são excluídos pela coleta (retenção básica).
- Em HTTP **429** ou **418**, o trabalhador interrompe as requisições e aguarda antes de tentar novamente; falhas de rede ou banco deixam as cotações marcadas como desatualizadas, sem afetar o simulador.
- `Rac:MarketData:Enabled` e `Rac:MarketData:RefreshSeconds` podem ser ajustados em `src/RAC.Api/appsettings.Development.json`. O intervalo é limitado entre **60 e 600 segundos**.
- A autenticação existente protege os endpoints `GET /api/market/overview` e `GET /api/market/candles/{symbol}`, que consultam exclusivamente o SQL Server.
- **Nunca** confunda essas cotações reais em USDT com o estado financeiro **sintético**: o motor do trader, seus sinais, operações e patrimônio ainda não utilizam preço público e não têm integração de ordens.

Consultas para verificar dados no SQL Server:

```sql
SELECT Symbol, PriceUsdt, ObservedUtc
FROM rac.MarketQuotes ORDER BY Symbol;

SELECT TOP (20) Symbol, OpenUtc, [Open], High, Low, [Close], Volume
FROM rac.MarketCandles WHERE Symbol = 'BTCUSDT'
ORDER BY OpenUtc DESC;
```

## Validar

```powershell
dotnet build RAC.slnx
dotnet test RAC.slnx
```

Testes incluem serialização e recuperação do checkpoint, pausa após reinício e recusa de reset quando bloqueado.
