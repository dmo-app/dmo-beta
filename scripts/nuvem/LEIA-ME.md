# Cópia da app na nuvem (DMO-736 / A-0509)

Para qualquer thread testar a app a sério **antes de dizer «pronto»**, sem tocar
na oficial nem no ensaio. Base PostgreSQL local, só com dados falsos
(`dados-falsos.sql`: 8 linhas, 30 Job On, 120 ferramentas CM/MF/BQ,
120 movimentos de Boquilhas). SAP desligado (nada configurado).

```sh
bash scripts/nuvem/arrancar.sh                       # ~2 min: .NET, base nova, migrações, dados, app em :5080
NODE_PATH=$(npm root -g) node scripts/nuvem/verificar.cjs   # entra e verifica as páginas
```

`verificar.cjs` entra com as duas contas de teste e abre as páginas principais
a 1366×768 e a meio ecrã (683×768). Falha se uma página der erro, mandar para o
login, tiver erro de JavaScript, não chegar ao fundo com scroll, ou tiver uma
caixa grande com conteúdo cortado sem scroll. Acrescentar páginas na lista do
topo do ficheiro quando se mexe nelas.

Contas (só existem nesta base): `admin@teste.local` (ADMIN) e
`operador@teste.local` (template com tudo). Palavra-passe `teste-nuvem-123`
ou `DMO_TESTE_PW`.

Notas: o site da Microsoft (`builds.dotnet.microsoft.com`) está bloqueado pelo
proxy; o .NET 10 vem do pacote `dotnet-sdk-10.0` do Ubuntu. NuGet passa.
Cada `arrancar.sh` apaga e refaz a base `dmo_nuvem` (só a local).
