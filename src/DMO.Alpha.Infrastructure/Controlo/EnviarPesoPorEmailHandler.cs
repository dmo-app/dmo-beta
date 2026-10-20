using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DMO.Alpha.Infrastructure.Controlo;

public enum EnviarPesoResultado
{
    Enviado,
    SemServidor,
    SemPeso,
    SemModelo,
    SemLista,
    Falhou
}

/// <summary>
/// O «Enviar» do Resumo (Diogo, 19/10/2026, D9 e «essa opção deve existir no
/// resumo do controlo»): faz o PDF do Peso da produção em memória, como o
/// «Exportar», e envia-o à lista escolhida com o assunto e o corpo do modelo.
/// O PDF nunca fica no servidor; fica o registo do envio (quem, quando, para
/// quem, com que modelo). Um envio que falha não deixa registo.
/// </summary>
public sealed class EnviarPesoPorEmailHandler(
    DmoDbContext db,
    PesoPdfDocumentBuilder pdfDocument,
    IEmailSender emailSender,
    ILogger<EnviarPesoPorEmailHandler> logger)
{
    public async Task<EnviarPesoResultado> EnviarAsync(
        int controloId, int? modeloId, int? listaId, string enviadoPor, string? generatedBy,
        CancellationToken cancellationToken = default)
    {
        if (!emailSender.Configurado) return EnviarPesoResultado.SemServidor;
        var modelo = modeloId is int m
            ? await db.EmailModelos.AsNoTracking().FirstOrDefaultAsync(x => x.Id == m, cancellationToken)
            : null;
        if (modelo is null) return EnviarPesoResultado.SemModelo;
        var lista = listaId is int l
            ? await db.EmailListas.AsNoTracking().FirstOrDefaultAsync(x => x.Id == l, cancellationToken)
            : null;
        if (lista is null) return EnviarPesoResultado.SemLista;

        var documento = await pdfDocument.BuildAsync(controloId, null, null, null, generatedBy, cancellationToken);
        if (documento is null) return EnviarPesoResultado.SemPeso;

        var destinatarios = lista.Destinatarios.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var assunto = EnvioEmailRegras.Preencher(modelo.Assunto, documento.Reference, documento.ProductionNumber, documento.Machine);
        var corpo = EnvioEmailRegras.Preencher(modelo.Corpo, documento.Reference, documento.ProductionNumber, documento.Machine);
        try
        {
            await emailSender.SendAsync(new EmailMensagem(
                modelo.Remetente, destinatarios, assunto, corpo, documento.FileName, documento.Bytes), cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // O motivo fica no log (sem a palavra-passe); o ecrã diz que não saiu.
            logger.LogWarning(exception, "O envio do PDF do Peso do controlo {ControloId} falhou.", controloId);
            return EnviarPesoResultado.Falhou;
        }

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
        return EnviarPesoResultado.Enviado;
    }

    /// <summary>Os últimos envios do PDF do Peso deste controlo, o mais recente primeiro.</summary>
    public async Task<IReadOnlyList<EmailEnvio>> UltimosAsync(int controloId, int quantos, CancellationToken cancellationToken = default) =>
        await db.EmailEnvios.AsNoTracking()
            .Where(e => e.ControloId == controloId)
            .OrderByDescending(e => e.EnviadoEmUtc).ThenByDescending(e => e.Id)
            .Take(quantos)
            .ToListAsync(cancellationToken);
}
