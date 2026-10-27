using System.Text.RegularExpressions;
using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Controlo;

/// <summary>
/// O REGISTO DAS PUNÇÕES NNPB (o Owner 17/10/2026): a folha por controlo_id
/// com a identificação (referência da punção e lote) e as punções
/// registadas, cada uma com «punção visto» e «arrefecedor visto». Só uma
/// produção cuja CM é NNPB começa um registo; um registo existente
/// continua editável mesmo que o processo da CM mude depois. Um registo
/// também nasce sem Job On (a referência total escrita à mão) e é
/// associado depois à mão a uma produção NNPB: as punções não têm tool_id
/// que encontre a produção sozinho.
/// </summary>
public sealed class PuncoesHandler
{
    private readonly DmoDbContext _db;

    public PuncoesHandler(DmoDbContext db)
    {
        _db = db;
    }

    /// <summary>A folha do controlo; null quando o controlo_id não existe.</summary>
    public async Task<PuncoesSheet?> GetSheetAsync(int controloId, CancellationToken cancellationToken = default)
    {
        var context = await (
            from c in _db.Controlos
            where c.Id == controloId
            join j in _db.JobOns on c.JobOnId equals j.Id
            join m in _db.Machines on j.MachineId equals m.Id
            join cm in _db.CmContexts on j.Id equals cm.JobOnId into cmGroup
            from cm in cmGroup.DefaultIfEmpty()
            join t in _db.Tools.Where(x => x.ToolId != ToolRegistryConstants.RemovedToolId) on cm.ToolId equals t.ToolId into toolGroup
            from t in toolGroup.DefaultIfEmpty()
            join tc in _db.ToolTechnicalContexts on cm.ToolId equals tc.ToolId into technicalGroup
            from tc in technicalGroup.DefaultIfEmpty()
            select new
            {
                ControloId = c.Id,
                JobOnId = j.Id,
                j.Reference,
                j.ProductionNumber,
                Machine = m.Code,
                j.InicioEm,
                j.FimEm,
                CmToolId = t != null ? t.ToolId : null,
                CmReference = t != null ? t.Reference : null,
                CmLot = t != null ? t.Lot : null,
                CmTechnicalName = t != null ? t.TechnicalName : null,
                CmProcess = tc != null ? tc.Process : null
            }).FirstOrDefaultAsync(cancellationToken);

        if (context is null)
        {
            return null;
        }

        var registo = await _db.Puncoes
            .AsNoTracking()
            .Include(p => p.Inspecoes)
            .FirstOrDefaultAsync(p => p.ControloId == controloId, cancellationToken);

        var (isNnpb, source) = ResolveNnpb(context.CmProcess, context.CmTechnicalName);
        // A referência da punção vem do Job On (PU · Referência) quando está escrita
        // (Diogo, 23/10/2026); senão, o modelo da referência da produção.
        var folha = await _db.JobOnFolhas.AsNoTracking()
            .FirstOrDefaultAsync(f => f.JobOnId == context.JobOnId, cancellationToken);
        var puDoJobOn = folha?.Valor("pu_referencia")?.Trim();
        return new PuncoesSheet(
            context.ControloId,
            context.JobOnId,
            context.Reference,
            context.ProductionNumber,
            context.Machine,
            context.CmReference,
            context.CmLot,
            context.CmProcess is { } process ? (process == CmProcess.Nnpb ? "NNPB" : "PS") : null,
            isNnpb,
            source,
            string.IsNullOrEmpty(puDoJobOn) ? DefaultPunchReference(context.Reference) : puDoJobOn,
            registo is null ? null : ToReadModel(registo))
        {
            CmToolId = context.CmToolId,
            InicioEm = context.InicioEm,
            FimEm = context.FimEm
        };
    }

    /// <summary>
    /// Quem registou as punções, pelo nome (o operador ou o email gravado em
    /// RegisteredBy, trocado pelo nome do utilizador quando existe), pela
    /// ordem do primeiro registo; null sem punções.
    /// </summary>
    public async Task<string?> RegistadoPorAsync(PuncoesReadModel registo, CancellationToken cancellationToken = default)
    {
        var quem = registo.Rows
            .Select(r => r.RegisteredBy)
            .OfType<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (quem.Length == 0)
        {
            return null;
        }

        var nomes = await _db.Users.AsNoTracking()
            .Where(u => quem.Contains(u.OperatorId) || (u.Email != null && quem.Contains(u.Email)))
            .Select(u => new { u.OperatorId, u.Email, u.Name })
            .ToListAsync(cancellationToken);
        return string.Join(", ", quem.Select(q =>
            nomes.FirstOrDefault(n => string.Equals(n.OperatorId, q, StringComparison.OrdinalIgnoreCase)
                || string.Equals(n.Email, q, StringComparison.OrdinalIgnoreCase))?.Name ?? q)
            .Distinct());
    }

