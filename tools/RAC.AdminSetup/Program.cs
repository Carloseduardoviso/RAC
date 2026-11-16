using System.Data;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RAC.Domain;
using RAC.Infrastructure;

if (Console.IsInputRedirected)
{
    Console.Error.WriteLine("Execute em um terminal interativo. Não forneça senhas por argumentos ou arquivos.");
    return 1;
}

var name = args.Length == 2 && args[0] == "--user" ? args[1].Trim() : "admin";
if (!Regex.IsMatch(name, @"^[a-zA-Z][a-zA-Z0-9._-]{2,39}$"))
{
    Console.Error.WriteLine("Usuário inválido. Use 3 a 40 caracteres, iniciando por letra.");
    return 1;
}

using var db = new RacDbContextFactory().CreateDbContext([]);
if (db.Database.GetPendingMigrations().Any())
{
    Console.Error.WriteLine("Execute primeiro as migrations com dotnet ef database update.");
    return 1;
}
if (db.Users.Any())
{
    Console.Error.WriteLine("Já existe uma conta RAC. A criação inicial foi encerrada para evitar contas administrativas não autorizadas.");
    return 1;
}

Console.WriteLine("Configuração local do administrador RAC.");
Console.WriteLine("A senha será solicitada neste terminal e não será exibida.");
var password = ReadPassword("Nova senha (mínimo 9 caracteres): ");
if (password.Length is < 9 or > 128 ||
    !password.Any(char.IsLetter) || !password.Any(char.IsDigit) ||
    !password.Any(c => !char.IsLetterOrDigit(c)))
{
    Console.Error.WriteLine("Senha inválida: use 9 a 128 caracteres com letras, números e símbolos.");
    return 1;
}
var confirmation = ReadPassword("Confirme a senha: ");
if (!string.Equals(password, confirmation, StringComparison.Ordinal))
{
    Console.Error.WriteLine("As senhas não coincidem.");
    return 1;
}

var user = new RacUser
{
    UserName = name,
    NormalizedUserName = name.ToUpperInvariant(),
    PasswordHash = string.Empty,
    Role = RacRoles.Administrator
};
user.PasswordHash = new PasswordHasher<RacUser>().HashPassword(user, password);

await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
if (await db.Users.AnyAsync())
{
    Console.Error.WriteLine("Outra conta foi criada. Operação cancelada.");
    return 1;
}
db.Users.Add(user);
db.AuditEvents.Add(new AuditEvent
{
    EventType = "AUTH_BOOTSTRAP",
    Description = "Conta administrativa inicial criada no terminal local: " + name
});
await db.SaveChangesAsync();
await transaction.CommitAsync();
Console.WriteLine("Administrador criado. Entre no RAC.Web com o usuário " + name + ".");
return 0;

static string ReadPassword(string message)
{
    Console.Write(message);
    var characters = new List<char>();
    while (true)
    {
        var key = Console.ReadKey(intercept: true);
        if (key.Key == ConsoleKey.Enter) break;
        if (key.Key == ConsoleKey.Backspace)
        {
            if (characters.Count != 0) characters.RemoveAt(characters.Count - 1);
            continue;
        }
        if (!char.IsControl(key.KeyChar) && characters.Count < 128)
            characters.Add(key.KeyChar);
    }
    Console.WriteLine();
    return new string(characters.ToArray());
}
