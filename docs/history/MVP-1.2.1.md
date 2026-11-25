# RAC MVP 1.2.1 — Homologação exclusivamente simulada

Data de implantação: 22/11/2026.

## Objetivo e isolamento

O objetivo é validar o pipeline de ordens da **Binance Spot Testnet** por meio de respostas **inteiramente simuladas em memória**. Nenhum teste abre conexão com a Binance, envia ordens, consulta a carteira real ou altera o SQL Server RAC. Os cenários BUY, SELL, DELETE e consulta por clientOrderId são simulados por `HttpMessageHandler` controlado nos testes. A consulta externa de diagnóstico com `--verify` da versão anterior permanece opcional e apenas GET.

## Segurança

- `TestnetExecutionPolicy.SubmissionExplicitlyAllowed` retorna `false` nesta versão. É uma trava de **código-fonte**, não um parâmetro de configuração que possa ser ativado no painel ou por variável de ambiente.
- O `POST /api/testnet/execution/start` rejeita a ativação para ordens Testnet nesta versão; o botão de envio permanece desabilitado.
- Caso uma instância antiga tenha `Enabled=true`, a versão 1.2.1 desarma a execução no próximo ciclo sem tocar na simulação PAPER.
- `EmergencyStopAsync` mantém o bloqueio de novas ordens; não envia DELETE remoto nesta versão, mas registra que cancelamento remoto exigiria revisão.
- No painel a mensagem **MVP 1.2.1: somente testes simulados** explica o estado e os contadores SQL; nenhum valor de credencial é exibido.
- **Ordens reais Binance permanecem desativadas em todo o projeto.** A Spot Testnet utiliza apenas domínio fixo `https://testnet.binance.vision/`.

## Cobertura automatizada

O arquivo `tests/RAC.Tests/TestnetMvp121Tests.cs` verifica:

- Os 12 pares suportados e IDs determinísticos por fill PAPER.
- Regras de perda diária 1%, drawdown 10%, risco 0,5% por operação, ciclos stale, risco bloqueado e conta PAPER pausada.
- Compra BUY e venda SELL simuladas com assinatura HMAC, método HTTP, URI fixa da Testnet e clientOrderId.
- Cancelamento DELETE simulado por clientOrderId (nenhuma chamada remota).
- Consulta GET após resposta ambígua de um POST, sem reenviar a operação.
- Resposta `-2013` indicando ordem não encontrada, sem permitir POST substituto.
- Falhas de transporte simuladas e rejeição de confirmações inconsistentes.
- Estados NEW, PARTIALLY_FILLED, FILLED, CANCELED, UNKNOWN e validação de quantidades sem regressão.
- Venda limitada ao saldo líquido de compras **confirmadas** no ledger Testnet; compras UNKNOWN não dão saldo.
- Índices SQL únicos para `PaperFillId` e `ClientOrderId`, com `RowVersion` de concorrência.

As classes `TestnetExecutionPolicy` e `TestnetOrderValidation` são separadas, determinísticas e sem rede. A geração/consulta de ordens é mockada; isso **não** comprova a homologação de ordens no ambiente externo.

## Validar sem desligar a simulação PAPER

Compilar/testar em **Debug** para não substituir os binários Release da API enquanto ela executa:

```powershell
cd "C:\Users\Carlos Eduardo\source\repos\RAC"
dotnet build RAC.slnx -c Debug
dotnet test RAC.slnx -c Debug --no-build
node --check src\RAC.Web\wwwroot\app.js
```

A verificação SQL é apenas leitura:

```sql
SELECT Running, RiskLocked, LastCycleUtc FROM rac.PaperAccounts WHERE Id=1;
SELECT Enabled, CircuitLocked, EmergencyStopped FROM rac.TestnetExecutionStates WHERE Id=1;
SELECT COUNT(*) AS Total FROM rac.TestnetOrders;
```

## Critérios para uma futura autorização separada

Uma versão subsequente deverá desbloquear explicitamente a trava de código após revisão de segurança, implementar simulações de reinício contra um banco descartável, provar reconciliação de ordens reais na Testnet com capital fictício e sem afetar PAPER, revisar a gestão de segredos e confirmar a revogação da chave Testnet anteriormente exposta. Até lá, o MVP 1.2.1 **não realiza envios nem cancelamentos de ordens externas**.

A implantação do código na `main` **não reinicia a API em Release**, preservando a sessão PAPER. A nova trava de código do backend só estará ativa após implantação/reinício programado; por isso a instância anterior deve continuar com o executor Testnet desligado no SQL até essa janela.