    /// <summary>
    /// Grava a identificação (referência da punção e o lote dos próximos
    /// registos), criando o registo quando ainda não existe.
    /// </summary>
    public async Task<PuncoesResult> SaveIdentificationAsync(
        PuncoesTarget target,
        string? punchReference,
        string? lot,
        string? updatedBy,
        CancellationToken cancellationToken = default,
        string? reference = null) =>
        await UniqueViolationRetry.RunAsync(_db, async () =>
        {
            var (result, _) = await LoadForWriteAsync(target, punchReference, lot, updatedBy, cancellationToken, reference);
            if (result == PuncoesResult.Success)
            {
                await _db.SaveChangesAsync(cancellationToken);
            }

            return result;
        });

    /// <summary>
    /// Regista uma punção com o lote da identificação; a punção e o
    /// arrefecedor ficam vistos por omissão. A identificação submetida com
    /// ela é gravada na mesma escrita. Um número que já está no registo é
    /// outra punção com esse número, com o lote dela (Diogo, 21/10/2026:
    /// «quando o numero de um puncao é diferente deveria pedir o lote»):
    /// <paramref name="punchLot"/> vale só para ela; sem ele fica o lote de cima.
    /// </summary>
    public async Task<PuncoesResult> AddAsync(
        PuncoesTarget target,
        string? number,
        string? punchReference,
        string? lot,
        string? registeredBy,
        CancellationToken cancellationToken = default,
        string? punchLot = null)
    {
        var canonicalNumber = number?.Trim();
        if (string.IsNullOrEmpty(canonicalNumber))
        {
            return PuncoesResult.MissingNumber;
        }

        if (canonicalNumber.Length > PuncaoInspecao.NumberMaxLength)
        {
            canonicalNumber = canonicalNumber[..PuncaoInspecao.NumberMaxLength];
        }

        // Duas punções registadas ao mesmo tempo colidem na posição (ou na
        // criação do registo): a segunda volta a correr com a posição
        // seguinte.
        return await UniqueViolationRetry.RunAsync(_db, async () =>
        {
            var (result, registo) = await LoadForWriteAsync(target, punchReference, lot, registeredBy, cancellationToken);
            if (result != PuncoesResult.Success || registo is null)
            {
                return result;
            }

            var punchLotText = punchLot?.Trim();
            var inspecaoLot = registo.Lot;
            if (!string.IsNullOrEmpty(punchLotText) && registo.Inspecoes.Any(i => i.Number == canonicalNumber))
            {
                if (CanonicalLot(punchLotText) is not { } canonicalPunchLot)
                {
                    return PuncoesResult.InvalidLot;
                }

                inspecaoLot = canonicalPunchLot;
            }

            // O mesmo número e o mesmo lote são a mesma punção: não se grava
            // outra vez e a página diz que já está registada (Diogo,
            // 24/10/2026; antes não gravava e não dizia nada). O duplo
            // envio do mesmo pedido continua a ser tratado pela chave do envio.
            if (registo.Inspecoes.Any(i => i.Number == canonicalNumber && i.Lot == inspecaoLot))
            {
                return PuncoesResult.DuplicatePunch;
            }

            var nextPosition = registo.Inspecoes.Count == 0 ? 1 : registo.Inspecoes.Max(i => i.Position) + 1;
            registo.Inspecoes.Add(new PuncaoInspecao
            {
                Position = nextPosition,
                Number = canonicalNumber,
                Lot = inspecaoLot,
                PunchSeen = true,
                CoolerSeen = true,
                RegisteredBy = registeredBy,
                RegisteredAtUtc = DateTime.UtcNow
            });
            await _db.SaveChangesAsync(cancellationToken);
            return PuncoesResult.Success;
        });
    }

