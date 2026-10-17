namespace DMO.Alpha.Core.Authentication;

public enum ChangePasswordResult
{
    Success,
    IdentityNotFound,
    PolicyViolation,
    ProviderError
}
