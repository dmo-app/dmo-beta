namespace DMO.Alpha.Infrastructure.Pdf;

/// <summary>
/// A PALETA OFICIAL DO DMO (decisão do Diogo, 17/10/2026;
/// docs/ui-reference/paleta-oficial.md) nos PDFs: os mesmos tons que
/// `--dmo-palette-*` e `--dmo-brand-*` em wwwroot/0_ASSET_CANONICAL_DESIGN_SYSTEM.css.
/// Se a paleta mudar lá, muda-se também aqui. As cores de estado ficam fora
/// da paleta, de propósito, mas também vivem aqui (abaixo): os geradores não
/// têm cores soltas.
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
    /// <summary>Fundo branco dos cartões e das linhas pares.</summary>
    public const string White = "#ffffff";

    // AS CORES DE ESTADO (sweep de 19/10/2026, 3.16): fora da paleta, como a
    // regra permite, e iguais às do ecrã. Antes cada gerador tinha as suas:
    // o OK das punções saía num verde no PDF do PU NNPB e noutro no do
    // Resumo, «por verificar» saía no castanho do erro, e «não avaliável»
    // saía a âmbar, a cor de «por verificar».

    // Medições e verificações: as áreas, os Pegamentos e o PU NNPB
    // (as mesmas do ecrã do Resumo).
    public const string Ok = "#15803d";
    public const string OkBackground = "#effaf3";
    public const string OkBorder = "#9fd5b3";
    public const string Nok = "#b91c1c";
    public const string NokBackground = "#fdf2f2";
    public const string NokBorder = "#efb1b1";
    /// <summary>O valor fora do corredor, na tabela dos Pegamentos.</summary>
    public const string NokCell = "#f8d4d4";
    public const string ToCheck = "#a16207";
    public const string ToCheckBackground = "#fdf8ec";
    /// <summary>«Não avaliável» (sem nominal): o cinzento do ecrã dos Pegamentos, nunca o âmbar.</summary>
    public const string NotEvaluable = "#94a3b8";
    public const string NotEvaluableBackground = "#f4f6f8";

    // A aprovação (os selos e o cartão «Aprovação»): as cores do cartão de
    // aprovação do ecrã do Resumo. O «Não aprovado» usa o vermelho do NOK.
    public const string Approved = "#3d7a5a";
    public const string ToApprove = "#a97943";
    public const string ToApproveBackground = ToCheckBackground;
    public const string ToApproveBorder = "#e3c58f";

    // Os avisos (as cores de --dmo-warning do ecrã) e a «Alteração» das punções.
    public const string Warning = ToApprove;
    public const string WarningBackground = "#f7f0e7";
    public const string WarningText = "#7a5428";
    public const string Change = "#956124";
}
