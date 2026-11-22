using RAC.Infrastructure;
using System.Text;

var store = new BinanceTestnetCredentialStore();
if (args.SequenceEqual(["--status"]))
{
    Console.WriteLine(store.IsConfigured
        ? "Credenciais Binance Spot TESTNET protegidas no Windows: configuradas."
        : "Credenciais Binance Spot TESTNET: não configuradas.");
    return;
}
if (!args.SequenceEqual(["--configure"]))
{
    Console.WriteLine("Uso: dotnet run --project tools/RAC.TestnetSetup -- --status | --configure");
    return;
}
if (Console.IsInputRedirected || !Environment.UserInteractive)
    throw new InvalidOperationException("Configure somente em um terminal Windows interativo.");
Console.WriteLine("ATENÇÃO: credenciais EXCLUSIVAS da Spot Testnet, nunca da Binance real.");
Console.Write("Chave Testnet: ");
var key = Console.ReadLine() ?? "";
Console.Write("Secret Testnet (oculto): ");
var secret = new StringBuilder();
ConsoleKeyInfo typed;
while ((typed = Console.ReadKey(intercept:true)).Key != ConsoleKey.Enter)
{
    if (typed.Key == ConsoleKey.Backspace)
    {
        if (secret.Length>0) secret.Length--;
    }
    else if (!char.IsControl(typed.KeyChar) && secret.Length < 512)
        secret.Append(typed.KeyChar);
}
Console.WriteLine();
store.Save(key,secret.ToString());
secret.Clear();
Console.WriteLine("Credenciais TESTNET guardadas sob DPAPI CurrentUser. Nenhuma ordem enviada.");