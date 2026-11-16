# RAC 0.4 — SQL Server / Entity Framework Core

## Organização

- `RAC.Api`: endpoints HTTP e simulador atual (em memória).
- `RAC.Web`: painel web.
- `RAC.Domain`: entidades para persistência.
- `RAC.Application`: projeto preparado para serviços e agentes.
- `RAC.Infrastructure`: `RacDbContext`, Fluent API e migrations.
- `RAC.Tests`: testes do mapeamento do EF.

## Banco de desenvolvimento

- Instância: `(localdb)\MSSQLLocalDB`
- Banco: `RAC`
- Schema: `rac`
- Autenticação: integrada do Windows.
- Certificado confiável dispensado **somente para desenvolvimento local** (`TrustServerCertificate=True`).
- Em produção, configurar TLS com certificado válido e a connection string via variáveis protegidas, nunca no Git.

A primeira migration `InitialCreate` foi aplicada na instância local. Ela criou as tabelas:
`Agents`, `Strategies`, `Orders`, `Executions`, `Positions`,
`Contributions`, `EquitySnapshots`, `RiskLocks` e `AuditEvents`.

## Atualizar banco (sob controle do desenvolvedor)

```powershell
dotnet ef database update --project src/RAC.Infrastructure --startup-project src/RAC.Infrastructure --context RacDbContext
```

Para alterar a conexão do design-time, defina `RAC_SQLSERVER_CONNECTION` no ambiente.
O projeto API usa `ConnectionStrings:RAC` em `appsettings.Development.json` e
pode ser substituído por configuração segura do ambiente.

## Testes

```powershell
dotnet build RAC.slnx
dotnet test RAC.slnx
```

## Limitações desta etapa

O simulador ainda guarda saldos e bloqueios de risco na memória; as novas tabelas
não são usadas automaticamente pelo simulador. **Não reinicie a aplicação confiando
que o estado financeiro simulado já será recuperado do banco.**

Faltam: serviços de persistência, reconciliação de dados, gravação transacional de
ordens/execuções, autenticação, testes de falhas e backtesting.

A Binance continua apenas em consulta pública da Testnet.
**Não há ordens reais nem ordens autenticadas na Testnet** nesta versão.
