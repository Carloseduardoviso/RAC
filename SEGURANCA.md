# RAC MVP 0.6 — Acesso e revisão de risco

## Primeira utilização (no computador do desenvolvedor)

A migration **RacUserSecurity** foi aplicada ao LocalDB `(localdb)\MSSQLLocalDB`, banco `RAC`. Ela adiciona `rac.Users`, com hash de senha, tentativas de login, bloqueio temporário e papel de acesso.

**Não existe conta padrão, senha padrão ou endpoint público de registro.**

Para criar a **primeira e única conta inicial de administrador**, abra o Terminal do Visual Studio na raiz do repositório e execute:

```powershell
dotnet run --project tools/RAC.AdminSetup -- --user admin
```

O terminal solicita uma senha de 9 a 128 caracteres, com letras, números e símbolo. **Digite a senha somente no seu terminal**, nunca no chat, em comandos de linha, no Git ou em `appsettings.json`. A senha é salva apenas como hash Identity no SQL Server.

Se aparecer "Já existe uma conta RAC", a etapa inicial já foi concluída. O utilitário não cria outros administradores silenciosamente.

Depois configure ambos os projetos como inicialização no Visual Studio e navegue para `http://localhost:5081`. Entre com o usuário e a senha recém-criados.

## Permissões

- **Administrator**: painel, comandos de simulação e revisão/liberação de bloqueio.
- **Operator**: painel e comandos de simulação; não pode liberar risco.
- **RiskReviewer**: painel e revisão de risco; não opera a simulação.
- **Viewer**: consulta estado e histórico; não envia comandos.

Os perfis estão definidos na API e verificados **no servidor**, independentemente dos botões do navegador. A criação e manutenção das demais contas não possuem interface administrativa nesta versão.

## Revisão de risco (agente Carlos)

Quando os limites configurados forem atingidos, o RAC pausa a simulação, encerra as posições **sintéticas** e grava um bloqueio no SQL Server. Esse bloqueio permanece após reiniciar a API.

Somente `Administrator` ou `RiskReviewer`, autenticado, pode usar **Revisão humana — Carlos**, com justificativa de 20 a 500 caracteres. O servidor confirma o bloqueio ativo, grava o evento `RISK_REVIEW_APPROVED` e encerra o registro do bloqueio numa **única transação** junto com o checkpoint de uma **nova simulação pausada**. O histórico anterior não é excluído e perdas anteriores não são recuperadas.

Uma falha de persistência interrompe os comandos. Nunca há envio de ordens à corretora.

## Controles básicos

- Cookie `RAC.Session` com `HttpOnly`, `SameSite=Strict` e duração máxima de 8 horas; `Secure` em ambientes não-Development.
- Senhas com `PasswordHasher<T>` (ASP.NET Core Identity).
- Bloqueio temporário após 5 falhas sucessivas de senha (15 minutos) e limitação de requisições de login.
- Métodos POST exigem cabeçalho `X-RAC-CSRF: 1`, sem CORS externo habilitado.
- Proxy Web com rotas permitidas, sem compartilhar cookies entre navegadores.
- A API consulta o SQL Server para verificar se a conta ainda existe e está habilitada em cada solicitação autenticada.
- `/api/health` é público e não divulga dados financeiros; `/api/state` e `/api/history` exigem login.

## Limitações e cuidados

**Uso local/de desenvolvimento apenas.** HTTP em localhost é permitido para depuração; para produção será necessário HTTPS completo, certificados válidos, política de usuários, recuperação de credenciais, MFA, proteção de segredos, auditoria ampliada e testes externos de segurança. Não exponha o RAC.Api ou RAC.Web na Internet nesta fase.

A criação inicial não pressupõe uma senha que o ChatGPT conheça. O banco de dados de produção, credenciais reais da Binance e rotas de ordens reais **não fazem parte deste MVP**.

## Testes

```powershell
dotnet build RAC.slnx
dotnet test RAC.slnx
```

Os testes integrados em banco temporário validaram 401 sem login, 403 para Viewer, isolamento de sessões entre navegadores, preservação do bloqueio após reinício, liberação autorizada com auditoria e logout.
