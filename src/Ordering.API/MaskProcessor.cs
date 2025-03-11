using System.Diagnostics;
using OpenTelemetry;

public class MaskProcessor : BaseProcessor<Activity>
{
    public override void OnEnd(Activity activity)
    {
        if (activity.Tags.Any(tag => tag.Key == "user.id"))
        {
            var userId = activity.Tags.FirstOrDefault(tag => tag.Key == "user.id").Value;
            var maskedUserId = MaskString(userId);
            activity.SetTag("user.id", maskedUserId);
        }
        if (activity.Tags.Any(tag => tag.Key == "request.command.buyer"))
        {
            var buyer = activity.Tags.FirstOrDefault(tag => tag.Key == "request.command.buyer").Value;
            var maskedBuyer = MaskString(buyer);
            activity.SetTag("request.command.buyer", maskedBuyer);
        }
        if (activity.Tags.Any(tag => tag.Key == "request.command.buyerId"))
        {
            var buyerId = activity.Tags.FirstOrDefault(tag => tag.Key == "request.command.buyerId").Value;
            var maskedBuyerId = MaskString(buyerId);
            activity.SetTag("request.command.buyerId", maskedBuyerId);
        }
        if (activity.Tags.Any(tag => tag.Key == "request.command.cardNumber"))
        {
            var cardNumber = activity.Tags.FirstOrDefault(tag => tag.Key == "request.command.cardNumber").Value;
            var maskedCardNumber = MaskString(cardNumber);
            activity.SetTag("request.command.cardNumber", maskedCardNumber);
        }
        if (activity.Tags.Any(tag => tag.Key == "request.command.cardHolderName"))
        {
            var cardHolderName = activity.Tags.FirstOrDefault(tag => tag.Key == "request.command.cardHolderName").Value;
            var maskedCardHolderName = MaskString(cardHolderName);
            activity.SetTag("request.command.cardHolderName", maskedCardHolderName);
        }
        if (activity.Tags.Any(tag => tag.Key == "request.command.cardExpiration"))
        {
            var cardExpiration = activity.Tags.FirstOrDefault(tag => tag.Key == "request.command.cardExpiration").Value;
            var maskedCardExpiration = MaskString(cardExpiration);
            activity.SetTag("request.command.cardExpiration", maskedCardExpiration);
        }
        if (activity.Tags.Any(tag => tag.Key == "request.command.cardSecurityNumber"))
        {
            var cardSecurityNumber = activity.Tags.FirstOrDefault(tag => tag.Key == "request.command.cardSecurityNumber").Value;
            var maskedCardSecurityNumber = MaskCVV(cardSecurityNumber);
            activity.SetTag("request.command.cardSecurityNumber", maskedCardSecurityNumber);
        }
        if (activity.Tags.Any(tag => tag.Key == "request.command.Street"))
        {
            var street = activity.Tags.FirstOrDefault(tag => tag.Key == "request.command.Street").Value;
            var maskedStreet = MaskString(street);
            activity.SetTag("request.command.Street", maskedStreet);
        }
        if (activity.Tags.Any(tag => tag.Key == "request.command.ZipCode"))
        {
            var zupCode = activity.Tags.FirstOrDefault(tag => tag.Key == "request.command.ZipCode").Value;
            var maskedZupCode = MaskString(zupCode);
            activity.SetTag("request.command.ZupCode", maskedZupCode);
        }
    }

    private static string MaskString(string str)
    {
        return str.Substring(0, 2) + new string('*', str.Length - 2);
    }
    private static string MaskCVV(string str)
    {
        return new string('*', str.Length);
    }
}