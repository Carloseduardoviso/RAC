# RAC - Solucao Visual Studio

Abra **RAC.slnx** no Visual Studio com suporte a .NET 10.

Projetos:
- RAC.Api: API REST e simulador local, http://localhost:5080
- RAC.Web: painel web responsivo, http://localhost:5081

No Visual Studio configure **Multiple startup projects** e selecione **Start** para RAC.Api e RAC.Web.
Ou execute em dois terminais:

    dotnet run --project src/RAC.Api --launch-profile RAC.Api
    dotnet run --project src/RAC.Web --launch-profile RAC.Web

A interface RAC.Web consulta a API atraves de um proxy restrito no proprio servidor web. A URL interna esta em src/RAC.Web/appsettings.json.

**Seguranca:** somente simulacao, sem ordens reais e sem chaves API. A API ainda nao possui login. Nao exponha nenhum dos serviços à Internet.

SQL Server, agentes inteligentes e persistencia serao implementados nas proximas fases.
