using DMO.Alpha.Infrastructure.Search;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace DMO.Alpha.Web.TagHelpers;

/// <summary>As opções de consulta também são filtradas na primeira resposta, sem depender de JavaScript.</summary>
[HtmlTargetElement("form", Attributes = "data-dmo-reference-scope,data-dmo-reference-name")]
public sealed class ReferenceFilterFormTagHelper(ReferenceOptionsQuery query) : TagHelper
{
    public override int Order => -1100;
    [ViewContext, HtmlAttributeNotBound]
    public ViewContext ViewContext { get; set; } = null!;
    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        var scope = context.AllAttributes["data-dmo-reference-scope"].Value.ToString()!;
        var referenceName = context.AllAttributes["data-dmo-reference-name"].Value.ToString()!;
        var request = ViewContext.HttpContext.Request;
        var reference = request.Query[referenceName].ToString();
        if (!string.IsNullOrWhiteSpace(reference))
        {
            var lot = request.Query["lote"].ToString();
            if (lot.Length == 0) lot = request.Query["lot"].ToString();
            var options = await query.ReadAsync(scope, reference, lot, request.HttpContext.RequestAborted);
            context.Items[typeof(ReferenceFilterFormTagHelper)] = options;
        }
        output.Content.SetHtmlContent(await output.GetChildContentAsync());
    }
}

[HtmlTargetElement("select", Attributes = "name,data-dmo-reference-select")]
public sealed class ReferenceFilterSelectTagHelper : TagHelper
{
    public override int Order => -1100;
    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        output.Attributes.RemoveAll("data-dmo-reference-select");
        var name = context.AllAttributes["name"].Value.ToString()!;
        if (context.Items.TryGetValue(typeof(ReferenceFilterFormTagHelper), out var data)
            && data is IReadOnlyDictionary<string, string[]> options && options.TryGetValue(name, out var values))
            context.Items[typeof(ReferenceFilterSelectTagHelper)] = values;
        else context.Items.Remove(typeof(ReferenceFilterSelectTagHelper));
        output.Content.SetHtmlContent(await output.GetChildContentAsync());
    }
}

[HtmlTargetElement("option", ParentTag = "select", Attributes = "value,data-dmo-reference-option")]
public sealed class ReferenceFilterOptionTagHelper : TagHelper
{
    public override int Order => -1100;
    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        output.Attributes.RemoveAll("data-dmo-reference-option");
        var value = context.AllAttributes["value"].Value.ToString()!;
        if (value.Length == 0 || !context.Items.TryGetValue(typeof(ReferenceFilterSelectTagHelper), out var data)
            || data is not string[] values || values.Contains(value, StringComparer.OrdinalIgnoreCase)) return;
        output.Attributes.SetAttribute("hidden", "hidden");
        output.Attributes.SetAttribute("disabled", "disabled");
        output.Attributes.SetAttribute("data-dmo-reference-excluded", "");
        output.Attributes.RemoveAll("selected");
    }
}
