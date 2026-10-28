using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Controlo;

/// <summary>
/// Cria um Peso tool-scoped: uma pesagem real de uma Tool sem produção nem
/// Job On. Nenhuma identidade de produção é inventada — o Peso nasce ancorado
/// às Tools reais (CM pesada + BQ cujo volume_marisa o cálculo consome) e
/// permanece consultável a partir da sua ferramenta. Os valores técnicos
/// Tool-owned EM FALTA fornecidos pelo comando são completados no tool_id
/// owner (regra Ferramentas: gravam no contexto técnico da própria Tool,
/// nunca como facto privado do Peso); valores já registados na Tool nunca
/// são sobrescritos por este caminho (completion, não correção silenciosa).
/// Os inputs consumidos — processo, volumes, nominal opcional, densidade do
/// vidro e o grau inteiro da temperatura da água — são congelados no Peso
/// como evidência histórica no momento da criação, tal como os FATOS HUMANOS
/// das tools usadas (referência + lote congelados: o histórico sobrevive à
/// edição/eliminação da Tool — a reatribuição à DUMMY preserva-os). A linha
/// DUMMY (REMOVED) nunca é utilizável como tool de pesagem (recusa tipada:
/// não é uma Tool real). Todas as recusas são tipadas e verificadas antes
/// de qualquer escrita: uma recusa não deixa valores técnicos completados
/// nem Pesos parcialmente criados.
/// </summary>
public sealed class CreateToolScopedPesoHandler : ICreateToolScopedPesoHandler
{
    private readonly DmoDbContext _db;
    private readonly IGlassDensityConfiguration _glassDensityConfiguration;

    public CreateToolScopedPesoHandler(
        DmoDbContext db,
        IGlassDensityConfiguration glassDensityConfiguration)
    {
        _db = db;
        _glassDensityConfiguration = glassDensityConfiguration;
    }

    // Dois envios iguais (duplo clique, alguém que carrega outra vez por
    // achar que está lento) esperam um pelo outro e o segundo abre o Peso
    // do primeiro em vez de criar outro (varredura de 18/10/2026, B5).
    public Task<CreateToolScopedPesoOutcome> HandleAsync(
        CreateToolScopedPeso command,
        CancellationToken cancellationToken = default) =>
        DbWrite.InTransactionAsync(_db, async () =>
        {
            await DbWrite.LockAsync(_db, "peso:criar", cancellationToken);
            return await HandleLockedAsync(command, cancellationToken);
        }, cancellationToken);

