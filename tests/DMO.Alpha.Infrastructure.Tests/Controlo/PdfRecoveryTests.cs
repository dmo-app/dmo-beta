using System.Xml.Linq;
using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Infrastructure.Controlo;

namespace DMO.Alpha.Infrastructure.Tests.Controlo;

public sealed class PdfRecoveryTests
{
    public static PegamentosSheetReadModel ReferenceSheet()
    {
        var rows = new List<PegamentosMeasurementReadModel>();
        void Add(PegamentosComponent component, decimal nominal, params (int Number, decimal Seam, decimal Across)[] values)
        {
            foreach (var v in values) rows.Add(new(rows.Count + 1, v.Number, component, v.Seam, v.Across,
                nominal, PegamentosCalculation.EvaluateRow(nominal, .20m, v.Seam, v.Across)));
        }
        Add(PegamentosComponent.Cm, 34.50m, (72,34.55m,34.50m), (27,34.70m,34.51m), (44,34.54m,34.51m));
        Add(PegamentosComponent.Bq, 34.70m, (59,34.70m,34.70m), (19,34.73m,34.72m), (63,34.70m,34.71m), (80,34.71m,34.70m));
        Add(PegamentosComponent.Mf, 34.90m, (33,34.94m,35.01m), (37,34.92m,34.80m), (29,35.03m,34.95m));
        var averages = rows.GroupBy(r => r.Component).Select(g => new PegamentosComponentAveragesReadModel(g.Key,
            g.Count(), g.Count(r => r.Evaluation.HasAlert), g.Count(r => r.Evaluation.IsNotEvaluable),
            g.Average(r => r.CosturaMm), g.Average(r => r.ContraCosturaMm), g.Average(r => r.Evaluation.OvalizacaoMm),
            g.Average(r => r.Evaluation.MediaMm))).ToArray();
        return new(1,1,"5447T173","202604","B1",1,"cm",2,"mf",3,"bq",
            [new(PegamentosComponent.Cm,"cm","5447","11",34.50m), new(PegamentosComponent.Bq,"bq","T173","33",34.70m),
             new(PegamentosComponent.Mf,"mf","5447","11",34.90m)], .20m,
            new(1,1,.20m,"cm","5447","11","bq","T173","33","mf","5447","11",rows,averages));
    }

    [Theory]
    [InlineData("34.70","34.51",true)]
    [InlineData("34.71","34.40",true)]
    [InlineData("34.40","34.71",true)]
    [InlineData("34.55","34.50",false)]
    public void IndividualAlertsSurviveMeanInsideCorridor(string seam, string across, bool alert)
    {
        var e = PegamentosCalculation.EvaluateRow(34.50m,.20m,
            decimal.Parse(seam,System.Globalization.CultureInfo.InvariantCulture),
            decimal.Parse(across,System.Globalization.CultureInfo.InvariantCulture));
        Assert.True(e.MediaMm > e.CorridorLowerMm && e.MediaMm < e.CorridorUpperMm);
        Assert.Equal(alert,e.HasAlert);
    }

    [Fact]
    public void MapHasEveryComponentAndHistoricalCorridorsAndCoreAlert()
    {
        var sheet = ReferenceSheet();
        var input = PegamentosPdfGenerator.FromSheet(sheet);
        var svg = XDocument.Parse(PegamentosLimitMap.Render(input));
        XNamespace ns="http://www.w3.org/2000/svg";
        Assert.Equal(10,svg.Descendants(ns+"circle").Count());
        Assert.Single(svg.Descendants(ns+"circle"), c => (string?)c.Attribute("fill")=="#b91c1c");
        Assert.Equal(3,svg.Descendants(ns+"polygon").Count());
        // Só os 3 corredores: os valores dos losangos não têm fundo (20/10/2026).
        Assert.Equal(3,svg.Descendants(ns+"rect").Count());
        var changed = sheet with { Components = sheet.Components.Select(c => c with { DiametroGargaloMm=99m }).ToArray() };
        Assert.Equal(PegamentosLimitMap.Render(input),PegamentosLimitMap.Render(PegamentosPdfGenerator.FromSheet(changed)));
        Assert.Single(input.Tables.Single(t=>t.Component=="CM").Rows,r=>r.IsAlert);
    }

