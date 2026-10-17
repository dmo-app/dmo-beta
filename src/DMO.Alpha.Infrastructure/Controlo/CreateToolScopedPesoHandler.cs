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

    public async Task<CreateToolScopedPesoOutcome> HandleAsync(
        CreateToolScopedPeso command,
        CancellationToken cancellationToken = default)
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
        var peso = new Peso
        {
            CmToolId = command.CmToolId,
            BqToolId = command.BqToolId,
            Process = CmProcessTokens.ToStorage(process.Value),
            WaterTemperatureC = temperatureWholeDegree,
            VolumeMarisaCm3 = volumeMarisa.Value,
            VolumePuncaoCm3 = volumePuncao.Value,
            NominalWeightGrams = cmTechnical?.NominalWeightGrams,
            GlassDensityGramsPerCm3 = resolvedGlassDensity,
            CmToolReferenceFrozen = cmTool.Reference,
            CmToolLotFrozen = cmTool.Lot,
            BqToolReferenceFrozen = bqTool.Reference,
            BqToolLotFrozen = bqTool.Lot,
            ControloId = command.ControloId
        };
        _db.Pesos.Add(peso);
        await _db.SaveChangesAsync(cancellationToken);

        return new CreateToolScopedPesoOutcome(
            CreateToolScopedPesoResult.Success,
            new CreatedPeso(
                peso.Id,
                peso.CmToolId,
                peso.BqToolId,
                peso.Process,
                peso.WaterTemperatureC,
                resolvedWaterDensity,
                resolvedGlassDensity,
                peso.VolumeMarisaCm3,
                peso.VolumePuncaoCm3,
                peso.NominalWeightGrams));
    }

    private static bool HasCmCompletion(CreateToolScopedPeso command, CmProcess? suppliedProcess) =>
        suppliedProcess is not null
        || command.CmVolumePuncaoCm3 is not null
        || command.CmNominalWeightGrams is not null;

    private static CreateToolScopedPesoOutcome Refused(CreateToolScopedPesoResult result) =>
        new(result);
}
