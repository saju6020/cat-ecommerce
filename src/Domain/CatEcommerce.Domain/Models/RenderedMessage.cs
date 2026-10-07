namespace CatEcommerce.Domain.Models;

/// <summary>HO-NTF-001 §8.3 -- the output of <c>NotificationRenderer.Render</c>.</summary>
public sealed record RenderedMessage(string? Subject, string? Title, string TextBody, string? HtmlBody, string? LinkPath);
