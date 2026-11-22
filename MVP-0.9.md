# RAC MVP 0.9 — prévia de histórico Binance Spot somente leitura

## O que foi implementado na branch

Branch: `feature/rac-mvp-0-9-readonly-ledger`. A implantação nesta máquina depende de compilar/testar e aplicar a nova migration.

- Cliente de histórico assinado **somente GET**; antes de importar, executa a verificação de permissões da chave HMAC usando o leitor de carteira do MVP 0.8
- Negociações do par escolhido por meio de `GET /api/v3/myTrades`, até 100 últimas execuções de cada par por solicitação
- Depósitos dos últimos 30 dias por `GET /sapi/v1/capital/deposit/hisrec`, até 100
- Retiradas dos últimos 30 dias por `GET /sapi/v1/capital/withdraw/history`, até 100
- SQL Server `rac.SpotActivities`: identificadores externos únicos por proprietário, tipo e par; conserva o ativo de comissão sem converter arbitrariamente o valor em BRL
- Consulta do ledger em até 200 eventos recentes, tipos de eventos e taxas **agrupadas por moeda**
- Botão de importação **manual** na interface, protegido por sessão de administrador; sem consultas privadas automáticas
- Taxas, preços e quantidades preservados com `decimal(28,12)`
- Limite de pelo menos 30 segundos entre importações com eventos salvos
- Nenhuma permissão de trading, saque, transferência ou alavancagem é habilitada

## Limites importantes: NÃO é ainda uma conciliação contábil concluída

O cálculo de **lucro líquido realizado em BRL está intencionalmente indisponível** (null).
Não seria seguro exibir um resultado a partir de uma amostra dos últimos 100 negócios de um único par,
pois faltariam custo médio, posições anteriores, todas as moedas, taxas convertidas, ordens antigas,
eventos de Funding/Earn e a conciliação de transferências internas. O painel exibe explicitamente
`completeHistory=false` e `profitReconciled=false`.

As movimentações consultadas têm limite de janela e paginação. Alguns endpoints podem retornar
erro por região, permissão ou restrição Binance; nesse caso a importação marca o histórico como
**parcial** e mostra um aviso genérico, sem exibir respostas privadas da Binance.

Os eventos de depósitos e retiradas armazenados podem incluir status não concluídos.
Não some quantidades desses eventos como capital investido: são **eventos observados**,
não depósitos confirmados sem análise de status e de movimentações internas.
`SpotCashFlowNotes` do MVP 0.8 continua sendo um livro de anotações **manuais e não verificadas**.

## Como testar após obter a branch no computador

1. Faça checkout da branch de forma segura, mantendo alterações locais protegidas.
2. Pare as instâncias atuais de `RAC.Api` e `RAC.Web`.
3. Execute, na pasta raiz do RAC:

```powershell
dotnet restore RAC.slnx
dotnet build RAC.slnx -c Release
dotnet test RAC.slnx -c Release
dotnet ef database update --project src/RAC.Infrastructure --startup-project src/RAC.Infrastructure --context RacDbContext
```

4. Inicie ambos os projetos (portas 5080 e 5081), faça login como `admin` e procure
   **MVP 0.9 — Conciliação de atividades Binance Spot**.
5. Escolha um par, como BTCUSDT, e clique em **Consultar e registrar histórico real**.
6. Confira os avisos de cobertura, os lançamentos e as taxas sem interpretar o saldo
   como lucro.

**Não envie API Key, Secret Key, capturas de credenciais ou arquivo DPAPI.** A chave
cadastrada deve permanecer **somente leitura**. O ledger SQL contém dados financeiros
pessoais em texto claro e deve permanecer protegido, acessível apenas ao proprietário.

## Próxima evolução antes de calcular P&L real

1. Implementar paginação por símbolo/janela e sincronização retomável sob rate limit
2. Conciliação das transferências internas e entre carteiras
3. Reconstrução de lotes/custo médio desde o primeiro evento relevante
4. Cálculo de taxas convertido à data da execução e P&L verificável, com erros de arredondamento controlados
5. Testes integrados com respostas Binance simuladas e instância SQL temporária
6. Revisão de segurança, permissões, backup e retenção antes de qualquer implantação

O RAC continua sem rotas de criação/cancelamento de ordens e sem pagamentos.
