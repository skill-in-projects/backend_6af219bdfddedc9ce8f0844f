namespace Backend.Services;

/// <summary>
/// The bike shop's own facts that the support agent may use. Sample values: replace them with the shop's real hours,
/// repairs and prices. The agent only answers from this text.
/// </summary>
public static class BikeShopInfo
{
    public const string Text = """
        Opening hours:
        - Monday to Friday: 9:00 to 18:00
        - Saturday: 10:00 to 14:00
        - Sunday: closed

        Repairs (sample prices, time needed in the shop):
        - Flat tire fix: from 25, about 20 minutes, done while you wait
        - Brake adjustment: from 35, about 30 minutes
        - Gear tuning: from 40, about 45 minutes
        - Chain replacement: from 50, about 45 minutes
        - Wheel truing (straightening a bent wheel): from 45, about 1 hour
        - Full service (clean, lube and check the whole bike): from 120, 1 to 2 days

        Bring the bike in during opening hours. We call you when the repair is ready.
        """;
}
