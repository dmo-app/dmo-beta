namespace DMO.Alpha.Core.Boquilhas;

/// <summary>
/// Recusas tipadas da escrita das entidades reparadoras (vocabulário
/// fechado, nunca texto livre; informativas — a app nunca bloqueia, o
/// operador corrige e volta a gravar).
/// </summary>
public enum BqRepairerWriteResult
{
    Success,

    /// <summary>O nome está vazio — o fato humano não pode ficar em branco.</summary>
    EmptyName,

    /// <summary>
    /// Já existe uma entidade com o mesmo nome: dois reparadores com o
    /// mesmo nome seriam o mesmo fato humano para a rota (a recusa é
    /// tipada; o índice único é a última linha da persistência).
    /// </summary>
    NameAlreadyInUse,

    /// <summary>A entidade indicada não existe (a edição de um reparador eliminado).</summary>
    NotFound
}
