namespace DMO.Alpha.Core.Controlo;

/// <summary>
/// As partes da Revisão que se aprovam. Só o Peso (Diogo, 19/10/2026:
/// «remover aprovaçao do spegamentos, manter apenas no peso»); os Pegamentos
/// só se consultam. O valor 2 era o dos Pegamentos: as decisões antigas com
/// ele ficam na base e já não contam.
/// </summary>
public enum ControloParte
{
    Peso = 1
}

/// <summary>
/// A decisão do chefe sobre uma parte do controlo: aprovado ou não aprovado
/// (com nota), quem e quando. Uma por parte; a última vence. Como a
/// aprovação do controlo, é só registo e nunca bloqueia nada. O estado do
/// controlo é o do Peso e calcula-se ao ler (<c>ControloAprovacao</c>). As colunas de aprovação em
/// <see cref="Controlo"/> guardam esse estado no momento da última decisão.
/// </summary>
public sealed class ControloDecisao
{
    public int Id { get; set; }

    public int ControloId { get; set; }

    public ControloParte Parte { get; set; }

    /// <summary>true = aprovado; false = não aprovado.</summary>
    public bool Aprovado { get; set; }

    public string OperatorId { get; set; } = "";

    public DateTime EmUtc { get; set; }

    /// <summary>A nota da decisão (obrigatória ao não aprovar).</summary>
    public string? Nota { get; set; }
}
