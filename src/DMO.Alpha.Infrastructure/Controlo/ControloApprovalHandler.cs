using System.Globalization;
using System.Linq.Expressions;
using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Core.Runtime;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Controlo;

/// <summary>
/// O SELO DE APROVAÇÃO de um controlo (o corte D — o verbatim do Owner:
/// «o nao estar aprovado nao deve criar blocks ou negar continuidade.
/// deve apenas deixar registado»): o chefe marca o controlo da produção
/// como APROVADO — o operador e o momento ficam no próprio registo (ao
/// contrário do visto, que é uma linha por visita; a aprovação é UMA,
/// a última vence, idempotente). NUNCA é gate: nada muda de comportamento
/// por estar aprovado ou não — é a informação do histórico.
/// </summary>
public sealed class ControloApprovalHandler
{
    private readonly DmoDbContext _db;

    public ControloApprovalHandler(DmoDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Aprova (ou reaprova) o controlo da produção: grava o operador e o
    /// momento. Sem operador identificado ou controlo inexistente: nada
    /// acontece (nenhum facto inventado). A leitura devolve o estado
    /// corrente para a folha reabrir com ele.
    /// </summary>
    public async Task<ControloApprovalReadModel?> ApproveAndGetAsync(
        int controloId,
        string? operatorId,
        CancellationToken cancellationToken = default) =>
        await ApproveAndGetAsync(controloId, operatorId, note: null, cancellationToken);

    /// <summary>A aprovação com a nota opcional da Revisão; limpa um «Não aprovar» anterior.</summary>
    public async Task<ControloApprovalReadModel?> ApproveAndGetAsync(
        int controloId,
        string? operatorId,
        string? note,
        CancellationToken cancellationToken = default)
    {
        return string.IsNullOrWhiteSpace(operatorId)
            ? await GetAsync(controloId, cancellationToken)
            : await DecideAsync(controloId, operatorId, note, aprovado: true, parte: null, cancellationToken);
    }

    /// <summary>
    /// O «Não aprovar» (17/10/2026): a nota é obrigatória e diz o que tem de
    /// mudar. Limpa uma aprovação anterior. Nunca bloqueia nada: é registo.
    /// Sem operador, sem nota ou controlo inexistente: nada é gravado.
    /// </summary>
    public async Task<ControloApprovalReadModel?> NotApproveAndGetAsync(
        int controloId,
        string? operatorId,
        string? note,
        CancellationToken cancellationToken = default)
    {
        return string.IsNullOrWhiteSpace(operatorId) || string.IsNullOrWhiteSpace(note)
            ? await GetAsync(controloId, cancellationToken)
            : await DecideAsync(controloId, operatorId, note, aprovado: false, parte: null, cancellationToken);
    }

    /// <summary>
    /// A decisão de uma parte do controlo (Diogo, 19/10/2026: só o Peso se
    /// aprova): grava a decisão da parte e resume o controlo. Não aprovar exige nota. Sem
    /// operador, sem nota ao não aprovar ou controlo inexistente: nada é
    /// gravado. Nunca bloqueia nada: é registo.
    /// </summary>
    public async Task<ControloApprovalReadModel?> DecidePartAsync(
        int controloId,
        ControloParte parte,
        bool aprovado,
        string? operatorId,
        string? note,
        CancellationToken cancellationToken = default)
    {
        return string.IsNullOrWhiteSpace(operatorId) || (!aprovado && string.IsNullOrWhiteSpace(note))
            ? await GetAsync(controloId, cancellationToken)
            : await DecideAsync(controloId, operatorId, note, aprovado, parte, cancellationToken);
    }

    /// <summary>As decisões das partes do controlo (as que existem; as antigas dos Pegamentos não contam).</summary>
    public async Task<IReadOnlyList<ControloDecisaoReadModel>> PartsAsync(
        int controloId,
        CancellationToken cancellationToken = default) =>
        await _db.ControloDecisoes
            .AsNoTracking()
            .Where(d => d.ControloId == controloId && d.Parte == ControloParte.Peso)
            .OrderBy(d => d.Parte)
            .Select(d => new ControloDecisaoReadModel(d.Parte, d.Aprovado, d.OperatorId, d.EmUtc, d.Nota))
            .ToListAsync(cancellationToken);

    /// <summary>
    /// Quando o Peso recebeu o último registo novo (um Peso), a última
    /// leitura e a última correção (mudar ou apagar uma leitura), para a
    /// Revisão comparar com a decisão: um Peso novo depois da aprovação pede
    /// aprovação própria (C7); uma leitura registada ou corrigida só avisa
    /// (C9). A correção vem do rasto das leituras (relatório do
    /// dmo-Improvements de 28/10/2026, S05: era sempre null). Uma consulta.
    /// </summary>
    public async Task<IReadOnlyDictionary<ControloParte, ControloParteRegistos>> RegistosAsync(
        int controloId,
        CancellationToken cancellationToken = default)
    {
        var row = await _db.Controlos
            .AsNoTracking()
            .Where(c => c.Id == controloId)
            .Select(c => new
            {
                PesoNovo = _db.Pesos.Where(p => p.ControloId == c.Id).Max(p => p.RegistadoEmUtc),
                PesoLeitura = _db.PesoMeasurements.Where(m => m.Peso.ControloId == c.Id).Max(m => m.RegistadoEmUtc),
                PesoCorrecao = _db.PesoMeasurementHistorico
                    .Where(h => _db.Pesos.Any(p => p.Id == h.PesoId && p.ControloId == c.Id))
                    .Max(h => (DateTime?)h.EmUtc)
            })
            .FirstOrDefaultAsync(cancellationToken);
        if (row is null)
        {
            return new Dictionary<ControloParte, ControloParteRegistos>();
        }

        return new Dictionary<ControloParte, ControloParteRegistos>
        {
            [ControloParte.Peso] = new(row.PesoNovo, row.PesoLeitura, row.PesoCorrecao)
        };
    }

    /// <summary>
    /// Grava a decisão de uma parte (ou de todas, sem parte: o Aprovar do
    /// Resumo e as chamadas antigas) e guarda nas colunas do controlo o
    /// estado que daí sai, pela regra de <see cref="ControloAprovacao"/> (a
    /// mesma que a Revisão, o Histórico e o Resumo leem).
    /// </summary>
    private Task<ControloApprovalReadModel?> DecideAsync(
        int controloId,
        string operatorId,
        string? note,
        bool aprovado,
        ControloParte? parte,
        CancellationToken cancellationToken) =>
        // A primeira decisão da mesma parte dada por dois chefes ao mesmo
        // tempo: a segunda colide no índice único e volta a correr, já com a
        // linha da primeira à vista, e substitui-a (a última vence).
        UniqueViolationRetry.RunAsync(_db, () => DbWrite.InTransactionAsync<ControloApprovalReadModel?>(_db, async () =>
        {
            var controlo = await _db.Controlos
                .FirstOrDefaultAsync(c => c.Id == controloId, cancellationToken);
            if (controlo is null)
            {
                return null;
            }

            var nota = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
            var agora = DateTime.UtcNow;
            var decisoes = await _db.ControloDecisoes
                .Where(d => d.ControloId == controloId)
                .ToListAsync(cancellationToken);
            foreach (var p in parte is { } uma ? new[] { uma } : Enum.GetValues<ControloParte>())
            {
                var decisao = decisoes.FirstOrDefault(d => d.Parte == p);
                if (decisao is null)
                {
                    decisao = new ControloDecisao { ControloId = controloId, Parte = p };
                    _db.ControloDecisoes.Add(decisao);
                }
                else
                {
                    WriteWholeDecision(decisao);
                }

                decisao.Aprovado = aprovado;
                decisao.OperatorId = operatorId;
                decisao.EmUtc = agora;
                decisao.Nota = nota;
            }

            await _db.SaveChangesAsync(cancellationToken);

            // O resumo sai de uma decisão de cada vez (sweep de 19/10/2026,
            // DB-4): com duas ao mesmo tempo em partes diferentes, cada uma
            // resumia só o que tinha lido, e o resumo podia ficar só com uma.
            // Com o trinco, a segunda espera que a primeira fique gravada e
            // resume as duas. O trinco só cobre o resumo: a decisão em si já
            // está protegida pelo índice único e pela gravação inteira.
            await DbWrite.LockAsync(_db, $"controlo:decisoes:{controloId}", cancellationToken);
            var (_, estado) = (await FirstAsync(e => e.ControloId == controloId, cancellationToken))!.Value;
            controlo.ApprovedByOperatorId = estado.ApprovedByOperatorId;
            controlo.ApprovedAtUtc = estado.ApprovedAtUtc;
            controlo.NotApprovedByOperatorId = estado.NotApprovedByOperatorId;
            controlo.NotApprovedAtUtc = estado.NotApprovedAtUtc;
            controlo.ReviewNote = estado.ReviewNote;
            WriteAllApprovalColumns(controlo);
            await _db.SaveChangesAsync(cancellationToken);
            return estado;
        }, cancellationToken));

    /// <summary>
    /// Como nas colunas do controlo: uma decisão que substitui outra grava
    /// os quatro campos, mesmo os que o EF julga iguais, para duas decisões
    /// ao mesmo tempo na mesma parte não ficarem misturadas (o «Aprovado» de
    /// uma com a nota da outra).
    /// </summary>
    private void WriteWholeDecision(ControloDecisao decisao)
    {
        var entry = _db.Entry(decisao);
        entry.Property(d => d.Aprovado).IsModified = true;
        entry.Property(d => d.OperatorId).IsModified = true;
        entry.Property(d => d.EmUtc).IsModified = true;
        entry.Property(d => d.Nota).IsModified = true;
    }

    /// <summary>
    /// O EF só grava as colunas que mudaram. Um «Aprovar» e um «Não aprovar»
    /// ao mesmo tempo no mesmo controlo pendente gravariam colunas
    /// diferentes e ficavam os dois; gravando sempre as cinco, a última
    /// decisão substitui a outra por inteiro (e a regra da base,
    /// CK_Controlos_ApprovedOrNotApproved, nunca é tocada).
    /// </summary>
    private void WriteAllApprovalColumns(DMO.Alpha.Core.Controlo.Controlo controlo)
    {
        var entry = _db.Entry(controlo);
        entry.Property(c => c.ApprovedByOperatorId).IsModified = true;
        entry.Property(c => c.ApprovedAtUtc).IsModified = true;
        entry.Property(c => c.NotApprovedByOperatorId).IsModified = true;
        entry.Property(c => c.NotApprovedAtUtc).IsModified = true;
        entry.Property(c => c.ReviewNote).IsModified = true;
    }

    /// <summary>
    /// O estado de aprovação corrente (sem marcar), calculado das decisões e
    /// dos registos (<see cref="ControloAprovacao"/>); null quando o
    /// controlo não existe.
    /// </summary>
    public async Task<ControloApprovalReadModel?> GetAsync(
        int controloId,
        CancellationToken cancellationToken = default) =>
        (await FirstAsync(e => e.ControloId == controloId, cancellationToken))?.Approval;

    /// <summary>
    /// O controlo_id da produção e o estado de aprovação numa só consulta;
    /// null quando a produção ainda não tem controlo. Só leitura.
    /// </summary>
    public Task<(int ControloId, ControloApprovalReadModel Approval)?> FindByJobOnAsync(
        int jobOnId,
        CancellationToken cancellationToken = default) =>
        FirstAsync(e => e.JobOnId == jobOnId, cancellationToken);

    /// <summary>
    /// A decisão aplicável ao Peso exportado, pela mesma regra do estado
    /// corrente. Um Peso criado depois de uma aprovação fica por aprovar;
    /// outro Peso mais recente não invalida a folha histórica pedida. Um
    /// «Não aprovar» mantém-se. Null se o Peso não pertence ao controlo.
    /// </summary>
    public async Task<ControloApprovalReadModel?> PesoAsync(
        int controloId,
        int pesoId,
        CancellationToken cancellationToken = default) =>
        (await FirstAsync(e => e.ControloId == controloId, cancellationToken, pesoId))?.Approval;

    /// <summary>
    /// A decisão de uma parte, com a forma do estado do controlo, para o PDF
    /// dessa parte (sweep de 19/10/2026, C-3: cada PDF mostra a decisão da
    /// sua parte): aprovada ou não aprovada, por quem, quando e a nota; sem
    /// decisão, por aprovar. Um controlo sem decisões por parte (anterior a
    /// elas) mostra o resumo gravado, como antes. Null quando o controlo não
    /// existe.
    /// </summary>
    public async Task<ControloApprovalReadModel?> ParteAsync(
        int controloId,
        ControloParte parte,
        CancellationToken cancellationToken = default)
    {
        var decisao = await _db.ControloDecisoes
            .AsNoTracking()
            .Where(d => d.ControloId == controloId && d.Parte == parte)
            .Select(d => new
            {
                d.Aprovado,
                d.OperatorId,
                d.EmUtc,
                d.Nota,
                Nome = _db.Users.Where(u => u.OperatorId == d.OperatorId).Select(u => u.Name).FirstOrDefault()
            })
            .FirstOrDefaultAsync(cancellationToken);
        if (decisao is not null)
        {
            return (decisao.Aprovado
                    ? new ControloApprovalReadModel(decisao.OperatorId, decisao.EmUtc, ReviewNote: decisao.Nota)
                    : new ControloApprovalReadModel(null, null, decisao.OperatorId, decisao.EmUtc, decisao.Nota))
                with { DecididoPorNome = decisao.Nome };
        }

        return await _db.ControloDecisoes.AnyAsync(d => d.ControloId == controloId, cancellationToken)
            ? new ControloApprovalReadModel(null, null)
            : await GetAsync(controloId, cancellationToken);
    }

    /// <summary>
    /// O estado do primeiro controlo que cumpre <paramref name="onde"/>, com
    /// o nome de quem decidiu (o utilizador com esse nº, como na Revisão),
    /// numa só consulta.
    /// </summary>
    private async Task<(int ControloId, ControloApprovalReadModel Approval)?> FirstAsync(
        Expression<Func<ControloAprovacaoEstado, bool>> onde,
        CancellationToken cancellationToken,
        int? pesoId = null)
    {
        var linha = await ControloAprovacao.Estados(_db, pesoId)
            .Where(onde)
            .Select(e => new
            {
                e.ControloId,
                Approval = new ControloApprovalReadModel(
                    e.ApprovedByOperatorId, e.ApprovedAtUtc,
                    e.NotApprovedByOperatorId, e.NotApprovedAtUtc, e.ReviewNote)
                {
                    DecididoPorNome = _db.Users
                        .Where(u => u.OperatorId == (e.ApprovedByOperatorId ?? e.NotApprovedByOperatorId))
                        .Select(u => u.Name)
                        .FirstOrDefault()
                }
            })
            .FirstOrDefaultAsync(cancellationToken);

        return linha is null ? null : (linha.ControloId, linha.Approval);
    }
}

/// <summary>
/// O estado de aprovação de um controlo: o operador e o momento (null = não
/// aprovado), o «Não aprovar» e a nota da última decisão.
/// </summary>
public sealed record ControloApprovalReadModel(
    string? ApprovedByOperatorId,
    DateTime? ApprovedAtUtc,
    string? NotApprovedByOperatorId = null,
    DateTime? NotApprovedAtUtc = null,
    string? ReviewNote = null)
{
    /// <summary>
    /// O nome de quem decidiu, quando o nº gravado é o de um utilizador (como
    /// na Revisão); null por aprovar ou sem utilizador.
    /// </summary>
    public string? DecididoPorNome { get; init; }

    /// <summary>Não aprovado: há um «Não aprovar» e nenhuma aprovação.</summary>
    public bool NaoAprovado => ApprovedByOperatorId is null && NotApprovedByOperatorId is not null;

    /// <summary>
    /// Quem decidiu, como o Resumo e os PDFs o mostram (sweep de 19/10/2026,
    /// C-3): o nome; sem utilizador, «Operador N» para um nº, ou o que ficou
    /// gravado (o email de quem entrou por email). Null por aprovar.
    /// </summary>
    public string? Quem => (ApprovedByOperatorId ?? NotApprovedByOperatorId) is { } id
        ? DecididoPorNome ?? (id.All(char.IsDigit) ? $"Operador {id}" : id)
        : null;

    /// <summary>
    /// Quem e quando, e a nota de um «Não aprovar» («Chefe · 19/10/2026 09:30
    /// · Repetir os pegamentos.»); null por aprovar.
    /// </summary>
    public string? Detalhe(CultureInfo cultura) => Quem is { } quem
        ? quem
            + ((ApprovedAtUtc ?? NotApprovedAtUtc) is { } em ? " · " + DmoBusinessTime.ToLocal(em).ToString("g", cultura) : "")
            + (NaoAprovado && ReviewNote is { } nota ? " · " + nota : "")
        : null;

    /// <summary>Os três estados numa frase: «Aprovado por …», «Não aprovado por …» ou «Por aprovar».</summary>
    public string Frase(CultureInfo cultura) => Detalhe(cultura) is { } detalhe
        ? (NaoAprovado ? "Não aprovado por " : "Aprovado por ") + detalhe
        : "Por aprovar";
}

/// <summary>
/// Os últimos registos de uma parte (UTC; null quando não há ou são de antes
/// de se guardar a data): um registo novo (Peso ou folha), uma leitura e uma
/// correção de leitura.
/// </summary>
public sealed record ControloParteRegistos(
    DateTime? UltimoRegistoNovo,
    DateTime? UltimaLeitura,
    DateTime? UltimaCorrecao);

/// <summary>A decisão de uma parte do controlo (o Peso).</summary>
public sealed record ControloDecisaoReadModel(
    ControloParte Parte,
    bool Aprovado,
    string OperatorId,
    DateTime EmUtc,
    string? Nota);
