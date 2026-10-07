using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace CatEcommerce.Api.Filters;

/// <summary>
/// HO-FIL-001 §11.4: the upload action streams the request body with <c>MultipartReader</c> --
/// removing MVC's own form-value providers stops MVC from also buffering the whole multipart body
/// into memory to populate model binding, which would defeat V4 (Kestrel/MVC must not buffer a
/// 50 MB upload).
///
/// The classic ASP.NET Core large-file-upload sample removes only
/// <see cref="FormValueProviderFactory"/> and <see cref="JQueryFormValueProviderFactory"/>, but
/// the current default value-provider-factory list (verified empirically against this solution's
/// actual ASP.NET Core 9.0.5 runtime, in a minimal from-scratch repro, not assumed from the older
/// docs) also includes <see cref="FormFileValueProviderFactory"/> -- constructing it calls
/// <c>HttpContext.Request.ReadFormAsync()</c> to populate <c>IFormFileCollection</c> regardless of
/// whether any action parameter actually binds an <c>IFormFile</c>, which reads the ENTIRE
/// multipart body into <c>IFormCollection</c> and leaves <c>Request.Body</c> at end-of-stream --
/// so <c>MultipartReader</c> then sees zero bytes and fails with "Unexpected end of Stream" on the
/// very first section. Leaving this factory in place is not a partial mitigation; it silently
/// defeats manual multipart streaming entirely. Removing all three is required.
/// </summary>
public sealed class DisableFormValueModelBindingAttribute : Attribute, IResourceFilter
{
    public void OnResourceExecuting(ResourceExecutingContext context)
    {
        var factories = context.ValueProviderFactories;
        factories.RemoveType<FormValueProviderFactory>();
        factories.RemoveType<JQueryFormValueProviderFactory>();
        factories.RemoveType<FormFileValueProviderFactory>();
    }

    public void OnResourceExecuted(ResourceExecutedContext context)
    {
    }
}
