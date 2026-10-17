using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Controlo;

/// <summary>
/// Handler do Peso production-scoped (corte C, spec PESO.md): resolve o
/// controlo → o contexto de produção (Job On + as Tools canónicas CM/BQ
/// herdadas — o operador NUNCA as escolhe nesta folha), é get-or-open
/// idempotente pelo índice único (um Peso baseline por controlo) e delega
/// a criação no slice tool-scoped canónico (as validações, a densidade da
/// tabela canónica, os congelamentos e a regra Ferramentas de completion
/// são os MESMOS — só a origem das ferramentas muda). O Peso nasce já
/// associado ao controlo (cardinalidade 1:1); num duplo clique, o segundo
/// pedido devolve o Peso que o primeiro gravou.
/// </summary>
public sealed class CreatePesoForControloHandler : ICreatePesoForControloHandler
{
    private readonly DmoDbContext _db;
    private readonly ICreateToolScopedPesoHandler _toolScopedCreate;

    public CreatePesoForControloHandler(DmoDbContext db, ICreateToolScopedPesoHandler toolScopedCreate)
    {
        _db = db;
        _toolScopedCreate = toolScopedCreate;
    }

    public async Task<CreatePesoForControloOutcome> HandleAsync(
        CreatePesoForControlo command,
        CancellationToken cancellationToken = default)
    {
        // 1. O controlo resolve a produção (o contexto estabelecido).
        var production = await _db.Controlos
            .Where(c => c.Id == command.ControloId)
            .Select(c => new
            {
                JobOnId = c.JobOnId,
                CmToolId = _db.CmContexts
                    .Where(ctx => ctx.JobOnId == c.JobOnId)
                    .Select(ctx => (string?)ctx.ToolId)
                    .FirstOrDefault(),
                BqToolId = _db.BqContexts
                    .Where(ctx => ctx.JobOnId == c.JobOnId)
                    .Select(ctx => (string?)ctx.ToolId)
                    .FirstOrDefault()
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (production is null)
        {
            return Refused(CreatePesoForControloResult.ControloNotFound);
        }

        if (production.CmToolId is null)
        {
            return Refused(CreatePesoForControloResult.ContextMissingCm);
        }

        if (production.BqToolId is null)
        {
            return Refused(CreatePesoForControloResult.ContextMissingBq);
        }

        // 2. Get-or-open: o controlo já tem o seu Peso baseline? O MESMO
        //    peso_id é devolvido — a associação é 1:1 e nunca se recria.
        if (await FindExistingAsync(command.ControloId, cancellationToken) is { } existing)
        {
            return existing;
        }

        // 3. A criação delegada no slice tool-scoped canónico (as mesmas
        //    validações e congelamentos), já associada ao controlo: o Peso e
        //    a associação são gravados de uma só vez.
        CreateToolScopedPesoOutcome created;
        try
        {
            created = await _toolScopedCreate.HandleAsync(
                new CreateToolScopedPeso(
                    production.CmToolId,
                    production.BqToolId,
                    command.WaterTemperatureC,
                    command.CmProcessToken,
                    command.CmVolumePuncaoCm3,
                    command.CmNominalWeightGrams,
                    command.BqVolumeMarisaCm3,
                    command.ControloId),
                cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Um duplo clique: o outro pedido gravou primeiro o Peso deste
            // controlo (índice único do ControloId). Devolve esse.
            _db.ChangeTracker.Clear();
            return await FindExistingAsync(command.ControloId, cancellationToken) ?? throw new InvalidOperationException(
                $"Não foi possível criar o Peso do controlo {command.ControloId}.");
        }

        if (created.Result != CreateToolScopedPesoResult.Success || created.Peso is null)
        {
            return Refused(Delegated(created.Result));
        }

        return new CreatePesoForControloOutcome(
            CreatePesoForControloResult.Success,
            created.Peso);
    }

    private async Task<CreatePesoForControloOutcome?> FindExistingAsync(int controloId, CancellationToken cancellationToken)
    {
        var existing = await _db.Pesos
            .Where(p => p.ControloId == controloId)
            .Select(p => new
            {
                PesoId = p.Id,
                p.CmToolId,
                p.BqToolId,
                p.Process,
                p.WaterTemperatureC,
                p.GlassDensityGramsPerCm3,
                p.VolumeMarisaCm3,
                p.VolumePuncaoCm3,
                p.NominalWeightGrams
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (existing is null)
        {
            return null;
        }

        // A densidade da água resolve-se à leitura pela tabela canónica; o
        // grau congelado foi validado na criação.
        var waterDensity = WaterDensityTable.Resolve(existing.WaterTemperatureC)
            ?? throw new InvalidOperationException(
                $"O grau de temperatura da água congelado no Peso {existing.PesoId} " +
                "está fora do intervalo suportado pela tabela canónica de densidade da água.");

        return new CreatePesoForControloOutcome(
            CreatePesoForControloResult.Success,
            new CreatedPeso(
                existing.PesoId,
                existing.CmToolId,
                existing.BqToolId,
                existing.Process,
                existing.WaterTemperatureC,
                waterDensity,
                existing.GlassDensityGramsPerCm3,
                existing.VolumeMarisaCm3,
                existing.VolumePuncaoCm3,
                existing.NominalWeightGrams));
    }

    private static CreatePesoForControloResult Delegated(CreateToolScopedPesoResult result) => result switch
    {
        CreateToolScopedPesoResult.CmToolNotFound => CreatePesoForControloResult.CmToolNotFound,
        CreateToolScopedPesoResult.BqToolNotFound => CreatePesoForControloResult.BqToolNotFound,
        CreateToolScopedPesoResult.MissingCmProcess => CreatePesoForControloResult.MissingCmProcess,
        CreateToolScopedPesoResult.UnknownProcessToken => CreatePesoForControloResult.UnknownProcessToken,
        CreateToolScopedPesoResult.MissingCmVolumePuncao => CreatePesoForControloResult.MissingCmVolumePuncao,
        CreateToolScopedPesoResult.MissingBqVolumeMarisa => CreatePesoForControloResult.MissingBqVolumeMarisa,
        CreateToolScopedPesoResult.GlassDensityNotConfigured => CreatePesoForControloResult.GlassDensityNotConfigured,
        CreateToolScopedPesoResult.InvalidWaterTemperature => CreatePesoForControloResult.InvalidWaterTemperature,
        CreateToolScopedPesoResult.InvalidTechnicalValue => CreatePesoForControloResult.InvalidTechnicalValue,
        _ => CreatePesoForControloResult.ContextMissingCm
    };

    private static CreatePesoForControloOutcome Refused(CreatePesoForControloResult result) =>
        new(result);
}
