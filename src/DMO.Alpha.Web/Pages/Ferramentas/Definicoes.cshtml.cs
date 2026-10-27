using DMO.Alpha.Core.Admin;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Web.Authorization;
using DMO.Alpha.Web.Presentation;
using DMO.Alpha.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DMO.Alpha.Web.Pages.Ferramentas;

/// <summary>
/// As Definições das Ferramentas (Diogo, 17/10/2026: «ferramentas nao deve
/// ficar no admin»): os TOTAIS DE LOTE POR MÁQUINA. Um lote de BQ da B1 tem
/// quase sempre 120: o total preenche a quantidade ao criar a ferramenta e
/// pode ser mudado nela. O quadro mostra as máquinas e os componentes em
/// letra legível e grava com um único «Guardar» por baixo. Gravar não altera
/// ferramentas já registadas.
/// </summary>
[Authorize]
[DefinicoesEntry("ferramentas", DmoCapabilities.Ferramentas)]
// Os limites do envio do SAP valem para a página toda: num handler o
// ASP.NET ignora-os (aviso MVC1001) e o conteúdo ficava nos 4 MB.
[RequestFormLimits(ValueLengthLimit = 16 * 1024 * 1024)]
[RequestSizeLimit(20 * 1024 * 1024)]
public sealed class DefinicoesModel(IToolLotQuantitySettings toolLotQuantities, IFerramentasUsoSap usoSap, IFerramentasFabricoSap fabricoSap) : PageModel
{
    /// <summary>A última lista do SAP lida (o cartão «Utilização pelo SAP»); nenhuma ainda.</summary>
    public UsoSapUltimaLeitura? UltimaLeituraSap { get; private set; }

    /// <summary>
    /// GET (o ficheiro ferramentas-sap.js): os códigos a pedir ao SAP (o nome
    /// técnico e o lote de cada ferramenta) e a data do último ficheiro lido
    /// (a lista da utilização ou o relatório de fabrico), para não ler duas
    /// vezes o mesmo.
    /// </summary>
    public async Task<IActionResult> OnGetUsoSapAsync()
    {
        var codigos = await usoSap.CodigosAsync(HttpContext.RequestAborted);
        var ultima = await usoSap.UltimaLeituraAsync(HttpContext.RequestAborted);
        var fabrico = await fabricoSap.UltimoFicheiroAsync(HttpContext.RequestAborted);
        var ultimoFicheiro = new[] { ultima?.FicheiroModificadoEmUtc, fabrico }.Max();
        return new JsonResult(new
        {
            codigos,
            ficheiroModificadoEm = ultimoFicheiro is { } data ? new DateTimeOffset(DateTime.SpecifyKind(data, DateTimeKind.Utc)).ToUnixTimeMilliseconds() : (long?)null
        });
    }

    // Um ficheiro mais antigo do que o último lido do mesmo tipo não volta a
    // gravar: o «Ler pasta» envia todos os ficheiros da pasta e uma lista
    // velha punha uma % antiga por cima da de hoje.
    private static bool MaisAntigo(DateTime? ficheiro, DateTime? ultimoLido) =>
        ficheiro is { } f && ultimoLido is { } u && f < u;

    /// <summary>O ficheiro mais antigo do que o último lido do mesmo tipo.</summary>
    public static string MensagemMaisAntigo(string? ficheiro) =>
        $"O ficheiro «{ficheiro}» é mais antigo do que o último lido do mesmo tipo: nada foi gravado.";

    /// <summary>
    /// POST (o ficheiro ferramentas-sap.js): a lista exportada pelo SAP, lida
    /// da pasta ligada. O servidor lê-a (a regra fica num só sítio, em
    /// <see cref="UsoSapExportacao"/>) e grava o uso de cada ferramenta. O
    /// relatório de fabrico (<see cref="FabricoSapExportacao"/>) vem pelo
    /// mesmo caminho e grava o que cada produção fez.
    /// </summary>
    public async Task<IActionResult> OnPostImportarUsoSapAsync([FromForm] string? conteudo, [FromForm] string? ficheiro, [FromForm] long? modificado)
    {
        DateTime? modificadoEm = modificado is > 0 and < 253402300799999
            ? DateTimeOffset.FromUnixTimeMilliseconds(modificado.Value).UtcDateTime
            : null;
        if (UsoSapExportacao.Ler(conteudo).CabecalhoEncontrado
            && MaisAntigo(modificadoEm, (await usoSap.UltimaLeituraAsync(HttpContext.RequestAborted))?.FicheiroModificadoEmUtc))
        {
            return new JsonResult(new { gravado = false, mensagem = MensagemMaisAntigo(ficheiro) });
        }

        var outcome = await usoSap.ImportarAsync(conteudo, ficheiro, modificadoEm, HttpContext.RequestAborted);
        if (outcome.Resultado == ImportarUsoSapResultado.NaoELista
            && FabricoSapExportacao.Ler(conteudo) is { CabecalhoEncontrado: true } relatorio)
        {
            if (MaisAntigo(modificadoEm, await fabricoSap.UltimoFicheiroAsync(HttpContext.RequestAborted)))
            {
                return new JsonResult(new { gravado = false, mensagem = MensagemMaisAntigo(ficheiro) });
            }

            var fabrico = await fabricoSap.ImportarAsync(relatorio.Linhas, ficheiro, modificadoEm, relatorio.Detalhes, HttpContext.RequestAborted);
            return new JsonResult(new { gravado = true, mensagem = MensagemFabricoSap(fabrico) });
        }

        return new JsonResult(new
        {
            gravado = outcome.Resultado == ImportarUsoSapResultado.Gravado,
            mensagem = MensagemUsoSap(outcome, ficheiro)
        });
    }

