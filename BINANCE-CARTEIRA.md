# RAC — Carteira Binance Spot real (somente leitura)

Esta funcionalidade permite consultar os **saldos reais da carteira Binance Spot**, sem negociar.

## O que é real e o que é simulado

- **Mapa dos agentes / Patrimônio simulado (R$ 100):** são resultados do simulador **sintético** do RAC.
- **Mercado Binance Spot — dados públicos reais:** cotações e candles públicos reais, sem exigir API Key.
- **Minha carteira Binance Spot — saldo real:** saldo privado da conta Binance, disponível apenas ao administrador após cadastrar uma chave de leitura.
- Não existe compra, venda, saque, conversão automática, reinvestimento real nem ordens de teste autenticadas nesta etapa.

A consulta inclui somente a **carteira Spot** (`free` disponível e `locked` reservado). Não inclui Funding, Earn, Futuros, margem ou saldos em outras carteiras. O saldo disponível de USDT **não é o patrimônio total** se você possui outros ativos e não é autorização para investir.

## 1. Crie uma API Key na Binance

No **site oficial da Binance**, abra a área de **Gerenciamento de API** e crie uma **nova chave HMAC, de preferência exclusiva para o RAC**.

Permissões que o RAC exige:
- `Enable Reading` (leitura): **HABILITADA**;
- negociação Spot/Margin, Futuros, saques, transferências internas e universais: **DESABILITADOS**;
- se possível, restrinja o acesso ao **IP público autorizado do seu computador**; confirme também o IP de saída da sua conexão.

Se a Binance retornar sinal de permissões perigosas, o RAC **recusa a consulta da carteira**. O RAC faz uma verificação `GET /sapi/v1/account/apiRestrictions` antes de ler `GET /api/v3/account`.

**Nunca envie API Key ou Secret Key por chat, e-mail, screenshot, README, Git ou appsettings.json.**

Referências oficiais:
- https://www.binance.com/en/support/faq/detail/360002502072
- https://developers.binance.com/en/docs/products/spot/rest-api

## 2. Cadastre a chave no seu próprio Windows

Na raiz `C:\Users\05035144208\source\repos\RAC`, abra um **PowerShell independente**, não o Console do Gerenciador de Pacotes NuGet (`PM>`).

```powershell
dotnet run --project tools/RAC.BinanceSetup -- --status
dotnet run --project tools/RAC.BinanceSetup
```

Digite a **API Key** e **Secret Key** quando solicitado. O texto é ocultado no terminal.

O configurador criptografa os dois valores com **Windows DPAPI, CurrentUser** e salva somente o arquivo criptografado em `%LOCALAPPDATA%\RAC\binance-spot-readonly.dpapi`. Nenhuma chave é salva no projeto Git, no SQL Server ou no navegador.

**O mesmo usuário Windows que cadastrou a chave deve executar a RAC.Api.** Se você iniciar a API com outra conta de serviço, o Windows não permitirá descriptografar o arquivo.

Para excluir a chave local posteriormente:

```powershell
dotnet run --project tools/RAC.BinanceSetup -- --remove
```

Você também pode revogar a chave diretamente no site oficial da Binance.

## 3. Consulte o saldo no painel

1. Reinicie `RAC.Api` e `RAC.Web` no Visual Studio.
2. Acesse `http://localhost:5081` e entre como **admin**.
3. Na seção **Minha carteira Binance Spot — saldo real**, clique em **Consultar saldo Binance Spot**.
4. Verifique o total de USDT disponível, o USDT bloqueado e os ativos não zerados.

A consulta é feita **somente quando você clica no botão**. Os saldos **não são gravados no SQL Server nem armazenados no navegador** (respostas HTTP com `Cache-Control: no-store`). Ao sair do sistema, o painel apaga os valores visíveis.

Endpoints: `GET /api/binance/spot/status` e `GET /api/binance/spot/balances`. Ambos exigem a função **Administrator**.

## Controle financeiro e reinvestimento

O RAC ainda **não calcula lucro real nem reinveste fundos da Binance**. Fazer isso de maneira segura exige:
- conciliação de ordens executadas, ativos, taxas, câmbio e custo médio;
- confirmação de disponibilidade real e filtros de lote, valor mínimo e precisão;
- lógica do agente Carlos aplicada sobre saldo real e bloqueio persistente;
- testes end-to-end na Binance Spot Testnet;
- autenticação forte, auditoria, limites, validação de slippage e aprovação explícita para transações reais.

**Não habilite permissões de negociação na chave de leitura.** Uma futura integração de ordens deverá utilizar outra chave, outra etapa e autorização específica.

## Validação e solução de problemas

```powershell
dotnet build RAC.slnx
dotnet test RAC.slnx
```

- **Chave não configurada:** execute o configurador do passo 2.
- **Binance recusou a consulta:** verifique leitura habilitada, chave HMAC, IP permitido e se saques, transferências e trading estão desabilitados.
- **Erro de descriptografia:** configure novamente no perfil Windows que executa a API.
- **Dados antigos na tela:** atualize com Ctrl+F5 e consulte novamente; nunca use saldos antigos para decidir transações.
- **Erro de autenticação 401/403:** entre como usuário admin; leitores e operadores não acessam os saldos privados.
