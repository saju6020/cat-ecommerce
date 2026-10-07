using System.Reflection;
using CatEcommerce.Api.Controllers;
using CatEcommerce.Api.Filters;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Controllers;
using Xunit;

namespace CatEcommerce.IntegrationTests.Unit;

/// <summary>
/// HO-FIL-001 §14 / A6: "A second upload path anywhere in the solution" is forbidden -- "the reason
/// this slice exists (README C-5)". No convention test existed for this before this TEST gate; the
/// ARCHITECTURE and SECURITY gates checked it by reading every controller by hand each pass, which
/// does not survive a future controller being added without the same care.
///
/// <see cref="DisableFormValueModelBindingAttribute"/> is the one marker that means "this action
/// intends to parse the multipart body itself with <c>MultipartReader</c>, bypassing MVC's own
/// form/file model binding" (see that attribute's own doc comment) -- exactly the shape a second
/// upload path would need. This reflects over every controller action in the Api assembly (via
/// ASP.NET Core's own <see cref="ControllerFeatureProvider"/>, matching
/// <c>EndpointAccessConventionTests</c>' own discovery technique) and fails if anything other than
/// <see cref="FilesController"/>'s two upload actions carries it, or if any action anywhere binds
/// an <c>IFormFile</c>/<c>IFormFileCollection</c> parameter (MVC's own, buffered upload path -- also
/// a second way to accept a file, just via normal model binding instead of manual streaming).
/// </summary>
public sealed class FileUploadConventionTests
{
    [Fact]
    public void Only_FilesController_disables_form_value_model_binding_or_parses_multipart_itself()
    {
        var apiAssembly = typeof(FilesController).Assembly;

        var partManager = new ApplicationPartManager();
        partManager.ApplicationParts.Add(new AssemblyPart(apiAssembly));
        partManager.FeatureProviders.Add(new ControllerFeatureProvider());
        var feature = new ControllerFeature();
        partManager.PopulateFeature(feature);

        var controllerTypes = feature.Controllers.Select(ti => ti.AsType()).ToList();
        Assert.NotEmpty(controllerTypes);

        var actionsWithTheMarker = controllerTypes
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            .Where(m => !m.IsSpecialName)
            .Where(m => m.GetCustomAttribute<DisableFormValueModelBindingAttribute>() is not null)
            .ToList();

        Assert.All(actionsWithTheMarker, m => Assert.Equal(typeof(FilesController), m.DeclaringType));
        Assert.Equal(2, actionsWithTheMarker.Count); // UploadStaffFile, UploadCustomerFile -- exactly the two documented upload routes

        var actionsBindingAFormFile = controllerTypes
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            .Where(m => !m.IsSpecialName)
            .Where(m => m.GetParameters().Any(p =>
                typeof(IFormFile).IsAssignableFrom(p.ParameterType) ||
                typeof(IFormFileCollection).IsAssignableFrom(p.ParameterType)))
            .ToList();

        Assert.Empty(actionsBindingAFormFile);
    }
}