    /// <summary>
    /// Marca ou desmarca «punção visto» ou «arrefecedor visto» de uma punção do
    /// registo. <paramref name="seen"/> null troca o estado que lá está (os
    /// botões do canto das Punções sem script).
    /// </summary>
    public async Task<PuncoesResult> SetSeenAsync(
        PuncoesTarget target,
        int inspecaoId,
        PuncaoSeenField field,
        bool? seen,
        string? updatedBy,
        CancellationToken cancellationToken = default)
    {
        var inspecao = await FindInspecaoAsync(target, inspecaoId, cancellationToken);
        if (inspecao is null)
        {
            return PuncoesResult.InspecaoNotFound;
        }

        if (field == PuncaoSeenField.Puncao)
        {
            inspecao.PunchSeen = seen ?? !inspecao.PunchSeen;
        }
        else
        {
            inspecao.CoolerSeen = seen ?? !inspecao.CoolerSeen;
        }

        Touch(inspecao.Puncoes!, updatedBy);
        await _db.SaveChangesAsync(cancellationToken);
        return PuncoesResult.Success;
    }

    /// <summary>Remove uma punção do registo (pedido com confirmação na folha).</summary>
    public async Task<PuncoesResult> RemoveAsync(
        PuncoesTarget target,
        int inspecaoId,
        string? updatedBy,
        CancellationToken cancellationToken = default)
    {
        var inspecao = await FindInspecaoAsync(target, inspecaoId, cancellationToken);
        if (inspecao is null)
        {
            return PuncoesResult.InspecaoNotFound;
        }

        Touch(inspecao.Puncoes!, updatedBy);
        _db.PuncaoInspecoes.Remove(inspecao);
        await _db.SaveChangesAsync(cancellationToken);
        return PuncoesResult.Success;
    }

    /// <summary>
    /// Começa um registo sem Job On: a referência total, a referência da
    /// punção e o lote escritos à mão. Devolve o puncoes_id do registo novo.
    /// </summary>
    public async Task<(PuncoesResult Result, int? PuncoesId)> CreateStandaloneAsync(
        string? reference,
        string? punchReference,
        string? lot,
        string? createdBy,
        CancellationToken cancellationToken = default)
    {
        if (CanonicalReference(reference) is not { } canonicalReference)
        {
            return (PuncoesResult.MissingReference, null);
        }

        if (CanonicalPunchReference(punchReference) is not { } canonicalPunchReference)
        {
            return (PuncoesResult.MissingPunchReference, null);
        }

        if (CanonicalLot(lot) is not { } canonicalLot)
        {
            return (PuncoesResult.InvalidLot, null);
        }

        // Dois envios iguais esperam um pelo outro; um registo sem produção
        // igual e ainda sem punções é aberto em vez de criar outro
        // (varredura de 18/10/2026, B5).
        return await DbWrite.InTransactionAsync(_db, async () =>
        {
            await DbWrite.LockAsync(_db, "puncoes:criar", cancellationToken);
            var existingId = await _db.Puncoes
                .AsNoTracking()
                .Where(p => p.ControloId == null
                    && p.Reference == canonicalReference
                    && p.PunchReference == canonicalPunchReference
                    && p.Lot == canonicalLot
                    && !p.Inspecoes.Any())
                .Select(p => (int?)p.Id)
                .FirstOrDefaultAsync(cancellationToken);
            if (existingId is int id)
            {
                return (PuncoesResult.Success, (int?)id);
            }

            var registo = new Puncoes
            {
                Reference = canonicalReference,
                PunchReference = canonicalPunchReference,
                Lot = canonicalLot
            };
            Touch(registo, createdBy);
            _db.Puncoes.Add(registo);
            await _db.SaveChangesAsync(cancellationToken);
            return (PuncoesResult.Success, (int?)registo.Id);
        }, cancellationToken);
    }

    /// <summary>Um registo pelo puncoes_id; null quando não existe.</summary>
    public async Task<PuncoesStandaloneSheet?> GetStandaloneAsync(int puncoesId, CancellationToken cancellationToken = default)
    {
        var registo = await _db.Puncoes
            .AsNoTracking()
            .Include(p => p.Inspecoes)
            .FirstOrDefaultAsync(p => p.Id == puncoesId, cancellationToken);

        return registo is null
            ? null
            : new PuncoesStandaloneSheet(registo.Id, registo.Reference ?? string.Empty, registo.ControloId, ToReadModel(registo));
    }