    // CM e BQ com o mesmo nominal partilham um rótulo, em vez de ficarem
    // escritos um por cima do outro (Diogo, 20/10/2026).
    [Fact]
    public void MapaJuntaOsRotulosDeNominaisIguais()
    {
        var sheet = ReferenceSheet();
        var input = PegamentosPdfGenerator.FromSheet(sheet);
        var cm = input.Tables.Single(t => t.Component == "CM").Rows.First(r => r.NominalMm is not null).NominalMm!.Value;
        var igual = input with { Tables = input.Tables.Select(t => t with { Rows = t.Rows.Select(r => r.NominalMm is null ? r
            : r with { NominalMm = t.Component == "BQ" ? cm : r.NominalMm, LowerMm = t.Component == "BQ" ? cm - .2m : r.LowerMm, UpperMm = t.Component == "BQ" ? cm + .2m : r.UpperMm }).ToArray() }).ToArray() };
        var svg = PegamentosLimitMap.Render(igual);
        var valor = cm.ToString("0.00", DMO.Alpha.Core.Runtime.DmoNumberFormat.Culture) + " mm";
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(svg, ">" + System.Text.RegularExpressions.Regex.Escape(valor) + "</text>"));
        Assert.Matches("'>CM</tspan><tspan fill='#[0-9a-fA-F]{6}'> · </tspan><tspan fill='#a855f7'>BQ</tspan>", svg);
        Assert.Contains(">MF</tspan></text>", svg);
        // Sem fundo atrás dos números (Diogo, 20/10/2026): os únicos
        // retângulos são os corredores.
        Assert.Equal(System.Text.RegularExpressions.Regex.Matches(svg, "<rect ").Count, System.Text.RegularExpressions.Regex.Matches(svg, "fill-opacity='0.12'").Count);
    }

    // Como o relatório original (Diogo, 19/10/2026: «melhorar o aspecto do
    // gráfico»): os limites de fora tracejados, com «CM − 0.20» e «MF + 0.20».
    [Fact]
    public void MapaMostraOsLimitesDeForaTracejadosComoOOriginal()
    {
        var svg = PegamentosLimitMap.Render(PegamentosPdfGenerator.FromSheet(ReferenceSheet()));
        System.IO.File.WriteAllText(Environment.GetEnvironmentVariable("DMO_MAPA_SVG") ?? "/dev/null", svg);
        Assert.Equal(2, System.Text.RegularExpressions.Regex.Matches(svg, "stroke-dasharray").Count);
        Assert.Contains(">CM − 0.20</text>", svg);
        Assert.Contains(">MF + 0.20</text>", svg);
    }

    [Fact]
    public void UnmeasuredComponentRemainsVisibleAndUnknownNominalDoesNotBecomeGreen()
    {
        var sheet=ReferenceSheet();
        var record=sheet.Pegamentos! with { Measurements=sheet.Pegamentos!.Measurements.Where(m=>m.Component!=PegamentosComponent.Mf).ToArray(),
            ComponentAverages=sheet.Pegamentos.ComponentAverages.Where(a=>a.Component!=PegamentosComponent.Mf).ToArray() };
        var input=PegamentosPdfGenerator.FromSheet(sheet with { Pegamentos=record });
        Assert.Equal(3,input.Components.Count);
        Assert.Empty(input.Tables.Single(t=>t.Component=="MF").Rows);
        Assert.Contains("Sem medições",PegamentosLimitMap.Render(input));
        var unknown=record.Measurements[0] with { NominalGargaloMm=null, Evaluation=PegamentosCalculation.EvaluateRow(null,.20m,34.55m,34.50m) };
        input=PegamentosPdfGenerator.FromSheet(sheet with { Pegamentos=record with { Measurements=[unknown] } });
        Assert.Null(input.Components.Single(c=>c.Component=="CM").NominalGargaloMm);
        Assert.Contains("#94a3b8",PegamentosLimitMap.Render(input));
    }

    [Fact]
    public void BlankRows_OnlyOnTheSeparateBlankSheet_PartialRecordAndResumoUnchanged()
    {
        var sheet=ReferenceSheet();
        // Registo com medições de CM e BQ, MF sem medições: como antes,
        // sem linhas vazias e com «Sem medições» no título.
        var record=sheet.Pegamentos! with { Measurements=sheet.Pegamentos!.Measurements.Where(m=>m.Component!=PegamentosComponent.Mf).ToArray(),
            ComponentAverages=sheet.Pegamentos.ComponentAverages.Where(a=>a.Component!=PegamentosComponent.Mf).ToArray() };
        var partial=PegamentosPdfGenerator.FromSheet(sheet with { Pegamentos=record });
        Assert.False(partial.BlankForHandFilling);
        var mf=partial.Tables.Single(t=>t.Component=="MF");
        Assert.Equal(0,PegamentosPdfGenerator.BlankRowsFor(partial,mf));
        Assert.EndsWith("· Sem medições",PegamentosPdfGenerator.TableTitle(partial,mf));
        Assert.Contains("Nominal 34.50 mm",PegamentosPdfGenerator.TableTitle(partial,partial.Tables.Single(t=>t.Component=="CM")));

        // Sem registo, pelo caminho do Resumo combinado (FromSheet sem a
        // folha em branco): também como antes.
        var resumo=PegamentosPdfGenerator.FromSheet(sheet with { Pegamentos=null });
        Assert.False(resumo.BlankForHandFilling);
        Assert.All(resumo.Tables,t=>
        {
            Assert.Equal(0,PegamentosPdfGenerator.BlankRowsFor(resumo,t));
            Assert.EndsWith("· Sem medições",PegamentosPdfGenerator.TableTitle(resumo,t));
        });
        Assert.StartsWith("%PDF",System.Text.Encoding.ASCII.GetString(new PegamentosPdfGenerator().GenerateBytes(resumo),0,4));

        // A folha em branco do download separado: 6 linhas vazias por
        // componente e o nominal com o corredor no título.
        var blank=resumo with { BlankForHandFilling=true };
        Assert.All(blank.Tables,t=>
        {
            Assert.Equal(PegamentosPdfGenerator.BlankRowsPerComponent,PegamentosPdfGenerator.BlankRowsFor(blank,t));
            Assert.Contains("Corredor",PegamentosPdfGenerator.TableTitle(blank,t));
        });
        Assert.Contains("Nominal 34.50 mm · Corredor 34.30–34.70 mm",PegamentosPdfGenerator.TableTitle(blank,blank.Tables.Single(t=>t.Component=="CM")));
        Assert.StartsWith("%PDF",System.Text.Encoding.ASCII.GetString(new PegamentosPdfGenerator().GenerateBytes(blank),0,4));
    }

    [Fact]
    public void GenerateCompleteVisualEvidence()
    {
        var root=Environment.GetEnvironmentVariable("DMO_PDF_EVIDENCE_DIR") ?? Path.Combine(Path.GetTempPath(),"dmo-pdf-evidence-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var peg=PegamentosPdfGenerator.FromSheet(ReferenceSheet(),generatedBy:"Operador de teste");
        var readings=new[] {
            new PesoPdfGenerator.Reading(1,"61",71.10m,71.38m,142.41m), new(2,"95",71.40m,71.68m,143.16m),
            new(3,"63",71.20m,71.48m,142.66m), new(4,"36",71.80m,72.09m,144.17m) };
        var average=new PesoAveragesReadModel(71.375m,71.6575m,143.10m);
        PesoComparisonView Comparison(int id, decimal offset) => new(1,id,"202601","B1",average,
            new(71.60m,71.87m,143.10m+offset),new(-.225m,-.2125m,-offset,-offset/(143.10m+offset)*100),
            readings.Select(r=>new PesoComparisonRow(r.Position,r.CmSubject,r.GlassWeightGrams,r.CmSubject,
                r.GlassWeightGrams+offset,r.CapacityCm3,r.CapacityCm3+.2125m,-offset,-.2125m,-offset/(r.GlassWeightGrams+offset)*100)).ToArray(),
            new(2026,3,5),new(2026,3,8),4);
        var peso=new PesoPdfGenerator.Input("202602","1075C142","B1","cm","ST100","8","NNPB",25,.99603m,2.5m,10m,24.42m,138m,
            readings,average,Comparison(2,.53m),"",GeneratedBy:"Operador de teste",PreviousPesoComparison:Comparison(3,1.90m),InicioEm:new(2026,10,10),VolumeTampaoCm3:3.27m,CaloteMm:4.10m);
        var resumo=new ResumoPdfGenerator.Input("202604","5447T173","B1",new(2026,7,20),new(2026,7,24),
            [new("CM","5447","11"),new("MF","5447","11"),new("BQ","T173","33")],new(4,143.10m,25,138m),new(10,1,.20m),
            [new("123",new(2026,7,24,12,0,0,DateTimeKind.Utc))],null,GeneratedBy:"Operador de teste",AreaStates:[new("CM","OK",null),new("BQ","NOK","Boquilha com marca na zona do gargalo; trocada no turno seguinte."),new("MF","OK",null)],PesoDocument:peso with { ProductionNumber="202604",Reference="5447T173" }, PegamentosDocument:peg);
        var pegamentosPdf=new PegamentosPdfGenerator().GenerateBytes(peg);
        File.WriteAllBytes(Path.Combine(root,"pegamentos.pdf"),pegamentosPdf);
        var pegamentosText=PdfText.Of(pegamentosPdf);
        Assert.Contains("34.55 mm 34.71 mm 34.94 mm",pegamentosText);
        Assert.Contains("Nominal 34.50 mm · Corredor 34.30–34.70 mm",pegamentosText);
        Assert.Contains("72 34.55 34.50 +0.05 34.53",pegamentosText);
        Assert.Contains("AVG 34.96 34.92 +0.04 34.94",pegamentosText);
        Assert.Contains("Existem medições que atingem ou ultrapassam os limites de tolerância",pegamentosText);
        foreach(var scenario in new[] { "inside", "seam-outside", "across-outside", "without-mf", "historical", "unknown" })
        {
            var sheet=ReferenceSheet();
            var rows=sheet.Pegamentos!.Measurements.Select(m =>
            {
                var seam=m.CosturaMm; var across=m.ContraCosturaMm;
                if(m.Component==PegamentosComponent.Cm && m.RowPosition==27)
                {
                    seam=scenario=="seam-outside" ? 34.71m : 34.69m;
                    across=scenario=="across-outside" ? 34.71m : scenario=="seam-outside" ? 34.40m : 34.51m;
                }
                var nominal=scenario=="unknown" ? null : m.NominalGargaloMm;
                return m with { CosturaMm=seam,ContraCosturaMm=across,NominalGargaloMm=nominal,
                    Evaluation=PegamentosCalculation.EvaluateRow(nominal,.20m,seam,across) };
            }).Where(m=>scenario!="without-mf" || m.Component!=PegamentosComponent.Mf).ToArray();
            var averages=rows.GroupBy(m=>m.Component).Select(g=>new PegamentosComponentAveragesReadModel(g.Key,g.Count(),
                g.Count(m=>m.Evaluation.HasAlert),g.Count(m=>m.Evaluation.IsNotEvaluable),g.Average(m=>m.CosturaMm),
                g.Average(m=>m.ContraCosturaMm),g.Average(m=>m.Evaluation.OvalizacaoMm),g.Average(m=>m.Evaluation.MediaMm))).ToArray();
            sheet=sheet with { Pegamentos=sheet.Pegamentos with { Measurements=rows,ComponentAverages=averages } };
            if(scenario=="historical") sheet=sheet with { Components=sheet.Components.Select(c=>c with { DiametroGargaloMm=40m }).ToArray() };
            File.WriteAllBytes(Path.Combine(root,"pegamentos-"+scenario+".pdf"),new PegamentosPdfGenerator().GenerateBytes(PegamentosPdfGenerator.FromSheet(sheet)));
        }
        File.WriteAllBytes(Path.Combine(root,"peso.pdf"),new PesoPdfGenerator().GenerateBytes(peso));
        var resumoPdf=new ResumoPdfGenerator().GenerateBytes(resumo);
        File.WriteAllBytes(Path.Combine(root,"resumo-completo.pdf"),resumoPdf);
        var resumoText=PdfText.Of(resumoPdf);
        Assert.Contains("Resumo do Controlo 5447T173 · 202604 · B1",resumoText);
        Assert.Contains("Existem medições fora das tolerâncias",resumoText);
        Assert.Contains("143.10 g",resumoText);
        Assert.Contains("Peso médio do vidro",resumoText);
        Assert.Contains("Nominal 138.00 g · +5.10 g (+3.70 %)",resumoText);
        Assert.Contains("não registado não registado OK NOK OK",resumoText);
        Assert.Contains("Boquilha com marca na zona do gargalo; trocada no turno seguinte.",resumoText);
        Assert.Contains("AVG 34.60 34.51 +0.09 34.55",resumoText);
        // O Peso cabe numa página desde que as duas tabelas são uma (20/10/2026).
        Assert.Contains("1 / 3",resumoText);
        // A standalone summary is also available without attached modules.
        var standalonePdf=new ResumoPdfGenerator().GenerateBytes(resumo with { PesoDocument=null,PegamentosDocument=null });
        File.WriteAllBytes(Path.Combine(root,"resumo.pdf"),standalonePdf);
        var standaloneText=PdfText.Of(standalonePdf);
        Assert.Contains("Resumo do Controlo 5447T173 · 202604 · B1",standaloneText);
        Assert.DoesNotContain("AVG 34.60 34.51 +0.09 34.55",standaloneText);
        if(Environment.GetEnvironmentVariable("DMO_PDF_EVIDENCE_DIR")==null) Directory.Delete(root,true);
    }

    [Fact]
    public void SummaryPdfRendersEmptyAndApprovedProductions()
    {
        var empty=new ResumoPdfGenerator.Input("202605","5447T173","B1",null,null,[],null,null,[],null);
        var emptyText=PdfText.Of(new ResumoPdfGenerator().GenerateBytes(empty));
        Assert.Contains("Sem registo Janela não definida",emptyText);
        Assert.Contains("Por aprovar",emptyText);
        Assert.Contains("Sem leituras registadas",emptyText);
        var approved=empty with { InicioEm=new(2026,7,20), Peso=new(4,143.10m,25), Pegamentos=new(10,0,.20m,2),
            Approval=new ControloApprovalReadModel("123",new DateTime(2026,7,24,12,0,0,DateTimeKind.Utc)) };
        var approvedText=PdfText.Of(new ResumoPdfGenerator().GenerateBytes(approved));
        Assert.Contains("Aprovado",approvedText);
        Assert.Contains("Operador 123 · 24/07/2026 13:00",approvedText);
        Assert.Contains("Existem medições não avaliáveis",approvedText);
        Assert.DoesNotContain("Por aprovar",approvedText);
    }

    [Fact]
    public void NominalWeightDeviationDoesNotInventMissingOrZeroReference()
    {
        Assert.Equal((5.10m,(decimal?)(5.10m/138m*100)),PesoCalculation.NominalDeviation(143.10m,138m));
        Assert.Equal(((decimal?)null,(decimal?)null),PesoCalculation.NominalDeviation(143.10m,null));
        Assert.Null(PesoCalculation.NominalDeviation(143.10m,0).Percent);
    }
}
