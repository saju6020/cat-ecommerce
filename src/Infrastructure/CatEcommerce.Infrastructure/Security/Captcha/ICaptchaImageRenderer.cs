namespace CatEcommerce.Infrastructure.Security.Captcha;

public interface ICaptchaImageRenderer
{
    /// <summary>Renders a 200x70 noisy PNG containing <paramref name="answer"/>.</summary>
    byte[] Render(string answer);
}
