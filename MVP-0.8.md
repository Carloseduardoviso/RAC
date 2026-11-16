# RAC MVP 0.8 — Gestão do patrimônio real Binance Spot

## Objetivo e limites

Esta versão separa claramente:
- **Patrimônio simulado (R$ 100 de referência):** 12 traders com preços, sinais, ordens e posições *sintéticos*; nenhuma negociação real
- **Carteira Binance Spot real:** leitura autenticada com chave HMAC **exclusivamente de leitura**, já cadastrada pelo usuário no Windows
- **Patrimônio estimado em BRL:** cálculo feito na consulta usando os saldos reais Spot e preços públicos de mercado da Binance
- **Histórico do SQL Server:** instantâneos reais criados somente quando o administrador clicar em **Consultar Binance e registrar patrimônio**
- **Aportes/reinvestimento:** apenas planejamento e declarações manuais, sem automatizar dinheiro

A API da Binance utiliza `GET /api/v3/account` (dados privados de leitura, com assinatura HMAC) e, para os preços públicos, `GET /api/v3/ticker/price` no domínio `data-api.binance.vision`. **Não existem endpoints no RAC para enviar ordens, converter moedas, depositar, sacar ou transferir.**

## Estimativa da carteira

Para cada saldo Spot `free + locked`:
- **BRL:** preço direto de R$ 1 por BRL
- **Ativo com par XXXBRL:** utiliza cotação pública atual desse par
- **USDT:** utiliza cotação pública `USDTBRL`
- **Demais ativos XXXUSDT:** utiliza `XXXUSDT × USDTBRL`

Se um ativo não tiver cotação válida, seu valor fica **sem preço**. Nesse caso, o painel apresenta apenas a **soma parcial avaliada**, sem inventar um patrimônio total. O valor em BRL é indicativo e não contempla taxas, spreads, slippage, impostos, carteiras Funding/Earn/Futuros ou posições fora de Spot. A cotação USDTBRL não é o câmbio oficial USD/BRL.

**O aumento do valor entre duas consultas NÃO equivale a lucro.** Depósitos, retiradas, movimentações, diferenças de preços e taxas precisam ser conciliados antes de calcular lucros realizados e uma base real de reinvestimento.

## Banco de dados e migration

Migration: `RealSpotPortfolioHistory`, já aplicada em `(localdb)\MSSQLLocalDB`, banco `RAC`.

Tabelas novas:
- `rac.SpotPortfolioSnapshots` — dados agregados por consulta, cotação, completude e data
- `rac.SpotPortfolioHoldings` — quantidades por ativo Spot e origem do valor BRL
- `rac.SpotCashFlowNotes` — anotações de aportes/retiradas informados manualmente, **não verificados** pela Binance
- `rac.SpotInvestmentPlans` — aporte mensal planejado (R$ 200 inicialmente) e percentual de reinvestimento pretendido (100% inicialmente)

Somente **Administrator** autenticado acessa esses endpoints e tabelas pelo painel. As consultas de histórico são filtradas pelo usuário responsável. Nenhum segredo Binance é armazenado nessas tabelas. **Atenção:** os saldos reais históricos são informações financeiras sensíveis, armazenadas em texto claro na instância SQL Server LocalDB; mantenha o computador protegido e não publique o banco.

## Rotas (todas exigem Administrator)

| Método | Rota | Efeito |
|---|---|---|
| GET | `/api/portfolio/history` | Consulta o SQL Server, **sem contatar a Binance** |
| POST | `/api/portfolio/snapshot` | Consulta a Binance uma vez e grava o instantâneo real; mínimo de 45 segundos entre registros |
| POST | `/api/portfolio/plan` | Salva o plano informativo, sem movimentar fundos |
| POST | `/api/portfolio/cashflows` | Registra um aporte/retirada *declarado manualmente*, sem movimentar fundos |

POST exige `Content-Type: application/json` e proteção de requisição `X-RAC-CSRF: 1`. Não foram criadas chaves de negociação; o fluxo depende da chave de leitura existente.

## Como usar no Visual Studio

1. Recompile a solução `RAC.slnx`, inicie `RAC.Api` e `RAC.Web` com F5
2. Entre com `admin` em `http://localhost:5081`
3. Use **Minha carteira Binance Spot — saldo real** para verificar o saldo *sem gravá-lo* no histórico
4. Use **Patrimônio real Binance Spot — gestão e histórico** para capturar e gravar um instantâneo
5. Confira o valor estimado, a cobertura de preços, o histórico, o plano de R$ 200/mês e reinvestimento planejado de 100%
6. Se quiser, registre aportes/retiradas que efetivamente ocorreram; as anotações são suas declarações, não comprovantes da Binance

O histórico começa vazio até a **primeira captura manual**. A API não consulta a carteira no background nem altera seu patrimônio.

## Testes

```powershell
dotnet build RAC.slnx
dotnet test RAC.slnx
```

Foram incluídos testes de cálculo para BRL, USDT, BTC, ativos sem preço, saldos bloqueados, arredondamento e modelo EF Core. Foi executado também um teste isolado com carteira Binance e HTTP inteiramente *simulados*, um banco temporário LocalDB e nenhuma credencial real, validando snapshot, histórico, aporte manual, plano e **zero ordens**.

## Próximos passos antes de negociar

1. Conciliação de depósitos, ordens executadas, custo médio, taxas e P&L líquido
2. Treinamento e testes de estratégias com preços reais e backtesting fora da amostra
3. Política de risco segregada para o capital real; limites por dia/posição e travamento persistente
4. Binance Spot Testnet com ordens autenticadas por **outra chave de teste**, nunca pela chave Spot de leitura
5. Auditoria de segurança e autorização **explícita** antes de qualquer operação financeira real

**Nunca habilite permissões de negociação na chave de leitura atual.**
