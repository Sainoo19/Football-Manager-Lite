using System;

public sealed class BallContestCoordinator
{
    private readonly GroundDuelResolver _groundDuelResolver;

    public BallContestCoordinator(GroundDuelResolver groundDuelResolver)
    {
        _groundDuelResolver = groundDuelResolver ??
            throw new ArgumentNullException(nameof(groundDuelResolver));
    }

    public GroundDuelResolution ResolveGroundDuel(GroundDuelContext context)
    {
        return _groundDuelResolver.Resolve(context);
    }
}
