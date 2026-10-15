using CatEcommerce.Application.CommandHandlers.Validators;
using CatEcommerce.Application.Commands.Order;
using CatEcommerce.Application.Tests.TestSupport;

namespace CatEcommerce.Application.Tests.Validators;

/// <summary>ORD-002 SECURITY LOW (backend half) -- the courier tracking-URL template is an absolute http(s) URL or absent; nothing else reaches an anchor.</summary>
public sealed class CourierTrackingUrlValidatorTests
{
    private static readonly Guid Id = Guid.NewGuid();

    private static CreateCourierCommand Create(string? template) => new()
    {
        CourierId = Id, Name = "Pathao", TrackingUrlTemplate = template, UserContext = TestUserContext.Create(), CorrelationId = Guid.NewGuid()
    };

    private static UpdateCourierCommand Update(string? template) => new()
    {
        CourierId = Id, Name = "Pathao", TrackingUrlTemplate = template, UserContext = TestUserContext.Create(), CorrelationId = Guid.NewGuid()
    };

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("https://track.example.test/t/{tracking}")]
    [InlineData("http://track.example.test/?id={tracking}")]
    [InlineData("HTTPS://track.example.test/{tracking}")]
    public void AnAbsoluteHttpOrHttpsTemplate_OrNone_IsAccepted(string? template)
    {
        Assert.True(new CreateCourierCommandValidator().Validate(Create(template)).IsValid);
        Assert.True(new UpdateCourierCommandValidator().Validate(Update(template)).IsValid);
    }

    [Theory]
    [InlineData("javascript:alert(1)//{tracking}")]
    [InlineData("data:text/html,{tracking}")]
    [InlineData("ftp://track.example.test/{tracking}")]
    [InlineData("//track.example.test/{tracking}")]
    [InlineData("/track/{tracking}")]
    [InlineData("track.example.test/{tracking}")]
    [InlineData("https:///{tracking}")]
    public void AnythingElse_IsRefused_OnTheTemplateProperty(string template)
    {
        var create = new CreateCourierCommandValidator().Validate(Create(template));
        var update = new UpdateCourierCommandValidator().Validate(Update(template));

        Assert.Contains(create.Errors, e => e.PropertyName == nameof(CreateCourierCommand.TrackingUrlTemplate));
        Assert.Contains(update.Errors, e => e.PropertyName == nameof(UpdateCourierCommand.TrackingUrlTemplate));
    }
}
