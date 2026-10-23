using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Controlo;

public enum EnviarPesoResultado
{
    Preparado,
    SemPeso,
    SemModelo,
    SemLista
}

/// <summary>O email preparado: o ficheiro .eml para o Outlook abrir, com o nome do PDF.</summary>
public sealed record EmailPreparado(byte[] Eml, string FileName);

/// <summary>
/// O «Enviar» do Resumo (Diogo, 19/10/2026, D9 e «essa opção deve existir no
/// resumo do controlo»): faz o PDF do Peso da produção em memória, como o
/// «Exportar», e prepara o email para a lista escolhida com o assunto e o
/// corpo do modelo. O email sai pelo Outlook do PC (Diogo, 21/10/2026): a app
/// devolve um .eml que o Outlook abre pronto a enviar. O PDF nunca fica no
/// servidor; fica o registo (quem, quando, para quem, com que modelo).
/// </summary>
public sealed class EnviarPesoPorEmailHandler(
    DmoDbContext db,
    PesoPdfDocumentBuilder pdfDocument)
{
    public async Task<(EnviarPesoResultado Resultado, EmailPreparado? Email)> PrepararAsync(
        int controloId, int? modeloId, int? listaId, string enviadoPor, string? generatedBy,
        CancellationToken cancellationToken = default)
    {
        var modelo = modeloId is int m
            ? await db.EmailModelos.AsNoTracking().FirstOrDefaultAsync(x => x.Id == m, cancellationToken)
            : null;
        if (modelo is null) return (EnviarPesoResultado.SemModelo, null);
        var lista = listaId is int l
            ? await db.EmailListas.AsNoTracking().FirstOrDefaultAsync(x => x.Id == l, cancellationToken)
            : null;
        if (lista is null) return (EnviarPesoResultado.SemLista, null);

        var documento = await pdfDocument.BuildAsync(controloId, null, null, null, generatedBy, cancellationToken);
        if (documento is null) return (EnviarPesoResultado.SemPeso, null);

        // Só os emails vão para o «Para» (os nomes ficam na lista, Diogo 23/10/2026).
        var destinatarios = EnvioEmailRegras.LerLista(lista.Destinatarios).Select(p => p.Email).ToArray();
        var assunto = EnvioEmailRegras.Preencher(modelo.Assunto, documento.Reference, documento.ProductionNumber, documento.Machine, documento.PesoGramas);
        var corpo = EnvioEmailRegras.Preencher(modelo.Corpo, documento.Reference, documento.ProductionNumber, documento.Machine, documento.PesoGramas);
        var eml = EmailRascunho.Gerar(new EmailMensagem(
            modelo.Remetente, destinatarios, assunto, corpo, documento.FileName, documento.Bytes));

        db.EmailEnvios.Add(new EmailEnvio
        {
            ControloId = documento.ControloId,
            PesoId = documento.PesoId,
            Ficheiro = documento.FileName,
            ModeloNome = modelo.Nome,
            ListaNome = lista.Nome,
            Remetente = modelo.Remetente,
            Destinatarios = lista.Destinatarios,
            Assunto = assunto.Length > EnvioEmailRegras.AssuntoMax ? assunto[..EnvioEmailRegras.AssuntoMax] : assunto,
            EnviadoPor = enviadoPor,
            EnviadoEmUtc = DateTime.UtcNow
        });
        await db.SaveChangesAsync(cancellationToken);
        return (EnviarPesoResultado.Preparado, new EmailPreparado(eml, Path.ChangeExtension(documento.FileName, ".eml")));
    }

    /// <summary>Os últimos emails preparados com o PDF do Peso deste controlo, o mais recente primeiro.</summary>
    public async Task<IReadOnlyList<EmailEnvio>> UltimosAsync(int controloId, int quantos, CancellationToken cancellationToken = default) =>
        await db.EmailEnvios.AsNoTracking()
            .Where(e => e.ControloId == controloId)
            .OrderByDescending(e => e.EnviadoEmUtc).ThenByDescending(e => e.Id)
            .Take(quantos)
            .ToListAsync(cancellationToken);
}
