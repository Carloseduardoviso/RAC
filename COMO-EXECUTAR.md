# RAC — Solução Visual Studio (.NET 10)

Abra `RAC.slnx` no Visual Studio.

## Banco de dados local

- SQL Server LocalDB: `(localdb)\MSSQLLocalDB`
- Banco de desenvolvimento: `RAC`
- Entity Framework Core 10 (migrations em `src/RAC.Infrastructure/Migrations`).

Atualize o banco explicitamente a partir da raiz do repositório:

```powershell
dotnet ef database update --project src/RAC.Infrastructure --startup-project src/RAC.Infrastructure --context RacDbContext
```

## Crie o administrador inicial (somente uma vez)

Abra o **Terminal** do Visual Studio:

```powershell
dotnet run --project tools/RAC.AdminSetup -- --user admin
```

Digite uma senha nova de pelo menos 12 caracteres, incluindo letras, números e símbolos. O terminal não mostra os caracteres. **Não compartilhe a senha nem a coloque no código**. Nenhuma conta é criada automaticamente pelo sistema.

## Inicie os dois serviços

Configure **Vários projetos de inicialização**:
- `RAC.Api` — Iniciar — `http://localhost:5080`
- `RAC.Web` — Iniciar — `http://localhost:5081`

Ou use dois terminais:

```powershell
dotnet run --project src/RAC.Api --launch-profile RAC.Api
dotnet run --project src/RAC.Web --launch-profile RAC.Web
```

Abra `http://localhost:5081` e entre com o administrador criado.

**Somente desenvolvimento local:** preços simulados, sem ordens reais na Binance. `RAC.Web` encaminha apenas rotas permitidas para a API; não exponha os serviços à Internet nesta fase.

Leia `SEGURANCA.md` para permissões, cookies e revisão do agente Carlos. Leia `SQL-SERVER.md` para persistência e histórico.

## Testes

```powershell
dotnet build RAC.slnx
dotnet test RAC.slnx
```