    /// <summary>O que a leitura fez, dito ao utilizador.</summary>
    public static string MensagemUsoSap(ImportarUsoSapOutcome outcome, string? ficheiro)
    {
        if (outcome.Resultado == ImportarUsoSapResultado.NaoELista)
        {
            return $"O ficheiro «{ficheiro}» não tem a lista do SAP (faltam as colunas Equipamento e % Use) nem o relatório de fabrico (Material, Line, Begin Date e End Date). Nada foi gravado.";
        }

        var texto = outcome.Atualizadas switch
        {
            0 => "Utilização lida do SAP: nenhuma ferramenta mudou.",
            1 => "Utilização lida do SAP: 1 ferramenta atualizada.",
            _ => $"Utilização lida do SAP: {outcome.Atualizadas} ferramentas atualizadas."
        };
        if (outcome.SemFerramenta.Count > 0)
        {
            var exemplos = string.Join(", ", outcome.SemFerramenta.Take(5));
            texto += $" {outcome.SemFerramenta.Count} da lista sem ferramenta no DMO com o mesmo nome técnico e lote ({exemplos}{(outcome.SemFerramenta.Count > 5 ? ", …" : "")}).";
        }

        return texto;
    }

    /// <summary>O que a leitura do relatório de fabrico fez.</summary>
    public static string MensagemFabricoSap(ImportarFabricoSapOutcome outcome) => outcome switch
    {
        { Linhas: 0 } => "Fabrico lido do SAP: o relatório não tem nenhuma linha.",
        { Linhas: 1, Novas: 1 } => "Fabrico lido do SAP: 1 produção nova.",
        { Novas: 0 } => $"Fabrico lido do SAP: {outcome.Linhas} {(outcome.Linhas == 1 ? "produção" : "produções")}, nenhuma nova.",
        _ => $"Fabrico lido do SAP: {outcome.Linhas} produções, {outcome.Novas} {(outcome.Novas == 1 ? "nova" : "novas")}."
    };

    public IReadOnlyList<ToolLotQuantityDefault> ToolQuantities { get; private set; } = Array.Empty<ToolLotQuantityDefault>();

    /// <summary>O aviso depois de gravar: as quantidades ilegíveis ou negativas que ficaram de fora.</summary>
    public string? ToolQuantityWarning { get; private set; }

    /// <summary>Quantos totais ainda estão por definir (máquina × tipo sem valor).</summary>
    public int MissingCount => ToolQuantities.Count(q => q.Quantity is null);

    public string MissingSummary => MissingCount == 0
        ? "Todos os totais estão definidos."
        : $"{MissingCount} de {ToolQuantities.Count} totais por definir.";

    /// <summary>GET: as definições vivem na página única /Definicoes (Diogo, 22/10/2026).</summary>
    public IActionResult OnGet() => Redirect(DefinicoesAcesso.Nova(Request, "ferramentas"));

    /// <summary>Carrega o que a secção deste módulo nas Definições mostra.</summary>
    public async Task CarregarAsync(int? guardado)
    {
        Saved = guardado == 1;
        await LoadAsync();
    }

    /// <summary>TRUE depois de gravar sem avisos (PRG ?guardado=1).</summary>
    public bool Saved { get; private set; }

    /// <summary>O quadro submetido, devolvido quando a gravação é recusada.</summary>
    public IReadOnlyDictionary<string, string?>? ToolQuantityDraft { get; private set; }

    /// <summary>TRUE quando o aviso é uma recusa (nada foi gravado).</summary>
    public bool ToolQuantityRecusada { get; private set; }

    /// <summary>
    /// POST: gravar o quadro dos totais com o botão «Guardar» (Diogo,
    /// 17/10/2026: «botão pode ser apenas guardar»). Uma quantidade má não
    /// recusa as outras (decisão do proprietário, 16/10/2026): as válidas
    /// ficam gravadas e o aviso diz o que ficou de fora.
    /// </summary>
    [DefinicaoGeral]
    public async Task<IActionResult> OnPostSaveToolLotQuantitiesAsync([FromForm] Dictionary<string, string?> quantities)
    {
        var outcome = await toolLotQuantities.SaveAsync(quantities, HttpContext.RequestAborted);
        if (outcome.Result == SaveToolLotQuantitiesResult.Success && outcome.Ignored.Count == 0)
            return RedirectToPage("/Definicoes/Index", new { guardado = 1 });

        if (outcome.Result == SaveToolLotQuantitiesResult.Success)
        {
            var ignored = string.Join(", ", outcome.Ignored.Select(i => $"{i.Type.ToString().ToUpperInvariant()} na {i.MachineCode} («{i.Submitted}»)"));
            TempData[AvisoTempData] = $"Os outros totais foram guardados, exceto: {ignored}. Estes campos mantêm o valor anterior; use quantidades inteiras iguais ou superiores a 0.";
            return RedirectToPage("/Definicoes/Index");
        }

        TempData[AvisoTempData] = "Uma das definições já não corresponde a uma máquina registada. Nenhuma definição foi alterada.";
        TempData[RecusaTempData] = true;
        return RedirectToPage("/Definicoes/Index");
    }

    private const string AvisoTempData = "ferr-def-aviso";
    private const string RecusaTempData = "ferr-def-recusa";

    private async Task LoadAsync()
    {
        if (TempData[AvisoTempData] is string aviso)
        {
            ToolQuantityWarning = aviso;
            ToolQuantityRecusada = TempData[RecusaTempData] is true;
        }

        ToolQuantities = await toolLotQuantities.ListAsync(HttpContext.RequestAborted);
        UltimaLeituraSap = await usoSap.UltimaLeituraAsync(HttpContext.RequestAborted);
    }
}
