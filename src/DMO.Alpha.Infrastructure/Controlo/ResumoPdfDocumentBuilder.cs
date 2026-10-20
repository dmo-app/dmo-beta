using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Core.Production;
using DMO.Alpha.Core.Tools;

namespace DMO.Alpha.Infrastructure.Controlo;

/// <summary>O PDF do Resumo feito em memória, com o nome e a pasta da produção.</summary>
public sealed record ResumoPdfDocumento(byte[] Bytes, string FileName, string Folder);

/// <summary>
/// O PDF do Resumo num só sítio (sweep de 19/10/2026, A-4): o «Exportar PDF»
/// do Resumo e o da Revisão fazem o mesmo papel, o Resumo separado ou o
/// documento composto pelos mesmos geradores completos de Peso e Pegamentos.
/// Antes a Revisão criava a página inteira do Resumo para o gerar. Geração em
/// memória: não grava PDFs no servidor nem altera dados persistidos.
/// </summary>
public sealed class ResumoPdfDocumentBuilder(
    IProductionContextQuery productionContext,
    IToolLookupQuery toolLookup,
    IEnsureControloForJobOnHandler ensureControlo,
    IPesoQuery pesoQuery,
    IPegamentosQuery pegamentosQuery,
    PuncoesHandler puncoesHandler,
    ControloViewingHandler viewingHandler,
    ControloApprovalHandler approvalHandler,
    ControloAreaStateHandler areaStateHandler,
    IPesoComparisonQuery pesoComparison,
    IPesoCapSupportQuery capSupport,
    PesoProducaoAnteriorQuery producaoAnterior,
    ResumoPdfGenerator pdfGenerator)
{
    /// <summary>
    /// Quem fez um registo, como no «Visto por»: «Operador 4302» para um
    /// número de operador; o email ou o nome tal como foram guardados.
    /// </summary>
    public static string PersonLabel(string? who) =>
        string.IsNullOrWhiteSpace(who) ? "—"
        : who.All(char.IsDigit) ? $"Operador {who}"
        : who;

    /// <summary>
    /// O PDF do Resumo da produção; com <paramref name="summaryOnly"/>, só o
    /// Resumo. As comparações escolhidas (comparar, comparar_peso) são as do
    /// Peso do cartão. Null quando a produção não existe.
    /// </summary>
    public async Task<ResumoPdfDocumento?> BuildAsync(
        int jobOnId,
        int? previousControlPesoId,
        int? previousPesoId,
        bool summaryOnly,
        string? generatedBy,
        CancellationToken cancellationToken)
    {
        var context = await productionContext.GetAsync(jobOnId, cancellationToken);
        if (context is null)
        {
            return null;
        }

        // AS FERRAMENTAS da combinação (os fatos humanos).
        var tools = new List<ResumoPdfGenerator.ToolCard>();
        if (context.CmToolId is { } cmId)
        {
            var cm = await toolLookup.FindByToolIdAsync(cmId, cancellationToken);
            if (cm is not null) { tools.Add(new ResumoPdfGenerator.ToolCard("CM", cm.Reference, cm.Lot)); }
        }
        if (context.BqToolId is { } bqId)
        {
            var bq = await toolLookup.FindByToolIdAsync(bqId, cancellationToken);
            if (bq is not null) { tools.Add(new ResumoPdfGenerator.ToolCard("BQ", bq.Reference, bq.Lot)); }
        }
        if (context.MfToolId is { } mfId)
        {
            var mf = await toolLookup.FindByToolIdAsync(mfId, cancellationToken);
            if (mf is not null) { tools.Add(new ResumoPdfGenerator.ToolCard("MF", mf.Reference, mf.Lot)); }
        }

        // O ESTADO DOS MÓDULOS: o peso e os pegamentos desta produção (o
        // controlo_id da âncora; o peso pela leitura dele).
        ResumoPdfGenerator.PesoSummary? pesoSummary = null;
        ResumoPdfGenerator.PegamentosSummary? pegamentosSummary = null;
        PesoReadModel? pesoDetalhe = null;
        IReadOnlyList<PesoReadModel> pesos = Array.Empty<PesoReadModel>();
        var pegamentosDetalhes = new List<PegamentosSheetReadModel>();
        PuncoesSheet? puncoesSheet = null;
        var existingControloId = await ensureControlo.FindExistingIdAsync(jobOnId, cancellationToken);
        if (existingControloId is { } controloAnchor)
        {
            // Todos os Pesos, um por CM+BQ (18/10/2026): o cartão mostra o
            // das ferramentas atuais, senão o mais recente, e uma linha por
            // Peso; o PDF composto leva uma secção por Peso. Com um só, como
            // sempre.
            pesos = await pesoQuery.ListByControloAsync(controloAnchor, cancellationToken);
            pesoDetalhe = pesos.FirstOrDefault(p => p.IsCurrent) ?? pesos.LastOrDefault();
            var peso = pesoDetalhe;
            if (peso is not null)
            {
                var varios = pesos.Count > 1;
                pesoSummary = new ResumoPdfGenerator.PesoSummary(
                    peso.Measurements.Count,
                    peso.Averages?.AverageGlassWeightGrams,
                    peso.WaterTemperatureC,
                    peso.NominalWeightGrams)
                {
                    Sheets = !varios ? null : pesos.Select((p, i) => new ResumoPdfGenerator.PesoSheetLine(
                        $"Peso {i + 1} · {ResumoPdfGenerator.ToolsOf(p)}{(p.IsCurrent ? "" : " · de antes da troca")}",
                        p.Measurements.Count,
                        p.Averages?.AverageGlassWeightGrams,
                        p.NominalWeightGrams,
                        ResumoPdfGenerator.PesoSection(i + 1))).ToArray(),
                    Section = ResumoPdfGenerator.PesoSection(varios ? pesos.ToList().FindIndex(p => p.PesoId == peso.PesoId) + 1 : 1)
                };
            }

            // Todas as folhas, cada uma com as suas ferramentas (
            // 18/10/2026): o cartão soma-as e toma o tom da pior; o PDF
            // composto leva uma secção por folha. Sem folhas, a folha da
            // produção como sempre (sem secção).
            var pegamentosSheet = await pegamentosQuery.FindSheetByControloAsync(
                controloAnchor, cancellationToken: cancellationToken);
            if (pegamentosSheet is not null)
            {
                pegamentosDetalhes.AddRange(pegamentosSheet.Sheets.Count == 0
                    ? new[] { pegamentosSheet }
                    : pegamentosSheet.Sheets.Select(folha => pegamentosSheet with
                    {
                        CmToolId = folha.CmToolId,
                        BqToolId = folha.BqToolId,
                        MfToolId = folha.MfToolId,
                        Components = folha.Components,
                        Pegamentos = folha
                    }));
            }
            if (pegamentosSheet is { Sheets.Count: > 0 } comFolhas)
            {
                var registos = comFolhas.Sheets;
                pegamentosSummary = new ResumoPdfGenerator.PegamentosSummary(
                    registos.Sum(r => r.Measurements.Count),
                    registos.Sum(r => r.Measurements.Count(m => m.Evaluation.HasAlert)),
                    registos[0].ToleranceMm,
                    registos.Sum(r => r.Measurements.Count(m => m.Evaluation.IsNotEvaluable)))
                {
                    Sheets = registos.Count < 2 ? null : registos.Select((r, i) => new ResumoPdfGenerator.PegamentosSheetLine(
                        $"Folha {i + 1} · {ResumoPdfGenerator.ToolsOf(r)}",
                        r.Measurements.Count,
                        r.Measurements.Count(m => m.Evaluation.HasAlert),
                        r.Measurements.Count(m => m.Evaluation.IsNotEvaluable),
                        r.ToleranceMm,
                        ResumoPdfGenerator.PegamentosSection(i + 1))).ToArray()
                };
            }

            puncoesSheet = await puncoesHandler.GetSheetAsync(controloAnchor, cancellationToken);
        }

        // Read existing viewings; downloading a PDF must not write to the database.
        var viewings = await viewingHandler.MarkAndGetViewingsAsync(
            existingControloId ?? 0,
            null, // PDF generation reads viewings without recording a new visit.
            cancellationToken);

        // O SELO DE APROVAÇÃO no papel (o corte D): o estado do controlo no
        // Resumo e, na folha do Peso do PDF completo, a decisão do Peso
        // (sweep de 19/10/2026, C-3).
        var approval = existingControloId is { } approvalControloId
            ? await approvalHandler.GetAsync(approvalControloId, cancellationToken)
            : null;
        var pesoApproval = !summaryOnly && existingControloId is { } pesoControloId
            ? await approvalHandler.ParteAsync(pesoControloId, ControloParte.Peso, cancellationToken)
            : null;
        var areaStates = existingControloId is { } areaControloId
            ? await areaStateHandler.GetAsync(areaControloId, cancellationToken)
            : null;

        var fileName = ControloDocumentPaths.GetResumoFileName(context.Reference, context.ProductionNumber);

        // O PDF COMPLETO (Diogo, 19/10/2026: «exportar resumo deveria ser
        // tudo o que está completo»): só entram os Pesos e as folhas de
        // Pegamentos com leituras; os vazios ficam no cartão sem ligação.
        static bool pesoCompleto(PesoReadModel p) => p.Measurements.Count > 0;
        static bool folhaCompleta(PegamentosSheetReadModel f) => f.Pegamentos?.Measurements.Count > 0;
        if (!summaryOnly)
        {
            if (pesoSummary is not null)
            {
                pesoSummary = pesoSummary with
                {
                    Sheets = pesoSummary.Sheets?.Select((linha, i) => pesoCompleto(pesos[i]) ? linha : linha with { Section = null }).ToArray(),
                    Section = pesoDetalhe is not null && pesoCompleto(pesoDetalhe) ? pesoSummary.Section
                        : pesos.Select((p, i) => (p, i + 1)).Where(x => pesoCompleto(x.p))
                            .Select(x => ResumoPdfGenerator.PesoSection(pesos.Count > 1 ? x.Item2 : 1)).FirstOrDefault()
                };
            }
            if (pegamentosSummary is not null)
            {
                pegamentosSummary = pegamentosSummary with
                {
                    Sheets = pegamentosSummary.Sheets?.Select((linha, i) => folhaCompleta(pegamentosDetalhes[i]) ? linha : linha with { Section = null }).ToArray(),
                    Section = pegamentosDetalhes.Select((f, i) => (f, i + 1)).Where(x => folhaCompleta(x.f))
                        .Select(x => ResumoPdfGenerator.PegamentosSection(x.Item2)).FirstOrDefault()
                };
            }
        }
        var pegamentosCompletos = pegamentosDetalhes.Select((f, i) => (f, i + 1)).Where(x => folhaCompleta(x.f)).ToList();

        // O detalhe de um Peso no PDF composto. As comparações escolhidas
        // (comparar, comparar_peso) são as do Peso do cartão.
        async Task<PesoPdfGenerator.Input> PesoDocumentAsync(PesoReadModel detalhe) => PesoPdfGenerator.FromModel(detalhe,
            context.ProductionNumber, context.Reference, context.Machine, "", pesoApproval, generatedBy,
            detalhe.PesoId == pesoDetalhe?.PesoId && previousControlPesoId is { } c ? await pesoComparison.CompareAsync(detalhe.PesoId, c, cancellationToken) : null,
            detalhe.PesoId == pesoDetalhe?.PesoId && previousPesoId is { } p ? await pesoComparison.CompareAsync(detalhe.PesoId, p, cancellationToken) : null,
            context.InicioEm, context.FimEm,
            await capSupport.GetAsync(new PesoCapSupportRequest(detalhe.CmToolId, existingControloId), cancellationToken),
            // A produção anterior com o peso do SAP, a mesma da folha do Peso.
            await producaoAnterior.FindAsync(detalhe.PesoId, cancellationToken));

        // Mais do que um Peso (a CM ou a BQ mudou a meio): uma secção por
        // Peso, cada uma com a sua etiqueta.
        List<PesoPdfGenerator.Input>? pesoDocuments = null;
        if (!summaryOnly && pesos.Count > 1)
        {
            pesoDocuments = new List<PesoPdfGenerator.Input>(pesos.Count);
            foreach (var (detalhe, numero) in pesos.Select((p, i) => (p, i + 1)))
            {
                if (!pesoCompleto(detalhe)) continue;
                pesoDocuments.Add(await PesoDocumentAsync(detalhe) with
                {
                    SectionName = ResumoPdfGenerator.PesoSection(numero),
                    SheetLabel = PesoPdfGenerator.SheetLabelOf(detalhe, pesos)
                });
            }
        }

        var bytes = pdfGenerator.GenerateBytes(new ResumoPdfGenerator.Input(
            context.ProductionNumber,
            context.Reference,
            context.Machine,
            context.InicioEm,
            context.FimEm,
            tools.OrderBy(t => t.Slot == "CM" ? 0 : t.Slot == "MF" ? 1 : 2).ToArray(),
            pesoSummary,
            pegamentosSummary,
            viewings.Select(v => new ResumoPdfGenerator.Viewing(
                v.OperatorId ?? "—",
                v.ViewedAtUtc)).ToList(),
            approval,
            GeneratedBy: generatedBy,
            AreaStates: areaStates?.Select(a => new ResumoPdfGenerator.AreaState(a.Area, a.Status, a.Comment, PersonLabel(a.UpdatedBy), a.UpdatedAtUtc)).ToArray(),
            // O registo das punções NNPB: o cartão quando a CM é NNPB ou já há
            // registo; a folha no fim do PDF único quando há registo.
            Puncoes: puncoesSheet is { } pu && (pu.IsNnpb || pu.Registo is not null)
                ? new ResumoPdfGenerator.PuncoesSummary(pu.Registo?.Total ?? 0, pu.Registo?.PunchSeenCount ?? 0, pu.Registo?.CoolerSeenCount ?? 0)
                : null,
            PuncoesDocument: summaryOnly || puncoesSheet?.Registo is not { } puRegisto ? null
                : PuncoesPdfGenerator.FromRegisto(puncoesSheet, puRegisto, generatedBy),
            PesoDetalhe: summaryOnly || pesoDetalhe is null || !pesoCompleto(pesoDetalhe) ? null : pesoDetalhe,
            PegamentosDetalhe: summaryOnly ? null : pegamentosCompletos.Select(x => x.f).FirstOrDefault(),
            PegamentosDetalhes: summaryOnly ? null : pegamentosCompletos.Select(x => x.f).ToList(),
            PegamentosSecoes: summaryOnly ? null : pegamentosCompletos.Select(x => ResumoPdfGenerator.PegamentosSection(x.Item2)).ToList(),
            PesoDocument: summaryOnly || pesoDetalhe is null || pesoDocuments is not null || !pesoCompleto(pesoDetalhe) ? null : await PesoDocumentAsync(pesoDetalhe),
            PesoDocuments: pesoDocuments,
            PesoApproval: pesoApproval));

        return new ResumoPdfDocumento(
            bytes,
            fileName,
            ControloDocumentPaths.GetProductionRelativeFolder(context.Reference, context.ProductionNumber));
    }
}