    private async Task<CreateToolScopedPesoOutcome> HandleLockedAsync(
        CreateToolScopedPeso command,
        CancellationToken cancellationToken)
    {
        // Guardas de entrada: um tool_id vazio é um caso de NEGÓCIO (recusa
        // tipada, nunca exceção), consistente com os handlers irmãos.
        if (string.IsNullOrWhiteSpace(command.CmToolId))
        {
            return Refused(CreateToolScopedPesoResult.CmToolNotFound);
        }

        if (string.IsNullOrWhiteSpace(command.BqToolId))
        {
            return Refused(CreateToolScopedPesoResult.BqToolNotFound);
        }

        // Um Peso só para comparação compara um Peso de uma produção; nunca
        // é das duas coisas ao mesmo tempo (o CHECK da base também o diz).
        if (command.ComparacaoDoPesoId is int comparacaoDe
            && (command.ControloId is not null
                || !await _db.Pesos.AnyAsync(p => p.Id == comparacaoDe && p.ControloId != null, cancellationToken)))
        {
            return Refused(CreateToolScopedPesoResult.ComparacaoPesoNotFound);
        }

        // As Tools têm de existir no registo canónico Ferramentas — a
        // linha DUMMY (REMOVED) NÃO é utilizável (excluída por constante:
        // nunca é uma Tool real de pesagem). O Tool.Type nunca é um gate:
        // a CM e a BQ são distinguidas pelos valores técnicos que cada
        // uma fornece ao cálculo. Os factos humanos (referência/lote)
        // são carregados para o congelamento à criação.
        var cmTool = await _db.Tools
            .AsNoTracking()
            .Where(t => t.ToolId == command.CmToolId
                && t.ToolId != ToolRegistryConstants.RemovedToolId)
            .Select(t => new { t.Reference, t.Lot })
            .FirstOrDefaultAsync(cancellationToken);
        if (cmTool is null)
        {
            return Refused(CreateToolScopedPesoResult.CmToolNotFound);
        }

        var bqTool = await _db.Tools
            .AsNoTracking()
            .Where(t => t.ToolId == command.BqToolId
                && t.ToolId != ToolRegistryConstants.RemovedToolId)
            .Select(t => new { t.Reference, t.Lot })
            .FirstOrDefaultAsync(cancellationToken);
        if (bqTool is null)
        {
            return Refused(CreateToolScopedPesoResult.BqToolNotFound);
        }

        // Temperatura da água medida: o lookup usa o grau inteiro
        // arredondado; fora do intervalo suportado 5–35 °C a tabela canónica
        // não resolve a densidade e a criação é recusada tipadamente.
        var temperatureWholeDegree = WaterDensityTable.RoundToWholeDegree(command.WaterTemperatureC);
        var waterDensity = WaterDensityTable.Resolve(temperatureWholeDegree);
        if (waterDensity is not { } resolvedWaterDensity)
        {
            return Refused(CreateToolScopedPesoResult.InvalidWaterTemperature);
        }

        // Contexto técnico das Tools (owner dos valores). Carregado com
        // tracking para a completion; uma Tool sem linha de contexto técnico
        // simplesmente ainda não tem valores registados.
        var cmTechnical = await _db.ToolTechnicalContexts
            .FirstOrDefaultAsync(t => t.ToolId == command.CmToolId, cancellationToken);

        var bqTechnical = await _db.ToolTechnicalContexts
            .FirstOrDefaultAsync(t => t.ToolId == command.BqToolId, cancellationToken);

        // Token do processo fornecido, quando usado, tem de ser um dos
        // autorizados (NNPB, PS) — recusa tipada, nunca exceção.
        CmProcess? suppliedProcess = null;
        if (command.CmProcessToken is { } processToken && processToken.Length > 0)
        {
            if (processToken is not ("NNPB" or "PS"))
            {
                return Refused(CreateToolScopedPesoResult.UnknownProcessToken);
            }

            suppliedProcess = processToken == "NNPB" ? CmProcess.Nnpb : CmProcess.Ps;
        }

        // Completion (apenas valores EM FALTA, no tool_id owner): o comando
        // transporta o que o utilizador completou no fluxo do Peso; um valor
        // que a Tool já tem registado não é substituído por este caminho.
        // A FÍSICA PRIMEIRO (o P2 do stresstest 2026-10-12: o lixo negativo
        // passava calado e era até gravado na técnica da Tool): volumes
        // (cm³) e peso nominal (g) são grandezas POSITIVAS — zero ou
        // negativo é input torto, recusado tipadamente ANTES de qualquer
        // escrita (nunca completado, nunca congelado).
        if (command.CmVolumePuncaoCm3 is <= 0
            || command.BqVolumeMarisaCm3 is <= 0
            || command.CmNominalWeightGrams is <= 0)
        {
            return Refused(CreateToolScopedPesoResult.InvalidTechnicalValue);
        }

        if (cmTechnical is null && HasCmCompletion(command, suppliedProcess))
        {
            cmTechnical = new ToolTechnicalContext { ToolId = command.CmToolId };
            _db.ToolTechnicalContexts.Add(cmTechnical);
        }

        if (cmTechnical is not null)
        {
            cmTechnical.Process ??= suppliedProcess;
            cmTechnical.VolumePuncaoCm3 ??= command.CmVolumePuncaoCm3;
            cmTechnical.NominalWeightGrams ??= command.CmNominalWeightGrams;
        }

        if (bqTechnical is null && command.BqVolumeMarisaCm3 is not null)
        {
            bqTechnical = new ToolTechnicalContext { ToolId = command.BqToolId };
            _db.ToolTechnicalContexts.Add(bqTechnical);
        }

        if (bqTechnical is not null)
        {
            bqTechnical.VolumeMarisaCm3 ??= command.BqVolumeMarisaCm3;
        }

        // Valores finais consumidos: o registo técnico da Tool depois da
        // completion. Os termos estruturais do cálculo principal têm de
        // existir (processo resolve a densidade do vidro; volume_puncao e
        // volume_marisa são termos da fórmula); o peso nominal é opcional.
        var process = cmTechnical?.Process;
        if (process is null)
        {
            return Refused(CreateToolScopedPesoResult.MissingCmProcess);
        }

        var volumePuncao = cmTechnical?.VolumePuncaoCm3;
        if (volumePuncao is null)
        {
            return Refused(CreateToolScopedPesoResult.MissingCmVolumePuncao);
        }

        var volumeMarisa = bqTechnical?.VolumeMarisaCm3;
        if (volumeMarisa is null)
        {
            return Refused(CreateToolScopedPesoResult.MissingBqVolumeMarisa);
        }

        // Densidade do vidro: configuração do Controlo por processo. Sem
        // valor configurado a criação é recusada tipadamente — o cálculo
        // principal não fecha sem ela e nenhum valor é inventado.
        var glassDensity = await _glassDensityConfiguration.FindByProcessAsync(
            process.Value,
            cancellationToken);
        if (glassDensity is not { } resolvedGlassDensity)
        {
            return Refused(CreateToolScopedPesoResult.GlassDensityNotConfigured);
        }

        // Novo peso_id durável, ancorado às Tools reais, com os inputs
        // consumidos congelados como evidência histórica — incluindo os
        // FATOS HUMANOS das tools (referência + lote): o histórico do Peso
        // mostra-os mesmo depois de a Tool ser editada ou eliminada. Nenhum
        // jobon_id/controlo_id/cm_id é criado ou exigido.
        // Um Peso que já é o mesmo e ainda está vazio é aberto em vez de
        // criar outro: o da produção com esta CM e esta BQ (um por
        // combinação; respostas R15, R16-2 e R16-5 do especialista) ou um sem
        // produção com as mesmas ferramentas e os mesmos valores.
        var storedProcess = CmProcessTokens.ToStorage(process.Value);
        var nominalWeight = cmTechnical?.NominalWeightGrams;
        var existing = command.ControloId is int controloId
            ? await _db.Pesos.AsNoTracking()
                .Where(p => p.ControloId == controloId
                    && p.CmToolId == command.CmToolId
                    && p.BqToolId == command.BqToolId)
                .OrderBy(p => p.Id)
                .FirstOrDefaultAsync(cancellationToken)
            : await _db.Pesos.AsNoTracking().FirstOrDefaultAsync(p =>
                p.ControloId == null
                && p.ComparacaoDoPesoId == command.ComparacaoDoPesoId
                && p.CmToolId == command.CmToolId
                && p.BqToolId == command.BqToolId
                && p.Process == storedProcess
                && p.WaterTemperatureC == temperatureWholeDegree
                && p.VolumeMarisaCm3 == volumeMarisa.Value
                && p.VolumePuncaoCm3 == volumePuncao.Value
                && p.NominalWeightGrams == nominalWeight
                && p.GlassDensityGramsPerCm3 == resolvedGlassDensity
                && !p.Measurements.Any(), cancellationToken);
        if (existing is not null)
        {
            await _db.SaveChangesAsync(cancellationToken);
            return Created(existing, WaterDensityTable.ForPeso(existing.WaterDensityGramsPerCm3, existing.WaterTemperatureC) ?? resolvedWaterDensity);
        }

        // O Peso novo de uma produção que já tinha outro (a CM ou a BQ mudou
        // a meio): a produção anterior e o peso do SAP vêm do Peso mais
        // recente dela, e continuam editáveis neste (resposta R16-7 do
        // especialista, 18/10/2026).
        var anterior = command.ControloId is int controloDoPeso
            ? await _db.Pesos.AsNoTracking()
                .Where(p => p.ControloId == controloDoPeso)
                .OrderByDescending(p => p.Id)
                .Select(p => new { p.ProducaoAnteriorControloId, p.ProducaoAnteriorPesoGramas })
                .FirstOrDefaultAsync(cancellationToken)
            : null;

        // O TAMPÃO congela-se com a punção (Diogo, 23/10/2026): o do Job On do
        // controlo; sem Job On (ou sem valor), o que a CM ainda tem de arranque.
        var tampaoJobOn = command.ControloId is int controloDoTampao
            ? await _db.Controlos
                .AsNoTracking()
                .Where(c => c.Id == controloDoTampao)
                .Select(c => new
                {
                    c.JobOn.TampaoDiametroMm,
                    c.JobOn.CaloteMm,
                    Folha = _db.JobOnFolhas.Where(f => f.JobOnId == c.JobOnId).Select(f => new { f.TpDiametroPs, f.TpBaciaPs }).FirstOrDefault()
                })
                .FirstOrDefaultAsync(cancellationToken)
            : null;

        var peso = new Peso
        {
            // Sem o Tampão gravado no Job On, o da folha (TampaoDaFolha).
            TampaoDiametroMm = TampaoDaFolha.Diametro(tampaoJobOn?.TampaoDiametroMm, tampaoJobOn?.Folha?.TpDiametroPs) ?? cmTechnical?.TpDiametroMm,
            TampaoCaloteMm = TampaoDaFolha.Calote(tampaoJobOn?.CaloteMm, tampaoJobOn?.Folha?.TpBaciaPs) ?? cmTechnical?.TpSagittaMm,
            CmToolId = command.CmToolId,
            BqToolId = command.BqToolId,
            Process = storedProcess,
            WaterTemperatureC = temperatureWholeDegree,
            WaterDensityGramsPerCm3 = resolvedWaterDensity,
            VolumeMarisaCm3 = volumeMarisa.Value,
            VolumePuncaoCm3 = volumePuncao.Value,
            NominalWeightGrams = nominalWeight,
            GlassDensityGramsPerCm3 = resolvedGlassDensity,
            CmToolReferenceFrozen = cmTool.Reference,
            CmToolLotFrozen = cmTool.Lot,
            BqToolReferenceFrozen = bqTool.Reference,
            BqToolLotFrozen = bqTool.Lot,
            ControloId = command.ControloId,
            ComparacaoDoPesoId = command.ComparacaoDoPesoId,
            ProducaoAnteriorControloId = anterior?.ProducaoAnteriorControloId,
            ProducaoAnteriorPesoGramas = anterior?.ProducaoAnteriorPesoGramas
        };
        _db.Pesos.Add(peso);
        await _db.SaveChangesAsync(cancellationToken);

        return Created(peso, resolvedWaterDensity);
    }

    private static CreateToolScopedPesoOutcome Created(Peso peso, decimal waterDensity) =>
        new(
            CreateToolScopedPesoResult.Success,
            new CreatedPeso(
                peso.Id,
                peso.CmToolId,
                peso.BqToolId,
                peso.Process,
                peso.WaterTemperatureC,
                waterDensity,
                peso.GlassDensityGramsPerCm3,
                peso.VolumeMarisaCm3,
                peso.VolumePuncaoCm3,
                peso.NominalWeightGrams));

    private static bool HasCmCompletion(CreateToolScopedPeso command, CmProcess? suppliedProcess) =>
        suppliedProcess is not null
        || command.CmVolumePuncaoCm3 is not null
        || command.CmNominalWeightGrams is not null;

    private static CreateToolScopedPesoOutcome Refused(CreateToolScopedPesoResult result) =>
        new(result);
}
