# RAC MVP 1.2 — Executor de ordens Binance Spot Testnet

## Segurança e limites

O MVP 1.2 foi desenvolvido na branch `main` como integração exclusivamente de teste. A única origem de ordens no novo cliente é `https://testnet.binance.vision/`, validada de modo estrito antes de cada chamada. Não existem rotas de criação de ordens reais no RAC. A chave de leitura da Binance real continua em outro arquivo DPAPI e **nunca** é usada pelo executor Testnet.

Na primeira inicialização do serviço o executor fica **PAUSADO**. Precisa de uma chave criada especificamente no ambiente Spot Testnet. Não configure a chave de produção, não envie credenciais pelo chat. Uma chave Testnet pode habilitar operações com saldos fictícios somente naquele ambiente.

## Configurar as credenciais de teste no próprio Windows

No PowerShell interativo, na conta Windows que executa RAC:

```powershell
cd "C:\Users\Carlos Eduardo\source\repos\RAC"
dotnet run --project tools/RAC.TestnetSetup -- --status
dotnet run --project tools/RAC.TestnetSetup -- --configure
```

Em `--configure`, informe API key Testnet e secret **apenas no terminal local**, sem salvar em arquivos de texto. O secret não é ecoado. O arquivo criptografado com DPAPI CurrentUser fica em `%LOCALAPPDATA%\RAC\binance-spot-testnet-only.dpapi`. O executor só pode ler esse arquivo sob o mesmo usuário Windows. Não é o arquivo `binance-spot-readonly.dpapi` usado para consulta da conta real.

## Execução e auditoria

- A carteira PAPER continua separada: capital virtual inicial R$ 100, aporte fictício de R$ 200 por período de 30 dias, risco máximo de 0,5% por operação, perda diária 1%, drawdown 10%, reinvestimento 100%, uma posição simultânea.
- Os 12 agentes continuam usando candles reais públicos e o motor de sinais existente. **Somente fills PAPER novos após a ativação Testnet podem gerar ordens de teste**, sem replay de fills antigos.
- Um identificador de cliente determinístico `RAC12{PaperFillId}` vincula exatamente uma intenção Testnet a um fill PAPER. Índices únicos do SQL Server impedem duplicação do identificador e da origem.
- `rac.TestnetOrders` registra intenção, resposta, execução acumulada, quantidade em base, observação e estado. `rac.TestnetExecutionStates` registra ativação, circuit breaker e emergência.
- A validação usa `exchangeInfo` da Testnet (LOT_SIZE, MARKET_LOT_SIZE, MIN_NOTIONAL/NOTIONAL, MARKET, TRADING), arredonda quantidade **sempre para baixo** e não aceita notional menor que o mínimo. Também confere discrepância de preço público até 3%, dado com até 4 minutos, saldo disponível Testnet e posição Testnet confirmada antes de vender.
- O envio usa somente `POST /api/v3/order` da Spot Testnet, com assinatura HMAC, timestamp da Testnet e `recvWindow` de 5000 ms.
- Antes de enviar, registra intenção como `SUBMITTING`. O worker consulta ordens pendentes por `origClientOrderId` antes de nova tentativa. **Uma ordem ambígua não é reenviada**; falhas acionam circuit breaker persistido.
- A cada ciclo de aproximadamente 60 segundos é permitido no máximo **um envio novo**. O circuito permanece bloqueado após falha grave até revisão manual futura. Reiniciar a aplicação desativa o executor, mesmo que o modo PAPER tenha estado ativo anteriormente.
- A parada de emergência desativa o executor imediatamente e tenta cancelar apenas ordens **TESTNET** conhecidas que estejam abertas. Cancelamento pendente ou sem confirmação é exposto no SQL e painel. **Não equivale a zerar posições nem garante cancelamento de ordens já executadas**.

## Rotas privadas e painel

Todas as rotas abaixo exigem autenticação e permissão de operador; comandos POST usam a proteção CSRF existente:
- `GET /api/testnet/execution/status`
- `POST /api/testnet/execution/start`
- `POST /api/testnet/execution/pause`
- `POST /api/testnet/execution/emergency`

O dashboard `http://localhost:5081` possui uma seção **MVP 1.2 — Executor Binance Spot Testnet**. O botão de ativação só funciona com credenciais Testnet, carteira PAPER ativa e saudável, e sem lock/emergência.

## Instalação e validação

```powershell
dotnet build RAC.slnx -c Release
dotnet test RAC.slnx -c Release
dotnet build tools/RAC.TestnetSetup/RAC.TestnetSetup.csproj -c Release
dotnet ef migrations has-pending-model-changes --project src/RAC.Infrastructure --startup-project src/RAC.Infrastructure
dotnet ef database update --project src/RAC.Infrastructure --startup-project src/RAC.Infrastructure
```

A migração `TestnetExecutionMvp12` cria exclusivamente as duas tabelas novas, sem alterar saldos reais.

## Limitações e homologação ainda necessária

- Sem chave Testnet configurada, a implantação é somente de infraestrutura e **nenhuma ordem Testnet real é enviada**. A verificação ponta a ponta de confirmações, taxas, saldos e reinícios com fundos de teste deverá ocorrer depois de registrar uma chave exclusivamente de teste.
- O circuito de segurança precisa de um procedimento de revisão e desbloqueio auditável, não implementado neste MVP.
- A execução do piloto assume **uma única instância** de RAC.Api; não foram homologadas liderança distribuída ou ordens concorrentes em múltiplos servidores.
- Não há prova de rentabilidade nem equivalência entre preços/fills simulados e resultados de Testnet.
- Se a posição PAPER vender mas a posição Testnet não tiver compra anterior confirmada, a venda Testnet é bloqueada para evitar consumir outros ativos de teste.

**Ordens reais e saques continuam impossibilitados pelo executor Testnet.**


## Verificação autenticada exclusivamente de leitura

Sem iniciar o executor, execute:

```powershell
dotnet run --project tools/RAC.TestnetSetup -- --verify
```

O comando consulta apenas GET /api/v3/time, GET /api/v3/account e GET /api/v3/exchangeInfo no domínio fixo `testnet.binance.vision`. Bloqueia explicitamente todos os métodos não-GET e todos os outros hosts. Não imprime credenciais nem respostas assinadas. Retorna código diferente de zero quando há falhas de autenticação ou par indisponível. Não envia, cancela nem modifica ordens.

Verificação realizada em 22/11/2026: autenticação confirmada com credencial Testnet local, saldo fictício livre 10000 USDT e 1 BTC, 12/12 pares TRADING/MARKET, 0 ordens Testnet. Saldo/condições mudam com o tempo; execute novamente para estado atual.

Os filtros Testnet exigem no mínimo 5 USDT na maioria dos pares e 1 USDT para DOGEUSDT na data da verificação. Com PAPER inicial R$100 e limite máximo de posição 20%, diversas ordens podem ficar abaixo dos mínimos e ser bloqueadas; **não flexibilizar os limites de risco** para forçar ordens.