    /// <summary>
    /// Os registos sem Job On de uma referência, o mais recente primeiro:
    /// a procura em dois passos (a referência e depois o registo).
    /// </summary>
    public async Task<IReadOnlyList<PuncoesStandaloneSummary>> ListStandaloneAsync(
        string? reference,
        CancellationToken cancellationToken = default)
    {
        if (CanonicalReference(reference) is not { } canonicalReference)
        {
            return Array.Empty<PuncoesStandaloneSummary>();
        }

        return await _db.Puncoes
            .AsNoTracking()
            .Where(p => p.ControloId == null && p.Reference == canonicalReference)
            .OrderByDescending(p => p.UpdatedAtUtc)
            .ThenByDescending(p => p.Id)
            .Select(p => new PuncoesStandaloneSummary(
                p.Id,
                p.Reference!,
                p.PunchReference,
                p.Lot,
                p.Inspecoes.Count,
                p.UpdatedBy,
                p.UpdatedAtUtc))
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Associa à mão um registo sem Job On à produção escolhida (o Owner
    /// 17/10/2026). A produção tem de ser NNPB e ainda não ter registo.
    /// </summary>
    public async Task<PuncoesResult> AssociateAsync(
        int puncoesId,
        int controloId,
        string? updatedBy,
        CancellationToken cancellationToken = default) =>
        // Dois registos associados ao mesmo tempo à mesma produção: o
        // segundo colide no índice único e volta a correr, recebendo
        // ControloHasRegisto. O mesmo registo associado ao mesmo tempo a duas
        // produções gravava as duas vezes e ficava a última (deep sweep,
        // 18/10/2026, I3-M-8): o trinco do registo antes de o ler, e o
        // segundo já o vê associado.
        await UniqueViolationRetry.RunAsync(_db, () => DbWrite.InTransactionAsync(_db, async () =>
        {
            await DbWrite.LockAsync(_db, $"puncoes:registo:{puncoesId}", cancellationToken);
            return await AssociateOnceAsync(puncoesId, controloId, updatedBy, cancellationToken);
        }, cancellationToken));

    private async Task<PuncoesResult> AssociateOnceAsync(
        int puncoesId,
        int controloId,
        string? updatedBy,
        CancellationToken cancellationToken)
    {
        var registo = await _db.Puncoes.FirstOrDefaultAsync(p => p.Id == puncoesId, cancellationToken);
        if (registo is null)
        {
            return PuncoesResult.RegistoNotFound;
        }

        if (registo.ControloId is not null)
        {
            return PuncoesResult.AlreadyAssociated;
        }

        var sheet = await GetSheetAsync(controloId, cancellationToken);
        if (sheet is null)
        {
            return PuncoesResult.ControloNotFound;
        }

        if (!sheet.IsNnpb)
        {
            return PuncoesResult.NotNnpb;
        }

        if (sheet.Registo is not null)
        {
            return PuncoesResult.ControloHasRegisto;
        }

        registo.ControloId = controloId;
        Touch(registo, updatedBy);
        await _db.SaveChangesAsync(cancellationToken);
        return PuncoesResult.Success;
    }

    /// <summary>
    /// NNPB pelo «Processo» da ficha da CM; com o campo vazio, pelo «NN» do
    /// nome técnico da CM (M…MPNN…). PS na ficha nunca é NNPB.
    /// </summary>
    public static (bool IsNnpb, PuncoesNnpbSource Source) ResolveNnpb(CmProcess? process, string? technicalName)
    {
        if (process is { } known)
        {
            return (known == CmProcess.Nnpb, PuncoesNnpbSource.Ficha);
        }

        if (!string.IsNullOrWhiteSpace(technicalName)
            && TechnicalNameParser.Decode(technicalName).Process is { } decoded
            && decoded.Contains("NNPB", StringComparison.Ordinal))
        {
            return (true, PuncoesNnpbSource.NomeTecnico);
        }

        return (false, PuncoesNnpbSource.Nenhuma);
    }

    /// <summary>O modelo da referência da produção (1075 em 1075C142); null quando não começa por dígitos.</summary>
    public static string? DefaultPunchReference(string reference)
    {
        var match = Regex.Match(reference ?? string.Empty, @"^\d+");
        return match.Success ? match.Value : null;
    }

    /// <summary>O lote canónico: só dígitos, sem zeros à esquerda e maior que zero; null quando inválido.</summary>
    public static string? CanonicalLot(string? lot)
    {
        var trimmed = lot?.Trim();
        if (string.IsNullOrEmpty(trimmed) || trimmed.Length > Puncoes.LotMaxLength || !trimmed.All(char.IsAsciiDigit))
        {
            return null;
        }

        var canonical = trimmed.TrimStart('0');
        return canonical.Length == 0 ? null : canonical;
    }

    private async Task<(PuncoesResult Result, Puncoes? Registo)> LoadForWriteAsync(
        PuncoesTarget target,
        string? punchReference,
        string? lot,
        string? updatedBy,
        CancellationToken cancellationToken,
        string? reference = null)
    {
        if (CanonicalPunchReference(punchReference) is not { } canonicalReference)
        {
            return (PuncoesResult.MissingPunchReference, null);
        }

        if (CanonicalLot(lot) is not { } canonicalLot)
        {
            return (PuncoesResult.InvalidLot, null);
        }

        Puncoes? registo;
        if (target.PuncoesId is int puncoesId)
        {
            registo = await _db.Puncoes
                .Include(p => p.Inspecoes)
                .FirstOrDefaultAsync(p => p.Id == puncoesId, cancellationToken);
            if (registo is null)
            {
                return (PuncoesResult.RegistoNotFound, null);
            }

            // A referência total só se escreve num registo sem Job On.
            if (registo.ControloId is null && reference is not null)
            {
                if (CanonicalReference(reference) is not { } canonicalTotal)
                {
                    return (PuncoesResult.MissingReference, null);
                }

                registo.Reference = canonicalTotal;
            }
        }
        else
        {
            var controloId = target.ControloId ?? 0;
            registo = await _db.Puncoes
                .Include(p => p.Inspecoes)
                .FirstOrDefaultAsync(p => p.ControloId == controloId, cancellationToken);

            if (registo is null)
            {
                var sheet = await GetSheetAsync(controloId, cancellationToken);
                if (sheet is null)
                {
                    return (PuncoesResult.ControloNotFound, null);
                }

                if (!sheet.IsNnpb)
                {
                    return (PuncoesResult.NotNnpb, null);
                }

                registo = new Puncoes { ControloId = controloId };
                _db.Puncoes.Add(registo);
            }
        }

        registo.PunchReference = canonicalReference;
        registo.Lot = canonicalLot;
        Touch(registo, updatedBy);
        return (PuncoesResult.Success, registo);
    }

    private Task<PuncaoInspecao?> FindInspecaoAsync(PuncoesTarget target, int inspecaoId, CancellationToken cancellationToken)
    {
        var query = _db.PuncaoInspecoes
            .Include(i => i.Puncoes)
            .Where(i => i.Id == inspecaoId);
        query = target.PuncoesId is int puncoesId
            ? query.Where(i => i.PuncoesId == puncoesId)
            : query.Where(i => i.Puncoes!.ControloId == target.ControloId);
        return query.FirstOrDefaultAsync(cancellationToken);
    }

    private static string? CanonicalPunchReference(string? punchReference)
    {
        var trimmed = punchReference?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return null;
        }

        return trimmed.Length > Puncoes.PunchReferenceMaxLength ? trimmed[..Puncoes.PunchReferenceMaxLength] : trimmed;
    }

    /// <summary>A referência total em maiúsculas, como em toda a app; null quando vazia.</summary>
    public static string? CanonicalReference(string? reference)
    {
        var trimmed = reference?.Trim().ToUpperInvariant();
        if (string.IsNullOrEmpty(trimmed))
        {
            return null;
        }

        return trimmed.Length > Puncoes.ReferenceMaxLength ? trimmed[..Puncoes.ReferenceMaxLength] : trimmed;
    }


    private static void Touch(Puncoes registo, string? updatedBy)
    {
        registo.UpdatedBy = updatedBy;
        registo.UpdatedAtUtc = DateTime.UtcNow;
    }

    private static PuncoesReadModel ToReadModel(Puncoes registo)
    {
        // O mesmo número, mesmo com outro lote, é uma «Alteração» (a folha do Owner).
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var rows = registo.Inspecoes
            .OrderBy(i => i.Position)
            .Select(i => new PuncaoReadModel(
                i.Id,
                i.Position,
                i.Number,
                i.Lot,
                i.PunchSeen,
                i.CoolerSeen,
                !seen.Add(i.Number),
                i.RegisteredBy,
                i.RegisteredAtUtc))
            .ToArray();

        return new PuncoesReadModel(
            registo.PunchReference,
            registo.Lot,
            rows,
            rows.Count(r => r.PunchSeen),
            rows.Count(r => r.CoolerSeen),
            registo.UpdatedBy,
            registo.UpdatedAtUtc);
    }
}
