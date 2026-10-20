using System.Net.Http.Headers;
using System.Text.RegularExpressions;

namespace DMO.Alpha.Web.Tests;

internal static partial class HtmlFormHelpers
{
    public static string? ExtractRequestVerificationToken(string html)
    {
        var match = RequestVerificationTokenRegex().Match(html);
        return match.Success ? match.Groups["value"].Value : null;
    }

    public static HttpContent BuildFormPost(params (string name, string value)[] fields)
    {
        var content = new FormUrlEncodedContent(fields.Select(f => new KeyValuePair<string, string>(f.name, f.value)));
        content.Headers.ContentType = new MediaTypeHeaderValue("application/x-www-form-urlencoded");
        return content;
    }

    [GeneratedRegex("<input[^>]*name=\"__RequestVerificationToken\"[^>]*value=\"(?<value>[^\"]+)\"", RegexOptions.IgnoreCase)]
    private static partial Regex RequestVerificationTokenRegex();
}
