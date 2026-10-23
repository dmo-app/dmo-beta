using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Core.Production;

namespace DMO.Alpha.Infrastructure.Controlo;

/// <summary>O PDF do Peso feito em memória, com o nome e a pasta da produção.</summary>
public sealed record PesoPdfDocumento(
    byte[] Bytes,
    string FileName,
    string Folder,
    string Reference,
    string ProductionNumber,
    string Machine,
    int PesoId,
    int? ControloId,
    decimal? PesoGramas = null);

/// <summary>
/// O PDF da folha do Peso num só sítio: o «Exportar PDF» do Peso e o
/// «Enviar» do Resumo (Diogo, 19/10/2026, D9) fazem o mesmo papel. Geração
/// em memória: não grava PDFs no servidor nem altera dados persistidos.
/// </summary>
public sealed class PesoPdfDocumentBuilder(
    IPesoQuery pesoQuery,
    IProductionContextQuery productionContext,
    IPesoComparisonQuery pesoComparison,
    ControloApprovalHandler approvalHandler,
    IPesoCapSupportQuery capSupport,
    PesoProducaoAnteriorQuery producaoAnterior,
    PesoPdfGenerator pdfGenerator)
{
    /// <summary>
    /// O PDF do Peso da folha da produção (controloId: o pedido, se é desta
    /// produção; senão o das ferramentas atuais, senão o mais recente) ou de
    /// uma pesagem do histórico (só pesoId). Null quando não há Peso.
    /// </summary>
    public async Task<PesoPdfDocumento?> BuildAsync(
        int? controloId,
        int? pesoId,
        int? compararPesoId,
        int? pesoAnteriorId,
        string? generatedBy,
        CancellationToken cancellationToken)
    {
        PesoReadModel? peso;
        ProductionContextReadModel? context;
        IReadOnlyList<PesoReadModel> pesosDaProducao = Array.Empty<PesoReadModel>();
        if (controloId is int canonicalControloId)
        {
            // O Peso da folha (18/10/2026): o enviado, se é desta produção;
            // senão o das ferramentas atuais, senão o mais recente.
            pesosDaProducao = await pesoQuery.ListByControloAsync(canonicalControloId, cancellationToken);
            peso = pesosDaProducao.FirstOrDefault(p => p.PesoId == pesoId)
                ?? pesosDaProducao.FirstOrDefault(p => p.IsCurrent)
                ?? pesosDaProducao.LastOrDefault();
            if (peso is null)
            {
                return null;
            }

            context = await productionContext.GetByControloAsync(canonicalControloId, cancellationToken);
        }
        else if (pesoId is int canonicalPesoId)
        {
            // O histórico: o PDF da folha de uma pesagem passada (reimprimir
            // o papel que a produção pede outra vez). O contexto vem do
            // controlo do próprio peso quando ele é production-scoped.
            peso = await pesoQuery.FindPesoAsync(canonicalPesoId, cancellationToken);
            if (peso is null)
            {
                return null;
            }

            context = peso.ControloId is { } pesoControloId
                ? await productionContext.GetByControloAsync(pesoControloId, cancellationToken)
                : null;
            if (peso.ControloId is { } controloDoPeso)
            {
                pesosDaProducao = await pesoQuery.ListByControloAsync(controloDoPeso, cancellationToken);
            }
        }
        else
        {
            return null;
        }

        PesoComparisonView? comparison = null;
        if (compararPesoId is int historicalPesoId && historicalPesoId != peso.PesoId)
        {
            comparison = await pesoComparison.CompareAsync(peso.PesoId, historicalPesoId, cancellationToken);
        }

        PesoComparisonView? previousPesoComparison = null;
        if (pesoAnteriorId is int selectedPesoId && selectedPesoId != peso.PesoId)
        {
            previousPesoComparison = await pesoComparison.CompareAsync(peso.PesoId, selectedPesoId, cancellationToken);
        }

        var reference = context?.Reference ?? peso.CmToolReference ?? peso.CmToolId;
        // Uma pesagem antes da produção (tool-scoped) descarrega como
        // «sem-producao» — o nome é honesto, nunca um travessão.
        var productionNumber = context?.ProductionNumber ?? "sem-producao";
        // Mais do que um Peso na produção (a CM ou a BQ mudou a meio): o
        // segundo em diante leva «-2», «-3»… (resposta R16-9 do especialista).
        var pesoNumber = pesosDaProducao.ToList().FindIndex(p => p.PesoId == peso.PesoId) + 1;
        var fileName = pesosDaProducao.Count > 1 && pesoNumber > 0
            ? ControloDocumentPaths.GetPesoFileName(reference, productionNumber, pesoNumber)
            : ControloDocumentPaths.GetPesoFileName(reference, productionNumber);

        var readings = peso.Measurements
            .Select(m => new PesoPdfGenerator.Reading(
                m.RowPosition,
                m.CmSubject,
                m.WaterWeightGrams,
                m.CapacityCm3,
                m.GlassWeightGrams))
            .ToList();

        // O SELO DE APROVAÇÃO no papel (o corte D — o alvo do peso real
        // tem o bloco «Aprovado por»): a decisão do Peso no controlo do
        // contexto (sweep de 19/10/2026, C-3); um peso tool-scoped (sem
        // controlo) simplesmente não tem selo.
        var approvalControloId = controloId ?? peso.ControloId;
        var approval = approvalControloId is { } decisaoControloId
            ? await approvalHandler.PesoAsync(decisaoControloId, peso.PesoId, cancellationToken)
            : null;

        // O volume do tampão desta produção (a calote do Job On; sem controlo, a referência da CM).
        var cap = await capSupport.GetAsync(new PesoCapSupportRequest(peso.CmToolId, approvalControloId), cancellationToken);

        var machine = context?.Machine ?? "—";
        var bytes = pdfGenerator.GenerateBytes(new PesoPdfGenerator.Input(
            productionNumber,
            reference,
            machine,
            peso.CmToolId,
            peso.CmToolReference,
            peso.CmToolLot,
            peso.Process.ToString().ToUpperInvariant(),
            peso.WaterTemperatureC,
            peso.WaterDensityGramsPerCm3,
            peso.GlassDensityGramsPerCm3,
            peso.VolumeMarisaCm3,
            peso.VolumePuncaoCm3,
            peso.NominalWeightGrams,
            readings,
            peso.Averages,
            comparison,
            "",
            Approval: approval,
            GeneratedBy: generatedBy,
            PreviousPesoComparison: previousPesoComparison,
            InicioEm: context?.InicioEm, FimEm: context?.FimEm,
            VolumeTampaoCm3: cap.VolumeEfetivoCm3, CaloteMm: cap.AlturaEfetivaMm,
            ProducaoAnterior: await producaoAnterior.FindAsync(peso.PesoId, cancellationToken))
        {
            SheetLabel = PesoPdfGenerator.SheetLabelOf(peso, pesosDaProducao)
        });

        return new PesoPdfDocumento(
            bytes,
            fileName,
            ControloDocumentPaths.GetProductionRelativeFolder(reference, productionNumber),
            reference,
            productionNumber,
            machine,
            peso.PesoId,
            approvalControloId,
            peso.Averages?.AverageGlassWeightGrams);
    }
}
