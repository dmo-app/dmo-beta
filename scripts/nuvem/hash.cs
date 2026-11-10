#:project ../../src/DMO.Alpha.Infrastructure/DMO.Alpha.Infrastructure.csproj
// Imprime o hash da palavra-passe de teste no formato da app (só para a cópia na nuvem).
Console.WriteLine(DMO.Alpha.Infrastructure.Authentication.Local.LocalPasswordHasher.Hash(args[0]));
