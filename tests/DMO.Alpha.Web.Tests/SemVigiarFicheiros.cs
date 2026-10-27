using System.Runtime.CompilerServices;

namespace DMO.Alpha.Web.Tests;

// Cada aplicação de teste gastava duas das 128 vigias de ficheiros que o
// Linux dá por utilizador (inotify): uma para o appsettings*.json e outra
// para o wwwroot (o asp-append-version). Com as classes de testes em
// paralelo chegava-se às 126 e, conforme a corrida, uma aplicação dava erro
// 500 (no login, por exemplo) ou nem arrancava. Os testes não mudam ficheiros
// da aplicação: a configuração não se relê e o wwwroot vê-se por consulta
// periódica, que não gasta vigias.
internal static class SemVigiarFicheiros
{
    [ModuleInitializer]
    internal static void Desligar()
    {
        Environment.SetEnvironmentVariable("DOTNET_hostBuilder__reloadConfigOnChange", "false");
        Environment.SetEnvironmentVariable("DOTNET_USE_POLLING_FILE_WATCHER", "true");
    }
}
