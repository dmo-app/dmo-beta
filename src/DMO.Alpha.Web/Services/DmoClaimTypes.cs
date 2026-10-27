namespace DMO.Alpha.Web.Services;

public static class DmoClaimTypes
{
    public const string ActorId = "dmo:actor_id";
    public const string DisplayName = "dmo:display_name";
    public const string IsAdmin = "dmo:is_admin";
    public const string Email = "dmo:email";
    public const string OperatorId = "dmo:operator_id";
    public const string ProfileLabel = "dmo:profile_label";
    public const string ProviderUserId = "dmo:provider_user_id";
    public const string RequiresPasswordChange = "dmo:requires_password_change";
    public const string PasswordChangedAt = "dmo:password_changed_at";
    public const string SessionVersion = "dmo:session_version";

    /// <summary>A sessão entrou pelo posto da RI: só a Reparação Interna.</summary>
    public const string Posto = "dmo:posto";

    /// <summary>Os minutos sem uso ao fim dos quais a sessão do posto fecha sozinha.</summary>
    public const string PostoMinutos = "dmo:posto_minutos";
}
