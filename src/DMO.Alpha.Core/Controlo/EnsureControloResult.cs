namespace DMO.Alpha.Core.Controlo;

/// <summary>
/// Resultado tipado do ensure do Controlo.
/// </summary>
public enum EnsureControloResult
{
    Success,

    /// <summary>
    /// O jobon_id indicado não existe na verdade persistida (JobOns).
    /// Nenhum controlo_id é criado para um jobon_id inexistente.
    /// </summary>
    JobOnNotFound
}
