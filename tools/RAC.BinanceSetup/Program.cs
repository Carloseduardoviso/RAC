using RAC.Infrastructure;

if (!OperatingSystem.IsWindows())
{
    Console.Error.WriteLine("O armazenamento protegido do RAC requer o Windows.");
    return 1;
}

var store = new BinanceSpotCredentialStore();
if (args.Length == 1 && args[0] == "--status")
{
    Console.WriteLine(store.IsConfigured
        ? "Binance Spot: credenciais protegidas configuradas neste perfil do Windows."
        : "Binance Spot: credenciais ainda não configuradas.");
    return 0;
}

if (Console.IsInputRedirected)
{
    Console.Error.WriteLine("Execute no PowerShell interativo do Windows, não no console PM>.");
    return 1;
}

if (args.Length == 1 && args[0] == "--remove")
{
    Console.Write("Excluir credenciais locais Binance? Digite SIM para confirmar: ");
    if (Console.ReadLine() != "SIM") return 1;
    store.Delete();
    Console.WriteLine("Credenciais locais excluídas.");
    return 0;
}

var showApiKey = args.Length == 1 && args[0] == "--show-api-key";
if (args.Length != 0 && !showApiKey)
{
    Console.Error.WriteLine("Use sem argumentos, --show-api-key, --status ou --remove.");
    return 1;
}

Console.WriteLine("RAC — Binance Spot (somente leitura, sem ordens reais)");
Console.WriteLine("Crie na Binance uma chave HMAC exclusiva com SOMENTE leitura.");
Console.WriteLine("DESATIVE trading, saques e transferências. Use lista de IPs permitidos, quando possível.");
Console.WriteLine("Digite as credenciais SOMENTE aqui. Não envie no chat e não use parâmetros de comando.");
if (store.IsConfigured)
{
    Console.Write("Já existe uma chave protegida. Substituir? Digite SIM: ");
    if (Console.ReadLine() != "SIM") return 1;
}
if (showApiKey)
{
    Console.WriteLine("ATENÇÃO: a API Key ficará visível neste terminal até você limpar a tela.");
    Console.WriteLine("Não faça capturas de tela nem compartilhe o conteúdo do terminal.");
}
var apiKey = showApiKey ? ReadVisible("API Key (visível): ") : ReadHidden("API Key: ");
var apiSecret = ReadHidden("Secret Key (sempre oculta): ");
try
{
    store.Save(apiKey, apiSecret);
    Console.WriteLine("Credenciais criptografadas com Windows DPAPI. Nenhuma ordem foi enviada.");
    Console.WriteLine("Reinicie RAC.Api e RAC.Web e clique em Consultar saldo Binance no painel.");
    return 0;
}
catch (ArgumentException)
{
    Console.Error.WriteLine("As credenciais não possuem o formato esperado.");
    return 1;
}
catch (Exception)
{
    Console.Error.WriteLine("Falha ao salvar credenciais locais; confirme acesso ao perfil Windows.");
    return 1;
}

static string ReadVisible(string prompt)
{
    Console.Write(prompt);
    return Console.ReadLine() ?? string.Empty;
}

static string ReadHidden(string prompt)
{
    Console.Write(prompt);
    var letters = new List<char>();
    while (true)
    {
        var key = Console.ReadKey(intercept: true);
        if (key.Key == ConsoleKey.Enter) break;
        if (key.Key == ConsoleKey.Backspace)
        {
            if (letters.Count > 0) letters.RemoveAt(letters.Count - 1);
            continue;
        }
        if (!char.IsControl(key.KeyChar) && letters.Count < 512)
            letters.Add(key.KeyChar);
    }
    Console.WriteLine();
    var value = new string(letters.ToArray());
    letters.Clear();
    return value;
}
