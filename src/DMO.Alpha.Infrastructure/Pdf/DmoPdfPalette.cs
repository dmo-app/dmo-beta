namespace DMO.Alpha.Infrastructure.Pdf;

/// <summary>
/// A PALETA OFICIAL DO DMO (decisão do Diogo, 17/10/2026;
/// docs/ui-reference/paleta-oficial.md) nos PDFs: os mesmos tons que
/// `--dmo-palette-*` e `--dmo-brand-*` em wwwroot/0_ASSET_CANONICAL_DESIGN_SYSTEM.css.
/// Se a paleta mudar lá, muda-se também aqui. As cores de estado (verde,
/// âmbar, vermelho) e as dos gráficos ficam nos geradores.
/// </summary>
internal static class DmoPdfPalette
{
    public const string Sky = "#afd1f2";
    public const string Mist = "#92b4cc";
    public const string Steel = "#6494b4";
    public const string Ocean = "#4c7c9c";
    public const string Slate = "#44647c";

    /// <summary>Cabeçalhos, títulos e links (como os botões da app).</summary>
    public const string Accent = Slate;
    /// <summary>Fundo escuro dos cartões em destaque (--dmo-brand-700).</summary>
    public const string Deep = "#3c586d";
    /// <summary>Texto principal (--dmo-brand-900).</summary>
    public const string Ink = "#253744";
    /// <summary>Texto secundário (--dmo-muted).</summary>
    public const string Muted = "#577389";
    /// <summary>Linhas e contornos (--dmo-border).</summary>
    public const string Line = "#d9e5ed";
    /// <summary>Texto claro sobre fundo escuro (--dmo-brand-100).</summary>
    public const string OnDeep = "#d7e8f8";
    /// <summary>Fundo suave azul (--dmo-brand-050).</summary>
    public const string Tint = "#ebf4fc";
    /// <summary>Fundo quase branco (--dmo-subtle).</summary>
    public const string Soft = "#f3f8fd";
}
