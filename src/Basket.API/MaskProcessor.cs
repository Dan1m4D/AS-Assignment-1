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
    }

    private static string MaskString(string str)
    {
        return str.Substring(0, 2) + new string('*', str.Length - 2);
    }
}